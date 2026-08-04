namespace Regira.Utilities;

public static class NumberUtility
{
    private static IDictionary<char, int> RomanMap => new Dictionary<char, int> {
        { 'I', 1 }, { 'V', 5 }, { 'X', 10 }, { 'L', 50 }, { 'C', 100 }, { 'D', 500 }, { 'M', 1000 }
    };
    private static List<string> Romans => ["M", "CM", "D", "CD", "C", "XC", "L", "XL", "X", "IX", "V", "IV", "I"];
    private static List<int> Numerals => [1000, 900, 500, 400, 100, 90, 50, 40, 10, 9, 5, 4, 1];

    /// <summary>
    /// Converts a Roman numeral string to its equivalent integer value.
    /// </summary>
    /// <param name="roman">
    /// The Roman numeral string to convert. The string is case-insensitive and may contain valid Roman numeral characters.
    /// </param>
    /// <returns>
    /// The integer value corresponding to the provided Roman numeral string.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown if the <paramref name="roman"/> parameter is <c>null</c>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown if the <paramref name="roman"/> parameter contains invalid characters or is not a valid Roman numeral.
    /// </exception>
    public static int FromRomanNumeral(string roman) {
        // https://stackoverflow.com/questions/14900228/roman-numerals-to-integers/27976977#answer-26667855

        static char Normalize(char c) {
            return char.ToUpperInvariant(c);
        }
        static bool IsSubtractive(char c1, char c2) {
            return RomanMap[c1] < RomanMap[c2];
        }

        var result = 0;
        for (var i = 0; i < roman.Length; i++) {
            if (i + 1 < roman.Length && IsSubtractive(Normalize(roman[i]), Normalize(roman[i + 1]))) {
                result -= RomanMap[Normalize(roman[i])];
            }
            else {
                result += RomanMap[Normalize(roman[i])];
            }
        }

        return result;
    }
    /// <summary>
    /// Converts an integer to its equivalent Roman numeral representation.
    /// </summary>
    /// <param name="number">
    /// The integer value to convert. Must be a positive number greater than zero.
    /// </param>
    /// <returns>
    /// A string representing the Roman numeral equivalent of the provided integer.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown if the <paramref name="number"/> parameter is less than or equal to zero.
    /// </exception>
    public static string ToRomanNumeral(int number) {
        // https://stackoverflow.com/questions/22392810/integer-to-roman-format#answer-22393404
        var roman = string.Empty;
        while (number > 0) {
            // find biggest numeral that is less than equal to number
            var index = Numerals.FindIndex(x => x <= number);
            // subtract it's value from your number
            number -= Numerals[index];
            // tack it onto the end of your roman numeral
            roman += Romans[index];
        }
        return roman;
    }
}