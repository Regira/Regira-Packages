using Regira.System.Hosting.Background.Abstractions;

namespace Regira.System.Hosting.Background.Models;

public class BackgroundTask : IBackgroundTask
{
    internal CancellationToken Token { get; set; }

    public string Id { get; set; } = null!;
    public BackgroundTaskStatus Status { get; private set; }
    public double Progress { get; private set; }
    public object? Result { get; private set; }

    public string? Error { get; set; }

    public void Start(CancellationToken token)
    {
        Status = BackgroundTaskStatus.Running;
        SetProgress(0);
        Token = token;
    }
    public void SetProgress(double progress)
    {
        Progress = progress;
    }
    public void SetError(Exception error)
    {
        Status = BackgroundTaskStatus.Error;
        Error = error.ToString();
    }
    public void Finish(object? result = null)
    {
        Result = result;
        SetProgress(100);
        Status = BackgroundTaskStatus.Finished;
    }
    public void Cancel()
    {
        Status = BackgroundTaskStatus.Canceled;
    }
    public TResult? GetResult<TResult>()
    {
        return (TResult?)Result;
    }
}