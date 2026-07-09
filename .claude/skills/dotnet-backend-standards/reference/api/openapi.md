# OpenAPI + Scalar

The API ships a machine-readable OpenAPI document and an interactive UI. Use the built-in
**`Microsoft.AspNetCore.OpenApi`** (`AddOpenApi`/`MapOpenApi`) for the document and
**`Scalar.AspNetCore`** for the UI. Swashbuckle is not used.

> Endpoints already carry their own metadata (`.WithName/.WithSummary/.WithDescription/.Produces/.WithTags`)
> — see [`api-endpoints.md`](api-endpoints.md). That metadata feeds the document automatically;
> this file covers the document/version/tags/UI wiring that sits above it.

## Packages

```xml
<PackageReference Include="Microsoft.AspNetCore.OpenApi" />   <!-- matches the target TFM, e.g. 10.0.x -->
<PackageReference Include="Scalar.AspNetCore" />              <!-- 2.x -->
```

## Single source of the version

The API version lives in **one** place and is reused for both the route prefix and the
document. Put it on the shared route base:

```csharp
// Core/Contracts/ApiConstants.cs
namespace Core.Contracts;

public static class ApiConstants
{
    public const string ApiVersion = "v1";
    public const string DocumentName = "v1";
    public const string RouteBase = $"/api/{ApiVersion}";   // every module route hangs off this
}
```

All module routes are anchored to `ApiConstants.RouteBase` (see
[`api-naming-conventions.md`](api-naming-conventions.md)), so the version appears in the path
(`/api/v1/...`) and in the document — never hard-coded twice.

## Document configuration

Register the document under the version name and shape its `Info` + `Tags` with document
transformers:

```csharp
// Api/Configuration/OpenApiConfig.cs
public static class OpenApiConfig
{
    public static IServiceCollection AddOpenApiDocs(this IServiceCollection services)
    {
        services.AddOpenApi(ApiConstants.DocumentName, options =>
        {
            options.AddDocumentTransformer<ApiInfoTransformer>();
            options.AddDocumentTransformer<TagDescriptionTransformer>();
            // Add a SecuritySchemeTransformer here if the API is authenticated.
        });
        return services;
    }

    public static WebApplication MapOpenApiDocs(this WebApplication app)
    {
        app.MapOpenApi();                          // /openapi/v1.json
        app.MapScalarApiReference("/scalar", options => options
            .WithTitle(ApiInfoTransformer.Title)
            .WithTheme(ScalarTheme.Purple));       // UI at /scalar
        return app;
    }
}
```

`ApiInfoTransformer` sets the document `Info` from the single version constant:

```csharp
internal sealed class ApiInfoTransformer : IOpenApiDocumentTransformer
{
    public const string Title = "Example API";

    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken ct)
    {
        document.Info = new OpenApiInfo
        {
            Title = Title,
            Version = ApiConstants.ApiVersion,
            Description = "…",
            Contact = new OpenApiContact { Name = "Example Team" },
        };
        return Task.CompletedTask;
    }
}
```

## Per-module tags

Each module documents its tag (name + Markdown description) by implementing
`IOpenApiTagProvider`; a single document transformer aggregates them. This keeps tag docs in
the module they describe.

```csharp
// Core/Contracts/IOpenApiTagProvider.cs
namespace Core.Contracts;

/// <summary>Modules implement this to contribute an OpenAPI tag + description.</summary>
public interface IOpenApiTagProvider
{
    string Tag { get; }
    string? Description { get; }
}

/// <summary>Inline provider so modules can register a tag without a class each.</summary>
public sealed record OpenApiTagDescriptor(string Tag, string? Description = null) : IOpenApiTagProvider;
```

```csharp
// Api/Configuration/TagDescriptionTransformer.cs
internal sealed class TagDescriptionTransformer(IEnumerable<IOpenApiTagProvider> providers)
    : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken ct)
    {
        document.Tags ??= [];
        foreach (var p in providers)
        {
            var existing = document.Tags.FirstOrDefault(t => t.Name == p.Tag);
            if (existing is not null) existing.Description = p.Description;
            else document.Tags.Add(new OpenApiTag { Name = p.Tag, Description = p.Description });
        }
        return Task.CompletedTask;
    }
}
```

Each module registers its tag in its `Add<Module>()` extension (see
[`../infrastructure/dependency-injection.md`](../infrastructure/dependency-injection.md)):

```csharp
services.AddSingleton<IOpenApiTagProvider>(new OpenApiTagDescriptor(Routes.Tag, "Order management…"));
// or a dedicated class for long Markdown: services.AddSingleton<IOpenApiTagProvider, OrdersOpenApiTags>();
```

Endpoints attach the tag via the group: `app.MapGroup("").WithTags(Routes.Tag)`.

## Reusable description fragments

Keep endpoint descriptions consistent with a small helper rather than ad-hoc strings:

```csharp
// Core/Endpoints/OpenApiSpecifications.cs
public static class OpenApiSpecifications
{
    public static string RequiredPermission(string permission)
        => $"**Required Permission:** `{permission}`\n\n";
}
```

```csharp
.WithDescription(OpenApiSpecifications.RequiredPermission(Permissions.Order.Read)
    + "Returns the order by id.")
```

## Pipeline placement

Map the document + UI early in the app phase. Gate exposure per environment/auth as the
project requires (e.g. dev-only, or behind auth in production):

```csharp
var app = builder.Build();
app.MapOpenApiDocs();   // /openapi/v1.json + /scalar
// … UseRouting / auth / endpoints …
app.MapModuleEndpoints();
```

## Checklist

- [ ] Version defined once on `ApiConstants` and reused for route prefix + document.
- [ ] `AddOpenApi(ApiConstants.DocumentName, …)` with an `Info` transformer (title + version).
- [ ] Every module registers an `IOpenApiTagProvider`; endpoints set `.WithTags(Routes.Tag)`.
- [ ] `MapOpenApi()` + `MapScalarApiReference("/scalar", …)` wired; exposure gated per environment.
- [ ] Authenticated APIs add a security-scheme document transformer.
