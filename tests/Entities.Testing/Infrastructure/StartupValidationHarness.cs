using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Entities.Testing;

/// <summary>
/// What the startup checks log, captured from information up, an exception's message appended to its entry; and the
/// host start that runs them. Shared by the tests of the startup checks and the reactors.
/// </summary>
internal sealed class StartupValidationHarness : ILoggerProvider
{
    public List<(LogLevel Level, string Message)> Entries { get; } = [];
    public List<string> Infos => Of(LogLevel.Information);
    public List<string> Warnings => Of(LogLevel.Warning);
    public List<string> Errors => Of(LogLevel.Error);

    public ILogger CreateLogger(string categoryName) => new CaptureLogger(this);
    public void Dispose() { }

    /// <summary>Starts every hosted service, as the host does: the startup validation among them.</summary>
    public static async Task RunHostedServices(ServiceProvider serviceProvider)
    {
        foreach (var hostedService in serviceProvider.GetServices<IHostedService>())
        {
            await hostedService.StartAsync(CancellationToken.None);
        }
    }

    private List<string> Of(LogLevel level) => Entries.Where(e => e.Level == level).Select(e => e.Message).ToList();

    private sealed class CaptureLogger(StartupValidationHarness harness) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel >= LogLevel.Information)
            {
                lock (harness.Entries)
                {
                    harness.Entries.Add((logLevel, formatter(state, exception) + (exception != null ? " " + exception.Message : "")));
                }
            }
        }
    }
}
