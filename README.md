[![](https://img.shields.io/nuget/v/Soenneker.Extensions.Configuration.svg?style=for-the-badge)](https://www.nuget.org/packages/Soenneker.Extensions.Configuration/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.extensions.configuration/publish-package.yml?style=for-the-badge)](https://github.com/soenneker/soenneker.extensions.configuration/actions/workflows/publish-package.yml)
[![](https://img.shields.io/nuget/dt/Soenneker.Extensions.Configuration.svg?style=for-the-badge)](https://www.nuget.org/packages/Soenneker.Extensions.Configuration/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.extensions.configuration/codeql.yml?label=CodeQL&style=for-the-badge)](https://github.com/soenneker/soenneker.extensions.configuration/actions/workflows/codeql.yml)

# ![](https://user-images.githubusercontent.com/4441470/224455560-91ed3ee7-f510-4041-a8d2-3fc093025112.png) Soenneker.Extensions.Configuration

A collection of helpful IConfiguration extension methods.

## Installation

```bash
dotnet add package Soenneker.Extensions.Configuration
```

## Quick start

```csharp
using Soenneker.Extensions.Configuration;

// Given an existing IConfiguration named configuration:
var result = configuration.GetValueStrict(key);
```

## Common operations

- `GetValueStrict()` - Retrieves a strongly-typed configuration value for the specified key, and throws if the key is missing or the value is null.
- `GetStringStrict()` - Retrieves a required string configuration value for the specified key, throwing if missing or null.
- `GetString()` - Retrieves an optional string configuration value for the specified key.
- `LogAll()` - Logs all effective key-value pairs from the current `IConfiguration` instance.
