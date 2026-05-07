# Refactoring Strategy Guide

## Preferred Approach: Partial Classes

**Use partial classes to split large services by area of responsibility.** This maintains a single interface and DI registration while organizing code into logical files.

### File Structure
```
ServiceName/
├── ServiceName.cs           # Main: constructor, dependencies, shared helpers
├── ServiceName.Area1.cs     # Partial: methods for area 1
├── ServiceName.Area2.cs     # Partial: methods for area 2
└── ServiceName.Area3.cs     # Partial: methods for area 3
```

### Example

**Main file** (`AuthenticationService.cs`):
```csharp
public partial class AuthenticationService(...) : IAuthenticationService
{
    private readonly MWTDbContext _context = context;
    // Field assignments and shared private helpers only
}
```

**Partial files** (`AuthenticationService.Login.cs`):
```csharp
public partial class AuthenticationService
{
    public async Task<AuthResponseDto> LoginAsync(LoginRequestDto request) { ... }
    public async Task<AuthResponseDto> RefreshTokenAsync(string refreshToken) { ... }
}
```

### Naming Convention
- `FolderService.cs` → Main file (constructor, dependencies)
- `FolderService.Create.cs` → Creation operations
- `FolderService.Query.cs` → Query/search operations
- `FolderService.Delete.cs` → Deletion operations

### When to Use Partial Classes
- Service exceeds 300 lines
- Methods group naturally by functional area
- Single responsibility with multiple operation types
- Need to maintain API compatibility

### Benefits
- Single DI registration
- Single interface
- Logical file organization
- No orchestrator overhead

## Alternative: Service Extraction

**Use service extraction only when:**
- Service violates Single Responsibility Principle
- Different areas need different dependencies
- Service exceeds ~800-1000 lines (too many partial files)
- You need independent testing/mocking of functionality

### Extraction Pattern
```
OriginalService (1500 lines)
    ↓
├── DomainService1 (250 lines)
├── DomainService2 (250 lines)
├── SharedUtilities (150 lines)
└── Orchestrator (100 lines)  # Implements original interface
```

### Other Patterns (When Appropriate)
- **Repository Extraction**: Separate data access from business logic
- **Command/Query Separation**: Split read and write operations
- **Strategy Pattern**: Replace giant switch/if-else with strategy interfaces

## Refactoring Checklist

### Pre-Refactoring
- [ ] Create git branch
- [ ] Identify domain boundaries
- [ ] Map dependencies
- [ ] Write characterization tests

### During Refactoring
- [ ] Extract one domain at a time
- [ ] Keep methods under 30 lines
- [ ] Keep files under 300 lines
- [ ] Maintain backward compatibility
- [ ] Run tests after each extraction

### Post-Refactoring
- [ ] All tests passing
- [ ] No build warnings
- [ ] Quality gates pass
- [ ] Code coverage ≥ 80%

## Common Pitfalls

### Don't
- Create services with multiple responsibilities
- Break existing APIs without migration plan
- Create circular dependencies
- Skip tests during refactoring

### Do
- Plan before coding
- Extract incrementally
- Maintain backward compatibility
- Follow SOLID principles
- Create focused, cohesive services

## Validation
```bash
# Check files under 300 lines
find . -name "*Service.cs" -exec wc -l {} \; | awk '$1 > 300 {print}'

# Run quality gates
dotnet build --warnaserror && dotnet test
```
