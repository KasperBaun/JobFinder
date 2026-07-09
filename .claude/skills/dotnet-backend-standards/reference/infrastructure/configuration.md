
# Configuration

> Example for how to set up a **.NET C# WebAPI configuration** using a clean, neutral pattern (`ExampleNamespace`).

## Summary  

Guidelines for managing application configuration, secrets, and environment-specific settings.

- Use centralized config classes (e.g., `EnvironmentConfig`, `DatabaseConfig`, `SwaggerConfig`) called from `Program.cs` — keeps wiring in one place and makes it obvious what is configured.
- Bind settings with `AddOptions<T>().Bind(...).ValidateDataAnnotations().ValidateOnStart()` — **fail-fast** so missing/invalid env values break at startup instead of wasting developer time at runtime.
- **Why call `EnvironmentConfig.ConfigureEnvVariables(builder)`?**  
It centralizes all configuration binding and validation in one place and ensures **fail-fast**: if an environment variable or required value is missing, the app stops at startup with a clear error, instead of failing later during a request.

## Rules  

- ✅ Use `IOptions<T>` (or `.AddOptions<T>()`) for strongly-typed config
- ✅ Store secrets in a secure vault or user secrets (development)
- ✅ Use environment variables for deployment-specific values
- ✅ Create configuration classes for each domain area and validate them
- ✅ Validate configuration on startup (`ValidateDataAnnotations()`, `ValidateOnStart()`)
- ✅ Use separate configurations for different environments
- ✅ Never commit secrets to source control
- ✅ Keep a checked-in `example.appsettings.json` for onboarding, without secrets.
- ✅ For one-off consumers you can bind with `GetRequiredSection(...).Get<T>()`.
- ✅ Skip DB migrations automatically in special envs like `Testing` to speed up test runs.

---

## Example Settings Classes

```csharp
namespace ExampleNamespace.Settings;
using System.ComponentModel.DataAnnotations;

public class DatabaseSettings
{
    [Required(ErrorMessage = "ConnectionString is required in DatabaseSettings")]
    public string ConnectionString { get; set; } = string.Empty;
}

public class CorsSettings
{
    [Required(ErrorMessage = "AllowedOrigins is required in CorsSettings")]
    public string AllowedOrigins { get; set; } = string.Empty;
}
```

---

## Centralized Configuration Registration

```csharp
namespace ExampleNamespace.Configs;
using ExampleNamespace.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

public static class EnvironmentConfig
{
    // Keep this small and universal so it works in any hosting environment.
    public static void ConfigureEnvVariables(WebApplicationBuilder builder)
    {
        builder.Services.AddOptions<DatabaseSettings>()
            .Bind(builder.Configuration.GetSection(nameof(DatabaseSettings)))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services.AddOptions<CorsSettings>()
            .Bind(builder.Configuration.GetSection(nameof(CorsSettings)))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Always add env vars last so they can override other providers
        builder.Configuration.AddEnvironmentVariables();
    }
}

public static class DatabaseConfig
{
    public static void AddDatabase(WebApplicationBuilder builder)
    {
        builder.Services.AddDbContext<DbContext>((sp, options) =>
        {
            var db = sp.GetRequiredService<IOptions<DatabaseSettings>>().Value;
            options.UseSqlServer(db.ConnectionString, sql => sql.EnableRetryOnFailure());
        });
    }

    public static void MigrateDatabase(DbContext ctx)
    {
        if (ctx.Database.IsRelational())
        {
            ctx.Database.Migrate();
        }
    }
}

public static class OpenApiConfig
{
    public static void AddOpenApiDocs(WebApplicationBuilder builder)
    {
        // Document + version + per-module tags — see reference/api/openapi.md.
        builder.Services.AddOpenApi(ApiConstants.DocumentName, options =>
        {
            options.AddDocumentTransformer<ApiInfoTransformer>();
            options.AddDocumentTransformer<TagDescriptionTransformer>();
        });
        builder.Services.ConfigureHttpJsonOptions(x =>
        {
            x.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        });
    }

    public static void UseOpenApiDocs(WebApplication app)
    {
        app.MapOpenApi();                    // /openapi/v1.json
        app.MapScalarApiReference("/scalar"); // interactive UI
    }
}
```

> Full OpenAPI + Scalar setup (document transformers, tag providers, Scalar options) lives in
> [`../api/openapi.md`](../api/openapi.md). This is a minimal-API backend — endpoints are mapped
> via `IEndpointRegistration` (`app.MapModuleEndpoints()`), not controllers.

---

## `Program.cs` (minimal)

```csharp
using ExampleNamespace.Configs;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

/* Environment variables */
EnvironmentConfig.ConfigureEnvVariables(builder);

DatabaseConfig.AddDatabase(builder);
OpenApiConfig.AddOpenApiDocs(builder);

var app = builder.Build();

OpenApiConfig.UseOpenApiDocs(app);

// Apply migrations automatically except in Testing
if (!builder.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<DbContext>();
    DatabaseConfig.MigrateDatabase(db);
}

app.MapModuleEndpoints();   // IEndpointRegistration discovery — see api-endpoints.md
app.Run();

// Make the implicit Program class public for test projects
public partial class Program { }
```

---

## `appsettings.json` (structure)

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "DatabaseSettings": {
    "ConnectionString": "Server=(localdb)\\mssqllocaldb;Database=ExampleDb;Trusted_Connection=True;"
  },
    "CorsSettings": {
    "AllowedOrigins": "http://localhost:3000,http://localhost:5173"
  },
}
```

### Environment-specific overrides

```json
// appsettings.Development.json
{
  "SystemSettings": {
    "EnableBackgroundJobs": false
  },
  "MailServiceSettings": {
    "EnableEmailSending": false
  }
}

// appsettings.Production.json
{
  "SystemSettings": {
    "EnableBackgroundJobs": true
  },
  "MailServiceSettings": {
    "EnableEmailSending": true
  }
}
```

---

## Using configuration in services

```csharp
using ExampleNamespace.Settings;
using Microsoft.Extensions.Options;

namespace ExampleNamespace.Services;

public class ExampleLifecycleService
{
    private readonly SystemSettings _system;
    private readonly MailServiceSettings _mail;
    private readonly ILogger<ExampleLifecycleService> _logger;

    public ExampleLifecycleService(
        IOptions<SystemSettings> system,
        IOptions<MailServiceSettings> mail,
        ILogger<ExampleLifecycleService> logger)
    {
        _system = system.Value;
        _mail = mail.Value;
        _logger = logger;
    }

    public async Task SendExampleEmailAsync(Guid entityId, CancellationToken ct = default)
    {
        if (!_mail.EnableEmailSending)
        {
            _logger.LogInformation("Email sending is disabled; skipping.");
            return;
        }

        var actionUrl = $"{_system.WebApiUrl}/api/example/entities/{entityId}";
        // TODO: send email via your mailer abstraction
        await Task.CompletedTask;
    }
}
```
