# Regira DAL — EF Core

Regira DAL.EFcore provides Entity Framework Core extensions and utilities for change tracking, string normalization, auto-truncation, and model configuration.

## Projects

| Project | Package | Description |
|---------|---------|-------------|
| `DAL.EFcore` | `Regira.DAL.EFcore` | EF Core extensions for DbContext, ModelBuilder, and interceptors |

## Installation

```xml
<PackageReference Include="Regira.DAL.EFcore" Version="6.*" />
```

---

## DbContext Extensions

### `GetPendingEntries`

Returns all tracked entries with pending changes (`Added`, `Modified`, or `Deleted`).

```csharp no-compile
var pending = dbContext.GetPendingEntries();
var pendingProducts = dbContext.GetPendingEntries<Product>();
```

### `SaveAndCleanUpOnError`

Wraps `SaveChangesAsync`; on a `DbUpdateException` it resets the failing entries (detaches added, reverts modified/deleted to `Unchanged`) and then **rethrows**. Catch the exception and call save again to persist the remaining entries.

```csharp no-compile
try
{
    await dbContext.SaveAndCleanUpOnError();
}
catch (DbUpdateException)
{
    // failing entries have been reset — retry saves the rest
    await dbContext.SaveChangesAsync();
}
```

### `AutoNormalizeStringsForEntries`

Runs string normalization (via `NormalizingUtility`) over all pending non-deleted entries.

```csharp no-compile
dbContext.AutoNormalizeStringsForEntries();
// or with custom options:
dbContext.AutoNormalizeStringsForEntries(new NormalizingOptions { … });
```

### `AddRegisteredInterceptors`

Discovers all `IInterceptor` registrations from an `IServiceCollection` and adds them to the `DbContextOptionsBuilder`.

```csharp no-compile
optionsBuilder.AddRegisteredInterceptors(services);
```

---

## Auto-Truncate

Automatically truncates string values to the maximum length defined by `[MaxLength]` or `[StringLength]` attributes before saving.

### `AutoTruncateStringsToMaxLengthForEntries` (extension method)

Call manually inside `SaveChanges` / `SaveChangesAsync` overrides:

```csharp no-compile
public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
{
    this.AutoTruncateStringsToMaxLengthForEntries();
    return await base.SaveChangesAsync(ct);
}
```

### `AutoTruncateDbContextInterceptor` (.NET Core 3.1+)

Register as an interceptor for automatic truncation on every save without touching the `DbContext`:

```csharp no-compile
// Manual registration
optionsBuilder.AddAutoTruncateInterceptors();

// Or register in DI and auto-discover with AddRegisteredInterceptors
services.AddSingleton<IInterceptor, AutoTruncateDbContextInterceptor>();
optionsBuilder.AddRegisteredInterceptors(services);
```

---

## ModelBuilder Extensions

### `SetDecimalPrecisionConvention`

Applies a uniform precision and scale to all `decimal` properties in the model.

```csharp no-compile
// In OnModelCreating (all TFMs)
modelBuilder.SetDecimalPrecisionConvention(precision: 18, scale: 4);

// In ConfigureConventions (.NET 6+)
configurationBuilder.SetDecimalPrecisionConvention(precision: 18, scale: 4);
```

### `SetUtcDateTimeConvention`

Applies `UtcDateTimeConverter` (`Regira.DAL.EFcore.Conversions`) to all `DateTime` (and `DateTime?`)
properties: values are normalized to UTC when saving (local kinds converted, unspecified kinds assumed UTC)
and materialized with `DateTimeKind.Utc` when reading, so JSON serialization produces ISO 8601 strings with
the `Z` suffix.

The converter honors the process-wide `Regira.Utilities.DateTimeDefaults.UseUtc` policy (enabled by
default): when the policy is disabled it passes values through unchanged. Properties that already have a
value converter configured are skipped, so a per-property converter acts as an opt-out.

```csharp no-compile
// In AddDbContext — DbContextOptionsBuilder extension
services.AddDbContext<AppDbContext>(options => options
    .UseSqlite(connectionString)
    .AddUtcDateTimeConvention());

// In OnModelCreating (all TFMs)
modelBuilder.SetUtcDateTimeConvention();

// In ConfigureConventions (.NET 6+)
configurationBuilder.SetUtcDateTimeConvention();
```

---

## EntityType / Entry Extensions

### `GetPropertyAttributes`

Retrieves the data-annotation attributes for each property of an entity entry, with results cached per entity type to avoid repeated reflection.

```csharp no-compile
var attributes = entry.GetPropertyAttributes();
// IDictionary<IProperty, Attribute[]>
```

---

## ServiceCollection Extensions

### `CollectDescriptors<TService>`

Finds all service descriptors in an `IServiceCollection` that implement the given interface — used internally by `AddRegisteredInterceptors`.

```csharp no-compile
var interceptorDescriptors = services.CollectDescriptors<IInterceptor>();
```

## License

Apache License 2.0 — this package contains no license validation and no runtime limits. See [LICENSE](https://github.com/Regira/Regira-Packages/blob/main/LICENSE). A few companion packages are commercially licensed with a free tier; see the [licensing overview](https://regira.github.io/Regira-Packages/licensing.html).
