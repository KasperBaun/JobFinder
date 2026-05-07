# Entity ID Standards

## Rule: Use GUID Type for Entity Primary Keys

**All entity primary keys MUST use `Guid` type, not `string`.**

### ✅ Correct Implementation

```csharp
public class TimeEntry
{
    [Key]
    public required Guid Id { get; set; } // ✅ GUID type
    
    // Foreign keys should also use Guid
    public Guid CategoryId { get; set; }
    public Guid? ClientId { get; set; }
}
```

### ❌ Incorrect Implementation

```csharp
public class TimeEntry
{
    [Key]
    public required string Id { get; set; } // ❌ String type
    
    // Even if you generate GUIDs as strings
    Id = Guid.NewGuid().ToString() // ❌ Still wrong approach
}
```

## Rationale

### Performance Benefits

- **Database Storage**: GUID uses 16 bytes vs 36-character string (36+ bytes)
- **Indexing**: Native GUID indexes are more efficient than string indexes
- **Query Performance**: GUID comparisons are faster than string comparisons

### Type Safety Benefits

- **Compile-time Validation**: Cannot accidentally pass string where GUID expected
- **IDE Support**: Better IntelliSense and refactoring support
- **Runtime Errors**: Fewer type conversion issues

### Consistency Benefits

- **Codebase Uniformity**: Matches existing ProjectOS entities (User, Project, etc.)
- **Pattern Recognition**: Developers know all IDs are GUIDs
- **Migration Safety**: Easier to bulk update references

## Implementation Guidelines

### 1. Entity Definition

```csharp
[Key]
public required Guid Id { get; set; }

// Use default GUID generation
public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
```

### 2. Service Layer

```csharp
// Generate new entities
var entity = new TimeEntry 
{
    Id = Guid.NewGuid(), // ✅ Native GUID
    // ...other properties
};

// Query by ID
var found = await context.TimeEntries.FindAsync(entityId); // Guid parameter
```

### 3. DTO Mapping

```csharp
public class TimeEntryDto
{
    public required Guid Id { get; set; } // ✅ Consistent with entity
    
    // Convert from entity
    Id = entity.Id // Direct assignment, no ToString()
}
```

### 4. API Parameters

```csharp
// Controller/Handler methods
public async Task<IResult> GetTimeEntry(Guid id) // ✅ Guid parameter
{
    var entry = await context.TimeEntries.FindAsync(id);
    // ...
}
```

## Migration Strategy

When converting existing `string` IDs to `Guid`:

### 1. Update Entity

```csharp
// Before
public required string Id { get; set; }

// After  
public required Guid Id { get; set; }
```

### 2. Create Migration

```bash
dotnet ef migrations add "ConvertIdsToGuid"
```

### 3. Data Migration (if needed)

```sql
-- Only if existing data needs conversion
UPDATE TimeEntries 
SET Id = NEWID() 
WHERE Id IS NULL OR Id = '';
```

## Exception Cases

The only acceptable exception is when interfacing with external systems that require string IDs:

```csharp
public class ExternalIntegrationEntity
{
    [Key]
    public required string ExternalId { get; set; } // ✅ External system requires string
    
    public required Guid InternalId { get; set; }   // ✅ Internal GUID for relationships
}
```

## Validation

All code reviews should verify:

- [ ] Primary keys use `Guid` type
- [ ] Foreign keys use `Guid` or `Guid?` types  
- [ ] No `Guid.NewGuid().ToString()` conversions
- [ ] API parameters accept `Guid` not `string`
- [ ] Database migrations handle GUID properly
