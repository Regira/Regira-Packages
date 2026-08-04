using Newtonsoft.Json;
using Regira.Utilities;
using NewtonsoftSerializer = Newtonsoft.Json.JsonSerializer;

namespace Regira.Serializing.Newtonsoft.Json;

public class BoolNumberConverter : JsonConverter
{
    public override void WriteJson(JsonWriter writer, object? value, NewtonsoftSerializer serializer)
        => writer.WriteValue((value != null && (bool)value) ? 1 : 0);

    public override object ReadJson(JsonReader reader, Type objectType, object? existingValue, NewtonsoftSerializer serializer)
        => new[] { "1", "true" }.Contains(reader.Value?.ToString(), StringComparer.InvariantCultureIgnoreCase);

    public override bool CanConvert(Type objectType)
        => typeof(bool) == TypeUtility.GetSimpleType(objectType);
}