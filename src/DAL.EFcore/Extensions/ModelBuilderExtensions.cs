using Microsoft.EntityFrameworkCore;
using Regira.DAL.EFcore.Conversions;

namespace Regira.DAL.EFcore.Extensions;

public static class ModelBuilderExtensions
{
    /// <summary>
    /// Applies <see cref="UtcDateTimeConverter"/> to all <see cref="DateTime"/> (and nullable) properties:
    /// values are normalized to UTC when saving and materialized with <see cref="DateTimeKind.Utc"/> when reading,
    /// so JSON serialization produces ISO 8601 strings with the <c>Z</c> suffix.<br />
    /// The converter follows the ambient <see cref="Regira.Utilities.DateTimeDefaults.UseUtc"/> policy and passes
    /// values through unchanged when that policy is disabled.<br />
    /// Properties that already have a value converter configured are skipped, so a per-property
    /// converter acts as an opt-out (with <see cref="ModelConfigurationBuilder"/>-based setup, any explicit
    /// per-property conversion in <c>OnModelCreating</c> overrides the convention as well).
    /// </summary>
    /// <param name="modelBuilder"></param>
    public static void SetUtcDateTimeConvention(this ModelBuilder modelBuilder)
    {
        var converter = new UtcDateTimeConverter();
        var properties = modelBuilder.Model
            .GetEntityTypes()
            .SelectMany(t => t.GetProperties())
            .Where(p => (p.ClrType == typeof(DateTime) || p.ClrType == typeof(DateTime?)) && p.GetValueConverter() == null);

        foreach (var property in properties)
        {
            property.SetValueConverter(converter);
        }
    }

    /// <inheritdoc cref="SetUtcDateTimeConvention(ModelBuilder)"/>
    /// <param name="configurationBuilder"></param>
    public static void SetUtcDateTimeConvention(this ModelConfigurationBuilder configurationBuilder)
        => configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();

    /// <summary>
    /// Sets the given precision and scale for all decimal properties<br />
    /// From EF core 6 possible by overriding <see cref="DbContext.ConfigureConventions"/> method
    /// </summary>
    /// <param name="modelBuilder"></param>
    /// <param name="precision"></param>
    /// <param name="scale"></param>
    public static void SetDecimalPrecisionConvention(this ModelBuilder modelBuilder, int precision = 18, int scale = 4)
    {
        // https://stackoverflow.com/questions/43277154/entity-framework-core-setting-the-decimal-precision-and-scale-to-all-decimal-p#answer-43282620
        var entityTypes = modelBuilder.Model
            .GetEntityTypes()
            .ToArray();

        var decimalColumnType = $"decimal({precision}, {scale})";
        var properties = entityTypes
            .SelectMany(t => t.GetProperties())
            .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?));

        foreach (var property in properties)
        {
            property.SetColumnType(decimalColumnType);
        }
    }

    /// <summary>
    /// Sets the given precision and scale for all decimal properties<br />
    /// </summary>
    /// <param name="configurationBuilder"></param>
    /// <param name="precision"></param>
    /// <param name="scale"></param>
    public static void SetDecimalPrecisionConvention(this ModelConfigurationBuilder configurationBuilder, int precision = 18, int scale = 4)
        => configurationBuilder.Properties<decimal>().HavePrecision(precision, scale);
}