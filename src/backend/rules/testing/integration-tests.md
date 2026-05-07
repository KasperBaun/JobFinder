# Integration Tests

## Summary

Guidelines for writing integration tests that verify complete workflows and system interactions.

## Rules

- ✅ Use `WebApplicationFactory` (or equivalent) for full system testing  
- ✅ Test complete user workflows end-to-end  
- ✅ Use a real database connection when possible  
- ✅ Mock only external dependencies (APIs, email, file storage, etc.)  
- ✅ Test hook/event execution and state transitions  
- ✅ Verify database state changes  
- ✅ Test authorization and authentication flows  
- ✅ Include timing considerations for background or async jobs  

---

## Examples

### Integration Test Base Class

```csharp
public class IntegrationTestBase : IClassFixture<CustomWebApplicationFactory<ApiProgram>>
{
    protected readonly CustomWebApplicationFactory<ApiProgram> Factory;
    protected readonly AppDbContext DbContext;
    protected readonly ICustomLogger Logger;

    public IntegrationTestBase(CustomWebApplicationFactory<ApiProgram> factory)
    {
        Factory = factory;
        
        using var scope = factory.Services.CreateScope();
        DbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Logger = scope.ServiceProvider.GetRequiredService<ICustomLogger>();
        
        // Clear database before each test
        DbContext.Database.EnsureDeleted();
        DbContext.Database.EnsureCreated();
    }
}
```

### Complete Workflow Integration Test

```csharp
public class EntityAWorkflowTests : IntegrationTestBase
{
    public EntityAWorkflowTests(CustomWebApplicationFactory<ApiProgram> factory) 
        : base(factory) { }

    [Fact]
    public async Task CompleteEntityAWorkflow_Success()
    {
        // Arrange
        var client = Factory.CreateAdminClient();
        
        // Mock external dependencies
        Factory.AdditionalTestServiceConfiguration = services =>
        {
            services.RemoveAll<IExternalService>();
            var serviceMock = Substitute.For<IExternalService>();
            serviceMock.CheckExistsAsync(Arg.Any<string>()).Returns(false);
            serviceMock.CreateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
                        .Returns("new-id");
            serviceMock.AssignAsync(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
            services.AddSingleton(serviceMock);

            services.RemoveAll<IEmailService>();
            var emailMock = Substitute.For<IEmailService>();
            emailMock.SendAsync(Arg.Any<EmailModel>()).Returns(Task.CompletedTask);
            services.AddSingleton(emailMock);
        };

        var dto = new { Name = "Integration Test EntityA", Email = "test@example.com", FirstName = "Integration", LastName = "Test" };

        // Act - Step 1: Create entity
        var createResponse = await client.PostAsJsonAsync("/api/entityA", dto);
        createResponse.EnsureSuccessStatusCode();

        // Allow hooks/events to process
        await Task.Delay(2000);

        // Assert creation and processing
        var entity = DbContext.EntityAItems.Include(e => e.RelatedEntityB).Single(e => e.Email == dto.Email);
        Assert.Equal(EntityAState.ExternalResourceCreated, entity.State);
        Assert.NotNull(entity.RelatedEntityB);
        Assert.Equal(dto.Name, entity.RelatedEntityB.Name);

        // Act - Step 2: Accept entity
        var acceptResponse = await client.PostAsync($"/api/entityA/{entity.Id}/accept", null);
        acceptResponse.EnsureSuccessStatusCode();

        await Task.Delay(1000);

        // Assert accepted state
        var accepted = DbContext.EntityAItems.Find(entity.Id);
        Assert.Equal(EntityAState.Accepted, accepted!.State);
        Assert.True(accepted.RelatedEntityB.IsActive);
    }
}
```

### Database State Verification

```csharp
[Fact]
public async Task Hooks_ProcessStateTransitions_CorrectDatabaseState()
{
    // Arrange
    var entity = new EntityApplication
    {
        RelatedEntityB = TestDataFactory.GetNewEntityB(),
        State = EntityApplicationState.Requested,
        PreviousState = EntityApplicationState.Requested,
        InitiatedOn = DateTime.UtcNow
    };

    DbContext.EntityApplications.Add(entity);
    await DbContext.SaveChangesAsync();

    // Act - Saving triggers hooks/events
    var saved = DbContext.EntityApplications.Include(a => a.RelatedApproval).First(a => a.Id == entity.Id);

    await Task.Delay(2000);

    // Refresh from database
    DbContext.Entry(saved).Reload();
    if (saved.RelatedApproval != null)
    {
        DbContext.Entry(saved.RelatedApproval).Collection(a => a.Votes).Load();
    }

    // Assert
    Assert.Equal(EntityApplicationState.VoteListPrepared, saved.State);
    Assert.NotNull(saved.RelatedApproval);
    Assert.True(saved.RelatedApproval.Votes.Count > 0);
    foreach (var vote in saved.RelatedApproval.Votes)
    {
        Assert.NotEqual(Guid.Empty, vote.VoteCode);
        Assert.False(string.IsNullOrEmpty(vote.VoterEmail));
        Assert.False(string.IsNullOrEmpty(vote.VoterName));
    }
}
```

### Authorization Integration Tests

```csharp
[Theory]
[InlineData("/api/entityA/applications", HttpStatusCode.Forbidden, "UserRole")]
[InlineData("/api/entityA/applications", HttpStatusCode.OK, "AdminRole")]
[InlineData("/api/entityA/applications", HttpStatusCode.OK, "ManagerRole")]
public async Task GetApplications_AuthorizationCheck(string endpoint, HttpStatusCode expectedStatus, string role)
{
    // Arrange
    var client = Factory.CreateClientWithRole(role);

    // Act
    var response = await client.GetAsync(endpoint);

    // Assert
    Assert.Equal(expectedStatus, response.StatusCode);
}
```

### Error Handling Integration Tests

```csharp
[Fact]
public async Task ExternalServiceFailure_HandledGracefully()
{
    // Arrange - Make external service fail
    Factory.AdditionalTestServiceConfiguration = services =>
    {
        services.RemoveAll<IExternalService>();
        var serviceMock = Substitute.For<IExternalService>();
        serviceMock.CheckExistsAsync(Arg.Any<string>()).ThrowsAsync<HttpRequestException>();
        services.AddSingleton(serviceMock);
    };

    var client = Factory.CreateAdminClient();
    var dto = new { Name = "Failing Entity", Email = "fail@example.com", FirstName = "Fail", LastName = "Test" };

    // Act
    var response = await client.PostAsJsonAsync("/api/entityA", dto);
    response.EnsureSuccessStatusCode();

    await Task.Delay(2000);

    var entity = DbContext.EntityAItems.Single(e => e.Email == dto.Email);
    Assert.Equal(EntityAState.Failed, entity.State);
    Assert.False(string.IsNullOrEmpty(entity.ErrorMessage));
}
```

### Helper Methods

```csharp
private async Task<T> GetEntityFromResponse<T>(HttpResponseMessage response)
{
    var content = await response.Content.ReadAsStringAsync();
    return JsonSerializer.Deserialize<T>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
}

private async Task WaitForStateTransition<T>(int entityId, Func<T, bool> predicate, int timeoutMs = 5000) where T : class
{
    var stopwatch = Stopwatch.StartNew();
    while (stopwatch.ElapsedMilliseconds < timeoutMs)
    {
        var entity = await DbContext.Set<T>().FindAsync(entityId);
        if (entity != null && predicate(entity))
            return;
        await Task.Delay(100);
        DbContext.Entry(entity!).Reload();
    }
    throw new TimeoutException($"Entity {typeof(T).Name} did not reach expected state within {timeoutMs}ms");
}
```

---

## Notes

- Use longer delays for tests involving hooks/events and background jobs  
- Mock external services but test against a real database when possible  
- Cover both **happy path** and **error scenarios**  
- Include authorization tests  
- In CI/CD, consider using isolated DB containers for test consistency  
