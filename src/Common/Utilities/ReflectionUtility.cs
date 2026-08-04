using System.Reflection;

namespace Regira.Utilities;

public static class ReflectionUtility
{
    /// <summary>
    /// Casts the specified object to the specified type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The target type to cast the object to.</typeparam>
    /// <param name="o">The object to be cast.</param>
    /// <returns>The object cast to the specified type <typeparamref name="T"/>.</returns>
    /// <exception cref="InvalidCastException">Thrown if the object cannot be cast to the specified type <typeparamref name="T"/>.</exception>
    public static T CastToHelper<T>(this object o) => (T)o;
    /// <summary>
    /// Casts the specified object to the given <see cref="Type"/> dynamically at runtime.
    /// </summary>
    /// <param name="o">The object to be cast.</param>
    /// <param name="type">The target <see cref="Type"/> to cast the object to.</param>
    /// <returns>The object cast to the specified <see cref="Type"/>, or <c>null</c> if the cast fails.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="type"/> is <c>null</c>.</exception>
    /// <exception cref="TargetInvocationException">
    /// Thrown if the underlying method invoked throws an exception.
    /// </exception>
    /// <remarks>
    /// This method uses reflection to dynamically cast an object to a specified type.
    /// It internally utilizes a generic helper method to perform the cast.
    /// </remarks>
    public static dynamic? CastTo(this object o, Type type)
    {
        // https://stackoverflow.com/questions/18052562/cast-to-a-reflected-type-in-c-sharp#55852845
        var methodInfo = typeof(ReflectionUtility).GetMethod(nameof(CastToHelper), BindingFlags.Static | BindingFlags.Public);
        var genericArguments = new[] { type };
        var genericMethodInfo = methodInfo?.MakeGenericMethod(genericArguments);
        return genericMethodInfo?.Invoke(null, [o]);
    }
    
    /// <summary>
    /// Invokes the specified asynchronous method represented by <see cref="MethodInfo"/> on the given object instance with the provided parameters.
    /// </summary>
    /// <param name="methodInfo">The <see cref="MethodInfo"/> representing the method to invoke.</param>
    /// <param name="obj">The object instance on which to invoke the method. Pass <c>null</c> for static methods.</param>
    /// <param name="parameters">An array of parameters to pass to the method.</param>
    /// <returns>
    /// A <see cref="Task{Object}"/> representing the asynchronous operation. 
    /// The task's result contains the return value of the invoked method, or <c>null</c> if the method has no return value.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="methodInfo"/> is <c>null</c>.</exception>
    /// <exception cref="TargetInvocationException">
    /// Thrown if the underlying method invoked throws an exception.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown if the method's parameters do not match the provided <paramref name="parameters"/>.
    /// </exception>
    /// <remarks>
    /// This method is a utility for invoking asynchronous methods dynamically at runtime using reflection.
    /// It internally awaits the task returned by the invoked method and retrieves its result if applicable.
    /// </remarks>
    public static Task<object?> InvokeAsync(this MethodInfo methodInfo, object obj, params object[] parameters)
        => InvokeAsync<object?>(methodInfo, obj, parameters);
    /// <summary>
    /// Invokes the specified asynchronous method represented by <see cref="MethodInfo"/> on the given object instance with the provided parameters,
    /// and returns the result of the asynchronous operation as a strongly-typed value.
    /// </summary>
    /// <typeparam name="TResult">The type of the result returned by the asynchronous method.</typeparam>
    /// <param name="methodInfo">The <see cref="MethodInfo"/> representing the method to invoke.</param>
    /// <param name="obj">The object instance on which to invoke the method. Pass <c>null</c> for static methods.</param>
    /// <param name="parameters">An array of parameters to pass to the method.</param>
    /// <returns>
    /// A <see cref="Task{TResult}"/> representing the asynchronous operation. 
    /// The task's result contains the return value of the invoked method.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="methodInfo"/> is <c>null</c>.</exception>
    /// <exception cref="TargetInvocationException">
    /// Thrown if the underlying method invoked throws an exception.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown if the method's parameters do not match the provided <paramref name="parameters"/>.
    /// </exception>
    /// <remarks>
    /// This method is a utility for invoking asynchronous methods dynamically at runtime using reflection.
    /// It internally awaits the task returned by the invoked method and retrieves its result as a strongly-typed value.
    /// </remarks>
    public static async Task<TResult> InvokeAsync<TResult>(this MethodInfo methodInfo, object obj, params object[] parameters)
    {
        // https://stackoverflow.com/questions/39674988/how-to-call-a-generic-async-method-using-reflection#39679855
        var task = (Task)methodInfo.Invoke(obj, parameters)!;
        await task.ConfigureAwait(false);
        var resultProperty = task.GetType().GetProperty("Result");
        return (TResult)resultProperty!.GetValue(task)!;
    }
}