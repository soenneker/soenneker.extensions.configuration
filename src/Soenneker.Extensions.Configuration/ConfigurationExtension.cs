using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Configuration;
using System.ComponentModel;
using System.Globalization;
using System;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;

namespace Soenneker.Extensions.Configuration;

/// <summary>
/// A collection of helpful <see cref="IConfiguration"/> extension methods.
/// </summary>
public static partial class ConfigurationExtension
{
    /// <summary>
    /// Retrieves a strongly-typed configuration value for the specified key, and throws if the key is missing or the value is null.
    /// </summary>
    /// <typeparam name="T">The expected type of the configuration value.</typeparam>
    /// <param name="configuration">The configuration source to retrieve the value from.</param>
    /// <param name="key">The key of the configuration value.</param>
    /// <returns>The resolved configuration value of type <typeparamref name="T"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="key"/> is null or whitespace.</exception>
    /// <exception cref="NullReferenceException">Thrown when the specified key cannot be found or its value is null.</exception>
    /// <remarks>
    /// This method behaves like <see cref="ConfigurationBinder.GetValue{T}(IConfiguration, string)"/> but enforces strict existence
    /// of the key. It is useful for configuration values that are mandatory at startup.
    /// </remarks>
    [Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
    [RequiresUnreferencedCode("Non-primitive configuration types may have members trimmed. Use GetStringStrict for required string values.")]
    public static T GetValueStrict<T>(this IConfiguration configuration, string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            ThrowInvalidKey(key);

        string? value = configuration[key];
        if (value is null)
            return ThrowMissingValue<T>(key);

        if (typeof(T) == typeof(string) || typeof(T) == typeof(object))
            return (T)(object)value;

        return ConvertStrict<T>(configuration, key, value);
    }

    [RequiresUnreferencedCode("Non-primitive configuration types may have members trimmed.")]
    private static T ConvertStrict<T>(IConfiguration configuration, string key, string value)
    {
        Type type = ConfigurationValueType<T>.EffectiveType;
        if (type != typeof(T) && value.Length == 0)
            return ThrowMissingValue<T>(key);

        // Resolve on every call so TypeDescriptor provider changes remain observable.
        TypeConverter converter = TypeDescriptor.GetConverter(type);
        // Exact converter checks preserve dynamically registered custom conversion behavior.
        // The JIT removes unrelated branches and boxing for these value types.
        if ((typeof(T) == typeof(bool) || typeof(T) == typeof(bool?)) && converter.GetType() == typeof(BooleanConverter) &&
            bool.TryParse(value, out bool parsedBoolean))
            return (T)(object)parsedBoolean;

        if ((typeof(T) == typeof(int) || typeof(T) == typeof(int?)) && converter.GetType() == typeof(Int32Converter) &&
            int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedInt32))
            return (T)(object)parsedInt32;

        if ((typeof(T) == typeof(long) || typeof(T) == typeof(long?)) && converter.GetType() == typeof(Int64Converter) &&
            long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsedInt64))
            return (T)(object)parsedInt64;

        if ((typeof(T) == typeof(uint) || typeof(T) == typeof(uint?)) && converter.GetType() == typeof(UInt32Converter) &&
            uint.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint parsedUInt32))
            return (T)(object)parsedUInt32;

        if ((typeof(T) == typeof(ulong) || typeof(T) == typeof(ulong?)) && converter.GetType() == typeof(UInt64Converter) &&
            ulong.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong parsedUInt64))
            return (T)(object)parsedUInt64;

        if ((typeof(T) == typeof(short) || typeof(T) == typeof(short?)) && converter.GetType() == typeof(Int16Converter) &&
            short.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out short parsedInt16))
            return (T)(object)parsedInt16;

        if ((typeof(T) == typeof(ushort) || typeof(T) == typeof(ushort?)) && converter.GetType() == typeof(UInt16Converter) &&
            ushort.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out ushort parsedUInt16))
            return (T)(object)parsedUInt16;

        if ((typeof(T) == typeof(byte) || typeof(T) == typeof(byte?)) && converter.GetType() == typeof(ByteConverter) &&
            byte.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out byte parsedByte))
            return (T)(object)parsedByte;

        if ((typeof(T) == typeof(sbyte) || typeof(T) == typeof(sbyte?)) && converter.GetType() == typeof(SByteConverter) &&
            sbyte.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out sbyte parsedSByte))
            return (T)(object)parsedSByte;

        if ((typeof(T) == typeof(float) || typeof(T) == typeof(float?)) && converter.GetType() == typeof(SingleConverter) &&
            float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsedSingle))
            return (T)(object)parsedSingle;

        if ((typeof(T) == typeof(double) || typeof(T) == typeof(double?)) && converter.GetType() == typeof(DoubleConverter) &&
            double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedDouble))
            return (T)(object)parsedDouble;

        if ((typeof(T) == typeof(decimal) || typeof(T) == typeof(decimal?)) && converter.GetType() == typeof(DecimalConverter) &&
            decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal parsedDecimal))
            return (T)(object)parsedDecimal;

        if ((typeof(T) == typeof(Guid) || typeof(T) == typeof(Guid?)) && converter.GetType() == typeof(GuidConverter) &&
            Guid.TryParse(value, out Guid parsedGuid))
            return (T)(object)parsedGuid;

        object? converted;
        if (converter.CanConvertFrom(typeof(string)))
        {
            try
            {
                converted = converter.ConvertFromInvariantString(value);
            }
            catch (Exception exception)
            {
                throw ConversionError(configuration, key, value, type, exception);
            }
        }
        else if (type == typeof(byte[]))
        {
            try
            {
                converted = value.Length == 0 ? Array.Empty<byte>() : Convert.FromBase64String(value);
            }
            catch (FormatException exception)
            {
                throw ConversionError(configuration, key, value, type, exception);
            }
        }
        else
        {
            // Match the binder's behavior for unsupported types, including value types.
            return default(T) is null ? ThrowMissingValue<T>(key) : (T)(object)null!;
        }

        return converted is null ? ThrowMissingValue<T>(key) : (T)converted;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static InvalidOperationException ConversionError(IConfiguration configuration, string key, string value, Type type, Exception exception)
    {
        return new InvalidOperationException($"Failed to convert configuration value '{value}' at '{configuration.GetSection(key).Path}' to type '{type}'.", exception);
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowInvalidKey(string key)
    {
        throw new ArgumentNullException(nameof(key), $"The configuration key: '{key}' is invalid; it cannot be null or whitespace.");
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    private static T ThrowMissingValue<T>(string key)
    {
        throw new NullReferenceException(
            $"Could not retrieve the required configuration key: '{key}' ({typeof(T).Name}). Be sure the key is present in the IConfiguration used.");
    }

    /// <summary>
    /// Retrieves a required string configuration value for the specified key, throwing if missing or null.
    /// </summary>
    /// <param name="configuration">The configuration source to retrieve the value from.</param>
    /// <param name="key">The key of the configuration value.</param>
    /// <returns>The non-null string value associated with the key.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="key"/> is null or whitespace.</exception>
    /// <exception cref="NullReferenceException">Thrown when the specified key cannot be found or its value is null.</exception>
    /// <remarks>
    /// This is a convenience wrapper around <see cref="GetValueStrict{T}(IConfiguration, string)"/> for string values.
    /// </remarks>
    [Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string GetStringStrict(this IConfiguration configuration, string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            ThrowInvalidKey(key);

        return configuration[key] ?? ThrowMissingValue<string>(key);
    }

    /// <summary>
    /// Retrieves an optional string configuration value for the specified key.
    /// </summary>
    /// <param name="configuration">The configuration source to retrieve the value from.</param>
    /// <param name="key">The key of the configuration value.</param>
    /// <returns>
    /// The string value associated with the key, or <see langword="null"/> if the key does not exist or the value is not set.
    /// </returns>
    /// <remarks>
    /// This behaves like <see cref="ConfigurationBinder.GetValue{T}(IConfiguration, string)"/> but returns null when the key is missing.
    /// </remarks>
    [Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string? GetString(this IConfiguration configuration, string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            ThrowInvalidKey(key);

        // Avoid binder for string
        return configuration[key];
    }

}
