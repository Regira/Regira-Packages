using System.Collections;
using System.ComponentModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Regira.Utilities;

public static class StringUtility
{
    // https://stackoverflow.com/questions/249087/how-do-i-remove-diacritics-accents-from-a-string-in-net#answer-34272324
    private static Dictionary<string, string> ForeignCharacterMappings => new()
    {
        {"äæǽ", "ae"},
        {"öœ", "oe"},
        {"ü", "ue"},
        //{"Ä", "Ae"},// double (see below)
        {"Ü", "Ue"},
        {"Ö", "Oe"},
        {"ÀÁÂÃÄÅǺĀĂĄǍΑΆẢẠẦẪẨẬẰẮẴẲẶА", "A"},
        {"àáâãåǻāăąǎªαάảạầấẫẩậằắẵẳặа", "a"},
        {"Б", "B"},
        {"б", "b"},
        {"ÇĆĈĊČ", "C"},
        {"çćĉċč", "c"},
        {"Д", "D"},
        {"д", "d"},
        {"ÐĎĐΔ", "Dj"},
        {"ðďđδ", "dj"},
        {"ÈÉÊËĒĔĖĘĚΕΈẼẺẸỀẾỄỂỆЕЭ", "E"},
        {"èéêëēĕėęěέεẽẻẹềếễểệеэ", "e"},
        {"Ф", "F"},
        {"ф", "f"},
        {"ĜĞĠĢΓГҐ", "G"},
        {"ĝğġģγгґ", "g"},
        {"ĤĦ", "H"},
        {"ĥħ", "h"},
        {"ÌÍÎÏĨĪĬǏĮİΗΉΊΙΪỈỊИЫ", "I"},
        {"ìíîïĩīĭǐįıηήίιϊỉịиыї", "i"},
        {"Ĵ", "J"},
        {"ĵ", "j"},
        {"ĶΚК", "K"},
        {"ķκк", "k"},
        {"ĹĻĽĿŁΛЛ", "L"},
        {"ĺļľŀłλл", "l"},
        {"М", "M"},
        {"м", "m"},
        {"ÑŃŅŇΝН", "N"},
        {"ñńņňŉνн", "n"},
        {"ÒÓÔÕŌŎǑŐƠØǾΟΌΩΏỎỌỒỐỖỔỘỜỚỠỞỢО", "O"},
        {"òóôõōŏǒőơøǿºοόωώỏọồốỗổộờớỡởợо", "o"},
        {"П", "P"},
        {"п", "p"},
        {"ŔŖŘΡР", "R"},
        {"ŕŗřρр", "r"},
        {"ŚŜŞȘŠΣС", "S"},
        {"śŝşșšſσςс", "s"},
        {"ȚŢŤŦτТ", "T"},
        {"țţťŧт", "t"},
        {"ÙÚÛŨŪŬŮŰŲƯǓǕǗǙǛỦỤỪỨỮỬỰУ", "U"},
        {"ùúûũūŭůűųưǔǖǘǚǜυύϋủụừứữửựу", "u"},
        {"ÝŸŶΥΎΫỲỸỶỴЙ", "Y"},
        {"ýÿŷỳỹỷỵй", "y"},
        {"В", "V"},
        {"в", "v"},
        {"Ŵ", "W"},
        {"ŵ", "w"},
        {"ŹŻŽΖЗ", "Z"},
        {"źżžζз", "z"},
        {"ÆǼ", "AE"},
        {"ß", "ss"},
        {"Ĳ", "IJ"},
        {"ĳ", "ij"},
        {"Œ", "OE"},
        {"ƒ", "f"},
        {"ξ", "ks"},
        {"π", "p"},
        {"β", "v"},
        {"μ", "m"},
        {"ψ", "ps"},
        {"Ё", "Yo"},
        {"ё", "yo"},
        {"Є", "Ye"},
        {"є", "ye"},
        {"Ї", "Yi"},
        {"Ж", "Zh"},
        {"ж", "zh"},
        {"Х", "Kh"},
        {"х", "kh"},
        {"Ц", "Ts"},
        {"ц", "ts"},
        {"Ч", "Ch"},
        {"ч", "ch"},
        {"Ш", "Sh"},
        {"ш", "sh"},
        {"Щ", "Shch"},
        {"щ", "shch"},
        {"ЪъЬь", ""},
        {"Ю", "Yu"},
        {"ю", "yu"},
        {"Я", "Ya"},
        {"я", "ya"},
    };
    private static Dictionary<char, string> ForeignCharacters => new(ForeignCharacterMappings
        .SelectMany(p => p.Key.Select(key => new KeyValuePair<char, string>(key, p.Value)))
        .ToDictionary(x => x.Key, v => v.Value)
    );
    /// <summary>
    /// Splits the input string into an array of substrings based on spaces, while preserving quoted segments as single elements.
    /// </summary>
    /// <param name="input">The input string to be split.</param>
    /// <returns>An array of substrings obtained by splitting the input string on spaces, with quoted segments preserved as single elements.</returns>
    public static string[] SplitOnSpace(string input)
    {
        var segments = Regex.Matches(input, @"[\""].+?[\""]|[^ ]+")
            .Select(m => m.Value);

        return segments.ToArray();
    }

    /// <summary>
    /// Converts the specified string to snake_case format.
    /// </summary>
    /// <param name="input">The input string to convert. Can be <c>null</c> or empty.</param>
    /// <returns>
    /// A string in snake_case format, or the original input if it is <c>null</c>, empty, or consists only of whitespace.
    /// </returns>
    /// <remarks>
    /// This method splits the input string into segments based on uppercase letters, lowercase letters, and numbers,
    /// and joins them using underscores. Leading and trailing underscores are removed.
    /// </remarks>
    public static string? ToSnakeCase(this string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return input;
        }

        // https://plainenglish.io/blog/convert-string-to-different-case-styles-snake-kebab-camel-and-pascal-case-in-javascript-da724b7220d7
        var matches = Regex.Matches(input, "[A-Z]{2,}(?=[A-Z][a-z]+[0-9]*|\\b)|[A-Z]?[a-z]+[0-9]*|[A-Z]|[0-9]+");
        return string.Join("_", matches).Trim('_');
    }
    /// <summary>
    /// Converts the specified string to PascalCase format.
    /// </summary>
    /// <param name="input">The input string to convert. Can be <c>null</c> or empty.</param>
    /// <returns>
    /// A string converted to PascalCase format, or <c>null</c> if the input is <c>null</c> or whitespace.
    /// </returns>
    /// <remarks>
    /// PascalCase format capitalizes the first letter of each word and removes any delimiters such as spaces, underscores, or hyphens.
    /// </remarks>
    public static string? ToPascalCase(this string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return input;
        }

        return string.Concat(
            input.ToSnakeCase()!
                .Split(['_'], StringSplitOptions.RemoveEmptyEntries)
                .Select(x => char.ToUpper(x[0]) + x.Substring(1).ToLower(CultureInfo.InvariantCulture))
        );
    }
    /// <summary>
    /// 
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    public static string? ToCamelCase(this string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return input;
        }

        var pascalCase = ToPascalCase(input)!;
        return char.ToLower(pascalCase[0]) + pascalCase.Substring(1);
    }
    /// <summary>
    /// Converts the specified string to kebab-case format.
    /// </summary>
    /// <param name="input">The input string to convert. Can be <c>null</c> or empty.</param>
    /// <returns>
    /// A string in kebab-case format, or the original input if it is <c>null</c>, empty, or consists only of whitespace.
    /// </returns>
    /// <remarks>
    /// This method transforms the input string into snake_case format using <see cref="ToSnakeCase"/>, 
    /// then replaces underscores with hyphens to produce the kebab-case result.
    /// </remarks>
    public static string? ToKebabCase(this string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return input;
        }

        return string.Join("-", ToSnakeCase(input)!.Split('_'));
    }
    /// <summary>
    /// Converts the specified string to Proper Case format, where each word starts with an uppercase letter
    /// followed by lowercase letters.
    /// </summary>
    /// <param name="input">The input string to convert. Can be <c>null</c> or empty.</param>
    /// <returns>
    /// A string in Proper Case format, or the original input if it is <c>null</c>, empty, or consists only of whitespace.
    /// </returns>
    /// <remarks>
    /// This method splits the input string into segments based on uppercase letters, lowercase letters, and numbers,
    /// and capitalizes the first letter of each segment while converting the rest to lowercase.
    /// </remarks>
    public static string? ToProperCase(this string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return input;
        }

        var matches = Regex.Matches(input, "[A-Z]{2,}(?=[A-Z][a-z]+[0-9]*|\\b)|[A-Z]?[a-z]+[0-9]*|[A-Z]|[0-9]+")
            .Select(m => m.Value)
            .ToArray();
        return string.Join(" ", matches.Select(m => $"{char.ToUpperInvariant(m[0])}{m.Substring(1)}")).Trim();
    }

    /// <summary>
    /// Extracts the initials from the specified string.
    /// </summary>
    /// <param name="input">The input string from which to extract initials. Can be <c>null</c> or empty.</param>
    /// <returns>
    /// A string containing the initials of each word in the input string, or the original input if it is <c>null</c> or consists only of whitespace.
    /// </returns>
    /// <remarks>
    /// This method splits the input string by spaces and takes the first character of each word to form the initials.
    /// </remarks>
    public static string? GetInitials(this string? input)
        => string.IsNullOrWhiteSpace(input) ? input : string.Join("", input.Split(' ').Select(s => s.First()));
    /// <summary>
    /// Capitalizes the first letter of each word in the input string, using the specified culture for casing rules.
    /// </summary>
    /// <param name="input">The input string to be capitalized. If null or whitespace, the original input is returned.</param>
    /// <param name="culture">
    /// The <see cref="CultureInfo"/> to use for capitalization. If null, the current culture is used.
    /// </param>
    /// <returns>
    /// A new string with the first letter of each word capitalized, or the original input if it is null or whitespace.
    /// </returns>
    public static string? Capitalize(this string? input, CultureInfo? culture = null)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return input;
        }

        culture ??= CultureInfo.CurrentCulture;
        return culture.TextInfo.ToTitleCase(input.ToLower(culture));
    }
    /// <summary>
    /// Removes diacritical marks (accents) from the specified string, replacing them with their base characters.
    /// </summary>
    /// <param name="input">The input string from which diacritical marks should be removed.</param>
    /// <returns>
    /// A new string with diacritical marks removed. If the input is <c>null</c> or whitespace, the original input is returned.
    /// </returns>
    /// <remarks>
    /// This method normalizes the input string to a decomposed form, removes non-spacing marks, and then re-normalizes it.
    /// Additionally, it replaces certain foreign characters with their ASCII equivalents.
    /// </remarks>
    public static string? RemoveDiacritics(this string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return input;
        }

        // https://stackoverflow.com/questions/249087/how-do-i-remove-diacritics-accents-from-a-string-in-net#answer-67569854
        var normalizedString = input.Normalize(NormalizationForm.FormD);
        var stringBuilder = new StringBuilder();

        foreach (var c in normalizedString.EnumerateRunes())
        {
            var unicodeCategory = Rune.GetUnicodeCategory(c);
            if (unicodeCategory != UnicodeCategory.NonSpacingMark)
            {
                stringBuilder.Append(c);
            }
        }

        var normalizedC = stringBuilder.ToString().Normalize(NormalizationForm.FormC);
        var chars = normalizedC.ToCharArray();
        var sb = new StringBuilder();
        foreach (var c in chars)
        {
            if (ForeignCharacters.ContainsKey(c))
            {
                sb.Append(ForeignCharacters[c]);
            }
            else
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }
    /// <summary>
    /// Truncates a <see cref="string">input</see> to a <see cref="int">maxLength</see>.
    /// Returns null if <see cref="string">input</see> is null.
    /// </summary>
    /// <param name="input"></param>
    /// <param name="maxLength"></param>
    /// <returns>Truncated <see cref="string">input</see> or null</returns>
    public static string? Truncate(this string? input, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(input) || input.Length <= maxLength)
        {
            return input;
        }

        return input.Substring(0, maxLength);
    }
    /// <summary>
    /// Cleans up the specified string by trimming leading and trailing whitespace.
    /// Optionally, returns <c>null</c> if the input is empty.
    /// </summary>
    /// <param name="input">The input string to clean up. Can be <c>null</c>.</param>
    /// <param name="nullWhenEmpty">
    /// A boolean value indicating whether to return <c>null</c> if the input is empty.
    /// Defaults to <c>true</c>.
    /// </param>
    /// <returns>
    /// The cleaned-up string with leading and trailing whitespace removed, or <c>null</c> if the input is empty
    /// and <paramref name="nullWhenEmpty"/> is <c>true</c>.
    /// </returns>
    public static string? CleanUp(this string? input, bool nullWhenEmpty = true)
    {
        if (input == null || nullWhenEmpty && input == string.Empty)
        {
            return null;
        }

        return input.Trim();
    }

    // Base64
    /// <summary>
    /// 
    /// </summary>
    /// <param name="plainText"></param>
    /// <param name="encoding"></param>
    /// <returns></returns>
    public static string Base64Encode(this string plainText, Encoding? encoding = null)
    {
        encoding ??= Encoding.Default;
        var plainTextBytes = encoding.GetBytes(plainText);
        return Convert.ToBase64String(plainTextBytes);
    }
    /// <summary>
    /// Decodes a Base64-encoded string to its original representation using the specified encoding.
    /// </summary>
    /// <param name="base64EncodedData">The Base64-encoded string to decode.</param>
    /// <param name="encoding">
    /// The <see cref="System.Text.Encoding"/> to use for decoding. If not specified, the default encoding is used.
    /// </param>
    /// <returns>The decoded string.</returns>
    /// <exception cref="System.ArgumentNullException">
    /// Thrown if <paramref name="base64EncodedData"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="System.FormatException">
    /// Thrown if <paramref name="base64EncodedData"/> is not a valid Base64 string.
    /// </exception>
    public static string Base64Decode(this string base64EncodedData, Encoding? encoding = null)
    {
        encoding ??= Encoding.Default;
        var base64EncodedBytes = Convert.FromBase64String(base64EncodedData);
        return encoding.GetString(base64EncodedBytes);
    }

    /// <summary>
    /// Generates a random string of the specified length using a cryptographically secure random number generator.
    /// </summary>
    /// <param name="length">
    /// The length of the random string to generate. Defaults to 32 if not specified.
    /// </param>
    /// <returns>
    /// A Base64-encoded string representing the generated random data.
    /// </returns>
    /// <remarks>
    /// The generated string is suitable for use in scenarios requiring secure random values, such as tokens or keys.
    /// </remarks>
    public static string GenerateRandomString(int length = 32)
    {
        var randomNumber = new byte[length];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }


    //Source: http://mo.notono.us/2008/07/c-stringinject-format-strings-by-key.html
    /// <summary>
    /// Replaces placeholders in the specified format string with values from the provided object.
    /// </summary>
    /// <param name="formatString">
    /// The format string containing placeholders to be replaced. Placeholders should be in the form of keys enclosed in braces, e.g., {Key}.
    /// </param>
    /// <param name="injectionObject">
    /// An object containing the values to inject into the format string. This can be an <see cref="IDictionary"/> or an object with properties.
    /// </param>
    /// <param name="ci">
    /// An optional <see cref="CultureInfo"/> to format the injected values. If not provided, the current culture is used.
    /// </param>
    /// <returns>
    /// A new string with the placeholders replaced by the corresponding values from the <paramref name="injectionObject"/>.
    /// If <paramref name="formatString"/> is <c>null</c>, <c>null</c> is returned.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="injectionObject"/> is <c>null</c>.
    /// </exception>
    /// <remarks>
    /// This method supports both dictionary-based and property-based injection. If the <paramref name="injectionObject"/> is a dictionary,
    /// its keys are used as placeholders. Otherwise, the object's property names are used as placeholders.
    /// </remarks>
    public static string? Inject(this string? formatString, object injectionObject, CultureInfo? ci = null)
    {
        return injectionObject is IDictionary dic
            ? formatString.Inject(dic, ci)
            : formatString.Inject(GetPropertyHash(injectionObject), ci);
    }
    /// <summary>
    /// Replaces placeholders in the format string with corresponding values from the provided dictionary.
    /// </summary>
    /// <param name="formatString">
    /// The string containing placeholders to be replaced. Placeholders should be enclosed in curly braces, e.g., {key}.
    /// </param>
    /// <param name="dictionary">
    /// A dictionary containing key-value pairs where keys correspond to placeholders in the format string,
    /// and values are the replacements for those placeholders.
    /// </param>
    /// <param name="ci">
    /// An optional <see cref="CultureInfo"/> object to format the replacement values according to specific culture settings.
    /// If not provided, the current culture is used.
    /// </param>
    /// <returns>
    /// A new string with placeholders replaced by their corresponding values from the dictionary.
    /// If the format string or dictionary is null, the original format string is returned.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown if a placeholder in the format string does not have a corresponding key in the dictionary.
    /// </exception>
    public static string? Inject(this string? formatString, IDictionary dictionary, CultureInfo? ci = null)
    {
        return formatString.Inject(new Hashtable(dictionary), ci);
    }
    /// <summary>
    /// Replaces placeholders in the specified format string with corresponding values from the provided <see cref="Hashtable"/>.
    /// </summary>
    /// <param name="formatString">
    /// The format string containing placeholders in the form of <c>{key}</c> or <c>{key:format}</c>.
    /// </param>
    /// <param name="attributes">
    /// A <see cref="Hashtable"/> containing key-value pairs where the keys correspond to the placeholders in the format string.
    /// </param>
    /// <param name="ci">
    /// An optional <see cref="CultureInfo"/> used to format the replacement values. If <c>null</c>, the current culture is used.
    /// </param>
    /// <returns>
    /// A string with the placeholders replaced by their corresponding values from the <paramref name="attributes"/>.
    /// If <paramref name="formatString"/> or <paramref name="attributes"/> is <c>null</c>, the original <paramref name="formatString"/> is returned.
    /// </returns>
    /// <remarks>
    /// This method iterates through the keys in the <paramref name="attributes"/> and replaces each corresponding placeholder
    /// in the <paramref name="formatString"/> with the associated value. Formatting options can be applied to the values
    /// using the <c>:format</c> syntax within the placeholders.
    /// </remarks>
    public static string? Inject(this string? formatString, Hashtable? attributes, CultureInfo? ci = null)
    {
        var result = formatString;
        if (attributes == null || formatString == null)
            return result;

        return attributes.Keys.Cast<string>().Aggregate(result, (current, attributeKey) => current?.InjectSingleValue(attributeKey, attributes[attributeKey], ci));
    }
    /// <summary>
    /// Replaces a placeholder in the format string with a specified value.
    /// </summary>
    /// <param name="formatString">
    /// The format string containing placeholders in the form of <c>{key}</c> or <c>{key:format}</c>.
    /// </param>
    /// <param name="key">The key of the placeholder to replace.</param>
    /// <param name="replacementValue">
    /// The value to replace the placeholder with. If the placeholder includes a format specifier, 
    /// the value will be formatted accordingly.
    /// </param>
    /// <param name="ci">
    /// The <see cref="CultureInfo"/> to use for formatting. If <c>null</c>, the current culture is used.
    /// </param>
    /// <returns>
    /// A new string with the specified placeholder replaced by the provided value, or the original 
    /// format string if the placeholder is not found.
    /// </returns>
    public static string? InjectSingleValue(this string? formatString, string key, object? replacementValue, CultureInfo? ci = null)
    {
        if (formatString == null)
        {
            return null;
        }

        var result = formatString;
        Regex attributeRegex = new Regex("{(" + key + ")(?:}|(?::(.[^}]*)}))");

        foreach (Match m in attributeRegex.Matches(formatString))
        {
            string? replacement;
            if (m.Groups[2].Length > 0)
            {
                var attributeFormatString = string.Format(CultureInfo.InvariantCulture, "{{0:{0}}}", m.Groups[2]);
                replacement = string.Format(ci ?? CultureInfo.CurrentCulture, attributeFormatString, replacementValue);
            }
            else
            {
                replacement = (replacementValue ?? string.Empty).ToString();
            }
            result = result.Replace(m.ToString(), replacement);
        }
        return result;
    }
    /// <summary>
    /// Converts the properties of the specified object into a <see cref="Hashtable"/>.
    /// </summary>
    /// <param name="properties">The object whose properties are to be converted. Can be <c>null</c>.</param>
    /// <returns>
    /// A <see cref="Hashtable"/> containing the property names and their corresponding values from the specified object,
    /// or <c>null</c> if the input object is <c>null</c>.
    /// </returns>
    /// <remarks>
    /// This method uses reflection to retrieve the properties of the specified object and their values.
    /// It is useful for scenarios where property-value pairs need to be dynamically accessed or injected.
    /// </remarks>
    private static Hashtable? GetPropertyHash(object? properties)
    {
        Hashtable? values = null;
        if (properties != null)
        {
            values = new Hashtable();
            PropertyDescriptorCollection props = TypeDescriptor.GetProperties(properties);
            foreach (PropertyDescriptor prop in props)
            {
                values.Add(prop.Name, prop.GetValue(properties));
            }
        }
        return values;
    }
}