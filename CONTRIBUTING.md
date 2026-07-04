# Contributing to Runnel.AzureMonitor.RequestLogging

Thanks for your interest in contributing! Bug reports, feature ideas, and pull requests are all welcome. This document explains how to get set up and what to know before opening a PR.

## Reporting issues

- **Bugs and feature requests** — open an issue using the matching [issue template](https://github.com/justinsoderstrom/runnel-azuremonitor-requestlogging/issues/new/choose).
- **Security vulnerabilities** — please do **not** open a public issue. See [SECURITY.md](SECURITY.md).

For anything non-trivial, opening an issue to discuss the change before writing code is appreciated — it avoids wasted effort on PRs that can't be merged (see [Hard constraints](#hard-constraints) below).

## Getting started

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0). Then:

```shell
git clone https://github.com/justinsoderstrom/runnel-azuremonitor-requestlogging.git
cd runnel-azuremonitor-requestlogging
dotnet build      # build the solution (.slnx)
dotnet test       # run all tests
```

Other useful commands:

```shell
dotnet test --filter "FullyQualifiedName~BodyReaderTests"   # run one test class
dotnet run --project samples/Runnel.AzureMonitor.RequestLogging.Sample
```

The sample app is the quickest way to exercise the middleware end to end. Set the `APPLICATIONINSIGHTS_CONNECTION_STRING` environment variable to send telemetry to a real Application Insights resource; without it the sample still runs, but no exporter is registered, so the middleware no-ops (that's expected — for observable behavior without Azure, look at the integration tests, which attach an `ActivityListener`).

## Project layout

```
src/       the package itself
test/      xUnit v3 tests, split into Unit/ and Integration/
samples/   a runnable ASP.NET Core sample with an .http file
```

## Writing tests

- Tests use **xUnit v3** with **Shouldly** assertions (`value.ShouldBe(...)`).
- **Unit tests** cover a single collaborator in isolation.
- **Integration tests** spin up a real pipeline with `Microsoft.AspNetCore.TestHost`. They must capture the request activity with `ActivityCapture` (an `ActivityListener` on the `Microsoft.AspNetCore` source) — without a listener the host creates no recorded activity and the middleware no-ops, so assertions on tags will silently pass against nothing.

New behavior needs tests; bug fixes ideally include a test that fails without the fix.

## Hard constraints

These are deliberate design decisions. PRs that violate them will be declined regardless of code quality, so please read this section before starting work.

1. **`BodyLoggerOptions` is a migration contract.** Property names and defaults are intentionally identical to the legacy [`Azureblue.ApplicationInsights.RequestLogging`](https://github.com/matthiasguentert/azure-appinsights-logger) package so migration is trivial. Do not rename options or change defaults — even ones that look wrong or low.
2. **Never write a version number into a csproj.** Versioning is tag-driven via [MinVer](https://github.com/adamralph/minver); releases happen by pushing a `vX.Y.Z` tag.
3. **Keep the deliberate behavioral fixes.** The README documents intentional differences from the legacy package (response stream restored in `finally`, truncation by characters actually read rather than `Content-Length`, IP tag scoped to this middleware). Don't "fix" these back, and keep README claims in sync with the code.
4. **No new telemetry-client dependencies.** The package works purely by writing tags on the request `Activity`; the only integration point with Azure Monitor is the exporter the consumer configures. (The existing `Azure.Monitor.OpenTelemetry.AspNetCore` reference is a deliberate exception — it exists so consumers get the distro by default, not because the code uses it.)

## Pull requests

- Branch from `master` and keep PRs focused — one logical change per PR.
- Follow the existing code style; it's enforced by [.editorconfig](.editorconfig).
- Make sure `dotnet build` and `dotnet test` pass locally. CI runs both on every PR and must be green.
- Update the README if your change affects documented behavior or options.
- Fill in the pull request template, including linking the related issue if there is one.

## Releases (maintainers)

Versions come from git tags via MinVer. Pushing a `vX.Y.Z` tag triggers [release.yml](.github/workflows/release.yml), which packs and publishes to NuGet.org. Contributors never need to touch versioning.

## License

By contributing, you agree that your contributions will be licensed under the [Apache-2.0 License](LICENSE.txt) that covers the project.
