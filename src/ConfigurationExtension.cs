using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Soenneker.Extensions.String;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Soenneker.Extensions.Configuration;

/// <summary>
/// A collection of helpful <see cref="IConfiguration"/> extension methods.
/// </summary>
public static class ConfigurationExtension
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T GetValueStrict<T>(this IConfiguration configuration, string key)
    {
        if (key.IsNullOrWhiteSpace())
            throw new ArgumentNullException(nameof(key), $"The configuration key: '{key}' is invalid; it cannot be null or whitespace.");

        var value = configuration.GetValue<T>(key);

        if (value is null)
            throw new NullReferenceException($"Could not retrieve the required configuration key: '{key}' ({typeof(T).Name}). Be sure the key is present in the IConfiguration used.");

        return value;
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string GetStringStrict(this IConfiguration configuration, string key)
    {
        return configuration.GetValueStrict<string>(key);
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string? GetString(this IConfiguration configuration, string key)
    {
        return configuration.GetValue<string>(key);
    }

    /// <summary>
    /// Logs all effective key-value pairs from the current <see cref="IConfiguration"/> instance.
    /// </summary>
    /// <param name="configuration">The configuration instance to enumerate and log.</param>
    /// <param name="logger">The <see cref="ILogger"/> used to output the configuration values.</param>
    /// <remarks>
    /// This method logs only when the configuration key <c>Log:StartupConfiguration</c> is set to <c>true</c>.
    /// It iterates through all non-null configuration values, orders them alphabetically by key,
    /// and logs them using the <c>Debug</c> level for easier startup diagnostics.
    /// </remarks>
    public static void LogAll(this IConfiguration configuration, ILogger logger)
    {
        if (!configuration.GetValue<bool>("Log:StartupConfiguration"))
            return;

        if (!logger.IsEnabled(LogLevel.Debug))
            return;

        // Gather values
        var list = new List<KeyValuePair<string, string?>>(128);

        foreach (KeyValuePair<string, string?> kvp in configuration.AsEnumerable())
        {
            if (kvp.Value is not null)
                list.Add(kvp);
        }

        if (list.Count == 0)
            return;

        // Sort by key for predictable output
        list.Sort(static (a, b) => StringComparer.Ordinal.Compare(a.Key, b.Key));

        logger.LogDebug("----- Start of effective IConfiguration -----");

        for (int i = 0; i < list.Count; i++)
        {
            KeyValuePair<string, string?> item = list[i];
            logger.LogDebug("{key}={value}", item.Key, item.Value);
        }

        logger.LogDebug("----- End of effective IConfiguration -----");
    }
}