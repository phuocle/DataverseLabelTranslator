---
name: "pl-unit-tests-server"
display-name: "PL Unit Tests Server"
description: "Regenerate Dataverse proxy types and run Dataverse Label Translator server-side MSTest/FakeXrmEasy unit tests."
argument-hint: "[generate-proxy|no-proxy|<test-filter>]"
---

# PL Unit Tests Server

> Deprecated: this repository currently has no active C# unit-test suite. Use this workflow only when the user explicitly asks to reintroduce server tests.

Use this workflow when the user explicitly asks to reintroduce, run, verify, debug, or explain server-side MSTest/FakeXrmEasy tests or coverage for Dataverse Label Translator.

## What This Skill Does

1. Regenerates early-bound proxy classes before server tests so FakeXrmEasy can resolve Dataverse entity types.
2. Runs the server-side MSTest suite from `DataverseLabelTranslator.Server.Test` with code coverage collection.
3. Reports coverage summary after tests pass, including per-assembly coverage percentages.
4. Uses shared test utilities from `DataverseLabelTranslator.Shared.Test`.
5. Tests server code from `DataverseLabelTranslator.Server` without deploying to Dataverse.
6. Reports exact failing test names, projects, and relevant assertion/error output.

## Project Map

```text
DataverseLabelTranslator.Server/       Server-side plugins, workflows, custom actions, and custom APIs under test
DataverseLabelTranslator.Server.Test/         MSTest project that runs server unit tests
DataverseLabelTranslator.Shared.Test/  Shared FakeXrmEasy helpers imported by the test project
DataverseLabelTranslator.ProxyTypes/   Early-bound classes required by FakeXrmEasy
```

`DataverseLabelTranslator.Shared.Test` is a shared project imported by `DataverseLabelTranslator.Server.Test`; do not try to run it as a separate test project.

## Commands

Regenerate early-bound proxy types first:

```powershell
DataverseLabelTranslator.ProxyTypes\run.bat
```

Run all server unit tests with coverage:

```powershell
dotnet test DataverseLabelTranslator.Server.Test\DataverseLabelTranslator.Server.Test.csproj --configuration Debug --collect:"Code Coverage" --results-directory DataverseLabelTranslator.Server.Test\TestResults
```

Run a filtered server test with coverage:

```powershell
dotnet test DataverseLabelTranslator.Server.Test\DataverseLabelTranslator.Server.Test.csproj --configuration Debug --filter "<test-filter>" --collect:"Code Coverage" --results-directory DataverseLabelTranslator.Server.Test\TestResults
```

Build the server test project without running tests:

```powershell
dotnet build DataverseLabelTranslator.Server.Test\DataverseLabelTranslator.Server.Test.csproj --configuration Debug
```

Convert the binary `.coverage` file to XML and report coverage for the four handler classes:

```powershell
powershell -ExecutionPolicy Bypass -File .codex\workflows\support\report-coverage.ps1
```

Generate an HTML coverage report (requires `dotnet-reportgenerator-globaltool`):

```powershell
reportgenerator -reports:DataverseLabelTranslator.Server.Test\TestResults\coverage.xml -targetdir:DataverseLabelTranslator.Server.Test\coverage -reporttypes:Html
start DataverseLabelTranslator.Server.Test\coverage\index.html
```

## Current Coverage Target

Coverage is tracked for these four handler files (matching the JS handler coverage targets):

| # | Handler | C# File | Class Name |
|---|---------|---------|------------|
| 15 | Sitemap | `DataverseLabelTranslator.Server/CustomActions/Synchronous/SiteMap.cs` | `SiteMap` |
| 16 | Dashboards | `DataverseLabelTranslator.Server/CustomActions/Synchronous/Dashboard.cs` | `Dashboard` |
| 17 | Web Resources | `DataverseLabelTranslator.Server/CustomActions/Synchronous/WebResource.cs` | `WebResource` |
| 18 | Global Option Sets | `DataverseLabelTranslator.Server/CustomActions/Synchronous/GlobalOptionSet.cs` | `GlobalOptionSet` |

Thresholds are 100% for lines, functions, branches, and statements.

Run `DataverseLabelTranslator.ProxyTypes\run.bat` before test execution unless the user explicitly asks to skip proxy generation. This command reads local Dataverse connection values from root `.env` or inherited `DEVKIT_*` environment variables and updates `DataverseLabelTranslator.ProxyTypes\GeneratedCode.cs`.

Proxy generation can call the dev Dataverse environment to retrieve metadata. The unit tests themselves must still use FakeXrmEasy and local fakes; they must not call live Dataverse services.

## Test Design Rules

- Unit tests must not deploy server components or call live Dataverse during execution.
- Use FakeXrmEasy to initialize metadata, data, organization service behavior, plugin context, and message executors.
- Prefer early-bound entity classes from `DataverseLabelTranslator.ProxyTypes` when arranging test data.
- Reuse helpers from `DataverseLabelTranslator.Shared.Test` such as `FakeXrmEasyTestBase`, `PluginContextBuilder`, `TestDataLoader`, `TestHelper`, and `TestTracingService`.
- Keep plugin/custom action/workflow tests focused on inputs, outputs, traces, entity mutations, exceptions, and outgoing organization service requests.
- Do not change production server code just to satisfy tests unless the production behavior is wrong.
- Do not deploy, commit, or push as part of this workflow unless the user explicitly asks for that separate workflow.

## Workflow

1. Check `git status --short` so generated proxy changes, test changes, and unrelated work are visible.
2. Regenerate proxy types with `DataverseLabelTranslator.ProxyTypes\run.bat` unless the user requested `no-proxy`.
3. Run `dotnet test DataverseLabelTranslator.Server.Test\DataverseLabelTranslator.Server.Test.csproj --configuration Debug --collect:"Code Coverage" --results-directory DataverseLabelTranslator.Server.Test\TestResults` unless the user supplied a specific test filter.
4. If a filter is supplied, pass it through with `--filter "<test-filter>"` (still with coverage collection).
5. If proxy generation fails, report the DevKit error and do not run tests.
6. If tests fail, report the failing project, test class/name, and relevant assertion/error.
7. If tests pass, run `.codex\workflows\support\report-coverage.ps1` to convert the latest `.coverage` file and report line/block coverage for the four handler classes: `SiteMap`, `Dashboard`, `WebResource`, `GlobalOptionSet`. The script exits non-zero if any class is below 100%.
8. Summarize the test and coverage results.

## Dependency Note

If restore is needed, run:

```powershell
dotnet restore DataverseLabelTranslator.Server.Test\DataverseLabelTranslator.Server.Test.csproj
```

Do not commit `bin`, `obj`, local `.env`, or generated secret-bearing files.
