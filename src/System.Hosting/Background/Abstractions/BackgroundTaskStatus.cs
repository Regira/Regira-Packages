namespace Regira.System.Hosting.Background.Abstractions;

public enum BackgroundTaskStatus
{
    Idle,
    Running,
    Finished,
    Canceled,
    Error
}