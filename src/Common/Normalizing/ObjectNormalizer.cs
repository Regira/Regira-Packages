using System.Collections;
using Regira.Normalizing.Abstractions;
using Regira.Normalizing.Models;
using Regira.Utilities;

namespace Regira.Normalizing;

public class ObjectNormalizer : IObjectNormalizer
{
    /// <summary>
    /// When exclusive, only the first exclusive normalizer should be executed for a type
    /// </summary>
    public virtual bool IsExclusive => false;
    public INormalizer DefaultNormalizer { get; }
    protected readonly NormalizingOptions? Options;
    public ObjectNormalizer(NormalizingOptions? options = null)
    {
        Options = options;
        DefaultNormalizer = Options?.DefaultNormalizer ?? NormalizingDefaults.DefaultPropertyNormalizer ?? new DefaultNormalizer(options);
    }


    public virtual Task HandleNormalizeMany(IEnumerable<object?> instances, bool recursive = false)
    {
        foreach (var instance in instances)
        {
            HandleNormalize(instance, recursive);
        }

        return Task.CompletedTask;
    }
    public virtual void HandleNormalize(object? instance, bool recursive = false)
        => HandleNormalize(instance, recursive, []);

    protected internal virtual void HandleNormalize(object? instance, bool recursive, HashSet<object> processedInstances)
    {
        if (instance == null)
        {
            return;
        }

        // prevent stack overflow
        if (!processedInstances.Add(instance))
        {
            return;
        }

        var type = instance.GetType();

        // Property Normalizing
        foreach (var prop in type.GetProperties())
        {
            NormalizingUtility.InvokePropertyNormalizer(instance, prop, Options);

            if (recursive)
            {
                if (!TypeUtility.IsSimpleType(prop.PropertyType))
                {
                    if (TypeUtility.IsTypeACollection(prop.PropertyType))
                    {
                        var value = prop.GetValue(instance);
                        if (value is IList list)
                        {
                            foreach (var item in list)
                            {
                                HandleNormalize(item, true, processedInstances);
                            }
                        }
                    }
                    else
                    {
                        HandleNormalize(prop.GetValue(instance), true, processedInstances);
                    }
                }
            }
        }
    }
}
