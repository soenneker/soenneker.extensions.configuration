using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Soenneker.Extensions.String;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;

namespace Soenneker.Extensions.Configuration;

/// <summary>
/// A collection of helpful <see cref="IConfiguration"/> extension methods.
/// </summary>
public static class ConfigurationExtension
{
    private static readonly SearchValues<char> _keySeparators = SearchValues.Create(":_-.");

    private static readonly string[] _sensitiveKeyFragments =
    [
        "password", "passwd", "secret", "token", "api-key", "apikey", "access-key", "accesskey", "account-key", "accountkey", "private-key",
        "privatekey", "signing-key", "signingkey", "encryption-key", "encryptionkey", "connection-string", "connectionstring", "credential",
        "authorization", "shared-access", "sharedaccess", "sas-token", "sastoken", "sas-key", "saskey", "AzureWebJobsStorage"
    ];

    private static readonly string[] _sensitiveValueFragments =
    [
        "password=", "passwd=", "clientsecret=", "accountkey=", "sharedaccesssignature=", "apikey=", "api-key=", "-----BEGIN PRIVATE KEY-----"
    ];

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
    public static T GetValueStrict<T>(this IConfiguration configuration, string key)
    {
        if (key.IsNullOrWhiteSpace())
            throw new ArgumentNullException(nameof(key), $"The configuration key: '{key}' is invalid; it cannot be null or whitespace.");

        // 🔥 Fast path for string (no binder, no boxing, no conversion)
        if (typeof(T) == typeof(string))
        {
            string? value = configuration[key];

            if (value is null)
                throw new NullReferenceException(
                    $"Could not retrieve the required configuration key: '{key}' (String). Be sure the key is present in the IConfiguration used.");

            return (T)(object)value;
        }

        IConfigurationSection section = configuration.GetSection(key);

        // GetValue<T> returns default(T) for a section that has children but no scalar value.
        if (section.Value is null)
        {
            throw new NullReferenceException(
                $"Could not retrieve the required configuration key: '{key}' ({typeof(T).Name}). Be sure the key is present in the IConfiguration used.");
        }

        var valueTyped = configuration.GetValue<T>(key);

        // Only meaningful for reference / nullable value types
        if (valueTyped is null)
            throw new NullReferenceException(
                $"Could not retrieve the required configuration key: '{key}' ({typeof(T).Name}). Be sure the key is present in the IConfiguration used.");

        return valueTyped;
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
    [Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string? GetString(this IConfiguration configuration, string key)
    {
        if (key.IsNullOrWhiteSpace())
            throw new ArgumentNullException(nameof(key), $"The configuration key: '{key}' is invalid; it cannot be null or whitespace.");

        // Avoid binder for string
        return configuration[key];
    }

    /// <summary>
    /// Logs effective configuration keys and values, redacting common secret-bearing entries.
    /// </summary>
    /// <param name="configuration">The configuration instance to enumerate and log.</param>
    /// <param name="logger">The <see cref="ILogger"/> used to output the configuration values.</param>
    /// <remarks>
    /// This method logs only when the configuration key <c>Log:StartupConfiguration</c> is set to <c>true</c>.
    /// It iterates through all non-null configuration values, orders them alphabetically by key,
    /// and logs them using the <c>Debug</c> level for easier startup diagnostics. Values for common secret-bearing keys are redacted,
    /// line breaks are escaped, and long values are truncated.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static void LogAll(this IConfiguration configuration, ILogger logger)
    {
        LogAll(configuration, logger, null);
    }

    /// <summary>
    /// Logs effective configuration keys and values with built-in and caller-supplied redaction.
    /// </summary>
    /// <param name="configuration">The configuration instance to enumerate and log.</param>
    /// <param name="logger">The logger used to output configuration values at Debug level.</param>
    /// <param name="shouldRedact">An optional predicate that returns <see langword="true"/> for additional keys whose values must be redacted.</param>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static void LogAll(this IConfiguration configuration, ILogger logger, Func<string, bool>? shouldRedact)
    {
        // Avoid binder for bool; treat invalid/missing as false (same effective behavior as GetValue<bool> default false).
        string? flag = configuration["Log:StartupConfiguration"];
        if (flag is null)
            return;

        bool enabled = flag.Length == 1 ? flag[0] == '1' : bool.TryParse(flag, out bool b) && b;

        if (!enabled)
            return;

        if (!logger.IsEnabled(LogLevel.Debug))
            return;

        // Gather values (store non-null value as string to avoid nullable checks later)
        var list = new List<(string Key, string Value)>(128);

        foreach ((string key, string? value) in configuration.AsEnumerable())
        {
            if (value is not null)
                list.Add((Key: key, Value: PrepareLoggedValue(key, value, shouldRedact)));
        }

        if (list.Count == 0)
            return;

        list.Sort(static (a, b) => string.Compare(a.Key, b.Key, StringComparison.Ordinal));

        logger.LogDebug("----- Start of effective IConfiguration -----");

        for (int i = 0; i < list.Count; i++)
        {
            (string Key, string Value) item = list[i];
            logger.LogDebug("{key}={value}", item.Key, item.Value);
        }

        logger.LogDebug("----- End of effective IConfiguration -----");
    }

    private static string PrepareLoggedValue(string key, string value, Func<string, bool>? shouldRedact)
    {
        if (IsSensitiveKey(key) || IsSensitiveValue(value) || shouldRedact?.Invoke(key) == true)
            return "[REDACTED]";

        string sanitized = value.Replace("\r", "\\r", StringComparison.Ordinal)
                                .Replace("\n", "\\n", StringComparison.Ordinal);

        const int maxLength = 512;
        return sanitized.Length <= maxLength ? sanitized : sanitized[..maxLength] + "…";
    }

    private static bool IsSensitiveKey(string key)
    {
        if (ContainsAny(key, _sensitiveKeyFragments))
            return true;

        ReadOnlySpan<char> remaining = key.AsSpan();
        while (!remaining.IsEmpty)
        {
            int separator = remaining.IndexOfAny(_keySeparators);
            ReadOnlySpan<char> segment = separator < 0 ? remaining : remaining[..separator];

            if (segment.Equals("key", StringComparison.OrdinalIgnoreCase) || segment.Equals("pwd", StringComparison.OrdinalIgnoreCase) ||
                segment.Equals("dsn", StringComparison.OrdinalIgnoreCase))
                return true;

            if (separator < 0)
                break;

            remaining = remaining[(separator + 1)..];
        }

        return false;
    }

    private static bool IsSensitiveValue(string value)
    {
        return ContainsAny(value, _sensitiveValueFragments);
    }

    private static bool ContainsAny(string value, string[] fragments)
    {
        for (var i = 0; i < fragments.Length; i++)
        {
            if (value.Contains(fragments[i], StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
