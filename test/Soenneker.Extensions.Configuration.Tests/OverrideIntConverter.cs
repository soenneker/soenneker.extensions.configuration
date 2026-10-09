using System;
using System.ComponentModel;
using System.Globalization;

namespace Soenneker.Extensions.Configuration.Tests;

public sealed class OverrideIntConverter : Int32Converter
{
    public override object ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value) => 123;
}
