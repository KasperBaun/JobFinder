# Background Jobs

## Summary

Guidelines for implementing background job processing using Hangfire with entity-change hook integration.

## Rules  

- ✅ Use Hangfire for reliable background job processing
- ✅ Configure durable storage (SQL Server, PostgreSQL, etc.) for job persistence
- ✅ Use scoped services within job execution context
- ✅ Implement retry logic for transient failures
- ✅ Monitor job execution via Hangfire dashboard
- ✅ Secure Hangfire dashboard in production environments

## Examples

### Hangfire Configuration  

```csharp
// In HangfireConfig.cs
public static void AddHangFire(WebApplicationBuilder builder)
{
    builder.Services.AddHangfire(cfg => {
        var conn = builder.Configuration.GetConnectionString("HangfireConnection");
        cfg.SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
           .UseSimpleAssemblyNameTypeSerializer()
           .UseRecommendedSerializerSettings()
           .UseSqlServerStorage(conn);
    });
    builder.Services.AddHangfireServer();
}

public static void UseHangfireUI(WebApplication app)
{
    app.UseHangfireDashboard("/hangfire", new DashboardOptions
    {
        Authorization = new[] { new HangfireAuthFilter() } // Secure in production
    });
}
```

### Manual Job Enqueuing  

```csharp
// For non-entity related background tasks
public class EmailService
{
    public void SendWelcomeEmailAsync(string email, string name)
    {
        BackgroundJob.Enqueue<IMailService>(x => x.SendWelcomeEmailAsync(email, name));
    }
    
    public void SendReminderEmail(string email, TimeSpan delay)
    {
        BackgroundJob.Schedule<IMailService>(
            x => x.SendReminderEmailAsync(email), 
            delay);
    }
}
```

## Job Monitoring

### Dashboard Access  

- **Development**: `http://localhost:<port>/hangfire`
- **Production**: Secure with authentication filters

### Job States  

- **Enqueued**: Job is waiting to be processed
- **Processing**: Job is currently executing
- **Succeeded**: Job completed successfully
- **Failed**: Job failed and may be retried
- **Scheduled**: Job is scheduled for future execution
- **Deleted**: Job was manually deleted

## Error Handling  

```csharp
[AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 60, 300, 900 })]
public async Task ProcessEntityHookAsync(string entityId)
{
    try
    {
        // Job logic here
    }
    catch (TransientException ex)
    {
        // This will be retried automatically
        throw;
    }
    catch (PermanentException ex)
    {
        // Log and don't retry
        _logger.Error(ex, "Permanent failure processing entity {EntityId}", entityId);
        return; // Don't throw to prevent retry
    }
}
```

## Performance Considerations  

- **Connection Strings**: Use separate connection string for Hangfire storage
- **Worker Count**: Configure based on server capacity
- **Queue Priorities**: Use different queues for different priority jobs
- **Cleanup**: Configure automatic cleanup of old jobs

## Notes  

- Use scoped services within job execution context
- Monitor job failures and set up alerting for production
- Consider job serialization impact on performance
