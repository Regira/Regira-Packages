using Microsoft.Extensions.DependencyInjection;

namespace Regira.Entities.Web.Mapping.Abstractions;

[Obsolete("Nothing takes or returns it. Will be removed in v7.")]
public interface IMappedEntityServiceCollection
{
    IServiceCollection Services { get; }
}