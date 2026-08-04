namespace Regira.Utilities;

public static class DateTimeUtility
{
    /// <summary>
    /// Converts a Unix timestamp, represented as the number of seconds since January 1, 1970 (UTC), to a local <see cref="DateTime"/>.
    /// </summary>
    /// <param name="secondsSince1970">The number of seconds elapsed since January 1, 1970 (UTC).</param>
    /// <returns>A <see cref="DateTime"/> object representing the local time equivalent of the provided Unix timestamp.</returns>
    public static DateTime FromUnix(double secondsSince1970)
    {
        // https://stackoverflow.com/questions/249760/how-can-i-convert-a-unix-timestamp-to-datetime-and-vice-versa#answer-250400
        return DateTimeOffset
            .FromUnixTimeSeconds((long)secondsSince1970)
            .LocalDateTime;
    }

    /// <summary>
    /// Normalizes a <see cref="DateTime"/> to UTC:
    /// <list type="bullet">
    ///     <item><see cref="DateTimeKind.Utc"/> values are returned unchanged</item>
    ///     <item><see cref="DateTimeKind.Local"/> values are converted using <see cref="DateTime.ToUniversalTime"/></item>
    ///     <item><see cref="DateTimeKind.Unspecified"/> values are assumed to already represent UTC and only get their kind set</item>
    /// </list>
    /// </summary>
    /// <param name="value">The value to normalize.</param>
    /// <returns>The same instant with <see cref="DateTime.Kind"/> set to <see cref="DateTimeKind.Utc"/>.</returns>
    public static DateTime AsUtc(this DateTime value)
        => value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

    /// <inheritdoc cref="AsUtc(DateTime)"/>
    public static DateTime? AsUtc(this DateTime? value)
        => value?.AsUtc();
}