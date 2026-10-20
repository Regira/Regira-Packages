using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Globalization;

namespace Regira.Serializing.Newtonsoft.Json;

/// <summary>
/// Credits: https://www.codeproject.com/Articles/5325820/DateOnly-in-NET-6-and-ASP-NET-Core-6
/// </summary>
public class DateAndTimeConverter : IsoDateTimeConverter
{
    static readonly Type typeOfDateTime = typeof(DateTime);
    static readonly Type typeOfNullableDateTime = typeof(DateTime?);
    static readonly Type typeOfDateTimeOffset = typeof(DateTimeOffset);
    static readonly Type typeOfNullDateTimeOffset = typeof(DateTimeOffset?);

    // the base reads and writes with the current culture, whose calendar may count other years (th-TH writes 2569)
    public DateAndTimeConverter() => Culture = CultureInfo.InvariantCulture;

    // ISO text reads in the invariant culture (the base); other date text reads in the current one, as it always has,
    // so a day-first server keeps reading 05/10/2026 as 5 October
    public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, global::Newtonsoft.Json.JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.String && reader.Value is string { Length: > 0 } text && string.IsNullOrEmpty(DateTimeFormat) && !DateText.IsIso(text))
        {
            return (Nullable.GetUnderlyingType(objectType) ?? objectType) == typeOfDateTimeOffset
                ? (object)DateTimeOffset.Parse(text, CultureInfo.CurrentCulture, DateTimeStyles)
                : (object)DateTime.Parse(text, CultureInfo.CurrentCulture, DateTimeStyles);
        }
        return base.ReadJson(reader, objectType, existingValue, serializer);
    }

    public override void WriteJson(JsonWriter writer, object? value, global::Newtonsoft.Json.JsonSerializer serializer)
    {
        var type = value?.GetType();
        if (type == typeOfDateTimeOffset)
        {
            var dto = (DateTimeOffset)(value);
            if (dto == DateTimeOffset.MinValue)
            {
                writer.WriteNull();
                return;
            }
            else if (dto.TimeOfDay == TimeSpan.Zero)
            {
                writer.WriteValue(dto.ToString("yyyy-MM-dd", Culture));
                return;
            }
        }
        else if (type == typeOfNullDateTimeOffset)
        {
            var dto = (DateTimeOffset?)value;
            if (!dto.HasValue || dto.Value == DateTimeOffset.MinValue)
            {
                writer.WriteNull();
                return;
            }
            else if (dto.Value.TimeOfDay == TimeSpan.Zero)
            {
                writer.WriteValue(dto.Value.ToString("yyyy-MM-dd", Culture));
                return;
            }
        }
        else if (type == typeOfDateTime)
        {
            var dt = (DateTime)value;
            if (dt.TimeOfDay == TimeSpan.Zero)
            {
                writer.WriteValue(dt.ToString("yyyy-MM-dd", Culture));
                return;
            }
        }
        else if (type == typeOfNullableDateTime)
        {
            var dto = (DateTime?)value;
            if (!dto.HasValue || dto.Value == DateTime.MinValue)
            {
                writer.WriteNull();
                return;
            }
            else if (dto.Value.TimeOfDay == TimeSpan.Zero)
            {
                writer.WriteValue(dto.Value.ToString("yyyy-MM-dd", Culture));
                return;
            }
        }

        base.WriteJson(writer, value, serializer);
    }
}
