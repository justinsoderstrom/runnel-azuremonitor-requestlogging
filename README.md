![Runnel icon](https://raw.githubusercontent.com/justinsoderstrom/runnel-azuremonitor-requestlogging/master/runnel-icon.png)

# Runnel.AzureMonitor.RequestLogging

[![CI](https://github.com/justinsoderstrom/runnel-azuremonitor-requestlogging/actions/workflows/ci.yml/badge.svg)](https://github.com/justinsoderstrom/runnel-azuremonitor-requestlogging/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/Runnel.AzureMonitor.RequestLogging.svg)](https://www.nuget.org/packages/Runnel.AzureMonitor.RequestLogging)
[![Downloads](https://img.shields.io/nuget/dt/Runnel.AzureMonitor.RequestLogging.svg)](https://www.nuget.org/packages/Runnel.AzureMonitor.RequestLogging)
[![License](https://img.shields.io/badge/license-Apache--2.0-blue.svg)](LICENSE.txt)

Logs HTTP request and response bodies from ASP.NET Core applications to **Application Insights** as custom dimensions on request telemetry — built for the modern [Azure Monitor OpenTelemetry distro](https://learn.microsoft.com/en-us/azure/azure-monitor/app/opentelemetry-enable?tabs=aspnetcore) (`Azure.Monitor.OpenTelemetry.AspNetCore`).

It fills the same niche for the OpenTelemetry distro that Matthias Guentert's [Azureblue.ApplicationInsights.RequestLogging](https://github.com/matthiasguentert/azure-appinsights-logger) fills for the classic Application Insights SDK. This is an independent project inspired by his work (see [Acknowledgements](#acknowledgements)) — the options model is intentionally identical, so if you're moving to OpenTelemetry, [migrating](#migrating-from-azureblueapplicationinsightsrequestlogging) takes minutes.

## Features

- 📄 Log request & response bodies as `customDimensions` on request telemetry
- 🎯 Log selectively by HTTP verb, response status code, and content type
- ✂️ Truncate captured bodies at a configurable length
- 🔒 Redact sensitive values (passwords, tokens, credit card numbers, …) by property name or regex
- 🌐 Preserve the client IP address without modifying your Application Insights resource
- 💥 Optionally capture request bodies even when downstream code throws

## Install

```shell
dotnet add package Runnel.AzureMonitor.RequestLogging
dotnet add package Azure.Monitor.OpenTelemetry.AspNetCore
```

Requires **.NET 10** (ASP.NET Core 10). The package targets `net10.0` only.

This package has **no dependency on the Azure Monitor distro** (or any other exporter) — it only writes tags on the request `Activity`. Add the distro alongside it to export those tags to Application Insights, or skip it if you already have one, or bring a different OpenTelemetry exporter entirely.

Tested against `Azure.Monitor.OpenTelemetry.AspNetCore` **1.5.0 and later**. Earlier versions will most likely work — the middleware only relies on the exporter surfacing request-span tags as custom dimensions, which the distro has done since its early releases — but they aren't verified here.

## Quickstart

```csharp
using Azure.Monitor.OpenTelemetry.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenTelemetry().UseAzureMonitor();
builder.Services.AddHttpBodyLogging();

var app = builder.Build();

app.UseHttpBodyLogging();   // early in the pipeline, before endpoints

app.MapPost("/orders", () => Results.BadRequest("rejected"));

app.Run();
```

Or with options:

```csharp
builder.Services.AddHttpBodyLogging(o =>
{
    o.HttpCodes.Add(StatusCodes.Status200OK);
    o.HttpVerbs.Add(HttpMethods.Get);
    o.MaxBytes = 10_000;
    o.DisableIpMasking = true;
});
```

With the defaults, the bodies of `POST`/`PUT`/`PATCH` requests that end in a 4xx or 5xx response are captured.

## How it works

The middleware buffers the request and response streams and writes the captured (redacted, truncated) bodies as **tags on the incoming request `Activity`** — the span that ASP.NET Core creates for each request. The Azure Monitor OpenTelemetry exporter emits unrecognized activity tags as `customDimensions` on the corresponding `requests` record in Application Insights, which is exactly where the classic Application Insights SDK put them.

Things to know:

- **`UseAzureMonitor()` (or another OpenTelemetry exporter) must be configured.** Without a listener the tags have nowhere to go; the middleware then no-ops safely.
- **Any OpenTelemetry exporter works.** The middleware itself has no Azure dependency — under a different exporter (OTLP to Jaeger, Honeycomb, …) the captured bodies appear as ordinary attributes on the server span. Azure Monitor is where this package is designed, documented, and tested.
- **Sampling applies.** If a request is sampled out, its span — including the body tags — is dropped. That matches how the classic SDK's sampling behaved.
- **Pipeline order matters.** Register `UseHttpBodyLogging()` early — before `MapControllers`/endpoints and before `UseResponseCompression()`, otherwise you'll log compressed bytes.
- **Exception handlers work in either order.** Registering `UseHttpBodyLogging()` before `app.UseExceptionHandler(...)` captures the error response the handler produces in a single pass, which is the recommended layout. Registering it after also works — the pipeline re-execution that `UseExceptionHandler("/path")` and `UseStatusCodePagesWithReExecute()` perform is supported — but with `EnableBodyLoggingOnExceptions` the request body is then captured on both passes (the duplicate gets a `-dupe-`-suffixed key).
- **Responses are buffered in memory.** For every request that matches `HttpVerbs` (the status code isn't known until afterwards), the full response is buffered and only sent to the client once the pipeline completes. Avoid routing streaming endpoints (SSE, large downloads) through this middleware — note that `ExcludedContentTypes` matches the *request* content type, so exclude such endpoints by verb or path instead.
- **Bodies are decoded as UTF-8.** The request's `charset` is not honored; a non-UTF-8 body logs garbled (the application itself is unaffected).
- **Valid JSON bodies are re-serialized** after redaction: formatting is compacted and non-ASCII characters are escaped (`é` becomes `\u00e9`), so the logged text can differ cosmetically from the bytes on the wire.
- **A telemetry failure never fails the request.** If capturing, redacting, or tag-writing throws, the middleware logs a warning through `ILogger` and the response is delivered normally.

Query the results in Log Analytics:

```kusto
requests
| where isnotempty(customDimensions.RequestBody)
| project timestamp, name, resultCode,
          requestBody = customDimensions.RequestBody,
          responseBody = customDimensions.ResponseBody,
          clientIp = customDimensions.ClientIp
```

## Options

| Option | Default | Description |
| --- | --- | --- |
| `HttpCodes` | all 4xx + 5xx | Response status codes that trigger logging. `StatusCodeRanges` provides ready-made lists (`Status2xx`, `Status4xx`, …). |
| `HttpVerbs` | `POST`, `PUT`, `PATCH` | Request methods that trigger logging. |
| `ExcludedContentTypes` | `[]` | Request content types to skip (prefix match, case-insensitive). |
| `MaxBytes` | `1000` | Maximum characters captured per body; `0` or less captures everything. |
| `Appendix` | `***TRUNCATED***` | Text appended when a body was truncated. |
| `RequestBodyPropertyKey` | `RequestBody` | Custom dimension key for the request body. |
| `ResponseBodyPropertyKey` | `ResponseBody` | Custom dimension key for the response body. |
| `ClientIpPropertyKey` | `ClientIp` | Custom dimension key for the client IP. |
| `DisableIpMasking` | `false` | Write the client IP as a custom dimension on every request. Application Insights [masks the built-in `client_IP` field](https://learn.microsoft.com/en-us/azure/azure-monitor/app/ip-collection) at ingestion; this is the escape hatch. |
| `EnableBodyLoggingOnExceptions` | `false` | Catch, log the request body, and rethrow when downstream code throws. |
| `PropertyNamesWithSensitiveData` | `password`, `secret`, `passwd`, `api_key`, `access_token`, `accessToken`, `auth`, `credentials`, `mysql_pwd` | JSON property names (case-insensitive substring match) whose values — including nested objects and arrays — are replaced with `***MASKED***`. |
| `SensitiveDataRegexes` | credit card pattern | Regexes applied to scalar values, including array elements; matches are replaced with `***MASKED***`. Non-JSON bodies that match anywhere are masked wholesale. |

> [!NOTE]
> When binding options from configuration (e.g. `appsettings.json`), .NET *appends* to list-typed defaults rather than replacing them — a bound `HttpCodes` or `PropertyNamesWithSensitiveData` array adds to the lists above. To replace them, configure in code and `Clear()` first.

Need custom redaction or tag-writing behavior? `ISensitiveDataFilter`, `IBodyReader`, and `IActivityTagWriter` are registered with `TryAdd*`, so your own implementations take precedence when registered first.

## Migrating from Azureblue.ApplicationInsights.RequestLogging

If you're staying on the classic Application Insights SDK (`Microsoft.ApplicationInsights.AspNetCore`), keep using [Azureblue.ApplicationInsights.RequestLogging](https://github.com/matthiasguentert/azure-appinsights-logger) — it remains the right tool for that stack. If you're moving to the Azure Monitor OpenTelemetry distro, the mapping is:

| Azureblue.ApplicationInsights.RequestLogging | This package |
| --- | --- |
| `services.AddAppInsightsHttpBodyLogging()` | `services.AddHttpBodyLogging()` |
| `app.UseAppInsightsHttpBodyLogging()` | `app.UseHttpBodyLogging()` |
| `services.AddApplicationInsightsTelemetry()` | `services.AddOpenTelemetry().UseAzureMonitor()` |
| `BodyLoggerOptions` | `BodyLoggerOptions` — same property names and defaults |
| `ITelemetryWriter` (writes to `RequestTelemetry.Properties`) | `IActivityTagWriter` (writes tags on the request `Activity`) |
| `ClientIpInitializer` (`ITelemetryInitializer`) | Built into the middleware via `DisableIpMasking` |

A few behaviors deliberately differ:

- The original response stream is restored in a `finally` block, so the client receives the response even on exception paths.
- Truncation is decided by the number of characters actually read, not by the `Content-Length` header (which is absent for chunked requests).
- With `DisableIpMasking`, the IP lands only on request telemetry passing through this middleware, not on every telemetry item.
- Redaction is stricter: a sensitive property name masks its entire value including nested objects and arrays (rather than recursing into containers and masking only inner properties that match on their own), scalar values inside arrays are also checked against `SensitiveDataRegexes`, regexes run compiled with a one-second timeout that masks the value when the timeout expires, and bodies that look like JSON but cannot be parsed (duplicate property names, truncation) are masked wholesale when a sensitive property name appears anywhere in the text.

## ⚠️ A word of caution

Writing HTTP bodies to Application Insights can reveal sensitive user information that would otherwise stay protected in transit via TLS. The built-in redaction is best-effort — you are responsible for compliance (GDPR, PCI, …) with whatever your application logs. Review `PropertyNamesWithSensitiveData`, `SensitiveDataRegexes`, and `ExcludedContentTypes` for your payloads before enabling this in production.

One known limitation to keep in mind: truncation happens **before** redaction. A body longer than `MaxBytes` is usually no longer valid JSON after truncation, so property-level masking cannot apply to it. Bodies that look like JSON but cannot be parsed (truncation, duplicate property names, …) are therefore masked **wholesale** when a sensitive property name appears anywhere in the text or a regex matches — deliberately conservative: over-masking beats leaking. If truncated bodies you care about keep arriving as `***MASKED***`, raise `MaxBytes` so they survive truncation intact.

## Sample

A runnable sample lives in [samples/Runnel.AzureMonitor.RequestLogging.Sample](samples/Runnel.AzureMonitor.RequestLogging.Sample) with an [.http file](samples/Runnel.AzureMonitor.RequestLogging.Sample/Runnel.AzureMonitor.RequestLogging.Sample.http) covering the interesting cases. Set `AzureMonitor:ConnectionString` — in the sample's `appsettings.json`, or as the `AzureMonitor__ConnectionString` environment variable — to see the telemetry arrive in a real Application Insights resource.

## Release process

Versions come from git tags via [MinVer](https://github.com/adamralph/minver). Tagging `vX.Y.Z` and pushing the tag triggers the release workflow, which packs and publishes to NuGet.org.

## Contributing

Contributions are welcome! See [CONTRIBUTING.md](CONTRIBUTING.md) for how to get set up and what to know before opening a PR — in particular the hard constraints around the frozen `BodyLoggerOptions` migration contract and tag-driven versioning. Security issues should be reported privately per [SECURITY.md](SECURITY.md).

## Acknowledgements

This project was inspired by [Azureblue.ApplicationInsights.RequestLogging](https://github.com/matthiasguentert/azure-appinsights-logger) by [Matthias Guentert](https://github.com/matthiasguentert), which pioneered this approach for the classic Application Insights SDK — thank you! The options model here intentionally mirrors that package's so migration is easy; see [NOTICE.txt](NOTICE.txt) for attribution. Runnel.AzureMonitor.RequestLogging is an independent project and is not affiliated with, or endorsed by, the original author.

## License

[Apache-2.0](LICENSE.txt), with attribution notices in [NOTICE.txt](NOTICE.txt).
