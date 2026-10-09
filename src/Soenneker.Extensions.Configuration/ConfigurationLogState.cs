using System;
using System.Buffers;
using Microsoft.Extensions.Logging;

namespace Soenneker.Extensions.Configuration;

// Keep search tables and logging delegates off the string getter initialization path.
internal static class ConfigurationLogState
{
    internal static readonly SearchValues<char> KeySeparators = SearchValues.Create(":_-.");

    internal static readonly SearchValues<string> SensitiveKeyFragments = SearchValues.Create(
    [
        "password", "passwd", "secret", "token", "api-key", "apikey", "access-key", "accesskey", "account-key", "accountkey", "private-key",
        "privatekey", "signing-key", "signingkey", "encryption-key", "encryptionkey", "connection-string", "connectionstring", "credential",
        "authorization", "shared-access", "sharedaccess", "sas-token", "sastoken", "sas-key", "saskey", "AzureWebJobsStorage"
    ], StringComparison.OrdinalIgnoreCase);

    internal static readonly SearchValues<string> SensitiveValueFragments = SearchValues.Create(
    [
        "password=", "passwd=", "clientsecret=", "accountkey=", "sharedaccesssignature=", "apikey=", "api-key=", "-----BEGIN PRIVATE KEY-----"
    ], StringComparison.OrdinalIgnoreCase);

    internal static readonly Action<ILogger, Exception?> Start = LoggerMessage.Define(LogLevel.Debug, default,
        "----- Start of effective IConfiguration -----", new LogDefineOptions { SkipEnabledCheck = true });

    internal static readonly Action<ILogger, string, string, Exception?> Entry = LoggerMessage.Define<string, string>(LogLevel.Debug, default,
        "{key}={value}", new LogDefineOptions { SkipEnabledCheck = true });

    internal static readonly Action<ILogger, Exception?> End = LoggerMessage.Define(LogLevel.Debug, default,
        "----- End of effective IConfiguration -----", new LogDefineOptions { SkipEnabledCheck = true });
}
