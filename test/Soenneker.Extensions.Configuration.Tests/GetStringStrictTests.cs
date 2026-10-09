using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;

namespace Soenneker.Extensions.Configuration.Tests;

public class GetStringStrictTests
{
    [Test]
    public void PreservesScalarValuesAndRejectsMissingValues()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Value"] = "  value  ",
                ["Empty"] = "",
                ["Parent:Child"] = "child"
            })
            .Build();

        if (configuration.GetStringStrict("Value") != "  value  " || configuration.GetStringStrict("Empty") != "")
            throw new InvalidOperationException("Required string values must be returned unchanged.");

        foreach (string key in new[] { "Missing", "Parent" })
        {
            try
            {
                configuration.GetStringStrict(key);
            }
            catch (NullReferenceException)
            {
                continue;
            }

            throw new InvalidOperationException($"The missing scalar '{key}' was accepted.");
        }

        foreach (string key in new[] { "", " " })
        {
            try
            {
                configuration.GetStringStrict(key);
            }
            catch (ArgumentNullException)
            {
                continue;
            }

            throw new InvalidOperationException("An invalid configuration key was accepted.");
        }
    }
}
