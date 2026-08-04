using Newtonsoft.Json.Linq;

namespace Regira.Serializing.Newtonsoft.Extensions;

// https://stackoverflow.com/questions/14886800/convert-jobject-into-dictionarystring-object-is-it-possible#answer-14903514
public static class JObjectExtensions
{
    public static IDictionary<string, object?>? ToDictionary(this JObject? @object)
    {
        if (@object == null)
        {
            return null;
        }

        var result = @object.ToObject<Dictionary<string, object?>>();

        if (result == null)
        {
            return null;
        }

        var jObjectKeys = (from r in result
            let key = r.Key
            let value = r.Value
            where value?.GetType() == typeof(JObject)
            select key).ToList();

        var jArrayKeys = (from r in result
            let key = r.Key
            let value = r.Value
            where value?.GetType() == typeof(JArray)
            select key).ToList();

        jArrayKeys.ForEach(key => result[key] = ((JArray)result[key]!).Values().Select(x => ((JValue)x).Value).ToArray());
        jObjectKeys.ForEach(key => result[key] = ToDictionary(result[key] as JObject)!);

        return result;
    }
}