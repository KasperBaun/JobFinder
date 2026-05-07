# Entity Framework Core

## Summary  

Guidelines for Entity Framework Core data access patterns, migrations, and configuration.

## Rules  

- ✅ Use code-first migrations for database schema management
- ✅ Configure entities using Fluent API in `OnModelCreating`
- ✅ Use required navigation properties with proper foreign keys
- ✅ Enable retry on failure for SQL Server connections
- ✅ Apply migrations automatically in development

## Examples

### DbContext Configuration  

```csharp
public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    // DbSets
    public DbSet<Category> Categories { get; set; }
    public DbSet<Entity> Entities { get; set; }
    public DbSet<Application> Applications { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Configure entity relationships
        modelBuilder.Entity<Application>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.State).IsRequired();
            entity.Property(e => e.PreviousState).IsRequired();
            
            entity.HasOne(e => e.Category)
                  .WithMany()
                  .HasForeignKey(e => e.CategoryId)
                  .OnDelete(DeleteBehavior.Restrict);
                  
            entity.HasOne(e => e.Owner)
                  .WithMany()
                  .HasForeignKey(e => e.OwnerId)
                  .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
```

### Entity with State Machine  

```csharp
public enum ApplicationState
{
    Requested,
    ReviewListPrepared,
    ReviewRequested,
    ReviewPending,
    ReviewInProgress,
    Approved,
    Rejected
}

public class Application : IWorkflow<ApplicationState>
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    public required ApplicationState State { get; set; }
    
    [Required]
    public required ApplicationState PreviousState { get; set; }
    
    public DateTime InitiatedOn { get; set; }
    public DateTime? CompletedOn { get; set; }
    
    [Required]
    public required Category Category { get; set; }
    
    [Required]
    public required Entity Owner { get; set; }
    
    // Foreign keys
    [Required]
    public int CategoryId { get; set; }
    
    [Required]
    public int OwnerId { get; set; }
}
```

### Migration Commands  

```bash
# Add new migration
dotnet ef migrations add MigrationName \
  --project ".\[Project].DataAccess\[Project].DataAccess.csproj" \
  --startup-project ".\[Project].WebAPI\[Project].WebAPI.csproj"

# Update database
dotnet ef database update \
  --project ".\[Project].DataAccess\[Project].DataAccess.csproj" \
  --startup-project ".\[Project].WebAPI\[Project].WebAPI.csproj"

# Generate SQL script
dotnet ef migrations script \
  --project ".\[Project].DataAccess\[Project].DataAccess.csproj" \
  --startup-project ".\[Project].WebAPI\[Project].WebAPI.csproj"
```

### Repository Pattern (Optional)  

```csharp
public interface ICategoryRepository
{
    Task<Category?> GetByIdAsync(int id);
    Task<List<Category>> GetAllAsync();
    Task<Category> AddAsync(Category category);
    Task UpdateAsync(Category category);
    Task DeleteAsync(int id);
}

public class CategoryRepository : ICategoryRepository
{
    private readonly ApplicationDbContext _context;

    public CategoryRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Category?> GetByIdAsync(int id)
    {
        return await _context.Categories
            .Include(c => c.Applications)
            .FirstOrDefaultAsync(c => c.Id == id);
    }
}
```

### Automatic Migration Application  

```csharp
// In Program.cs after app.Build()
using var scope = app.Services.CreateScope();
var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
DatabaseConfig.MigrateDatabase(dbContext);

public static void MigrateDatabase(ApplicationDbContext ctx)
{
    if (ctx.Database.IsRelational())
    {
        ctx.Database.Migrate();
    }
}
```

## Query Patterns  

```csharp
// Include related data
var applications = await _context.Applications
    .Include(a => a.Category)
    .Include(a => a.Owner)
    .Include(a => a.Reviews)
        .ThenInclude(r => r.ReviewVotes)
    .Where(a => a.State == ApplicationState.Approved)
    .ToListAsync();

// Projection for performance
var summaries = await _context.Applications
    .Select(a => new ApplicationSummaryDto
    {
        Id = a.Id,
        CategoryName = a.Category.Name,
        State = a.State,
        InitiatedOn = a.InitiatedOn
    })
    .ToListAsync();
```

## Notes  

- Use `[Required]` attributes for non-nullable reference properties
- Configure cascade delete behavior explicitly
- Use enum properties for state management
- Consider using owned entity types for value objects
- Always include navigation properties when needed for business logic
