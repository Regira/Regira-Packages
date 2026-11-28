using Microsoft.Extensions.DependencyInjection;
using Regira.System.Scheduling.Abstractions;

namespace Regira.System.Hosting.Scheduling;

/// <summary>
/// Runs a job handler from the scope of one run. A scheduling backend calls it with the provider of the scope it created for that run.
/// A registered handler comes from the container; any other is created with the scope's services and disposed after the run.
/// </summary>
public static class JobHandlerInvoker
{
    public static async Task Run<TJob>(IServiceProvider scopedServices, JobContext context, CancellationToken token)
        where TJob : IJobHandler
    {
        var handler = Resolve<TJob>(scopedServices, out var owned);
        try
        {
            await handler.Execute(context, token);
        }
        finally
        {
            await Release(handler, owned);
        }
    }
    public static async Task Run<TJob, TInput>(IServiceProvider scopedServices, TInput input, JobContext context, CancellationToken token)
        where TJob : IJobHandler<TInput>
    {
        var handler = Resolve<TJob>(scopedServices, out var owned);
        try
        {
            await handler.Execute(input, context, token);
        }
        finally
        {
            await Release(handler, owned);
        }
    }

    private static TJob Resolve<TJob>(IServiceProvider scopedServices, out bool owned)
    {
        var handler = scopedServices.GetService<TJob>();
        owned = handler == null;
        return handler ?? ActivatorUtilities.CreateInstance<TJob>(scopedServices);
    }
    // The container disposes what it created; a handler created here is disposed here.
    private static async ValueTask Release(object? handler, bool owned)
    {
        if (!owned)
        {
            return;
        }

        if (handler is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
        }
        else if (handler is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
