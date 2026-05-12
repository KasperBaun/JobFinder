using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text.Json;
using Jobmatch;
using Jobmatch.Api;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using NReco.Logging.File;

var port = ResolvePort();
var url = $"http://127.0.0.1:{port}";
var cts = new CancellationTokenSource();

// Resolve UserContext once at boot so we know where the log file lives. The DI
// factory in AddJobmatchApi resolves it again (cheap and idempotent — the file
// system ops are all "create if missing").
var userContextForLog = UserContext.Resolve();
var logDir = Path.Combine(userContextForLog.RootDir, "logs");
Directory.CreateDirectory(logDir);
var logPath = Path.Combine(logDir, "host.log");

var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
{
    ApplicationName = typeof(Program).Assembly.GetName().Name,
});

builder.WebHost.UseUrls(url);

// Terminal stays quiet (Kestrel info chatter would be noise), but WARN+ goes
// to data/<email>/logs/host.log so silent failures stop requiring console-logger
// surgery to diagnose.
builder.Logging.ClearProviders();
builder.Logging.AddFile(logPath, options =>
{
    options.Append = true;
    options.MinLevel = LogLevel.Warning;
    options.FileSizeLimitBytes = 5_000_000;
    options.MaxRollingFiles = 1;
});

builder.Services.AddJobmatchApi();
builder.Services.AddSingleton(cts);

var app = builder.Build();

// Global exception handler — prevents unhandled throws from silently dropping the connection.
// CreateSlimBuilder doesn't add exception middleware, so without this a throw in any endpoint
// causes Kestrel to close the TCP connection, which the browser sees as "Failed to fetch".
app.Use(async (context, next) =>
{
    try
    {
        await next(context);
    }
    catch (Exception ex) when (!context.Response.HasStarted)
    {
        HostLog.Error($"{context.Request.Method} {context.Request.Path} — {ex.Message}");
        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(
            JsonSerializer.Serialize(new { error = ex.Message }));
    }
});

// Serve React app from {BaseDir}/gui/ (copied there by MSBuild when -p:BuildGui=true)
var guiPath = Path.Combine(AppContext.BaseDirectory, "gui");
if (Directory.Exists(guiPath))
{
    var fileProvider = new PhysicalFileProvider(guiPath);

    app.UseDefaultFiles(new DefaultFilesOptions
    {
        FileProvider = fileProvider,
        RequestPath = "",
    });

    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = fileProvider,
        RequestPath = "",
        OnPrepareResponse = staticCtx =>
        {
            // HTML files must never be cached — the SPA shell changes between tool versions.
            if (staticCtx.Context.Response.ContentType?.StartsWith("text/html", StringComparison.OrdinalIgnoreCase) == true)
            {
                staticCtx.Context.Response.Headers.CacheControl = "no-store";
            }
        },
    });
}

app.MapJobmatchApi();

// Host-owned endpoints — shutdown is not part of the standalone Api surface.
new Jobmatch.Host.Endpoints.HostShutdownEndpoint().Register(app);

// SPA fallback — any unmatched route returns index.html (so React Router works on hard refresh)
app.MapFallback(async http =>
{
    var indexPath = Path.Combine(guiPath, "index.html");
    if (File.Exists(indexPath))
    {
        http.Response.ContentType = "text/html; charset=utf-8";
        http.Response.Headers.CacheControl = "no-store";
        await http.Response.SendFileAsync(indexPath);
    }
    else
    {
        http.Response.StatusCode = 404;
        await http.Response.WriteAsync("GUI not built. Run with -p:BuildGui=true");
    }
});

// Honour Ctrl+C and process termination signals — cancel the host gracefully.
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.CancelAfter(TimeSpan.FromMilliseconds(500));
};
AppDomain.CurrentDomain.ProcessExit += (_, _) => cts.Cancel();

Console.ForegroundColor = ConsoleColor.Yellow;
Console.WriteLine($"\n  jobfinder GUI → {url}");
Console.ResetColor();
Console.WriteLine($"  log file → {logPath}");

if (ShouldOpenBrowser())
{
    Console.WriteLine("  Opening browser...\n");
    OpenBrowser(url);
}
else
{
    Console.WriteLine("  Browser launch suppressed (JOBFINDER_NO_BROWSER).\n");
}

await ((IHost)app).RunAsync(cts.Token);

static int ResolvePort()
{
    var env = Environment.GetEnvironmentVariable("JOBFINDER_PORT");
    if (!string.IsNullOrWhiteSpace(env) &&
        int.TryParse(env, out var fixedPort) &&
        fixedPort > 0 && fixedPort < 65536)
    {
        return fixedPort;
    }
    return FindAvailablePort();
}

static bool ShouldOpenBrowser()
{
    var v = Environment.GetEnvironmentVariable("JOBFINDER_NO_BROWSER");
    return string.IsNullOrEmpty(v) || v == "0" || v.Equals("false", StringComparison.OrdinalIgnoreCase);
}

static int FindAvailablePort()
{
    var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();
    var port = ((IPEndPoint)listener.LocalEndpoint).Port;
    listener.Stop();
    return port;
}

static void OpenBrowser(string url)
{
    try
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Process.Start(new ProcessStartInfo("cmd", $"/c start {url}")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            });
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            Process.Start(new ProcessStartInfo("open", url) { UseShellExecute = false });
        }
        else
        {
            Process.Start(new ProcessStartInfo("xdg-open", url) { UseShellExecute = false });
        }
    }
    catch
    {
        // Browser launch is best-effort — don't crash if it fails.
    }
}
