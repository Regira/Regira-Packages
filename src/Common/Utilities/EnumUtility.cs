namespace Regira.Utilities;

public static class EnumUtility
{
    /// <summary>
    /// Retrieves all individual bit fields set in the specified enumeration value.
    /// </summary>
    /// <typeparam name="T">
    /// The enumeration type. Must be a struct, an enumeration, and implement <see cref="IConvertible"/>.
    /// </typeparam>
    /// <param name="enumValue">
    /// The enumeration value from which to extract the individual bit fields.
    /// </param>
    /// <returns>
    /// An <see cref="IEnumerable{T}"/> containing all individual bit fields set in the specified enumeration value.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown if <typeparamref name="T"/> is not an enumeration type.
    /// </exception>
    public static IEnumerable<T> FindBitFields<T>(T enumValue)
        where T : struct, Enum, IConvertible
    {
        if (!typeof(T).IsEnum)
        {
            throw new ArgumentException("T must be an enumerated type");
        }

        return FindBitFields<T>((int)Enum.ToObject(typeof(T), enumValue));
    }
    /// <summary>
    /// Retrieves all individual bit fields set in the specified bitmap value.
    /// </summary>
    /// <typeparam name="T">
    /// The enumeration type. Must be a struct, an enumeration, and implement <see cref="IConvertible"/>.
    /// </typeparam>
    /// <param name="bitmapValue">
    /// The integer representation of the bitmap value from which to extract the individual bit fields.
    /// </param>
    /// <returns>
    /// An <see cref="IEnumerable{T}"/> containing all individual bit fields set in the specified bitmap value.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown if <typeparamref name="T"/> is not an enumeration type.
    /// </exception>
    public static IEnumerable<T> FindBitFields<T>(int bitmapValue)
        where T : struct, Enum, IConvertible
    {
        if (bitmapValue == 0)
        {
            yield break;
        }

        var values = Enum.GetValues(typeof(T));
        foreach (var value in values)
        {
            var intValue = (int)value;
            if (intValue != 0 && (bitmapValue & intValue) == intValue)// exclude 0 enums
            {
                // ReSharper disable once PossibleInvalidCastException
                yield return (T)value;
            }
        }
    }
    /// <summary>
    /// Retrieves all individual bit fields set in the specified enumeration value.
    /// </summary>
    /// <param name="enumValue">
    /// The enumeration value from which to extract the individual bit fields.
    /// </param>
    /// <returns>
    /// An <see cref="IEnumerable{Object}"/> containing all individual bit fields set in the specified enumeration value.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="enumValue"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown if <paramref name="enumValue"/> is not an enumeration type.
    /// </exception>
    public static IEnumerable<object> FindBitFields(object enumValue)
    {
        if (enumValue == null)
        {
            throw new ArgumentNullException(nameof(enumValue));
        }
        if (!enumValue.GetType().IsEnum)
        {
            throw new ArgumentException("T must be an enumerated type");
        }

        var values = Enum.GetValues(enumValue.GetType());
        var bitmapValue = (int)enumValue;
        foreach (var value in values)
        {
            var intValue = (int)value;
            if (intValue != 0 && (bitmapValue & intValue) == intValue)
            {
                yield return value;
            }
        }
    }
    /// <summary>
    /// Determines whether the specified bit field is set in the given enumeration value.
    /// </summary>
    /// <typeparam name="T">
    /// The enumeration type. Must be a struct, an enumeration, and implement <see cref="IConvertible"/>.
    /// </typeparam>
    /// <param name="enumValue">
    /// The enumeration value to check.
    /// </param>
    /// <param name="bitfield">
    /// The bit field to check for in the enumeration value.
    /// </param>
    /// <returns>
    /// <c>true</c> if the specified bit field is set in the enumeration value; otherwise, <c>false</c>.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown if <typeparamref name="T"/> is not an enumeration type.
    /// </exception>
    public static bool HasFlag<T>(T enumValue, T bitfield)
        where T : struct, Enum, IConvertible
    {
        return FindBitFields(enumValue).Contains(bitfield);
    }

    /// <summary>
    /// Retrieves all valid flag values for the specified enumeration type.
    /// </summary>
    /// <typeparam name="TEnum">
    /// The enumeration type for which to retrieve valid flag values. 
    /// This type must be a struct and an enumeration.
    /// </typeparam>
    /// <returns>
    /// An <see cref="IEnumerable{T}"/> containing all valid flag values of the specified enumeration type.
    /// </returns>
    /// <remarks>
    /// Valid flag values are those that represent individual bits or combinations of bits in a bitmask.
    /// For example, in an enumeration marked with the <see cref="FlagsAttribute"/>, valid flag values 
    /// typically include powers of two (e.g., 1, 2, 4, 8) and any explicitly defined combinations.
    /// </remarks>
    public static IEnumerable<TEnum> ListValidFlagValues<TEnum>()
        where TEnum : struct, Enum
    {
        return ListValidFlagNumericValues<TEnum>()
            .Select(v => (TEnum)(object)v);
    }
    /// <summary>
    /// Retrieves all valid numeric values for the individual bit flags of the specified enumeration type.
    /// </summary>
    /// <typeparam name="TEnum">
    /// The enumeration type. Must be a struct and an enumeration.
    /// </typeparam>
    /// <returns>
    /// An <see cref="IEnumerable{T}"/> containing the numeric values of all valid bit flags for the specified enumeration type.
    /// </returns>
    /// <remarks>
    /// This method filters out values that are not valid bitmask values (e.g., values that are not powers of two).
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Thrown if <typeparamref name="TEnum"/> is not an enumeration type.
    /// </exception>
    static IEnumerable<int> ListValidFlagNumericValues<TEnum>()
        where TEnum : struct, Enum
    {
        var values = Enum.GetValues<TEnum>();
        return values
            .Select(v => (int)(object)v)
            // exclude non-bitmask values
            .Where(v => (v & (v - 1)) == 0)
            // alias members (e.g. All = TheOnlyFlag) repeat a value; summing duplicates would corrupt the max-flag bitmask
            .Distinct();
    }
    /// <summary>
    /// Retrieves all possible flag values for the specified enumeration type.
    /// </summary>
    /// <typeparam name="TEnum">
    /// The enumeration type for which to retrieve flag values. Must be a struct and an enumeration.
    /// </typeparam>
    /// <param name="showAllCombinations">
    /// If <see langword="true"/>, includes all possible combinations of flag values.
    /// If <see langword="false"/>, only includes valid individual flag values.
    /// </param>
    /// <param name="onlyDefined">
    /// If <see langword="true"/>, excludes undefined flag combinations.
    /// If <see langword="false"/>, includes undefined flag combinations.
    /// </param>
    /// <returns>
    /// An <see cref="IEnumerable{T}"/> containing the flag values of the specified enumeration type.
    /// </returns>
    /// <remarks>
    /// This method supports both retrieving individual flag values and generating combinations of flags.
    /// It can also include or exclude undefined flag combinations based on the <paramref name="onlyDefined"/> parameter.
    /// </remarks>
    /// <example>
    /// For an enumeration with the following flags:
    /// <code>
    /// [Flags]
    /// public enum TestEnum
    /// {
    ///     None = 0,
    ///     One = 1,
    ///     Two = 2,
    ///     Four = 4,
    ///     Seven = One | Two | Four
    /// }
    /// </code>
    /// Calling <c>ListFlagValues&lt;TestEnum&gt;(true, false)</c> will return all combinations, including undefined ones.
    /// </example>
    public static IEnumerable<TEnum> ListFlagValues<TEnum>(bool showAllCombinations = false, bool onlyDefined = true)
        where TEnum : struct, Enum
    {
        if (!showAllCombinations)
        {
            return ListValidFlagValues<TEnum>()
                .OrderBy(v => (int)(object)v);
        }

        var definedFlagValues = Enum
            .GetValues<TEnum>()
            .Select(v => (int)(object)v)
            .Where(v => v > 0 && (v & (v - 1)) == 0)
            .ToArray();

        var values = ListValidFlagNumericValues<TEnum>()
            .ToArray();
        var minValue = values.Min(v => v);
        var maxValue = values.Sum(v => v);

        var allValues = Enumerable.Range(minValue, (maxValue - minValue) + 1).ToArray();

        var undefinedValues = allValues
            .Where(v => v > 0 && (v & (v - 1)) == 0)
            .Where(x => !definedFlagValues.Contains(x))
            .ToArray();

        var combos = allValues
            .Where(v => !onlyDefined || undefinedValues.All(uv => (uv & v) != uv));

        return combos
            .Distinct()
            .OrderBy(x => x)
            .Select(x => (TEnum)(object)x);
    }
    /// <summary>
    /// Calculates the maximum valid flag value for the specified enumeration type.
    /// </summary>
    /// <typeparam name="TEnum">
    /// The enumeration type. Must be a struct and an enumeration.
    /// </typeparam>
    /// <returns>
    /// The maximum valid flag value of the specified enumeration type, which is the sum of all valid individual bit flags.
    /// </returns>
    /// <remarks>
    /// This method considers only valid bitmask values (e.g., powers of two and their combinations) 
    /// and excludes any invalid or undefined values.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Thrown if <typeparamref name="TEnum"/> is not an enumeration type.
    /// </exception>
    public static TEnum GetMaxFlagValue<TEnum>()
        where TEnum : struct, Enum
    {
        var values = ListValidFlagNumericValues<TEnum>()
            .ToArray();
        return (TEnum)(object)values.Sum();
    }

    /// <summary>
    /// Converts a collection of enum values into a single bitmask value.
    /// </summary>
    /// <typeparam name="TEnum">
    /// The type of the enum. Must be a struct, an enumeration, and implement <see cref="System.IConvertible"/>.
    /// </typeparam>
    /// <param name="values">
    /// The collection of enum values to be combined into a bitmask.
    /// </param>
    /// <returns>
    /// A bitmask value of type <typeparamref name="TEnum"/> that represents the combined values of the provided collection.
    /// </returns>
    /// <exception cref="System.InvalidOperationException">
    /// Thrown if <typeparamref name="TEnum"/> is not a valid enum type.
    /// </exception>
    public static TEnum ToBitmask<TEnum>(this IEnumerable<TEnum> values)
        => (TEnum)(object)(values.Cast<int>().Sum());
}