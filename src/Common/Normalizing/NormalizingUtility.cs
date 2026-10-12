using System.Collections.Concurrent;
using System.Reflection;
using Regira.Normalizing.Abstractions;
using Regira.Normalizing.Models;

namespace Regira.Normalizing;

public class NormalizingUtility
{
    private static readonly ConcurrentDictionary<PropertyInfo, NormalizedAttribute?> CachedAttributes = new();

    public static void InvokeObjectNormalizer(object instance, NormalizingOptions? options = null)
    {
        options ??= new NormalizingOptions();

        IObjectNormalizer? objNormalizerInstance = null;
        var attr = GetNormalizedAttribute(instance.GetType());
        if (attr != null)
        {
            objNormalizerInstance = GetObjectNormalizer(attr, options.ServiceProvider);
        }

        objNormalizerInstance ??= options.DefaultObjectNormalizer ?? NormalizingDefaults.DefaultObjectNormalizer ?? new ObjectNormalizer(options);
        objNormalizerInstance.HandleNormalize(instance, attr?.Recursive ?? options.DefaultRecursive);
    }
    public static void InvokePropertyNormalizer(object instance, PropertyInfo prop, NormalizingOptions? options = null)
    {
        var attribute = GetNormalizedAttribute(prop);
        if (attribute == null)
        {
            return;
        }

        var propNames = attribute.SourceProperties ?? [attribute.SourceProperty ?? prop.Name];

        var targetValues = propNames
            .Aggregate(new List<string>(), (r, propName) =>
            {
                var sourceProp = instance.GetType().GetProperty(propName);
                if (sourceProp != null)
                {
                    var value = sourceProp.GetValue(instance)?.ToString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        r.Add(value);
                    }
                }
                return r;
            });

        if (targetValues.Any())
        {
            var normalizerInstance = GetPropertyNormalizer(attribute, options?.ServiceProvider) ?? options?.DefaultNormalizer ?? NormalizingDefaults.DefaultPropertyNormalizer ?? new DefaultNormalizer(options);
            prop.SetValue(instance, normalizerInstance.Normalize(string.Join(" ", targetValues)));
        }
    }

    public static NormalizedAttribute? GetNormalizedAttribute(Type classType)
        => classType.GetCustomAttributes<NormalizedAttribute>().FirstOrDefault();
    public static NormalizedAttribute? GetNormalizedAttribute(PropertyInfo prop)
        => CachedAttributes.GetOrAdd(prop, p => p.GetCustomAttributes(true).OfType<NormalizedAttribute>().FirstOrDefault());

    public static IObjectNormalizer? GetObjectNormalizer(NormalizedAttribute attribute, IServiceProvider? sp = null)
    {
        var normalizerType = attribute.Normalizer;
        if (normalizerType == null)
        {
            return null;
        }

        return (IObjectNormalizer)(sp?.GetService(normalizerType) ?? Activator.CreateInstance(normalizerType)!);
    }
    public static INormalizer? GetPropertyNormalizer(NormalizedAttribute attribute, IServiceProvider? sp = null)
    {
        var normalizerType = attribute.Normalizer;

        if (normalizerType == null)
        {
            return null;
        }

        return (INormalizer)(sp?.GetService(normalizerType) ?? Activator.CreateInstance(normalizerType)!);
    }
}