#if NETCOREAPP3_1_OR_GREATER

using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;
using Regira.DAL.EFcore.Extensions;

namespace Regira.DAL.EFcore.Services;

/// <summary>
/// Truncates all string properties with a <see cref="MaxLengthAttribute"/> for <see cref="EntityEntry">Entries</see> that have pending changes,
/// on <c>SaveChanges()</c> and <c>SaveChangesAsync()</c> alike<br />
/// Credits: https://gist.github.com/abrari/dfe772db172f950e9f0d8acdd3982fbb
/// </summary>
public class AutoTruncateDbContextInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is not null)
        {
            TruncatePendingEntries(eventData.Context);
        }

        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = new())
    {
        if (eventData.Context is not null)
        {
            TruncatePendingEntries(eventData.Context);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void TruncatePendingEntries(DbContext context)
    {
        var logger = context.GetService<ILoggerFactory>()?.CreateLogger<AutoTruncateDbContextInterceptor>();
        foreach (var entry in context.GetPendingEntries())
        {
            if (entry.State != EntityState.Deleted)
            {
                entry.AutoTruncate(logger);
            }
        }
    }
}

public static class DbContextInterceptorExtensions
{
    /// <summary>
    /// Adds the <see cref="AutoTruncateDbContextInterceptor"/>. Idempotent: a second call on the same
    /// options builder is a no-op, so manual wiring composes with the automatic wiring of <c>UseDefaults()</c>.
    /// </summary>
    /// <param name="optionsBuilder"></param>
    /// <returns></returns>
    public static DbContextOptionsBuilder AddAutoTruncateInterceptors(this DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.HasInterceptor<AutoTruncateDbContextInterceptor>()
            ? optionsBuilder
            : optionsBuilder.AddInterceptors(new AutoTruncateDbContextInterceptor());
}

#endif