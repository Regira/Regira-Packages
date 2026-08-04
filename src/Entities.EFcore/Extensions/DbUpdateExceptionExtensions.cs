using Microsoft.EntityFrameworkCore;
using System.Data.Common;

namespace Regira.Entities.EFcore.Extensions;

public static class DbUpdateExceptionExtensions
{
    // SQL Server: 547 = FK/check, 515 = NOT NULL, 2601/2627 = unique index/constraint
    private static readonly int[] SqlServerConstraintNumbers = [547, 515, 2601, 2627];

    /// <summary>
    /// True when the failure is an integrity-constraint violation (unique index, foreign key, NOT NULL,
    /// check) rather than a transient fault (deadlock, timeout, dropped connection).<br />
    /// Detected provider-agnostically: SQLSTATE class 23 (PostgreSQL, MySQL, ...), SQLite error 19,
    /// SQL Server numbers 547/515/2601/2627. Unknown providers return <c>false</c>, so their failures
    /// keep surfacing unwrapped.
    /// </summary>
    public static bool IsConstraintViolation(this DbUpdateException ex)
    {
        if (ex is DbUpdateConcurrencyException)
        {
            return false;
        }
        // walk the inner chain — interceptors/retry strategies may wrap the provider exception one level deeper
        var inner = ex.InnerException;
        while (inner is not null && inner is not DbException)
        {
            inner = inner.InnerException;
        }
        if (inner is not DbException dbEx)
        {
            return false;
        }
        if (dbEx.SqlState?.StartsWith("23", StringComparison.Ordinal) == true)
        {
            return true;
        }
        // provider packages aren't referenced here — match by type name + error-code property
        return dbEx.GetType().Name switch
        {
            "SqliteException" => GetIntProperty(dbEx, "SqliteErrorCode") == 19,
            "SqlException" => SqlServerConstraintNumbers.Contains(GetIntProperty(dbEx, "Number") ?? -1),
            _ => false,
        };
    }

    private static int? GetIntProperty(object instance, string name)
        => instance.GetType().GetProperty(name)?.GetValue(instance) as int?;
}
