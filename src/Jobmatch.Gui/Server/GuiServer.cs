using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text.Json;
using Jobmatch.Gui.Server.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jobmatch.Gui.Server;

public static class GuiServer
{
    public static async Task RunAsync(Jobmatch.UserContext ctx)
    {
        var port = ResolvePort();
        var url = $"http://127.0.0.1:{port}";
        var cts = new CancellationTokenSource();

        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(GuiServer).Assembly.GetName().Name,
        });

        builder.WebHost.UseUrls(url);

        // Suppress all Kestrel/hosting output — the terminal belongs to the user
        builder.Logging.ClearProviders();
        builder.Logging.AddFilter((_, _, level) => level >= LogLevel.None);

        builder.Services.AddSingleton(ctx);
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
                GuiLog.Error($"{context.Request.Method} {context.Request.Path} — {ex.Message}");
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
                    // HTML files must never be cached — the SPA shell changes between tool versions
                    if (staticCtx.Context.Response.ContentType?.StartsWith("text/html", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        staticCtx.Context.Response.Headers.CacheControl = "no-store";
                    }
                },
            });
        }

        // Register API endpoint groups
        WhoamiEndpoints.Map(app);
        ProvidersEndpoints.Map(app);
        SkillsetEndpoints.Map(app);
        SearchEndpoints.Map(app);
        HistoryEndpoints.Map(app);
        MarksEndpoints.Map(app);

        // Heartbeat — client polls to detect server disconnect
        app.MapGet(Routes.System.Ping, () => Results.Ok());

        // Graceful shutdown — React calls this when the user is done
        app.MapPost(Routes.System.Shutdown, (CancellationTokenSource shutdownCts) =>
        {
            shutdownCts.CancelAfter(TimeSpan.FromMilliseconds(300));
            return Results.Ok();
        });

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

        // Honour Ctrl+C and process termination signals — cancel the host gracefully
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true; // prevent immediate process kill; let RunAsync wind down
            cts.CancelAfter(TimeSpan.FromMilliseconds(500));
        };
        AppDomain.CurrentDomain.ProcessExit += (_, _) => cts.Cancel();

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"\n  jobfinder GUI → {url}");
        Console.ResetColor();

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
    }

    /// <summary>
    /// Picks the listen port. Honours <c>JOBFINDER_PORT</c> when set (used by `npm run dev`
    /// so the vite dev server can proxy <c>/api</c> to a stable address); otherwise grabs an
    /// ephemeral free port so installed-tool launches never collide.
    /// </summary>
    private static int ResolvePort()
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

    private static bool ShouldOpenBrowser()
    {
        var v = Environment.GetEnvironmentVariable("JOBFINDER_NO_BROWSER");
        return string.IsNullOrEmpty(v) || v == "0" || v.Equals("false", StringComparison.OrdinalIgnoreCase);
    }

    private static int FindAvailablePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static void OpenBrowser(string url)
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
            // Browser launch is best-effort — don't crash if it fails
        }
    }
}
