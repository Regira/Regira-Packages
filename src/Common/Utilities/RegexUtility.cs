using System.Globalization;
using System.Text.RegularExpressions;

namespace Regira.Utilities;

public static class RegexUtility
{
    // https://docs.microsoft.com/en-us/dotnet/standard/base-types/how-to-verify-that-strings-are-in-valid-email-format
    private static string EmailPattern => @"(?("")("".+?(?<!\\)""@)|(([0-9a-z]((\.(?!\.))|[-!#\$%&'\*\+/=\?\^`\{\}\|~\w])*)(?<=[0-9a-z])@))(?(\[)(\[(\d{1,3}\.){3}\d{1,3}\])|(([0-9a-z][-\w]*[0-9a-z]*\.)+[a-z0-9][\-a-z0-9]{0,22}[a-z0-9]))";
    // https://www.freecodecamp.org/news/how-to-validate-urls-in-javascript/
    private static string UrlPattern => @"([a-zA-Z]+:\/\/)?((([a-z\d]([a-z\d-]*[a-z\d])*)\.)+[a-z]{2,}|((\d{1,3}\.){3}\d{1,3}))(\:\d+)?(\/[-a-z\d%_.~+]*)*(\?[;&a-z\d%_.~+=-]*)?(\#[-a-z\d_]*)?";
    // https://uibakery.io/regex-library/phone-number (added '/' to allowed chars and allowed (0) in second segment)
    private static string PhoneNumberPattern => @"\+?\d{1,4}?[-./\s]?(\(0\))?\(?\d{1,3}?\)?[-.\s]?\d{1,4}[-.\s]?\d{1,4}[-.\s]?\d{1,9}";
    private static string IPv4AddressPattern => @"(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)";
    private static string IPv6AddressPattern => @"(([0-9a-fA-F]{1,4}:){7,7}[0-9a-fA-F]{1,4}|([0-9a-fA-F]{1,4}:){1,7}:|([0-9a-fA-F]{1,4}:){1,6}:[0-9a-fA-F]{1,4}|([0-9a-fA-F]{1,4}:){1,5}(:[0-9a-fA-F]{1,4}){1,2}|([0-9a-fA-F]{1,4}:){1,4}(:[0-9a-fA-F]{1,4}){1,3}|([0-9a-fA-F]{1,4}:){1,3}(:[0-9a-fA-F]{1,4}){1,4}|([0-9a-fA-F]{1,4}:){1,2}(:[0-9a-fA-F]{1,4}){1,5}|[0-9a-fA-F]{1,4}:((:[0-9a-fA-F]{1,4}){1,6})|:((:[0-9a-fA-F]{1,4}){1,7}|:)|fe80:(:[0-9a-fA-F]{0,4}){0,4}%[0-9a-zA-Z]{1,}|::(ffff(:0{1,4}){0,1}:){0,1}((25[0-5]|(2[0-4]|1{0,1}[0-9]){0,1}[0-9])\.){3,3}(25[0-5]|(2[0-4]|1{0,1}[0-9]){0,1}[0-9])|([0-9a-fA-F]{1,4}:){1,4}:((25[0-5]|(2[0-4]|1{0,1}[0-9]){0,1}[0-9])\.){3,3}(25[0-5]|(2[0-4]|1{0,1}[0-9]){0,1}[0-9]))";

    /// <summary>
    /// Validates whether the provided input string is in a valid email format.
    /// </summary>
    /// <param name="input">The email address string to validate. Can be <c>null</c> or empty.</param>
    /// <returns>
    /// <c>true</c> if the input string is a valid email address; otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    /// This method uses a regular expression to validate the email format and normalizes the domain part of the email.
    /// It handles exceptions such as <see cref="RegexMatchTimeoutException"/> and <see cref="ArgumentException"/> to ensure robust validation.
    /// </remarks>
    public static bool IsValidEmail(string? input)
    {
        // https://docs.microsoft.com/en-us/dotnet/standard/base-types/how-to-verify-that-strings-are-in-valid-email-format

        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        try
        {
            // Normalize the domain
            input = Regex.Replace(input, @"(@)(.+)$", DomainMapper, RegexOptions.None, TimeSpan.FromMilliseconds(200));

            // Examines the domain part of the email and normalizes it.
            string DomainMapper(Match match)
            {
                // Use IdnMapping class to convert Unicode domain names.
                var idn = new IdnMapping();

                // Pull out and process domain name (throws ArgumentException on invalid)
                var domainName = idn.GetAscii(match.Groups[2].Value);

                return match.Groups[1].Value + domainName;
            }
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }

        try
        {
            // modified original regex -> https://emailregex.com/
            return Regex.IsMatch(input, PatternToExactMatch(EmailPattern), RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(250));
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }
    /// <summary>
    /// Validates whether the provided input string is in a valid URL format.
    /// </summary>
    /// <param name="input">The URL string to validate. Can be <c>null</c> or empty.</param>
    /// <returns>
    /// <c>true</c> if the input string is a valid URL; otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    /// This method uses a regular expression to validate the URL format. 
    /// It ensures that the input matches common URL patterns, including optional schemes (e.g., "http", "https") and domain structures.
    /// </remarks>
    public static bool IsValidUrl(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        return Regex.IsMatch(input, PatternToExactMatch(UrlPattern), RegexOptions.IgnoreCase);
    }
    /// <summary>
    /// Validates whether the provided input string is in a valid phone number format.
    /// </summary>
    /// <param name="input">The phone number string to validate. Can be <c>null</c> or empty.</param>
    /// <param name="allowShortNumber">
    /// A boolean value indicating whether short numbers (e.g., 4-6 digits) are allowed.
    /// If <c>true</c>, short numbers are considered valid.
    /// </param>
    /// <returns>
    /// <c>true</c> if the input string is a valid phone number; otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    /// This method uses a regular expression to validate phone number formats, including international formats.
    /// It supports various delimiters such as spaces, dashes, dots, and slashes.
    /// Additionally, it can validate short numbers if <paramref name="allowShortNumber"/> is set to <c>true</c>.
    /// </remarks>
    public static bool IsValidPhoneNumber(string? input, bool allowShortNumber = false)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        if (!allowShortNumber && input!.Length <= 6)
        {
            return false;
        }

        // short number (sms)
        if (allowShortNumber && Regex.IsMatch(input, @"\d{4,6}"))
        {
            return true;
        }

        return Regex.IsMatch(input, PatternToExactMatch(PhoneNumberPattern));
    }
    /// <summary>
    /// Validates whether the provided input string is a valid IPv4 address.
    /// </summary>
    /// <param name="input">The string to validate as an IPv4 address. Can be <c>null</c> or empty.</param>
    /// <returns>
    /// <c>true</c> if the input string is a valid IPv4 address; otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    /// This method uses a regular expression to validate the IPv4 address format. 
    /// It ensures that the input matches the standard IPv4 address structure, consisting of four octets separated by dots.
    /// </remarks>
    public static bool IsValidIPv4Address(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }
        return Regex.IsMatch(input, IPv4AddressPattern);
    }
    /// <summary>
    /// Validates whether the provided input string is in a valid IPv6 address format.
    /// </summary>
    /// <param name="input">The IPv6 address string to validate. Can be <c>null</c> or empty.</param>
    /// <returns>
    /// <c>true</c> if the input string is a valid IPv6 address; otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    /// This method uses a regular expression to validate the IPv6 address format. 
    /// It supports various IPv6 notations, including compressed and mixed IPv6/IPv4 formats.
    /// </remarks>
    public static bool IsValidIPv6Address(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }
        return Regex.IsMatch(input, IPv6AddressPattern, RegexOptions.IgnoreCase);
    }
    /// <summary>
    /// Validates whether the provided input string is a valid IP address (either IPv4 or IPv6).
    /// </summary>
    /// <param name="input">The string to validate as an IP address. Can be <c>null</c> or empty.</param>
    /// <returns>
    /// <c>true</c> if the input string is a valid IPv4 or IPv6 address; otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    /// This method combines the validation of both IPv4 and IPv6 formats by internally calling 
    /// <see cref="IsValidIPv4Address"/> and <see cref="IsValidIPv6Address"/>. It ensures that the input 
    /// matches either of the standard IP address structures.
    /// </remarks>
    public static bool IsValidIPAddress(string? input)
        => IsValidIPv4Address(input) || IsValidIPv6Address(input);

    static string PatternToExactMatch(string input) => $"^{input}$";
    static string PatternToExtract(string input) => $@"\b{input}\b";

    /// <summary>
    /// 
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    public static string[] ExtractEmails(string input)
    {
        if (!string.IsNullOrWhiteSpace(input))
        {
            return Regex.Matches(input, PatternToExtract(EmailPattern), RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(250))
                .Select(m => m.Value)
                .Distinct()
                .ToArray();
        }

        return [];
    }
    /// <summary>
    /// Extracts all unique URLs from the specified input string.
    /// </summary>
    /// <param name="input">The input string from which to extract URLs.</param>
    /// <returns>An array of unique URLs found in the input string. If no URLs are found, returns an empty array.</returns>
    /// <remarks>
    /// The method uses a regular expression to identify and extract URLs. It supports various URL formats, 
    /// including those with or without protocols (e.g., "http://", "https://") and domain names or IP addresses.
    /// </remarks>
    /// <exception cref="RegexMatchTimeoutException">Thrown if the regular expression operation times out.</exception>
    /// <exception cref="ArgumentException">Thrown if the input string contains invalid regular expression patterns.</exception>
    public static string[] ExtractUrls(string input)
    {
        if (!string.IsNullOrWhiteSpace(input))
        {
            return Regex.Matches(input, PatternToExtract(UrlPattern), RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(250))
                .Select(m => m.Value)
                .Distinct()
                .ToArray();
        }

        return [];
    }
    /// <summary>
    /// Extracts phone numbers from the specified input string.
    /// </summary>
    /// <param name="input">The input string from which phone numbers will be extracted.</param>
    /// <param name="allowShortNumbers">
    /// A boolean value indicating whether short phone numbers (4 to 6 digits) should be included in the extraction.
    /// </param>
    /// <returns>
    /// An array of unique phone numbers found in the input string. Returns an empty array if no phone numbers are found.
    /// </returns>
    /// <remarks>
    /// This method uses regular expressions to identify phone numbers in the input string. 
    /// The extraction process is case-insensitive and has a timeout of 250 milliseconds for regex matching.
    /// </remarks>
    public static string[] ExtractPhoneNumbers(string input, bool allowShortNumbers = false)
    {
        if (!string.IsNullOrWhiteSpace(input))
        {
            var pattern = allowShortNumbers ? $@"(\d{{4,6}})|{PhoneNumberPattern})" : PhoneNumberPattern;
            return Regex.Matches(input, pattern, RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(250))
                .Select(m => m.Value)
                .Distinct()
                .ToArray();
        }

        return [];
    }
    /// <summary>
    /// Extracts all unique IPv4 and IPv6 addresses from the specified input string.
    /// </summary>
    /// <param name="input">
    /// The input string to search for IP addresses. Can include both IPv4 and IPv6 formats.
    /// </param>
    /// <returns>
    /// An array of unique IP addresses found in the input string. Returns an empty array if no matches are found.
    /// </returns>
    /// <remarks>
    /// This method uses regular expressions to identify and extract IP addresses. 
    /// It supports both IPv4 and IPv6 formats and performs case-insensitive matching.
    /// </remarks>
    /// <exception cref="RegexMatchTimeoutException">
    /// Thrown if the regular expression operation exceeds the specified timeout.
    /// </exception>
    public static string[] ExtractIPAddresses(string input)
    {
        if (!string.IsNullOrWhiteSpace(input))
        {
            var pattern = $"({IPv4AddressPattern})|({IPv6AddressPattern})";
            return Regex.Matches(input, PatternToExtract(pattern), RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(250))
                .Select(m => m.Value)
                .Distinct()
                .ToArray();
        }

        return [];
    }
}