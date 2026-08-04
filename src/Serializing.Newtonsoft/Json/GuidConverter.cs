//using Newtonsoft.Json;
//using NewtonsoftSerializer = Newtonsoft.Json.JsonSerializer;

//namespace Regira.Serializing.Newtonsoft.Json;

//public class GuidConverter : JsonConverter
//{
//    public override object ReadJson(JsonReader reader, Type objectType, object? existingValue, NewtonsoftSerializer serializer)
//    {
//        if (reader.Value == null)
//        {
//            return existingValue != null ? Guid.Parse(existingValue.ToString()!) : Guid.NewGuid();
//        }
//        return Guid.Parse(reader.Value.ToString()!);
//    }
//    public override void WriteJson(JsonWriter writer, object? value, NewtonsoftSerializer serializer)
//    {
//        if (value == null || "UniqueKey".Equals(writer.Path, StringComparison.CurrentCultureIgnoreCase))
//        {
//            return;
//        }
//        writer.WriteValue(value);
//    }
//    public override bool CanConvert(Type objectType)
//        => typeof(Guid) == objectType;
//}