using System.Collections;
using System.Linq.Expressions;
using System.Reflection;

namespace Regira.Utilities;

public static class TypeUtility
{
    /// <summary>
    /// Get Base type and interfaces, including inherited ones
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    public static IEnumerable<Type> GetBaseTypes(Type type)
    {
        var typeInterfaces = type.GetInterfaces();
        if (type.BaseType == null)
        {
            return typeInterfaces;
        }

        return new[] { type.BaseType }
            .Concat(typeInterfaces)
            .Concat(typeInterfaces.SelectMany(GetBaseTypes))
            .Concat(GetBaseTypes(type.BaseType))
            .Distinct();
    }
    /// <summary>
    /// Checks if a type implements a given base type (class or interface)
    /// </summary>
    /// <typeparam name="TBaseType"></typeparam>
    /// <param name="type"></param>
    /// <returns></returns>
    public static bool ImplementsBaseType<TBaseType>(Type type)
        => GetBaseTypes(type).Any(baseType => baseType == typeof(TBaseType));
    public static bool ImplementsType<TType>(Type type)
        => type == typeof(TType) || GetBaseTypes(type).Any(baseType => baseType == typeof(TType));
    public static bool ImplementsType(Type type, Type typeToImplement)
        => type == typeToImplement || GetBaseTypes(type).Any(baseType => baseType == typeToImplement);

    /// <summary>
    /// Converts a nullable type to it's corresponding simple type (int? -> int)
    /// </summary>
    /// <param name="nullableType"></param>
    /// <returns></returns>
    public static Type GetSimpleType(Type nullableType)
    {
        return Nullable.GetUnderlyingType(nullableType) ?? nullableType;
    }
    /// <summary>
    /// Checks if a type is a simple type:
    /// <list type="bullet">
    ///     <item>(Nullable) Primitive type</item>
    ///     <item>Enum</item>
    ///     <item>String</item>
    ///     <item>DateTime</item>
    ///     <item>DateOnly</item>
    ///     <item>DateTimeOffset</item>
    ///     <item>TimeSpan</item>
    ///     <item>Guid</item>
    /// </list>
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    public static bool IsSimpleType(Type type)
    {
        return type.IsPrimitive || type.IsEnum
                                || new[] { typeof(string), typeof(decimal), typeof(DateTime), typeof(DateTimeOffset), typeof(TimeSpan), typeof(Guid) }.Contains(type)
#if NETCOREAPP2_1_OR_GREATER
                                || new[] { typeof(DateOnly) }.Contains(type)
#endif
                                || Convert.GetTypeCode(type) != TypeCode.Object || (IsNullableType(type) && IsSimpleType(type.GetGenericArguments()[0]));
    }
    /// <summary>
    /// Checks if type implements Nullable&lt;&gt;
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    public static bool IsNullableType(Type type)
    {
        return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>);
    }

    /// <summary>
    /// <inheritdoc cref="IsTypeEnumerable"/>
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="sourceType"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public static bool IsTypeEnumerable<T>(Type sourceType)
        where T : IEnumerable
        => IsTypeEnumerable(sourceType, typeof(T));
    /// <summary>
    /// Checks if type can be enumerated
    /// </summary>
    /// <param name="sourceType"></param>
    /// <param name="type"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public static bool IsTypeEnumerable(Type sourceType, Type? type = null)
    {
        if (type != null && !type.GetInterfaces().Contains(typeof(IEnumerable)))
        {
            throw new ArgumentException($"{nameof(type)} is not an enumerable type");
        }

        if (type == sourceType)
        {
            return true;
        }

        if (sourceType.IsGenericType)
        {
            var faceType = sourceType.GetInterface(typeof(IEnumerable<>).FullName!);
            if (faceType == null)
            {
                return false;
            }

            type ??= typeof(IEnumerable<>);
            var genericCollectionType = type.MakeGenericType(GetGenericCollectionType(sourceType)!);
            var baseTypes = GetBaseTypes(sourceType).ToArray();
            return baseTypes.Contains(genericCollectionType);
        }

        type ??= typeof(IEnumerable);
        return GetBaseTypes(sourceType).Contains(type);
    }

    /// <summary>
    /// <inheritdoc cref="IsTypeACollection"/>
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="sourceType"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public static bool IsTypeACollection<T>(Type sourceType)
        where T : IEnumerable
        => IsTypeACollection(sourceType, typeof(T));
    /// <summary>
    /// Checks if type is a collection (or list)
    /// </summary>
    /// <param name="sourceType"></param>
    /// <param name="type"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public static bool IsTypeACollection(Type sourceType, Type? type = null)
    {
        if (type != null)
        {
            var interfaces = (type.IsInterface ? new[] { type }.Concat(type.GetInterfaces()) : type.GetInterfaces())
                .ToArray();
            var genericArg = GetGenericCollectionType(type);
            var listType = genericArg == null ? typeof(ICollection) : typeof(ICollection<>).MakeGenericType(genericArg);
            if (!interfaces.Contains(listType))
            {
                throw new ArgumentException($"{nameof(type)} is not a collection type");
            }
        }

        return IsTypeEnumerable(sourceType, type);
    }

    /// <summary>
    /// Retrieves the generic type argument of a collection type, if applicable.
    /// </summary>
    /// <param name="collectionType">The type of the collection to analyze.</param>
    /// <returns>
    /// The generic type argument of the collection if it is a generic collection; otherwise, <c>null</c>.
    /// </returns>
    /// <remarks>
    /// This method examines the provided <paramref name="collectionType"/> to determine if it implements
    /// <see cref="System.Collections.Generic.IEnumerable{T}"/>. If so, it extracts and returns the generic type argument.
    /// If the collection type is not generic, the method attempts to retrieve the element type.
    /// </remarks>
    public static Type? GetGenericCollectionType(Type collectionType)
    {
        var enumerableType = collectionType.GetInterface(typeof(IEnumerable<>).FullName!);
        return enumerableType?.GetGenericArguments().FirstOrDefault()
               ?? enumerableType?.GetElementType();
    }

    /// <summary>
    /// Retrieves the name of the property referenced in the specified lambda expression.
    /// </summary>
    /// <param name="selector">
    /// A lambda expression that selects a property. For example, <c>() => instance.Property</c>.
    /// </param>
    /// <returns>
    /// The name of the property if the expression references a property; otherwise, <c>null</c>.
    /// </returns>
    /// <remarks>
    /// This method is useful for obtaining property names in a type-safe manner, avoiding the use of string literals.
    /// </remarks>
    public static string? GetPropertyName(Expression<Func<object>> selector)
    {
        return GetMemberName(selector);
    }
    /// <summary>
    /// Get the property name by selecting an object's property member
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="selector"></param>
    /// <returns>Name of property</returns>
    public static string? GetPropertyName<T>(Expression<Func<T, object>> selector)
    {
        return GetMemberName(selector);
    }
    private static string? GetMemberName<TFunc>(Expression<TFunc> selector)
    {
        // https://stackoverflow.com/questions/671968/retrieving-property-name-from-lambda-expression#answer-71965639
        var memberExpression = selector.Body is UnaryExpression expression
            ? expression.Operand as MemberExpression
            : selector.Body as MemberExpression;

        return (memberExpression?.Member as PropertyInfo)?.Name;

        // https://stackoverflow.com/questions/671968/retrieving-property-name-from-lambda-expression#answer-2916344
        //if (selector.Body is MemberExpression body)
        //{
        //    return body.Member.Name;
        //}

        //var ubody = (UnaryExpression)selector.Body;
        //var bodyExpression = ubody.Operand as MemberExpression;
        //return bodyExpression?.Member.Name;
    }

    /// <summary>
    /// <inheritdoc cref="ImplementsInterface"/>
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="type"></param>
    /// <returns></returns>
    public static bool ImplementsInterface<T>(Type type)
        => ImplementsInterface(type, typeof(T));
    /// <summary>
    /// Checks if a type implements a given interface
    /// </summary>
    /// <param name="type"></param>
    /// <param name="implementedInterface"></param>
    /// <returns></returns>
    public static bool ImplementsInterface(Type type, Type implementedInterface)
    {
        if (type == implementedInterface)
        {
            return true;
        }

        var interfaces = type.GetInterfaces();
        return !implementedInterface.IsGenericType
            ? interfaces.Contains(implementedInterface)
            : interfaces.Any(x => x == implementedInterface || x.IsGenericType && x.GetGenericTypeDefinition() == implementedInterface);
    }

    /// <summary>
    /// Determines whether the specified type or any of its base types or interfaces
    /// contains the specified generic argument type.
    /// </summary>
    /// <param name="type">The type to inspect.</param>
    /// <param name="genericType">The generic argument type to look for.</param>
    /// <returns>
    /// <see langword="true"/> if the specified type or any of its base types or interfaces
    /// contains the specified generic argument type; otherwise, <see langword="false"/>.
    /// </returns>
    public static bool HasGenericArgument(Type type, Type genericType)
        => type.GetGenericArguments()
            .Union(GetBaseTypes(type).SelectMany(t => t.GetGenericArguments()))
            .Contains(genericType);

    /// <summary>
    /// Creates a generic list type with the specified generic type and list type constraints.
    /// </summary>
    /// <typeparam name="TList">The type of the list, which must implement <see cref="IList{TGeneric}"/>.</typeparam>
    /// <typeparam name="TGeneric">The generic type parameter for the list.</typeparam>
    /// <returns>A constructed generic list type based on the specified parameters.</returns>
    public static Type CreateGenericListType<TList, TGeneric>()
        where TList : IList<TGeneric>
        => CreateGenericListType(typeof(TGeneric), typeof(TList));
    /// <summary>
    /// Creates a generic list type using the specified generic type.
    /// </summary>
    /// <typeparam name="TGeneric">The type of elements in the generic list.</typeparam>
    /// <returns>A <see cref="Type"/> representing a generic list of the specified type.</returns>
    public static Type CreateGenericListType<TGeneric>()
        => CreateGenericListType(typeof(TGeneric));
    /// <summary>
    /// Creates a generic list type using the specified generic type and an optional list type.
    /// </summary>
    /// <param name="genericType">The type of elements in the generic list.</param>
    /// <param name="listType">
    /// The type of the list to create. If <c>null</c>, the default type <see cref="List{T}"/> is used.
    /// The specified list type must implement <see cref="IList"/>.
    /// </param>
    /// <returns>A <see cref="Type"/> representing a generic list of the specified type.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown if the specified <paramref name="listType"/> does not implement <see cref="IList"/>.
    /// </exception>
    public static Type CreateGenericListType(Type genericType, Type? listType = null)
    {
        listType ??= typeof(List<>);
        if (!ImplementsInterface<IList>(listType))
        {
            throw new ArgumentException($"{nameof(listType)} is not a list type");
        }
        return listType.MakeGenericType(genericType);
    }
}