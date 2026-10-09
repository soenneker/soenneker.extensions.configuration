using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;

namespace Soenneker.Extensions.Configuration.Tests;

public class LoggingBoundaryTests
{
    [Test]
    public void PooledEntriesGrowAndRemainSortedAcrossCalls()
    {
        var values = new Dictionary<string, string?> { ["Log:StartupConfiguration"] = "1" };
        for (int i = 99; i >= 0; i--)
            values[$"Value{i:D3}"] = i.ToString();
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        for (int repeat = 0; repeat < 2; repeat++)
        {
            var logger = new CapturingLogger();
            configuration.LogAll(logger);
            if (logger.Messages.Count != 103 || logger.Messages[1] != "Log:StartupConfiguration=1")
                throw new InvalidOperationException("Unexpected entry count or ordering.");
            for (int i = 0; i < 100; i++)
                if (logger.Messages[i + 2] != $"Value{i:D3}={i}")
                    throw new InvalidOperationException("Entries were lost or incorrectly sorted.");
        }
    }

    [Test]
    public void EscapingAndTruncationMatchOriginalAtEveryBoundary()
    {
        foreach (int length in new[] { 0, 1, 255, 256, 510, 511, 512, 513, 1024 })
        foreach (string suffix in new[] { "", "\r", "\n", "\r\n", "\n\r", "\r\nrest", new string('\n', 1024) })
        {
            string value = new string('x', length) + suffix;
            string expected = value.Replace("\r", "\\r").Replace("\n", "\\n");
            if (expected.Length > 512)
                expected = expected[..512] + "…";
            IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Log:StartupConfiguration"] = "true", ["Value"] = value
            }).Build();
            var logger = new CapturingLogger();
            configuration.LogAll(logger);
            if (!logger.Messages.Contains("Value=" + expected))
                throw new InvalidOperationException($"Escaping/truncation changed at input length {length}, suffix length {suffix.Length}.");
        }
    }

    [Test]
    public void SecretsBeyondTruncationBoundaryAreStillRedacted()
    {
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Log:StartupConfiguration"] = "true", ["Value"] = new string('x', 1024) + "password=hidden"
        }).Build();
        var logger = new CapturingLogger();
        configuration.LogAll(logger);
        if (!logger.Messages.Contains("Value=[REDACTED]"))
            throw new InvalidOperationException("Secret detection must inspect the complete value.");
    }
}
