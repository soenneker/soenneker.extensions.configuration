using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Buffers;
using System.Runtime.CompilerServices;

namespace Soenneker.Extensions.Configuration;

public static partial class ConfigurationExtension
{
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
        // Preserve the startup flag convention: true or 1 enables logging.
        string? flag = configuration["Log:StartupConfiguration"];
        if (flag is null)
            return;

        bool enabled = flag.Length == 1 ? flag[0] == '1' : bool.TryParse(flag, out bool b) && b;

        if (!enabled)
            return;

        if (!logger.IsEnabled(LogLevel.Debug))
            return;

        var pool = ArrayPool<(string Key, string Value)>.Shared;
        (string Key, string Value)[] entries = pool.Rent(16);
        int count = 0;
        try
        {
            foreach ((string key, string? value) in configuration.AsEnumerable())
            {
                if (value is null)
                    continue;

                if (count == entries.Length)
                {
                    (string Key, string Value)[] larger = pool.Rent(checked(count * 2));
                    entries.AsSpan(0, count).CopyTo(larger);
                    pool.Return(entries, clearArray: true);
                    entries = larger;
                }

                entries[count++] = (key, PrepareLoggedValue(key, value, shouldRedact));
            }

            if (count == 0)
                return;

            entries.AsSpan(0, count).Sort(static (a, b) => string.CompareOrdinal(a.Key, b.Key));
            ConfigurationLogState.Start(logger, null);

            for (int i = 0; i < count; i++)
            {
                (string Key, string Value) item = entries[i];
                ConfigurationLogState.Entry(logger, item.Key, item.Value, null);
            }

            ConfigurationLogState.End(logger, null);
        }
        finally
        {
            // Do not retain configuration strings, even when a predicate or logger throws.
            pool.Return(entries, clearArray: true);
        }
    }

    private static string PrepareLoggedValue(string key, string value, Func<string, bool>? shouldRedact)
    {
        if (IsSensitiveKey(key) || IsSensitiveValue(value) || shouldRedact?.Invoke(key) == true)
            return "[REDACTED]";

        const int maxLength = 512;
        ReadOnlySpan<char> prefix = value.AsSpan(0, Math.Min(value.Length, maxLength));
        int firstLineBreak = prefix.IndexOfAny('\r', '\n');
        if (firstLineBreak < 0)
            return value.Length <= maxLength ? value : string.Concat(prefix, "…");

        // Only the first 512 escaped characters can be emitted, regardless of input length.
        Span<char> buffer = stackalloc char[maxLength + 1];
        prefix[..firstLineBreak].CopyTo(buffer);
        int written = firstLineBreak;
        int consumed = firstLineBreak;
        while (consumed < value.Length && written < maxLength)
        {
            char c = value[consumed++];
            if (c is '\r' or '\n')
            {
                buffer[written++] = '\\';
                if (written == maxLength)
                {
                    buffer[written++] = '…';
                    return new string(buffer[..written]);
                }

                buffer[written++] = c == '\r' ? 'r' : 'n';
            }
            else
            {
                buffer[written++] = c;
            }
        }

        if (consumed < value.Length)
            buffer[written++] = '…';

        return new string(buffer[..written]);
    }

    private static bool IsSensitiveKey(string key)
    {
        if (key.AsSpan().ContainsAny(ConfigurationLogState.SensitiveKeyFragments))
            return true;

        ReadOnlySpan<char> remaining = key.AsSpan();
        while (!remaining.IsEmpty)
        {
            int separator = remaining.IndexOfAny(ConfigurationLogState.KeySeparators);
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
        return value.AsSpan().ContainsAny(ConfigurationLogState.SensitiveValueFragments);
    }

}
