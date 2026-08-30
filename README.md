[![](https://img.shields.io/nuget/v/Soenneker.Extensions.Configuration.svg?style=for-the-badge)](https://www.nuget.org/packages/Soenneker.Extensions.Configuration/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.extensions.configuration/publish-package.yml?style=for-the-badge)](https://github.com/soenneker/soenneker.extensions.configuration/actions/workflows/publish-package.yml)
[![](https://img.shields.io/nuget/dt/Soenneker.Extensions.Configuration.svg?style=for-the-badge)](https://www.nuget.org/packages/Soenneker.Extensions.Configuration/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.extensions.configuration/codeql.yml?label=CodeQL&style=for-the-badge)](https://github.com/soenneker/soenneker.extensions.configuration/actions/workflows/codeql.yml)

# ![](https://user-images.githubusercontent.com/4441470/224455560-91ed3ee7-f510-4041-a8d2-3fc093025112.png) Soenneker.Extensions.Configuration

Strict scalar configuration lookup, optional string lookup, and guarded startup diagnostics for `IConfiguration`.

## Installation

```bash
dotnet add package Soenneker.Extensions.Configuration
```

## Required scalar values

```csharp
using Soenneker.Extensions.Configuration;

string connectionName = configuration.GetStringStrict("Database:Name");
int port = configuration.GetValueStrict<int>("Server:Port");
bool enabled = configuration.GetValueStrict<bool>("Feature:Enabled");
```

`GetValueStrict<T>()` requires an actual scalar value at the key. A parent section that only contains children is not considered a value. Missing/null values throw `NullReferenceException`; invalid or unsupported conversions surface the configuration binder's conversion exception. An empty string is present, although it may fail conversion for a non-string target.

`GetStringStrict()` is the required-string convenience wrapper. `GetString()` returns `null` for a missing value:

```csharp
string? optionalRegion = configuration.GetString("Service:Region");
```

These methods retrieve scalar values; they do not bind object graphs, validate ranges or formats, reload options, or aggregate startup errors. Use options binding and options validation for structured configuration.

## Startup diagnostics

`LogAll()` does nothing unless both conditions are true:

- `Log:StartupConfiguration` is `true` or `1`
- the logger has `Debug` enabled

```csharp
configuration.LogAll(logger);
```

Entries are sorted by ordinal key. Common password, secret, token, API/access/account/private/signing/encryption key, connection-string, credential, authorization, shared-access, and Azure Functions storage keys are logged as `[REDACTED]`. Secret-looking connection-string values and private keys are also redacted. Non-redacted line breaks are escaped and values longer than 512 characters are truncated.

Add application-specific redaction with the overload:

```csharp
configuration.LogAll(
    logger,
    key => key.StartsWith("TenantSecrets:", StringComparison.OrdinalIgnoreCase));
```

The custom predicate adds redaction; it cannot disable built-in redaction.

Redaction is heuristic and cannot recognize every secret name or proprietary value format. Do not enable full configuration logging in production or treat this as a secret-scanning boundary. Prefer logging an explicit allowlist of diagnostic settings when possible, and remember that configuration keys themselves are still visible.
