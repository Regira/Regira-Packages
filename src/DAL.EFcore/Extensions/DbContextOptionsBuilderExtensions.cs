using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Regira.DAL.EFcore.Conversions;

namespace Regira.DAL.EFcore.Extensions;

public static class DbContextOptionsBuilderExtensions
{
    /// <summary>
    /// Whether an interceptor of the given type is already registered on this options builder.
    /// Used to keep the <c>Add*Interceptors()</c> extensions idempotent, so manual wiring in
    /// <c>AddDbContext</c> composes with automatic wiring (e.g. by <c>UseDefaults()</c>).
    /// </summary>
    /// <typeparam name="TInterceptor"></typeparam>
    /// <param name="optionsBuilder"></param>
    /// <returns></returns>
    public static bool HasInterceptor<TInterceptor>(this DbContextOptionsBuilder optionsBuilder)
        where TInterceptor : IInterceptor
        => optionsBuilder.Options.Extensions.OfType<CoreOptionsExtension>()
            .FirstOrDefault()?.Interceptors?.OfType<TInterceptor>().Any() == true;

    /// <summary>
    /// Applies the UTC <see cref="DateTime"/> convention from the options builder — the <c>AddDbContext</c>
    /// counterpart of <see cref="ModelBuilderExtensions.SetUtcDateTimeConvention(ModelBuilder)"/> (use one or the other):
    /// all <see cref="DateTime"/> (and nullable) properties are normalized to UTC when saving and materialize
    /// with <see cref="DateTimeKind.Utc"/> when reading, honoring the process-wide
    /// <see cref="Regira.Utilities.DateTimeDefaults.UseUtc"/> policy. An explicit per-property conversion
    /// configured in <c>OnModelCreating</c> wins (= per-property opt-out).
    /// <code>
    /// services.AddDbContext&lt;AppDbContext&gt;(options =&gt; options
    ///     .UseSqlite(connectionString)
    ///     .AddUtcDateTimeConvention());
    /// </code>
    /// </summary>
    /// <param name="optionsBuilder"></param>
    /// <returns></returns>
    public static DbContextOptionsBuilder AddUtcDateTimeConvention(this DbContextOptionsBuilder optionsBuilder)
    {
        var extension = optionsBuilder.Options.FindExtension<UtcDateTimeOptionsExtension>() ?? new UtcDateTimeOptionsExtension();
        ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(extension);
        return optionsBuilder;
    }
}
