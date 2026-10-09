using System;
using System.Collections.Generic;
using System.ComponentModel;
using Microsoft.Extensions.Configuration;

namespace Soenneker.Extensions.Configuration.Tests;

[NotInParallel]
public class ConversionCompatibilityTests
{
    [Test]
    public void ScalarConversionsMatchBinder()
    {
        string[] values = ["", " ", "42", "-1", "0x2A", "#FF", "0Xffffffff", "+17", "1,234", "1.25", "1e3", "NaN", "Infinity", "true", "FALSE", "\u00a042\u00a0", "2147483648", "invalid", "AQID", "00000000-0000-0000-0000-000000000001"];
        foreach (string value in values)
        {
            IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Parent:Value"] = value }).Build();
            IConfiguration section = configuration.GetSection("Parent");
            Check<bool>(section); Check<int>(section); Check<long>(section); Check<uint>(section); Check<ulong>(section);
            Check<short>(section); Check<ushort>(section); Check<byte>(section); Check<sbyte>(section);
            Check<float>(section); Check<double>(section); Check<decimal>(section); Check<Guid>(section);
            Check<int?>(section); Check<bool?>(section); Check<decimal?>(section); Check<string>(section); Check<object>(section);
            Check<byte[]>(section); Check<DayOfWeek>(section); Check<TimeSpan>(section); Check<DateTime>(section);
            Check<Uri>(section); Check<IDisposable>(section);
        }
    }

    [Test]
    public void RegisteredPrimitiveConverterIsRespected()
    {
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Value"] = "42" }).Build();
        _ = configuration.GetValueStrict<int>("Value");
        TypeDescriptionProvider provider = TypeDescriptor.AddAttributes(typeof(int), new TypeConverterAttribute(typeof(OverrideIntConverter)));
        try
        {
            if (configuration.GetValueStrict<int>("Value") != 123 || configuration.GetValueStrict<int?>("Value") != 123)
                throw new InvalidOperationException("The registered converter was bypassed.");
        }
        finally
        {
            TypeDescriptor.RemoveProvider(provider, typeof(int));
        }
    }

    private static void Check<T>(IConfiguration configuration)
    {
        T? expected = default;
        Exception? expectedError = null;
        try
        {
            expected = configuration.GetValue<T>("Value");
            if (expected is null)
                throw new NullReferenceException();
        }
        catch (Exception exception) { expectedError = exception; }

        T? actual = default;
        Exception? actualError = null;
        try { actual = configuration.GetValueStrict<T>("Value"); }
        catch (Exception exception) { actualError = exception; }

        if (expectedError?.GetType() != actualError?.GetType() || expectedError?.InnerException?.GetType() != actualError?.InnerException?.GetType())
            throw new InvalidOperationException($"Exception mismatch for {typeof(T)}: '{configuration["Value"]}'.", actualError);
        if (expectedError is InvalidOperationException && expectedError.Message != actualError!.Message)
            throw new InvalidOperationException($"Conversion error message mismatch: {actualError.Message}");
        if (expectedError is not null)
            return;
        if (expected is byte[] expectedBytes && actual is byte[] actualBytes)
        {
            if (!expectedBytes.AsSpan().SequenceEqual(actualBytes))
                throw new InvalidOperationException("Base64 conversion mismatch.");
        }
        else if (!EqualityComparer<T>.Default.Equals(expected!, actual!))
            throw new InvalidOperationException($"Value mismatch for {typeof(T)}: '{configuration["Value"]}'.");
    }
}
