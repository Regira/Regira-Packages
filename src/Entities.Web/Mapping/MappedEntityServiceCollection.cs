using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.Web.Mapping.Abstractions;

namespace Regira.Entities.Web.Mapping;

[Obsolete("Nothing takes or returns it. Will be removed in v7.")]
public class MappedEntityServiceCollection(IServiceCollection services) : IMappedEntityServiceCollection
{
    public IServiceCollection Services => services;
}