using Newtonsoft.Json;

namespace Regira.Serializing.Newtonsoft.Json;

/// <summary>
/// Credits: https://www.codeproject.com/Articles/5325820/DateOnly-in-NET-6-and-ASP-NET-Core-6
/// </summary>
public sealed class DateOnlyJsonConverter : JsonConverter<DateOnly?>
{
    public override void WriteJson(JsonWriter writer, DateOnly? value, global::Newtonsoft.Json.JsonSerializer serializer)
    {
        writer.WriteValue(value?.ToString("O"));
    }

    public override DateOnly? ReadJson(JsonReader reader, Type objectType, DateOnly? existingValue, bool hasExistingValue, global::Newtonsoft.Json.JsonSerializer serializer)
    {
        var v = reader.Value;
        if (v == null)
        {
            return null;
        }

        var vType = v.GetType();
        if (vType == typeof(DateTimeOffset)) //when the object is from a property 
                                             //in POST body. When used in service, 
                                             //better to have 
                                             //options.SerializerSettings.DateParseHandling = 
                                             //Newtonsoft.Json.DateParseHandling.DateTimeOffset;
        {
            return DateOnly.FromDateTime(((DateTimeOffset)v).DateTime);
        }

        if (vType == typeof(string))
        {
            return DateOnly.Parse((string)v); //DateOnly can parse 00001-01-01
        }

        if (vType == typeof(DateTime)) //when the object is from a property 
                                       //in POST body from a TS client
        {
            return DateOnly.FromDateTime((DateTime)v);
        }

        throw new NotSupportedException($"Not yet support {vType} in {this.GetType()}.");
    }
}