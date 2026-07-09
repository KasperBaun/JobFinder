# Logging

## Summary  

Guidelines for implementing structured logging using a custom logging interface (your structured logger abstraction).

## Rules  

- ✅ Use your structured logger abstraction (e.g. `ICustomLogger`) for all application logging  
- ✅ Log with structured parameters using placeholders `{Parameter}`  
- ✅ Include relevant context information (IDs, emails, states)  
- ✅ Use appropriate log levels (Info, Warning, Error)  
- ✅ Log state transitions and important operations  
- ✅ Log exceptions with full context  
- ✅ Avoid logging sensitive information (passwords, tokens)  
- ✅ Use consistent log message formats  

## Examples

### Custom Logger Configuration  

```csharp
// In Program.cs
builder.Services.AddSingleton<ICustomLogger>(sp =>
{
    var ext = sp.GetRequiredService<ILogger<CustomLogger>>();
    return new CustomLogger(isEnabled: true, prefix: "Log", externalLogger: ext);
});
```

### Custom Logger Interface  

```csharp
public interface ICustomLogger
{
    void Info(string message);
    void Info(string messageTemplate, params object[] args);
    void Warning(string message);
    void Warning(string messageTemplate, params object[] args);
    void Error(string message);
    void Error(string messageTemplate, params object[] args);
    void Error(Exception exception, string messageTemplate, params object[] args);
}
```

### Structured Logging in Services  

```csharp
public async Task OnApprovedAsync(EntityApplication app, AppDbContext db)
{
    if (app.State != EntityApplicationState.Approved) 
    {
        _log?.Info("OnApprovedAsync triggered for id {EntityId}. State: {EntityState}. No action taken.", 
            app.Id, app.State);
        return;
    }

    _log?.Info("OnApprovedAsync triggered for id {EntityId}. State: {EntityState}.", 
        app.Id, app.State);

    try
    {
        await CreateEntityAsync(app, db);
        
        _log?.Info("OnApprovedAsync completed for id {EntityId}. Entity created successfully.", 
            app.Id);
    }
    catch (Exception ex)
    {
        _log?.Error(ex, "OnApprovedAsync failed for id {EntityId}. State: {EntityState}", 
            app.Id, app.State);
        throw;
    }
}
```

### Handler Logging  

```csharp
public static async Task<IResult> RecordActionAsync(
    Guid applicationId,
    string email,
    string code,
    AppDbContext db,
    ICustomLogger log)
{
    log?.Info("RecordActionAsync started for application {ApplicationId}. User: {Email}", 
        applicationId, email);

    try
    {
        var record = await db.ActionVotes
            .FirstOrDefaultAsync(v => v.Code.ToString() == code && 
                                     v.UserEmail.ToLower() == email.ToLower());

        if (record == null)
        {
            log?.Warning("RecordActionAsync: Invalid code {Code} for user {Email}", 
                code, email);
            return Results.NotFound("Invalid code or email");
        }

        if (record.CompletedOn != default)
        {
            log?.Warning("RecordActionAsync: Action already recorded for user {Email}. Application: {ApplicationId}", 
                email, applicationId);
            return Results.BadRequest("Action already recorded");
        }

        record.CompletedOn = DateTime.UtcNow;
        await db.SaveChangesAsync();

        log?.Info("RecordActionAsync completed successfully for application {ApplicationId}. User: {Email}", 
            applicationId, email);
        
        return Results.Ok();
    }
    catch (Exception ex)
    {
        log?.Error(ex, "RecordActionAsync failed for application {ApplicationId}. User: {Email}", 
            applicationId, email);
        return Results.Problem("An error occurred while recording the action");
    }
}
```

### Communication Service Logging  

```csharp
public async Task SendNotificationEmailAsync(
    ActionVote vote,
    EntityApplication application,
    string actionUrl,
    AppDbContext db)
{
    _log?.Info("Sending notification email to {UserEmail} for application {ApplicationId}", 
        vote.UserEmail, application.Id);

    try
    {
        var mailModel = new SendMailModel
        {
            ToEmail = vote.UserEmail,
            Subject = $"Action Request - {application.EntityName}",
            // ... email content
        };

        await _mailService.SendMailAsync(mailModel);
        
        _log?.Info("Notification email sent successfully to {UserEmail} for application {ApplicationId}", 
            vote.UserEmail, application.Id);
    }
    catch (Exception ex)
    {
        _log?.Error(ex, "Failed to send notification email to {UserEmail} for application {ApplicationId}", 
            vote.UserEmail, application.Id);
        throw;
    }
}
```

## Log Level Guidelines

### Info Level  

- Successful business operations  
- State transitions  
- Integration calls (start/completion)  
- User actions  

### Warning Level  

- Business rule violations  
- Invalid requests handled gracefully  
- Retryable failures  

### Error Level  

- Exceptions preventing operation completion  
- Integration failures  
- Data consistency issues  
- Security violations  

## Log Message Templates  

```csharp
// State transitions
"Entity {EntityType} {EntityId} transitioned from {PreviousState} to {CurrentState}"

// User actions
"User {UserEmail} performed action {Action} on {EntityType} {EntityId}"

// Integration calls
"Started {IntegrationName} call for {Purpose}. Parameters: {Parameters}"
"Completed {IntegrationName} call for {Purpose}. Duration: {Duration}ms"

// Errors
"Operation {OperationName} failed for {EntityType} {EntityId}. Error: {ErrorMessage}"
```

## Notes  

- Use consistent parameter names across the application  
- Include timing information for performance monitoring  
- Log both successful and failed operations  
- Consider log retention and storage costs in production  
- Use correlation IDs for tracking related operations across services  
