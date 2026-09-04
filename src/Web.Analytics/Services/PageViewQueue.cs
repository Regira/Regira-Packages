using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Regira.Web.Analytics.Config;
using Regira.Web.Analytics.Models;

namespace Regira.Web.Analytics.Services;

/// <summary>
/// Bounded, non-blocking hand-off between the request pipeline and the writer: if the writer stalls,
/// page views are dropped instead of holding up responses.
/// </summary>
public class PageViewQueue<TPageView>(AnalyticsConfig config, ILogger<PageViewQueue<TPageView>> logger)
    where TPageView : IPageView
{
    private readonly Channel<PendingPageView<TPageView>> _channel =
        Channel.CreateBounded<PendingPageView<TPageView>>(new BoundedChannelOptions(config.QueueCapacity)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true,
            SingleWriter = false
        });

    private int _droppedTotal;

    public ChannelReader<PendingPageView<TPageView>> Reader => _channel.Reader;

    public void Enqueue(PendingPageView<TPageView> item)
    {
        if (_channel.Writer.TryWrite(item))
            return;

        var dropped = Interlocked.Increment(ref _droppedTotal);
        if (dropped % 1000 == 1)
            logger.LogWarning("Analytics queue is full, dropping page views ({Dropped} dropped so far)", dropped);
    }
}