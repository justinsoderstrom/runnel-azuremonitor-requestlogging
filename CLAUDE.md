# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

A NuGet package (`Runnel.AzureMonitor.RequestLogging`) that logs HTTP request/response bodies from ASP.NET Core apps to Application Insights via the Azure Monitor OpenTelemetry distro. It is an independent project inspired by Matthias Guentert's `Azureblue.ApplicationInsights.RequestLogging` (which serves the classic App Insights SDK); attribution lives in NOTICE.txt (packed into the nupkg). Targets net10.0 only.

In docs and comments, refer to that package as "the original package" — never "legacy", "successor", or anything implying the original is obsolete, that this project is its official continuation, or that its author endorses this one. The original is actively maintained; the README's migration section deliberately points users staying on the classic SDK back to it.

## Commands

```shell
dotnet build                                          # build the solution (.slnx)
dotnet test                                           # run all tests
dotnet test --filter "FullyQualifiedName~BodyReaderTests"          # one test class
dotnet test --filter "FullyQualifiedName~BodyReaderTests.MethodName"  # one test
dotnet pack src/Runnel.AzureMonitor.RequestLogging -c Release -o artifacts
dotnet run --project samples/Runnel.AzureMonitor.RequestLogging.Sample
```

Tests use xUnit v3 with Shouldly assertions (`value.ShouldBe(...)`), split into `Unit/` and `Integration/`. Integration tests spin up a real pipeline with `Microsoft.AspNetCore.TestHost`.

## Architecture

The core flow: `BodyLoggerMiddleware` buffers the request/response streams, redacts sensitive data, and writes the captured bodies as **tags on the incoming request `Activity`** (the ASP.NET Core request span). The Azure Monitor OpenTelemetry exporter surfaces unrecognized activity tags as `customDimensions` on the `requests` record in Application Insights. The *code* has no dependency on any telemetry client — if no listener is attached, `Activity.Current` is null and the middleware no-ops. The package deliberately has **no `Azure.Monitor.OpenTelemetry.AspNetCore` reference** (user-approved 2026-07-04): it works with any OpenTelemetry exporter that records the ASP.NET Core request span, but Azure Monitor remains the designed-for, documented, and tested target. Consumers install the distro (or another exporter) themselves per the README; the sample references the distro directly.

Collaborators, each behind an interface registered with `TryAdd*` in `BodyLoggingServiceCollectionExtensions` (so consumer registrations take precedence):

- `IBodyReader` / `BodyReader` — stream buffering and truncation. **Scoped and stateful**: it swaps `HttpResponse.Body` for a `MemoryStream` and holds the original stream in fields, restored in the middleware's `finally` and **re-preparable afterwards** — `UseExceptionHandler("/path")`-style pipeline re-execution re-enters the same scoped instance, so restore must reset the fields, never latch (a per-instance "restored" flag once swallowed re-executed responses). Don't make it a singleton. Implements `IDisposable` only as a scope-end backstop for the buffer.
- `ISensitiveDataFilter` / `SensitiveDataFilter` — walks JSON bodies masking values by property name or regex: a sensitive property name masks the whole value (containers included), regexes check every scalar including array elements; non-JSON bodies matching a regex are masked wholesale; JSON-looking bodies that fail to parse (duplicate keys throw `ArgumentException`, not `JsonException`; truncation) are masked wholesale when they contain a sensitive property name. Singleton, registered via explicit factory because it has two constructors.
- `IActivityTagWriter` / `ActivityTagWriter` — writes tags, suffixing duplicate keys (`-dupe-xxxxxxxx`) instead of overwriting, mirroring the original package's behavior.

Redaction and tag-writing in the middleware are wrapped in catch-alls that log a warning (source-generated `LoggerMessage`) — a telemetry bug must never fail the request it decorates.

Public entry points are the two extension methods: `services.AddHttpBodyLogging([options])` and `app.UseHttpBodyLogging()`.

Integration tests capture the request activity with `ActivityCapture` (an `ActivityListener` on the `Microsoft.AspNetCore` source) — without a listener the host creates no recorded activity, so any new integration test needs it.

## Hard constraints

- **`BodyLoggerOptions` is a migration contract.** Property names and defaults are intentionally identical to the original `Azureblue.ApplicationInsights.RequestLogging` package so migration is trivial. Do not rename options or change defaults.
- **Versioning is tag-driven via MinVer** (`v` prefix). Never write a version number into a csproj. Releasing = pushing a `vX.Y.Z` tag, which triggers `.github/workflows/release.yml` to pack and push to NuGet.org.
- The README documents deliberate behavioral differences from the original package (response stream restored in `finally`, truncation by characters read rather than `Content-Length`, IP tag scoped to this middleware). Keep those behaviors and keep README claims in sync with code.
- The README states the tested `Azure.Monitor.OpenTelemetry.AspNetCore` baseline (currently 1.5.0+), which is the version pinned in the sample csproj — when bumping the sample's distro reference, update the README line to match.
