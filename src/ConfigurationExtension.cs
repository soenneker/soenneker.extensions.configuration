using System;
using Microsoft.Extensions.Configuration;
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

        if (value == null)
            throw new NullReferenceException($"Could not retrieve the required configuration key: '{key}' ({typeof(T).Name}). Be sure the key is present in the IConfiguration used.");

        return value;
    }
}