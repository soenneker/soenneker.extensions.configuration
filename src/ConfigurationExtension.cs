using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Soenneker.Extensions.String;

namespace Soenneker.Extensions.Configuration;

/// <summary>
/// A collection of helpful IConfiguration extension methods
/// </summary>
public static class ConfigurationExtension
{
    /// <summary>
    /// Wraps IConfiguration.GetValue{T}(string), and if it's not present, it throws a <see cref="NullReferenceException"/>
    /// </summary>
    /// <exception cref="ArgumentNullException">Key is required</exception>
    /// <exception cref="NullReferenceException">Cannot find the key in the configuration</exception>
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
    /// Logs all effective key-value pairs from the <see cref="IConfiguration"/> instance.
    /// </summary>
    /// <param name="configuration">The configuration instance to log.</param>
    /// <param name="logger">The logger used to output configuration values.</param>
    /// <remarks>
    /// This method retrieves the final configuration values after all sources have been applied,
    /// ignoring overridden or null values. It logs the settings in alphabetical order for readability.
    /// </remarks>
    public static void LogAll(this IConfiguration configuration, ILogger logger)
    {
        if (!configuration.GetValue<bool>("Log:StartupConfiguration"))
            return;

        IOrderedEnumerable<KeyValuePair<string, string?>> configValues = configuration
                                                                         .AsEnumerable()
                                                                         .Where(kvp => kvp.Value != null)
                                                                         .OrderBy(kvp => kvp.Key);

        logger.LogDebug("----- Start of effective IConfiguration -----");

        foreach ((string key, string? value) in configValues)
        {
            logger.LogDebug("{key}={value}", key, value);
        }

        logger.LogDebug("----- End of effective IConfiguration -----");
    }
}