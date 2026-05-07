# Unit Tests

## Summary

Guidelines for writing unit tests using xUnit, FluentAssertions, and in-memory testing patterns.

## Rules

- ✅ Use xUnit as the testing framework
- ✅ Use FluentAssertions for all assertions (`.Should().Be(...)`, `.Should().NotBeNull()`, `.Should().Contain(...)`)
- ✅ Substitute dependencies by swapping DI registrations via `TestWebApplicationFactory.AdditionalTestServiceConfiguration` — use hand-rolled fakes (no NSubstitute / Moq)
- ✅ Use in-memory database for data access tests
- ✅ Follow AAA pattern (Arrange, Act, Assert)
- ✅ Use Theory tests for multiple input scenarios
- ✅ Test both happy path and error scenarios
- ✅ Use descriptive test method names
- ✅ Group related tests using nested classes or test collections

## Examples

### Basic Test Structure

```csharp
public class EntityATests : IClassFixture<CustomWebApplicationFactory<ApiProgram>>
{
    private readonly CustomWebApplicationFactory<ApiProgram> _factory;
    private readonly AppDbContext _ctx;
    private readonly ICustomLogger _log;

    public EntityATests(CustomWebApplicationFactory<ApiProgram> factory)
    {
        _factory = factory;

        using var scope = factory.Services.CreateScope();
        _ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        _log = scope.ServiceProvider.GetRequiredService<ICustomLogger>();
    }
}
```

### Theory Tests for Validation

```csharp
[Theory]
[InlineData(null, "a@example.com", "A", "B")]     // Name missing
[InlineData("Name", null, "A", "B")]              // Email missing
[InlineData("Name", "a@example.com", null, "B")]  // FirstName missing
[InlineData("Name", "a@example.com", "A", null)]  // LastName missing
public async Task CreateEntity_InvalidInput_ReturnsBadRequest(
    string? name, string? email, string? first, string? last)
{
    // Arrange
    var client = _factory.CreateAdminClient();
    var dto = new
    {
        Name = name,
        Email = email,
        FirstName = first,
        LastName = last
    };

    // Act
    var resp = await client.PostAsJsonAsync("/api/entityA", dto);

    // Assert
    resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
}
```

### Testing Business Logic with Hand-Rolled Fakes

Substitute a dependency by registering a fake implementation of its interface in place of the real one. `TestWebApplicationFactory.AdditionalTestServiceConfiguration` runs after the default registrations, so remove first, then add.

```csharp
// Hand-rolled fake — lives next to the test or in a Fakes/ folder
private sealed class FailingExternalService : IExternalService
{
    public Task<bool> AssignAsync(string a, string b) => Task.FromResult(false);
    public Task<bool> ExistsAsync(string id)          => Task.FromResult(true);
    public Task<ExternalEntity> GetAsync(string id)   => Task.FromResult(new ExternalEntity { Id = "fake" });
}

[Fact]
public async Task ExternalServiceFails_Assignment_SetsFailedState()
{
    // Arrange – swap IExternalService registration with the failing fake
    _factory.AdditionalTestServiceConfiguration = services =>
    {
        services.RemoveAll<IExternalService>();
        services.AddSingleton<IExternalService, FailingExternalService>();
    };

    var client = _factory.CreateAdminClient();
    var dto = new { name = "Failing Entity", email = "rep@fail.com", firstName = "Test", lastName = "Fail" };

    // Act
    var resp = await client.PostAsJsonAsync("/api/entityA", dto);

    // Assert
    resp.EnsureSuccessStatusCode();

    await Task.Delay(500);
    var entity = _ctx.EntityAItems.Single(i => i.Email == dto.email);
    entity.State.Should().Be(EntityAState.Failed);
    entity.ErrorMessage.Should().NotBeNullOrWhiteSpace();
}
```

### Testing Happy Path Scenarios

```csharp
[Fact]
public async Task CreateEntity_ValidRequest_PersistsEntities()
{
    // Arrange
    var client = _factory.CreateAdminClient();
    var dto = new
    {
        Name = "New Entity",
        Email = "contact@entity.com",
        FirstName = "Alice",
        LastName = "Example"
    };

    // Act
    var resp = await client.PostAsJsonAsync("/api/entityA", dto);

    // Assert
    resp.EnsureSuccessStatusCode();   // 200-OK

    var entity = _ctx.EntityAItems.Single(p => p.Name == dto.Name);
    entity.Email.Should().Be(dto.Email);
    entity.Initiator.Should().NotBeNull();
}
```

### Testing Conflict Scenarios

```csharp
[Fact]
public async Task CreateEntity_Duplicate_ReturnsConflict()
{
    // Arrange
    var existing = TestDataFactory.GetNewEntityA();
    _ctx.EntityAItems.Add(existing);

    var initiator = new User
    {
        Email = "admin@test.local",
        FirstName = "Unit",
        LastName = "Tester"
    };
    _ctx.Users.Add(initiator);

    _ctx.EntityAItems.Add(new EntityA
    {
        CreatedOn = DateTime.UtcNow,
        Initiator = initiator,
        State = EntityAState.Pending,
        Email = "john@example.com",
        FirstName = "John",
        LastName = "Doe"
    });

    await _ctx.SaveChangesAsync();

    var client = _factory.CreateAdminClient();
    var dto = new
    {
        Name = existing.Name,
        Email = "john@example.com",
        FirstName = "John",
        LastName = "Doe"
    };

    // Act
    var resp = await client.PostAsJsonAsync("/api/entityA", dto);

    // Assert
    resp.StatusCode.Should().Be(HttpStatusCode.Conflict);
}
```

### Test Data Factories

```csharp
public static class TestDataFactory
{
    public static EntityA GetNewEntityA()
    {
        return new EntityA
        {
            Name = $"EntityA {Guid.NewGuid():N}",
            Email = "test@entity.com",
            IsActive = true
        };
    }

    public static (User, User) GetNewUser()
    {
        var email = $"test{Guid.NewGuid():N}@test.local";
        var user1 = new User
        {
            Email = email,
            FirstName = "Test",
            LastName = "User"
        };

        var user2 = new User
        {
            Email = email,
            FirstName = "Test",
            LastName = "User"
        };

        return (user1, user2);
    }
}
```

### Test Web Application Factory

The real `TestWebApplicationFactory` lives in `Mwt.Tests/TestWebApplicationFactory.cs` and exposes `AdditionalTestServiceConfiguration` as the extension point for per-test DI swaps. It boots under `Environment = "Testing"`, replaces the PostgreSQL provider with EF Core in-memory, stubs Redis with `MemoryDistributedCache`, and registers the `TestAuthHandler` scheme so tests can build authenticated clients without a live auth provider.

```csharp
public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    public Action<IServiceCollection>? AdditionalTestServiceConfiguration { get; set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove the real EF Core provider and re-add in-memory
            RemoveEfRegistrations(services);
            var dbName = "TestDb_" + Guid.NewGuid();
            services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(dbName));

            // Stub Redis / distributed cache
            services.AddSingleton<IDistributedCache>(new MemoryDistributedCache(
                Options.Create(new MemoryDistributedCacheOptions())));

            // Register test auth scheme so CreateAdminClient() works
            services.AddAuthentication()
                .AddScheme<TestAuthSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });

            // Per-test overrides run LAST so they can replace anything above
            AdditionalTestServiceConfiguration?.Invoke(services);
        });

        builder.UseEnvironment("Testing");
    }
}
```

Tests never construct a factory directly — they receive one via `IClassFixture<TestWebApplicationFactory>` and set `AdditionalTestServiceConfiguration` in their Arrange block to swap specific registrations for that test only.

## Test Organization

```
Tests/
├── UseCases/
│   ├── UC01-EntityA/
│   │   └── EntityATests.cs
│   ├── UC02-EntityB/
│   │   └── EntityBTests.cs
│   └── UC03-Workflows/
│       ├── WorkflowTests.cs
│       └── HookTests.cs
├── WebAPI/
│   └── AuthEndpointTests.cs
├── ExternalIntegrations/
│   ├── ApiEndpointTests.cs
│   └── RepositoryTests.cs
└── Helpers/
    ├── TestDataFactories/
    └── CustomWebApplicationFactory.cs
```

## Notes

- Use the EF Core in-memory provider for isolation between tests (each test gets a unique DB name).
- Substitute external dependencies by swapping DI registrations with hand-rolled fakes — do not add NSubstitute or Moq to the project.
- Test both API endpoints and business logic separately.
- Use realistic but generic test data.
- Consider xUnit collections for shared expensive setup.
