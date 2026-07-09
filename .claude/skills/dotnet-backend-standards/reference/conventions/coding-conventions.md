# Coding Conventions

## Summary  

General coding style conventions for backend C# code.

## Rules  

- ✅ Prefer collection expressions (`[]`) for initializing collections instead of `new List<T>()`.
- ✅ Prefer `.Count > 0` over `.Any()` for performance when checking if a collection has elements.
- ✅ Use `enum` types instead of string constants for domain values such as `SystemRole`, `Role`, `PermissionLevel`, and `AccessType`.
- ✅ Use primary constructors!

## Examples

### Collection Initialization  

```csharp
// Bad
ICollection<GroupMember> members = new List<GroupMember>();

// Good
ICollection<GroupMember> members = [];
```

### Performance: Count vs Any

```csharp
// Bad - .Any() has overhead for empty collections
if (items.Any())
{
    // process items
}

// Good - .Count > 0 is more efficient
if (items.Count > 0)
{
    // process items
}
```

### Enum Based Domain Values  

```csharp
public enum SystemRole
{
    SystemAdmin,
    User
}

public enum OrganizationRole
{
    Owner,
    Member
}

public enum PermissionLevel
{
    Read,
    Write
}

public enum AccessType
{
    Private,
    OrganizationWide
}
```
