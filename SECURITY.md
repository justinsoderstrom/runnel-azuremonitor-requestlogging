# Security Policy

## Supported versions

Only the latest release on [NuGet.org](https://www.nuget.org/packages/Runnel.AzureMonitor.RequestLogging) receives security fixes.

## Reporting a vulnerability

Please do **not** report security vulnerabilities through public GitHub issues.

Instead, report them privately via [GitHub Security Advisories](https://github.com/justinsoderstrom/runnel-azuremonitor-requestlogging/security/advisories/new) ("Report a vulnerability").

Please include as much of the following as you can:

- The type of issue and its impact (e.g. a redaction bypass that leaks values `SensitiveDataFilter` should have masked)
- Steps to reproduce, ideally with a minimal request payload and configuration
- The package version affected

You should get an initial response within a few days. Please allow a fix to be released before disclosing publicly.

## Scope notes

This package intentionally writes HTTP request/response bodies to Application Insights — that is its purpose, not a vulnerability. Redaction of sensitive values is documented as **best-effort**, and consumers are responsible for configuring `PropertyNamesWithSensitiveData`, `SensitiveDataRegexes`, and `ExcludedContentTypes` for their payloads. Reports about the *default* redaction failing in ways a reasonable consumer wouldn't expect (e.g. a crafted JSON body that bypasses masking of a listed property name) are in scope and very welcome.
