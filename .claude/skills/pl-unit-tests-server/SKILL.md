---
name: "PL Unit Tests Server"
description: "Regenerate Dataverse proxy types and run Dataverse Label Translator server-side MSTest/FakeXrmEasy unit tests."
argument-hint: "[generate-proxy|no-proxy|<test-filter>]"
disable-model-invocation: true
---

<!-- Generated from ../../.agents/skills/pl-unit-tests-server/SKILL.md. Do not edit manually; run DataverseLabelTranslator.Scripts/sync-ai-config.ps1. -->

# PL Unit Tests Server

Use this skill when the user asks to run, verify, debug, or explain server-side unit tests, MSTest tests, FakeXrmEasy tests, plugin tests, workflow tests, or server code coverage for Dataverse Label Translator.

## What This Skill Does

1. Regenerates early-bound proxy classes before server tests so FakeXrmEasy can resolve Dataverse entity types.
2. Runs the server-side MSTest suite from `DataverseLabelTranslator.Test`.
3. Uses shared test utilities from `DataverseLabelTranslator.Shared.Test`.
4. Tests server code from `DataverseLabelTranslator.Server` without deploying to Dataverse.
5. Reports exact failing test names, projects, and relevant assertion/error output.

## Project Map

```text
DataverseLabelTranslator.Server/       Server-side plugins, workflows, custom actions, and custom APIs under test
DataverseLabelTranslator.Test/         MSTest project that runs server unit tests
DataverseLabelTranslator.Shared.Test/  Shared FakeXrmEasy helpers imported by the test project
DataverseLabelTranslator.ProxyTypes/   Early-bound classes required by FakeXrmEasy
```

`DataverseLabelTranslator.Shared.Test` is a shared project imported by `DataverseLabelTranslator.Test`; do not try to run it as a separate test project.

## Commands

Regenerate early-bound proxy types first:

```powershell
DataverseLabelTranslator.ProxyTypes\run.bat
```

Run all server unit tests:

```powershell
dotnet test DataverseLabelTranslator.Test\DataverseLabelTranslator.Test.csproj --configuration Debug
```

Run a filtered server test:

```powershell
dotnet test DataverseLabelTranslator.Test\DataverseLabelTranslator.Test.csproj --configuration Debug --filter "<test-filter>"
```

Build the server test project without running tests:

```powershell
dotnet build DataverseLabelTranslator.Test\DataverseLabelTranslator.Test.csproj --configuration Debug
```

## Proxy Type Rule

Run `DataverseLabelTranslator.ProxyTypes\run.bat` before test execution unless the user explicitly asks to skip proxy generation. This command reads local Dataverse connection values from root `.env` or inherited `DEVKIT_*` environment variables and updates `DataverseLabelTranslator.ProxyTypes\GeneratedCode.cs`.

Proxy generation can call the dev Dataverse environment to retrieve metadata. The unit tests themselves must still use FakeXrmEasy and local fakes; they must not call live Dataverse services.

## Test Design Rules

- Unit tests must not deploy server components or call live Dataverse during execution.
- Use FakeXrmEasy to initialize metadata, data, organization service behavior, plugin context, and message executors.
- Prefer early-bound entity classes from `DataverseLabelTranslator.ProxyTypes` when arranging test data.
- Reuse helpers from `DataverseLabelTranslator.Shared.Test` such as `FakeXrmEasyTestBase`, `PluginContextBuilder`, `TestDataLoader`, `TestHelper`, and `TestTracingService`.
- Keep plugin/custom action/workflow tests focused on inputs, outputs, traces, entity mutations, exceptions, and outgoing organization service requests.
- Do not change production server code just to satisfy tests unless the production behavior is wrong.
- Do not deploy, commit, or push as part of this skill unless the user explicitly asks for that separate workflow.

## Workflow

1. Check `git status --short` so generated proxy changes, test changes, and unrelated work are visible.
2. Regenerate proxy types with `DataverseLabelTranslator.ProxyTypes\run.bat` unless the user requested `no-proxy`.
3. Run `dotnet test DataverseLabelTranslator.Test\DataverseLabelTranslator.Test.csproj --configuration Debug` unless the user supplied a specific test filter.
4. If a filter is supplied, pass it through with `--filter "<test-filter>"`.
5. If proxy generation fails, report the DevKit error and do not run tests.
6. If tests fail, report the failing project, test class/name, and relevant assertion/error.
7. If tests pass, summarize the command and result.

## Dependency Note

If restore is needed, run:

```powershell
dotnet restore DataverseLabelTranslator.Test\DataverseLabelTranslator.Test.csproj
```

Do not commit `bin`, `obj`, local `.env`, or generated secret-bearing files.
