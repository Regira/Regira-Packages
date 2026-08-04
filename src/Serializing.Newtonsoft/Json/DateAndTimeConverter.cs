using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

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
                writer.WriteValue(dto.ToString("yyyy-MM-dd"));
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
                writer.WriteValue(dto.Value.ToString("yyyy-MM-dd"));
                return;
            }
        }
        else if (type == typeOfDateTime)
        {
            var dt = (DateTime)value;
            if (dt.TimeOfDay == TimeSpan.Zero)
            {
                writer.WriteValue(dt.ToString("yyyy-MM-dd"));
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
                writer.WriteValue(dto.Value.ToString("yyyy-MM-dd"));
                return;
            }
        }

        base.WriteJson(writer, value, serializer);
    }
}
