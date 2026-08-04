using Microsoft.Extensions.DependencyInjection;

namespace Regira.Entities.Web.Mapping.Abstractions;

public interface IMappedEntityServiceCollection
{
    IServiceCollection Services { get; }
}