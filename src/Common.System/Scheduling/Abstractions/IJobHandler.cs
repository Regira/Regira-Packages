namespace Regira.System.Scheduling.Abstractions;

/// <summary>
/// A job without input. It is resolved from a new scope for every run, so it may inject scoped services.
/// A run may happen more than once (a retry, a recovery, a second node): write it to be repeated safely.
/// </summary>
public interface IJobHandler
{
    Task Execute(JobContext context, CancellationToken token);
}

/// <summary>
/// A job that takes an input. The input is a small JSON-serializable value (an id, a few fields):
/// a persistent backend stores it and another node may read it.
/// </summary>
/// <inheritdoc cref="IJobHandler" path="/summary"/>
public interface IJobHandler<in TInput>
{
    Task Execute(TInput input, JobContext context, CancellationToken token);
}
