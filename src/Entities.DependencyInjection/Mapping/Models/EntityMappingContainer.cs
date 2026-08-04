//using Microsoft.Extensions.DependencyInjection;

//namespace Regira.Entities.DependencyInjection.Mapping.Models;

//public class EntityMappingContainer(IServiceCollection services)
//{
//    public IList<IEntityMappingDefinition> Definitions { get; } = [];
    
//    public EntityMappingContainer Add<TSource, TDestination>(Action<IServiceCollection>? configureServices = null)
//    {
//        Definitions.Add(new EntityMappingDefinition<TSource, TDestination>());
//        configureServices?.Invoke(services);

//        return this;
//    }
//}