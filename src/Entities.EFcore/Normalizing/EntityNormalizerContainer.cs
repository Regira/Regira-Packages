using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.Normalizing.Abstractions;
using Regira.Utilities;

namespace Regira.Entities.EFcore.Normalizing;

public class EntityNormalizerContainer(IServiceProvider services)
{
    public record NormalizerDescriptor(Type Type, ServiceDescriptor Descriptor);
    public IList<NormalizerDescriptor> Descriptors { get; init; } = new List<NormalizerDescriptor>();

    public EntityNormalizerContainer Register<T>(Func<IServiceProvider, IEntityNormalizer> factory)
        => Register(typeof(T), factory);
    public EntityNormalizerContainer Register(Type type, Func<IServiceProvider, IEntityNormalizer> factory)
    {
        Descriptors.Add(new NormalizerDescriptor(type, new ServiceDescriptor(typeof(IEntityNormalizer<>).MakeGenericType(type), factory, ServiceLifetime.Transient)));
        return this;
    }

    public EntityNormalizerContainer Insert<T>(int index, Func<IServiceProvider, IEntityNormalizer> factory)
        => Insert(index, typeof(T), factory);
    public EntityNormalizerContainer Insert(int index, Type type, Func<IServiceProvider, IEntityNormalizer> factory)
    {
        Descriptors.Insert(index, new NormalizerDescriptor(type, new ServiceDescriptor(typeof(IEntityNormalizer<>).MakeGenericType(type), factory, ServiceLifetime.Transient)));
        return this;
    }

    /// <summary>
    /// <inheritdoc cref="FindAll(Type)"/>
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public IEnumerable<IEntityNormalizer> FindAll<T>()
        => FindAll(typeof(T));
    /// <summary>
    /// Returns all corresponding normalizers<br />
    /// If any exclusive normalizers are present, only the first one will be returned
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    public IEnumerable<IEntityNormalizer> FindAll(Type type)
    {
        var normalizer = Find(type);
        if (normalizer?.IsExclusive == true)
        {
            return [normalizer];
        }

        return Descriptors.Where(d => d.Type == type || TypeUtility.GetBaseTypes(type).Contains(d.Type))
                .Select(d => (IEntityNormalizer)(d.Descriptor.ImplementationInstance ?? d.Descriptor.ImplementationFactory!(services)))
                //.Select(d => (IEntityNormalizer)ActivatorUtilities.GetServiceOrCreateInstance(_services, d!.Descriptor.ServiceType))
                ;
    }

    public IEntityNormalizer? Find<T>()
        => Find(typeof(T));
    public IEntityNormalizer? Find(Type type)
    {
        var descriptor = Descriptors.FirstOrDefault(d => d.Type == type);
        if (descriptor == null)
        {
            if (type.BaseType != null)
            {
                return Find(type.BaseType);
            }
        }

        return (IEntityNormalizer?)(descriptor?.Descriptor.ImplementationInstance ?? descriptor?.Descriptor.ImplementationFactory!(services));
        //return (IEntityNormalizer?)ActivatorUtilities.GetServiceOrCreateInstance(_services, descriptor!.Descriptor.ServiceType);
    }
}