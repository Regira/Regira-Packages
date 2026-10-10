using System.Collections;
using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Regira.Entities.Mediator.Abstractions;
using Regira.Entities.Models;

namespace Regira.Entities.Mediator;

/// <summary>
/// The default <see cref="IEntityInputValidator"/>: <c>System.ComponentModel.DataAnnotations</c> on the input and on
/// every object and collection item below it, keyed by property path (<c>Address.Street</c>, <c>Lines[0].Quantity</c>).
/// <c>Validator.TryValidateObject</c> alone stops at the first level; MVC's model validation does not, and neither does
/// this. It stops at the depth MVC stops at, and visits an object once.
/// </summary>
public class DataAnnotationsEntityInputValidator : IEntityInputValidator
{
    /// <summary>A shared instance; the validator holds no state.</summary>
    public static readonly DataAnnotationsEntityInputValidator Instance = new();

    private const int MaxDepth = 32;// MVC's default MvcOptions.MaxValidationDepth
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> NestedProperties = new();

    public virtual IReadOnlyList<EntityInputError> Validate(object input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var errors = new List<EntityInputError>();
        Validate(input, string.Empty, errors, new HashSet<object>(ReferenceEqualityComparer.Instance), 0);
        return errors;
    }

    private static void Validate(object model, string prefix, List<EntityInputError> errors, HashSet<object> visited, int depth)
    {
        if (depth > MaxDepth || !visited.Add(model))
        {
            return;
        }

        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
        foreach (var result in results)
        {
            var message = result.ErrorMessage ?? "The value is not valid.";
            var members = result.MemberNames.ToArray();
            if (members.Length == 0)
            {
                errors.Add(new EntityInputError(prefix, message));
            }
            foreach (var member in members)
            {
                errors.Add(new EntityInputError(Join(prefix, member), message));
            }
        }

        foreach (var property in NestedProperties.GetOrAdd(model.GetType(), FindNestedProperties))
        {
            object? value;
            try
            {
                value = property.GetValue(model);
            }
            catch (TargetInvocationException)
            {
                continue;// a getter that throws holds nothing to validate
            }

            var key = Join(prefix, property.Name);
            if (value is IEnumerable items and not string)
            {
                var index = 0;
                foreach (var item in items)
                {
                    if (item != null && !IsSimple(item.GetType()))
                    {
                        Validate(item, $"{key}[{index}]", errors, visited, depth + 1);
                    }
                    index++;
                }
            }
            else if (value != null)
            {
                Validate(value, key, errors, visited, depth + 1);
            }
        }
    }

    // the properties that can hold an object or a collection of objects to descend into
    private static PropertyInfo[] FindNestedProperties(Type type)
        => type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0 && !IsSimple(p.PropertyType))
            .ToArray();

    // value types (numbers, dates, Guid, enums, their nullables), strings and collections of them — a byte[] of file
    // content among them — carry no properties of their own to check
    private static bool IsSimple(Type type)
        => type.IsValueType || type == typeof(string) || type == typeof(Uri) || type == typeof(Type)
           || (ElementTypeOf(type) is { } element && IsSimple(element));

    private static Type? ElementTypeOf(Type type)
        => type.IsArray
            ? type.GetElementType()
            : type.GetInterfaces().Prepend(type)
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>))?
                .GetGenericArguments()[0];

    private static string Join(string prefix, string member)
        => prefix.Length == 0 ? member : member.Length == 0 ? prefix : $"{prefix}.{member}";
}
