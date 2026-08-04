//using System.Linq.Expressions;

//namespace Regira.Entities.DependencyInjection.Mapping.Models;

//public interface IEntityMappingDefinition
//{
//    Type SourceType { get; }
//    Type DestinationType { get; }
//}

//public class MemberMappingDefinition<TDestination>
//{
//    public Expression<Func<TDestination, object>> Selector { get; set; } = null!;
//    public Func<TDestination, object> Action { get; set; } = null!;
//}

//public class EntityMappingDefinition<TSource, TDestination> : IEntityMappingDefinition
//{
//    public Type SourceType => typeof(TSource);
//    public Type DestinationType => typeof(TDestination);
//    public IList<MemberMappingDefinition<TDestination>> MemberMappings { get; } = [];
//}