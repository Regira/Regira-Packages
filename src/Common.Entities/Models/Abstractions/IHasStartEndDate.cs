namespace Regira.Entities.Models.Abstractions;

public interface IHasStartDate
{
    /// <summary>
    /// Start of the active period, as a UTC instant by default (see <see cref="Regira.Utilities.DateTimeDefaults.UseUtc"/> and <c>FilterIsActiveOn</c>).<br />
    /// For pure calendar semantics (no time component) prefer a <see cref="DateOnly"/> property instead.
    /// </summary>
    DateTime? StartDate { get; set; }
}

public interface IHasEndDate
{
    /// <summary>
    /// End of the active period, as a UTC instant by default (see <see cref="Regira.Utilities.DateTimeDefaults.UseUtc"/> and <c>FilterIsActiveOn</c>).<br />
    /// For pure calendar semantics (no time component) prefer a <see cref="DateOnly"/> property instead.
    /// </summary>
    DateTime? EndDate { get; set; }
}
public interface IHasStartEndDate : IHasStartDate, IHasEndDate;
