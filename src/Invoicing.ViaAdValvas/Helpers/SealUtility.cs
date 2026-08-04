using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Regira.Invoicing.ViaAdValvas.Helpers;

public static class SealUtility
{
    public static string Generate(string key, string token, DateTime date, string? senderId = null, string? referenceId = null)
    {
        var md5 = MD5.Create();

        var senderIdentification = string.IsNullOrEmpty(senderId)
            ? string.Empty
            : senderId;

        var dateTimeString = date.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);

        var toCode = token + senderIdentification + (referenceId ?? string.Empty) + dateTimeString + key;

        var bytes = Encoding.UTF8.GetBytes(toCode);
        var md5Result = md5.ComputeHash(bytes);
        return BitConverter.ToString(md5Result).ToLower();
    }
}