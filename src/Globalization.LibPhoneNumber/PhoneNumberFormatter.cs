using PhoneNumbers;
using Regira.Globalization.Utilities;
using Regira.Normalizing.Abstractions;
using System.Globalization;

namespace Regira.Globalization.LibPhoneNumber;

public class PhoneNumberFormatter(CultureInfo? culture = null) : INormalizer, IFormatter
{
    protected CultureInfo Culture { get; } = culture ?? CultureInfo.CurrentCulture;
    protected PhoneNumberUtil PhoneNumberUtil { get; } = PhoneNumberUtil.GetInstance();


    public string? Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return input;
        }

        var phoneNumber = ParseInput(input);
        return phoneNumber != null
            ? PhoneNumberUtil.Format(phoneNumber, PhoneNumberFormat.E164)
            : null;
    }
    public string? Format(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return input;
        }

        var phoneNumber = ParseInput(input);
        return phoneNumber != null
            ? PhoneNumberUtil.Format(phoneNumber, PhoneNumberFormat.INTERNATIONAL)
            : null;
    }

    internal PhoneNumber? ParseInput(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        var countryCode = Culture.GetCountry()?.Iso2Code;
        return PhoneNumberUtil.Parse(input, countryCode);
    }
}