using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Regira.System.Hosting.Background.Abstractions;
using Regira.System.Hosting.Background.Core;
using Regira.System.Hosting.Background.DependencyInjection;
using Regira.System.Hosting.Background.Services;

namespace Hosting.Testing;

[TestFixture]
public class BackgroundQueueTests
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(5);

    [Test]
    public async Task Execute_Action_Finishes_The_Task()
    {
        var queue = new BackgroundTaskQueue();
        var manager = new BackgroundQueueManager(queue, new BackgroundTaskManager(), new ServiceCollection().BuildServiceProvider());
        var ran = false;

        var bgTask = manager.Execute((_, _) => ran = true);
        var workItem = await queue.DequeueAsync(CancellationToken.None);
        await workItem!(CancellationToken.None);

        Assert.That(ran, Is.True);
        Assert.That(bgTask.Status, Is.EqualTo(BackgroundTaskStatus.Finished), bgTask.Error);
    }

    [Test]
    public async Task Execute_Action_That_Throws_Ends_In_Error()
    {
        var queue = new BackgroundTaskQueue();
        var manager = new BackgroundQueueManager(queue, new BackgroundTaskManager(), new ServiceCollection().BuildServiceProvider());
        Exception? reported = null;
        manager.OnError += (ex, _, _) => reported = ex;

        var bgTask = manager.Execute((_, _) => throw new InvalidOperationException("failed on purpose"));
        var workItem = await queue.DequeueAsync(CancellationToken.None);
        await workItem!(CancellationToken.None);

        Assert.That(bgTask.Status, Is.EqualTo(BackgroundTaskStatus.Error));
        Assert.That(reported, Is.TypeOf<InvalidOperationException>());
    }

    [Test]
    public async Task Initial_Queue_Items_Are_Dequeued()
    {
        var ran = new List<string>();
        var queue = new BackgroundTaskQueue(new BackgroundTaskQueue.Options
        {
            InitialQueue = [_ => { ran.Add("first"); return Task.CompletedTask; }, _ => { ran.Add("second"); return Task.CompletedTask; }]
        });
        using var cts = new CancellationTokenSource(Wait);

        for (var i = 0; i < 2; i++)
        {
            var workItem = await queue.DequeueAsync(cts.Token);
            await workItem!(cts.Token);
        }

        Assert.That(ran, Is.EqualTo(new[] { "first", "second" }));
    }

    [Test]
    public async Task Initial_Queue_Items_With_A_Matching_Count_Are_Dequeued_Once()
    {
        var queue = new BackgroundTaskQueue(new BackgroundTaskQueue.Options
        {
            InitialConcurrentCount = 1,
            InitialQueue = [_ => Task.CompletedTask]
        });

        var first = await queue.DequeueAsync(CancellationToken.None);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        Assert.That(first, Is.Not.Null);
        await Assert.CatchAsync<OperationCanceledException>(() => queue.DequeueAsync(cts.Token), "nothing else is queued");
    }

    [Test]
    public async Task Hosted_Service_Survives_A_Throwing_Work_Item()
    {
        var queue = new BackgroundTaskQueue();
        using var service = new QueuedHostedService(queue);
        var nextItemRan = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        queue.QueueBackgroundWorkItem(_ => throw new InvalidOperationException("failed on purpose"));
        queue.QueueBackgroundWorkItem(_ =>
        {
            nextItemRan.SetResult();
            return Task.CompletedTask;
        });

        await service.StartAsync(CancellationToken.None);
        try
        {
            var finished = await Task.WhenAny(nextItemRan.Task, service.ExecuteTask!, Task.Delay(Wait));

            Assert.That(finished, Is.SameAs(nextItemRan.Task), "the item after the throwing one runs, and the service keeps going");
            Assert.That(service.ExecuteTask!.IsFaulted, Is.False);
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    [Test]
    public void Hosted_Service_Resolves_With_And_Without_Logging()
    {
        foreach (var withLogging in new[] { true, false })
        {
            var services = new ServiceCollection();
            if (withLogging)
            {
                services.AddLogging();
            }
            using var sp = services.UseBackgroundQueue().BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

            var hostedServices = sp.GetServices<IHostedService>().ToArray();

            Assert.That(hostedServices.OfType<QueuedHostedService>().Count(), Is.EqualTo(1), $"withLogging: {withLogging}");
        }
    }

    [Test]
    public async Task Hosted_Service_Stops_When_The_Host_Stops()
    {
        var queue = new BackgroundTaskQueue();
        using var service = new QueuedHostedService(queue);

        await service.StartAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);

        Assert.That(service.ExecuteTask!.IsCompleted, Is.True);
        Assert.That(service.ExecuteTask.IsFaulted, Is.False);
    }
}
