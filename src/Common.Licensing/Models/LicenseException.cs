namespace Regira.Licensing.Models;

public class LicenseException : Exception
{
    public LicenseException(string message) : base(message) { }
    public LicenseException(string message, Exception inner) : base(message, inner) { }
}
