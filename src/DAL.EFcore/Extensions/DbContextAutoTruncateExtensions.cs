using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.Logging;

namespace Regira.DAL.EFcore.Extensions;

public static class DbContextAutoTruncateExtensions
{

    /// <summary>
    /// Truncates all string properties with a <see cref="MaxLengthAttribute"/> for <see cref="EntityEntry">Entries</see> that have pending changes<br />
    /// Credits: https://gist.github.com/abrari/dfe772db172f950e9f0d8acdd3982fbb
    /// </summary>
    /// <param name="dbContext"></param>
    /// <param name="logger">When supplied, a warning is logged for every property whose value is actually shortened.</param>
    public static void AutoTruncateStringsToMaxLengthForEntries(this DbContext dbContext, ILogger? logger = null)
    {
        foreach (var entry in dbContext.GetPendingEntries())
        {
            if (entry.State != EntityState.Deleted)
            {
                entry.AutoTruncate(logger);
            }
        }
    }
}