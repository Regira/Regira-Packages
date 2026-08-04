namespace Regira.System.Hosting.Background.Abstractions;

public interface IBackgroundTask
{
    string Id { get; set; }
    BackgroundTaskStatus Status { get; }
    double Progress { get; }
    object? Result { get; }
    string? Error { get; set; }
    void Start(CancellationToken token);
    void SetProgress(double progress);
    void SetError(Exception error);
    void Finish(object? result = null);
    void Cancel();
    TResult? GetResult<TResult>();
}