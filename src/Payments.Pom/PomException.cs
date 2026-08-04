namespace Regira.Payments.Pom;

public class PomException(string message, Exception? innerException = null) : Exception(message, innerException)
{
    public string? PomResponse { get; set; }
    public int StatusCode { get; set; }
}