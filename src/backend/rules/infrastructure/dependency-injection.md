# Dependency Injection

## Layer Dependencies

| Layer | Lifetime | Injects | Injected By |
|-------|----------|---------|-------------|
| Endpoints | N/A | Handlers via `[FromServices]` | N/A |
| Handlers | Scoped | Services, Logger | Endpoints |
| Services | Scoped | DbContext, Logger, AuditService, other Services | Handlers |

## Rules

| MUST | MUST NOT |
|------|----------|
| Register interfaces, not concrete types | Handler inject `DbContext` |
| Use Scoped for handlers and services | Service inject handlers |
| Use Singleton for logger, HttpClient | Create circular dependencies |
| Group registrations by domain area | |

## Module Registration

Each module ships a single `AddMwt<ModuleName>()` extension in `<Module>/Mwt<Module>Extensions.cs` that registers everything the module owns — services, handlers, validators, permissions, background jobs, DB extensions, and options. A matching `MapMwt<ModuleName>Endpoints()` wires up the HTTP endpoints. `Program.cs` calls each module's extension in sequence.

This per-module pattern keeps each module self-contained: you can add or remove a module by adding or deleting one `Add*()` call in `Program.cs`, and the module's DI graph lives with the module's code.

```csharp
// Mwt.Files/MwtFilesExtensions.cs
public static class MwtFilesExtensions
{
    public static IServiceCollection AddMwtFiles(this IServiceCollection services, IConfiguration configuration)
    {
        // Settings / options
        services.AddOptions<FileStorageSettings>()
            .Bind(configuration.GetSection(FileStorageSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Validators (assembly-scan)
        services.AddValidatorsFromAssembly(typeof(MwtFilesExtensions).Assembly, ServiceLifetime.Scoped, includeInternalTypes: true);

        // Module DB extension + OpenAPI tags
        services.AddSingleton<IMwtModuleDbExtension, FilesDbExtension>();
        services.AddSingleton<IOpenApiTagProvider, FilesOpenApiTags>();

        // Services
        services.AddScoped<IFileService, FileService>();
        services.AddScoped<IFolderService, FolderService>();
        services.AddScoped<IMountService, MountService>();
        // ... other services

        // Handlers
        services.AddScoped<IFileHandler, FileHandler>();
        services.AddScoped<IFolderHandler, FolderHandler>();
        services.AddScoped<IMountHandler, MountHandler>();
        // ... other handlers

        // Background jobs
        services.AddScoped<GenerateThumbnailJob>();

        // Permissions
        services.AddSingleton<IPermissionContributor, FilesPermissionContributor>();
        services.AddPermissionPolicies(FilePermissions.All);

        return services;
    }

    public static WebApplication MapMwtFilesEndpoints(this WebApplication app)
    {
        new FileEndpoints().Register(app);
        new FolderEndpoints().Register(app);
        new MountEndpoints().Register(app);
        // ... other endpoints
        return app;
    }
}
```

Called in `Program.cs`:

```csharp
builder.Services.AddMwtOrganisation();
builder.Services.AddMwtFiles(builder.Configuration);
builder.Services.AddMwtEmail(builder.Configuration);
builder.Services.AddMwtNotifications();
builder.Services.AddMwtEconomy();
// ...

app.MapMwtFilesEndpoints();
app.MapMwtEmailEndpoints();
app.MapMwtEconomyEndpoints();
```

**Note:** Top-level API handlers and services that do not belong to a domain module (dev-only endpoints, health checks, admin dashboard) are registered directly in `Program.cs` alongside module calls — they live under `Mwt.Api/Handlers/` and `Mwt.Api/Services/` and are wired with plain `services.AddScoped<I, T>()` calls. A future `AddMwtApi()` extension could group these, but today they are inline.

## Lifetimes

| Component | Lifetime | Reason |
|-----------|----------|--------|
| `ICustomLogger` | Singleton | Stateless |
| `HttpClient` | Singleton | Connection pooling |
| `IAzureCredentialProvider` | Singleton | Stateless credential access |
| `DbContext` | Scoped | Tracks changes per request |
| Services | Scoped | Use scoped DbContext |
| Handlers | Scoped | Use scoped services |

## Constructor Limits

| Layer | Max Params | If Exceeded |
|-------|------------|-------------|
| Handlers | 5 | Split handler |
| Services | 7 | Extract facade or split service |

## Anti-Patterns

```csharp
// ❌ Handler injecting DbContext
public class OrderHandler(AppDbContext db) { }

// ✅ Handler injecting service
public class OrderHandler(IOrderService svc, ICustomLogger log) { }

// ❌ Service injecting handler
public class OrderService(ICustomerHandler handler) { }

// ✅ Service injecting another service
public class OrderService(ICustomerService customerSvc, AppDbContext db) { }

// ❌ Circular dependency
public class ServiceA(IServiceB b) { }
public class ServiceB(IServiceA a) { }
```

## Related

- [../api/handlers.md](../api/handlers.md) - Handler patterns
- [../api/services.md](../api/services.md) - Service patterns
