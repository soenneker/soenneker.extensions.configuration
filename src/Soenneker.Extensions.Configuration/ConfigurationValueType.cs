using System;

namespace Soenneker.Extensions.Configuration;

internal static class ConfigurationValueType<T>
{
    // Nullable.GetUnderlyingType allocates its generic argument array; resolve it once per T.
    internal static readonly Type EffectiveType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
}
