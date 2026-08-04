using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.Web.Mapping.Abstractions;

namespace Regira.Entities.Web.Mapping;

public class MappedEntityServiceCollection(IServiceCollection services) : IMappedEntityServiceCollection
{
    public IServiceCollection Services => services;
}