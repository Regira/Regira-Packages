using Regira.Invoicing.UblSharp.Attributes;
using UblSharp.UnqualifiedDataTypes;

namespace Regira.Invoicing.UblSharp.Utilities;

internal static class PeppolUtility
{
    public const string CURRENCY_CODE = "EUR";


    public static AmountType BuildAmountType(decimal amount, string currencyCode = CURRENCY_CODE)
    {
        return new AmountType { currencyID = currencyCode, Value = RoundAmount(amount) };
    }
    /// <summary>
    /// Every amount in the document rounds to two decimals the commercial way, half away from zero, so the line
    /// amounts, their sums and the VAT agree with an invoice printed from the same lines.
    /// </summary>
    public static decimal RoundAmount(decimal amount) => decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
    /// <summary>
    /// An item price keeps its own decimals: UBL-DT-01's two-decimal limit leaves <c>PriceAmount</c> out, and a rounded
    /// price times a large quantity drifts from the line amount past PEPPOL-EN16931-R120's 0.02.
    /// </summary>
    public static AmountType BuildPriceAmountType(decimal price, string currencyCode = CURRENCY_CODE)
    {
        return new AmountType { currencyID = currencyCode, Value = price };
    }
    public static CodeType BuildCodeType<TEnum>(TEnum enumCode)
        where TEnum : Enum
    {
        var enumType = typeof(TEnum);
        var listAttribute = enumType.GetCustomAttributes(typeof(PeppolListAttribute), false)
            .FirstOrDefault() as PeppolListAttribute;
        return new CodeType { listID = listAttribute?.Code, Value = GetPeppolCodeValue(enumCode) };
    }
    public static string GetPeppolCodeValue<TEnum>(TEnum enumCode)
        where TEnum : Enum
    {
        var enumType = typeof(TEnum);
        var memberInfos = enumType.GetMember(enumCode.ToString());
        var enumValueMemberInfo = memberInfos.First(m => m.DeclaringType == enumType);
        var valueAttributes = enumValueMemberInfo.GetCustomAttributes(typeof(PeppolCodeAttribute), false);
        return ((PeppolCodeAttribute)valueAttributes[0]).Value;
    }
    public static IdentifierType BuildCodeTypeIdentifierType<TEnum>(TEnum enumCode)
        where TEnum : Enum
    {
        //var enumType = typeof(TEnum);
        //var enumAttributes = enumType.GetCustomAttributes(typeof(PeppolListAttribute), false);
        //var schemeId = ((PeppolListAttribute)enumAttributes[0]).Code;
        var enumValue = GetPeppolCodeValue(enumCode);
        return new IdentifierType { /*schemeID = schemeId,*/ Value = enumValue };
    }
}