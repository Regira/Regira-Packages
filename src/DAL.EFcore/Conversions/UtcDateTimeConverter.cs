using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Regira.Utilities;

namespace Regira.DAL.EFcore.Conversions;

/// <summary>
/// Keeps <see cref="DateTime"/> values UTC across the database round-trip while the ambient
/// <see cref="DateTimeDefaults.UseUtc"/> policy is enabled (the default):
/// <list type="bullet">
///     <item>writing: normalizes to UTC (<see cref="DateTimeKind.Local"/> values are converted, <see cref="DateTimeKind.Unspecified"/> values are assumed UTC)</item>
///     <item>reading: marks materialized values as <see cref="DateTimeKind.Utc"/>, so serializers emit an UTC offset (e.g. the ISO 8601 <c>Z</c> suffix in JSON)</item>
/// </list>
/// When the policy is disabled the converter passes values through unchanged, so a stale
/// <c>SetUtcDateTimeConvention()</c> call cannot shift values that were written as local time.<br />
/// Nullable <see cref="DateTime"/> properties are supported as well (<c>null</c> bypasses conversion).
/// </summary>
public class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    v => DateTimeDefaults.UseUtc ? v.AsUtc() : v,
    v => DateTimeDefaults.UseUtc ? DateTime.SpecifyKind(v, DateTimeKind.Utc) : v);
