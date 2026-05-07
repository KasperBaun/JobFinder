# Maintainability Rules

## Summary

Maintainability standards to ensure code remains readable, testable, and modifiable.

## 🚨 CRITICAL: File Size Limits

### Production Code - Hard Limits (MUST NOT EXCEED)
- **Maximum file size**: 300 lines per class file
- **Maximum method size**: 50 lines per method
- **Maximum constructor parameters**: 7 parameters
- **Maximum public methods per class**: 12 methods

### Production Code - Recommended Limits (TARGET)
- **Optimal file size**: 150-250 lines
- **Optimal method size**: 30 lines
- **Optimal constructor parameters**: 3-5 parameters
- **Optimal public methods**: 5-8 methods

### Test Code - File Size Limits by Type
> See [DECISION-001](../../../decisions/DECISION-001-test-file-size-limits.md) for full rationale

| Test Type | Hard Limit | Recommended Target | Rationale |
|-----------|------------|-------------------|-----------|
| **Unit Tests** | 300 lines | 150-250 lines | Unit tests should be small and focused |
| **Integration Tests** | 500 lines | 250-400 lines | Allow for setup overhead, multiple scenarios |
| **E2E Tests** | 500 lines | 250-400 lines | Complex workflows require more setup |

**When to split test files:**
- DO split: Testing unrelated concerns, file exceeds target significantly, hard to navigate
- DON'T split: Testing variations of same concern, would duplicate setup, cohesive and under hard limit

### Refactoring Triggers (Production Code)
| Metric | Warning Level | Action Required | Critical Level | Immediate Action |
|--------|--------------|-----------------|----------------|------------------|
| File Size | >250 lines | Plan refactoring | >300 lines | BLOCK MERGE |
| Method Size | >20 lines | Consider splitting | >30 lines | MUST REFACTOR |
| Constructor Params | >5 | Review dependencies | >7 | Extract services |
| Cyclomatic Complexity | >7 | Simplify logic | >10 | MUST SIMPLIFY |
| Class Responsibilities | >1 | Evaluate SRP | >2 | VIOLATES SOLID |

## Rules

### File Organization
- ✅ **One class per file** (except nested private classes)
- ✅ **Logical method ordering**: Constructor → Public → Protected → Private
- ✅ **Related functionality grouped** together
- ✅ **Clear separation** between business logic and infrastructure
- ❌ **NO "God classes"** doing everything
- ❌ **NO production code files exceeding 300 lines** (test files have different limits, see above)

### Method Complexity
- ✅ **Single responsibility** per method
- ✅ **Early returns** to reduce nesting
- ✅ **Guard clauses** at method start
- ✅ **Maximum 3 levels** of nesting
- ✅ **Descriptive method names** that explain intent
- ❌ **NO methods exceeding 30 lines**
- ❌ **NO deeply nested if/else chains** (use strategy pattern)

### Class Design
- ✅ **Single Responsibility Principle** (SRP) - one reason to change
- ✅ **Dependency Injection** for all dependencies
- ✅ **Interface segregation** - small, focused interfaces
- ✅ **Composition over inheritance**
- ❌ **NO circular dependencies**
- ❌ **NO static mutable state**
- ❌ **NO business logic in DTOs/Entities**

### Dependency Management
- ✅ **Constructor injection** preferred
- ✅ **Maximum 5-7 constructor parameters**
- ✅ **Group related dependencies** into service facades
- ✅ **Use factory pattern** for complex object creation
- ❌ **NO service locator pattern**
- ❌ **NO hidden dependencies**

## Refactoring Patterns

### Service Extraction Pattern
When a service exceeds 300 lines, extract domain-specific services:

```csharp
// BEFORE: Monolithic 1500+ line service
public class AnalyticsService : IAnalyticsService
{
    // 50+ methods handling everything
    public async Task<TimeDistribution> GetTimeDistribution() { }
    public async Task<ProductivityMetrics> CalculateProductivity() { }
    public async Task<TrendAnalysis> AnalyzeTrends() { }
    // ... 47 more methods
}

// AFTER: Domain-specific services
public class TimeDistributionService : ITimeDistributionService
{
    // 8-10 focused methods
    public async Task<TimeDistribution> GetDaily() { }
    public async Task<TimeDistribution> GetWeekly() { }
    // ... related methods only
}

public class ProductivityService : IProductivityService
{
    // 8-10 focused methods
    public async Task<ProductivityMetrics> Calculate() { }
    public async Task<ProductivityScore> GetScore() { }
}

// Orchestrator for coordination
public class AnalyticsOrchestrator : IAnalyticsService
{
    private readonly ITimeDistributionService _timeService;
    private readonly IProductivityService _productivityService;
    
    // Thin orchestration layer
    public async Task<AnalyticsSummary> GetSummary()
    {
        var time = await _timeService.GetWeekly();
        var productivity = await _productivityService.Calculate();
        return new AnalyticsSummary(time, productivity);
    }
}
```

### Method Extraction Pattern
When a method exceeds 30 lines, extract logical blocks:

```csharp
// BEFORE: 50+ line method
public async Task<Result> ProcessTimeEntry(TimeEntryDto dto)
{
    // Validation logic (15 lines)
    if (dto.StartTime > dto.EndTime) { }
    if (dto.Category == null) { }
    // ... more validation
    
    // Business logic (20 lines)
    var overlapping = await CheckOverlaps();
    var calculations = PerformCalculations();
    // ... more logic
    
    // Persistence (15 lines)
    var entity = MapToEntity(dto);
    await _context.SaveChangesAsync();
    // ... more persistence
}

// AFTER: Extracted methods
public async Task<Result> ProcessTimeEntry(TimeEntryDto dto)
{
    var validationResult = ValidateTimeEntry(dto);
    if (!validationResult.IsValid) 
        return Result.Failure(validationResult.Errors);
    
    var businessResult = await ApplyBusinessRules(dto);
    if (!businessResult.IsValid)
        return Result.Failure(businessResult.Errors);
    
    return await PersistTimeEntry(dto);
}

private ValidationResult ValidateTimeEntry(TimeEntryDto dto) { /* 10 lines */ }
private async Task<BusinessResult> ApplyBusinessRules(TimeEntryDto dto) { /* 15 lines */ }
private async Task<Result> PersistTimeEntry(TimeEntryDto dto) { /* 10 lines */ }
```

### Interface Segregation Pattern
When interfaces become too large, split them:

```csharp
// BEFORE: Fat interface
public interface IUserService
{
    // Authentication
    Task<User> Authenticate(string username, string password);
    Task<Token> RefreshToken(string token);
    
    // Profile management
    Task<UserProfile> GetProfile(Guid userId);
    Task UpdateProfile(UserProfile profile);
    
    // Permissions
    Task<List<Permission>> GetPermissions(Guid userId);
    Task GrantPermission(Guid userId, Permission permission);
    
    // Activity tracking
    Task<List<Activity>> GetRecentActivity(Guid userId);
    Task LogActivity(Activity activity);
}

// AFTER: Segregated interfaces
public interface IAuthenticationService
{
    Task<User> Authenticate(string username, string password);
    Task<Token> RefreshToken(string token);
}

public interface IUserProfileService
{
    Task<UserProfile> GetProfile(Guid userId);
    Task UpdateProfile(UserProfile profile);
}

public interface IPermissionService
{
    Task<List<Permission>> GetPermissions(Guid userId);
    Task GrantPermission(Guid userId, Permission permission);
}
```

### Code Review Checklist
- [ ] All files under 300 lines
- [ ] All methods under 50 lines
- [ ] SOLID principles followed
- [ ] No code duplication (DRY)
- [ ] Clear separation of concerns
- [ ] Appropriate abstraction levels
- [ ] Testable design
- [ ] No circular dependencies

### Continuous Monitoring
- Track file size trends
- Monitor complexity metrics
- Identify refactoring candidates
- Maintain technical debt register

## Examples of Violations

### ❌ BAD: Monolithic Service
```csharp
public class ProjectService : IProjectService
{
    // 1500+ lines handling everything project-related
    // - Project CRUD
    // - Time tracking
    // - Budget calculations
    // - Report generation
    // - Permission checks
    // - Email notifications
    // - File attachments
    // - Activity logging
    // - Search functionality
    // - Export/Import
}
```

### ✅ GOOD: Separated Concerns
```csharp
public class ProjectService : IProjectService
{
    // 200 lines - Core project CRUD only
}

public class ProjectTimeService : IProjectTimeService
{
    // 150 lines - Time tracking for projects
}

public class ProjectBudgetService : IProjectBudgetService
{
    // 180 lines - Budget calculations
}

public class ProjectReportService : IProjectReportService
{
    // 200 lines - Report generation
}
```

## Related Rules

- [SOLID Principles](./coding-conventions.md)
- [Handler Pattern](./handlers.md)
- [Service Layer](./dependency-injection.md)
- [Testing Standards](./testing/unit-tests.md)