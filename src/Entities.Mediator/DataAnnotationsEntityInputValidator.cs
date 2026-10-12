using System.Collections;
using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Regira.Entities.Models;

namespace Regira.Entities.Mediator;

/// <summary>
/// Checks an input DTO before a save or a patch maps it, by the rules MVC judges a request body by:
/// <c>System.ComponentModel.DataAnnotations</c> and <see cref="IValidatableObject"/> on the input and on every object
/// and collection item below it, and the implicit <c>[Required]</c> on a property of a non-nullable reference type
/// (<c>public string Title { get; set; } = null!;</c>), keyed by property path (<c>Address.Street</c>,
/// <c>Lines[0].Quantity</c>). <c>Validator.TryValidateObject</c> alone stops at the first level and knows no nullability;
/// MVC's model validation does not, and neither does this. It stops at the depth MVC stops at, and visits an object once.
/// A failure is thrown as an <c>EntityInputException</c>, so the 400 has the body of a validator's refusal.
/// </summary>
public class DataAnnotationsEntityInputValidator
{
    /// <summary>A shared instance; the validator holds no state.</summary>
    public static readonly DataAnnotationsEntityInputValidator Instance = new();

    private const int MaxDepth = 32;// MVC's default MvcOptions.MaxValidationDepth
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> NestedProperties = new();
    private static readonly ConcurrentDictionary<Type, (PropertyInfo Property, string Message)[]> NonNullableProperties = new();
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> ValidatedProperties = new();
    // MVC's [ValidateNever], matched by name, so this package needs no reference to MVC
    private const string ValidateNeverAttribute = "Microsoft.AspNetCore.Mvc.ModelBinding.Validation.ValidateNeverAttribute";

    /// <summary>Every error of <paramref name="input"/>, keyed by property path (<c>Lines[0].Quantity</c>); empty when it is valid.</summary>
    public IReadOnlyList<EntityInputError> Validate(object input)
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

        // MVC's implicit [Required]: a non-nullable reference left null
        var missing = false;
        foreach (var (property, message) in NonNullableProperties.GetOrAdd(model.GetType(), FindNonNullableProperties))
        {
            try
            {
                if (property.GetValue(model) == null)
                {
                    errors.Add(new EntityInputError(Join(prefix, property.Name), message));
                    missing = true;
                }
            }
            catch (TargetInvocationException)
            {
                // a getter that throws holds nothing to validate
            }
        }

        var results = new List<ValidationResult>();
        if (missing)
        {
            // as MVC does, an object whose properties fail is not validated itself: its type's attributes and its
            // IValidatableObject.Validate, which may count on the property that is missing, do not run
            foreach (var property in ValidatedProperties.GetOrAdd(model.GetType(), FindValidatedProperties))
            {
                try
                {
                    Validator.TryValidateProperty(property.GetValue(model), new ValidationContext(model) { MemberName = property.Name }, results);
                }
                catch (TargetInvocationException)
                {
                    // a getter that throws holds nothing to validate
                }
            }
        }
        else
        {
            Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
        }
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

    // the properties MVC gives an implicit [Required]: a reference type declared non-nullable, without [Required] of its own
    private static (PropertyInfo, string)[] FindNonNullableProperties(Type type)
    {
        var nullability = new NullabilityInfoContext();
        return type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0 && !p.PropertyType.IsValueType && !IsValidateNever(p)
                        && p.GetCustomAttribute<RequiredAttribute>() == null
                        && nullability.Create(p).ReadState == NullabilityState.NotNull)
            .Select(p => (p, new RequiredAttribute().FormatErrorMessage(DisplayName(p))))
            .ToArray();
    }

    // the properties Validator.TryValidateProperty can check: readable, not indexed, with a validation attribute
    private static PropertyInfo[] FindValidatedProperties(Type type)
        => type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0 && p.GetCustomAttributes<ValidationAttribute>().Any())
            .ToArray();

    // MVC validates neither the property nor anything below it
    private static bool IsValidateNever(PropertyInfo property)
        => property.GetCustomAttributes().Concat(property.PropertyType.GetCustomAttributes())
            .Any(attribute => attribute.GetType().FullName == ValidateNeverAttribute);

    private static string DisplayName(PropertyInfo property)
        => property.GetCustomAttribute<DisplayAttribute>()?.GetName()
           ?? property.GetCustomAttribute<System.ComponentModel.DisplayNameAttribute>()?.DisplayName
           ?? property.Name;

    // the properties that can hold an object or a collection of objects to descend into
    private static PropertyInfo[] FindNestedProperties(Type type)
        => type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0 && !IsSimple(p.PropertyType) && !IsValidateNever(p))
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
