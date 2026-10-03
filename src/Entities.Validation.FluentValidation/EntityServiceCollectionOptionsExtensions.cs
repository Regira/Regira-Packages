using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Regira.Entities.DependencyInjection.ServiceCollections.Models;
using Regira.Entities.DependencyInjection.Validators;

namespace Regira.Entities.Validation.FluentValidation;

public static class EntityServiceCollectionOptionsExtensions
{
    /// <summary>
    /// Runs FluentValidation validators in the entity write pipeline (<see cref="FluentEntityValidator"/>), and registers
    /// every validator found in <paramref name="assemblies"/> (scoped, so they can take the <c>DbContext</c>). Without
    /// assemblies it registers nothing but the pipeline stage — register the validators yourself.
    /// Call it inside <c>UseEntities()</c>: it is a global validator, so it runs before the ones <c>For&lt;&gt;()</c> adds.
    /// </summary>
    public static EntityServiceCollectionOptions UseFluentValidation(this EntityServiceCollectionOptions options, params Assembly[] assemblies)
    {
        if (assemblies.Length > 0)
        {
            options.Services.AddValidatorsFromAssemblies(assemblies);
        }
        options.Services.TryAddSingleton<FluentValidatorLookup>();
        options.AddValidator<FluentEntityValidator>();
        return options;
    }
}
