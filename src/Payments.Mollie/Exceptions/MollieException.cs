using System.Net;

namespace Regira.Payments.Mollie.Exceptions;

public class MollieException(string message, Exception? innerException = null)
    : WebException(message, innerException), IPaymentException
{
    public string? MollieResponse { get; set; }
}