# End-to-End Tests

## Summary

Guidelines for implementing end-to-end tests that verify complete user journeys across the entire system.

## Rules

- ✅ Test complete user journeys from UI to database  
- ✅ Use realistic but generic test data and scenarios  
- ✅ Test across different user roles and permissions  
- ✅ Include timing considerations for async operations  
- ✅ Verify email notifications and external integrations  
- ✅ Test error scenarios and recovery paths  
- ✅ Use page object pattern for UI interactions  
- ✅ Maintain test data independence  

## Examples

### E2E Test Base Class

```csharp
public abstract class E2ETestBase : IAsyncLifetime
{
    protected readonly WebApplicationFactory<Program> Factory;
    protected readonly HttpClient ApiClient;
    protected readonly AppDbContext DbContext;
    
    protected E2ETestBase()
    {
        Factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureServices(services =>
                {
                    ConfigureTestServices(services);
                });
            });
            
        ApiClient = Factory.CreateClient();
        
        using var scope = Factory.Services.CreateScope();
        DbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    }
    
    protected virtual void ConfigureTestServices(IServiceCollection services)
    {
        // Override in derived classes for specific service mocking
    }
    
    public async Task InitializeAsync()
    {
        await DbContext.Database.EnsureDeletedAsync();
        await DbContext.Database.EnsureCreatedAsync();
        await SeedTestDataAsync();
    }
    
    public async Task DisposeAsync()
    {
        await DbContext.DisposeAsync();
        await Factory.DisposeAsync();
    }
    
    protected abstract Task SeedTestDataAsync();
}
```

### Complete EntityA Onboarding Journey

```csharp
public class EntityAOnboardingE2ETests : E2ETestBase
{
    private EntityB _testEntityB = null!;
    private List<User> _approverUsers = null!;
    
    protected override async Task SeedTestDataAsync()
    {
        // Create test EntityB
        _testEntityB = new EntityB 
        { 
            Name = "E2E Test EntityB",
            Description = "EntityB for E2E testing",
            IsActive = true 
        };
        DbContext.EntityBItems.Add(_testEntityB);
        
        // Create approver users
        _approverUsers = new List<User>
        {
            new User { Email = "approver1@example.com", FirstName = "Approver", LastName = "One" },
            new User { Email = "approver2@example.com", FirstName = "Approver", LastName = "Two" },
            new User { Email = "approver3@example.com", FirstName = "Approver", LastName = "Three" }
        };
        DbContext.Users.AddRange(_approverUsers);
        
        await DbContext.SaveChangesAsync();
    }
}
```
