using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Regira.Serializing.Abstractions;
using Regira.Serializing.Newtonsoft.Extensions;

namespace Regira.Serializing.Newtonsoft.Json;

/// <summary>
/// Converter based on Newtonsoft to (de)serialize objects
/// </summary>
public class JsonSerializer : ISerializer
{
    public class Options
    {
        public bool EnumAsString { get; set; } = true;
        public bool BoolAsNumber { get; set; } = true;
        public bool IgnoreNullValues { get; set; } = true;
        public bool WriteIndented { get; set; } = false;
    }

    private readonly List<JsonConverter> _converters = [];
    private readonly IContractResolver _contractResolver;
    private readonly Formatting _formatting;

    private readonly NullValueHandling _nullValueHandling;
    public JsonSerializer(Options? options = null)
    {
        options ??= new Options();

        if (options.EnumAsString)
        {
            _converters.Add(new StringEnumConverter());
        }
        if (options.BoolAsNumber)
        {
            _converters.Add(new BoolNumberConverter());
        }

        _converters.Add(new DateOnlyJsonConverter());
        _converters.Add(new DateAndTimeConverter());

        _contractResolver = new DefaultContractResolver { NamingStrategy = new CamelCaseNamingStrategy() };
        _formatting = options.WriteIndented ? Formatting.Indented : Formatting.None;
        _nullValueHandling = options.IgnoreNullValues ? NullValueHandling.Ignore : NullValueHandling.Include;
    }


    public string Serialize<T>(T item)
    {
        var settings = GetSettings();
        return JsonConvert.SerializeObject(item, _formatting, settings);
    }
    public T? Deserialize<T>(string? content)
    {
        if (content == null)
        {
            return default;
        }

        var settings = GetSettings();
        var result = JsonConvert.DeserializeObject<T>(content, settings);

        if (new[] { typeof(Dictionary<string, object?>), typeof(IDictionary<string, object?>) }.Contains(result?.GetType()))
        {
            var processedDic = ProcessDictionaryEntries((result as IDictionary<string, object?>)!);
            return (T)processedDic;
        }

        return result;
    }

    public object? Deserialize(string? content, Type type)
    {
        if (content == null)
        {
            return null;
        }

        var settings = GetSettings();
        var result = JsonConvert.DeserializeObject(content, type, settings);

        if (new[] { typeof(Dictionary<string, object?>), typeof(IDictionary<string, object?>) }.Contains(result?.GetType()))
        {
            var processedDic = ProcessDictionaryEntries((result as IDictionary<string, object?>)!);
            return processedDic;
        }

        return result;
    }

    public static IDictionary<string, object?> ProcessDictionaryEntries<T>(T input)
        where T : class, IDictionary<string, object?>
    {
        return input.ToDictionary(
            x => x.Key,
            x => x.Value is JArray jArr
                ? jArr.Children<JObject>().Select(x2 => x2.ToDictionary()).ToList()
                : x.Value is JObject jObj
                    ? jObj.ToDictionary()
                    : x.Value
        );
    }

    private JsonSerializerSettings GetSettings()
    {
        var settings = new JsonSerializerSettings
        {
            //PreserveReferencesHandling = PreserveReferencesHandling.All,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            NullValueHandling = _nullValueHandling
        };
        if (_converters.Any())
        {
            settings.Converters = _converters;
        }

        settings.ContractResolver = _contractResolver;

        return settings;
    }
}