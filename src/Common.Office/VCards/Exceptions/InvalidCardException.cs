namespace Regira.Office.VCards.Exceptions;

public class InvalidCardException(string message, string? cardContent, Exception? innerException = null)
    : Exception(message, innerException)
{
    public string? CardContent { get; } = cardContent;

    public InvalidCardException(string message, Exception? innerException = null)
        : this(message, null, innerException)
    {

    }
}