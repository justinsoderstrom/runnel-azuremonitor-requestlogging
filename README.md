![Runnel icon](https://raw.githubusercontent.com/justinsoderstrom/runnel-azuremonitor-requestlogging/master/runnel-icon.png)

# Runnel.AzureMonitor.RequestLogging

[![CI](https://github.com/justinsoderstrom/runnel-azuremonitor-requestlogging/actions/workflows/ci.yml/badge.svg)](https://github.com/justinsoderstrom/runnel-azuremonitor-requestlogging/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/Runnel.AzureMonitor.RequestLogging.svg)](https://www.nuget.org/packages/Runnel.AzureMonitor.RequestLogging)
[![Downloads](https://img.shields.io/nuget/dt/Runnel.AzureMonitor.RequestLogging.svg)](https://www.nuget.org/packages/Runnel.AzureMonitor.RequestLogging)
[![License](https://img.shields.io/badge/license-Apache--2.0-blue.svg)](LICENSE.txt)

Logs HTTP request and response bodies from ASP.NET Core applications to **Application Insights** as custom dimensions on request telemetry — built for the modern [Azure Monitor OpenTelemetry distro](https://learn.microsoft.com/en-us/azure/azure-monitor/app/opentelemetry-enable?tabs=aspnetcore) (`Azure.Monitor.OpenTelemetry.AspNetCore`).

This is the OpenTelemetry-era successor to [Azureblue.ApplicationInsights.RequestLogging](https://github.com/matthiasguentert/azure-appinsights-logger), which is built on the legacy Application Insights SDK. The options model is intentionally identical, so [migrating](#migrating-from-azureblueapplicationinsightsrequestlogging) takes minutes.

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
```

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

The middleware buffers the request and response streams and writes the captured (redacted, truncated) bodies as **tags on the incoming request `Activity`** — the span that ASP.NET Core creates for each request. The Azure Monitor OpenTelemetry exporter emits unrecognized activity tags as `customDimensions` on the corresponding `requests` record in Application Insights, which is exactly where the legacy Application Insights SDK put them.

Things to know:

- **`UseAzureMonitor()` (or another OpenTelemetry exporter) must be configured.** Without a listener the tags have nowhere to go; the middleware then no-ops safely.
- **Sampling applies.** If a request is sampled out, its span — including the body tags — is dropped. That matches how the legacy SDK's sampling behaved.
- **Pipeline order matters.** Register `UseHttpBodyLogging()` early — before `MapControllers`/endpoints and before `UseResponseCompression()`, otherwise you'll log compressed bytes.

Query the results in Log Analytics:

```kusto
requests
| where isnotempty(customDimensions.RequestBody)
| project timestamp, name, resultCode,
          requestBody = customDimensions.RequestBody,
          responseBody = customDimensions.ResponseBody
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
| `PropertyNamesWithSensitiveData` | `password`, `secret`, `passwd`, `api_key`, `access_token`, `accessToken`, `auth`, `credentials`, `mysql_pwd` | JSON property names (case-insensitive substring match) whose values are replaced with `***MASKED***`. |
| `SensitiveDataRegexes` | credit card pattern | Regexes applied to values; matches are replaced with `***MASKED***`. Non-JSON bodies that match anywhere are masked wholesale. |

Need custom redaction or tag-writing behavior? `ISensitiveDataFilter`, `IBodyReader`, and `IActivityTagWriter` are registered with `TryAdd*`, so your own implementations take precedence when registered first.

## Migrating from Azureblue.ApplicationInsights.RequestLogging

| Legacy | This package |
| --- | --- |
| `services.AddAppInsightsHttpBodyLogging()` | `services.AddHttpBodyLogging()` |
| `app.UseAppInsightsHttpBodyLogging()` | `app.UseHttpBodyLogging()` |
| `services.AddApplicationInsightsTelemetry()` | `services.AddOpenTelemetry().UseAzureMonitor()` |
| `BodyLoggerOptions` | `BodyLoggerOptions` — same property names and defaults |
| `ITelemetryWriter` (writes to `RequestTelemetry.Properties`) | `IActivityTagWriter` (writes tags on the request `Activity`) |
| `ClientIpInitializer` (`ITelemetryInitializer`) | Built into the middleware via `DisableIpMasking` |

Behavioral differences (deliberate fixes):

- The original response stream is restored in a `finally` block, so the client receives the response even on exception paths.
- Truncation is decided by the number of characters actually read, not by the `Content-Length` header (which is absent for chunked requests).
- With `DisableIpMasking`, the IP lands only on request telemetry passing through this middleware, not on every telemetry item.

## ⚠️ A word of caution

Writing HTTP bodies to Application Insights can reveal sensitive user information that would otherwise stay protected in transit via TLS. The built-in redaction is best-effort — you are responsible for compliance (GDPR, PCI, …) with whatever your application logs. Review `PropertyNamesWithSensitiveData`, `SensitiveDataRegexes`, and `ExcludedContentTypes` for your payloads before enabling this in production.

## Sample

A runnable sample lives in [samples/Runnel.AzureMonitor.RequestLogging.Sample](samples/Runnel.AzureMonitor.RequestLogging.Sample) with an [.http file](samples/Runnel.AzureMonitor.RequestLogging.Sample/Runnel.AzureMonitor.RequestLogging.Sample.http) covering the interesting cases. Set the `APPLICATIONINSIGHTS_CONNECTION_STRING` environment variable to see the telemetry arrive in a real Application Insights resource.

## Release process

Versions come from git tags via [MinVer](https://github.com/adamralph/minver). Tagging `vX.Y.Z` and pushing the tag triggers the release workflow, which packs and publishes to NuGet.org.

## License

[Apache-2.0](LICENSE.txt)
