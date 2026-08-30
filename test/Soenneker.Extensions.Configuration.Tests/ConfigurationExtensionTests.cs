
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace Soenneker.Extensions.Configuration.Tests;

public class ConfigurationExtensionTests
{
    [Test]
    public void GetValueStrict_RejectsSectionWithoutScalarValue()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Parent:Child"] = "42" })
            .Build();

        var threw = false;
        try
        {
            _ = configuration.GetValueStrict<int>("Parent");
        }
        catch (NullReferenceException)
        {
            threw = true;
        }

        if (!threw)
            throw new InvalidOperationException("A section without a scalar value was accepted.");
    }

    [Test]
    public void LogAll_RedactsAndSanitizesValues()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Log:StartupConfiguration"] = "true",
                ["Database:Password"] = "super-secret",
                ["Jwt:Key"] = "jwt-secret",
                ["Custom:Hide"] = "custom-secret",
                ["Safe:Value"] = "line1\r\nline2"
            })
            .Build();
        var logger = new CapturingLogger();

        configuration.LogAll(logger, key => key == "Custom:Hide");

        string output = string.Join("\n", logger.Messages);
        if (output.Contains("super-secret", StringComparison.Ordinal) || output.Contains("jwt-secret", StringComparison.Ordinal) ||
            output.Contains("custom-secret", StringComparison.Ordinal))
            throw new InvalidOperationException("A secret value was logged.");
        if (!output.Contains("[REDACTED]", StringComparison.Ordinal) || !output.Contains("line1\\r\\nline2", StringComparison.Ordinal))
            throw new InvalidOperationException("Expected redaction or line-break escaping was not applied.");
    }

    private sealed class CapturingLogger : ILogger
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel == LogLevel.Debug;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }
    }
}
