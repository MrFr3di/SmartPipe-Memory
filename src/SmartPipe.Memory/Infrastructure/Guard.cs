using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace SmartPipe.Memory.Infrastructure;

/// <summary>
/// Precondition checks for internal use.
/// Throws appropriate exceptions for invalid arguments or states.
/// </summary>
internal static class Guard
{
    /// <summary>
    /// Throws <see cref="ArgumentNullException"/> if the value is null.
    /// </summary>
    [return: NotNull]
    public static T NotNull<T>(
        [NotNull] this T? value,
        [CallerArgumentExpression("value")] string? paramName = null)
        where T : class
    {
        if (value is null)
            throw new ArgumentNullException(paramName);
        return value;
    }

    /// <summary>
    /// Throws <see cref="ArgumentException"/> if the string is null or empty.
    /// </summary>
    [return: NotNull]
    public static string NotNullOrEmpty(
        [NotNull] this string? value,
        [CallerArgumentExpression("value")] string? paramName = null)
    {
        if (string.IsNullOrEmpty(value))
            throw new ArgumentException("Value cannot be null or empty.", paramName);
        return value;
    }

    /// <summary>
    /// Throws <see cref="ArgumentOutOfRangeException"/> if the value is negative or zero.
    /// </summary>
    public static int Positive(
        int value,
        [CallerArgumentExpression("value")] string? paramName = null)
    {
        if (value <= 0)
            throw new ArgumentOutOfRangeException(paramName, value, "Value must be positive.");
        return value;
    }

    /// <summary>
    /// Throws <see cref="InvalidOperationException"/> if the condition is false.
    /// </summary>
    public static void That(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}