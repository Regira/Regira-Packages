using Microsoft.Extensions.Options;

namespace Web.Analytics.Testing.Infrastructure;

/// <summary>Hand-rolled monitor so tests can trigger the hot-reload path without a file watcher.</summary>
public class TestOptionsMonitor<T>(T value) : IOptionsMonitor<T>
{
    private event Action<T, string?>? Listeners;

    public T CurrentValue { get; private set; } = value;

    public T Get(string? name) => CurrentValue;

    public IDisposable? OnChange(Action<T, string?> listener)
    {
        Listeners += listener;
        return null;
    }

    public void Set(T newValue)
    {
        CurrentValue = newValue;
        Listeners?.Invoke(newValue, null);
    }
}