---
name: "PL Unit Tests WebResource"
description: "Run Dataverse Label Translator Vitest unit tests and coverage checks with mocked Dataverse APIs."
argument-hint: "[coverage|<test-filter>]"
disable-model-invocation: true
---

<!-- Generated from ../../.agents/skills/pl-unit-tests-webresource/SKILL.md. Do not edit manually; run DataverseLabelTranslator.Scripts/sync-ai-config.ps1. -->

# PL Unit Tests WebResource

Use this skill when the user asks to run, verify, debug, or explain unit tests, Vitest coverage, mocked Dataverse API tests, or code coverage for Dataverse Label Translator.

## What This Skill Does

1. Runs the client Vitest unit tests from `DataverseLabelTranslator.WebResource`.
2. Runs coverage when requested and checks the configured coverage thresholds.
3. Keeps unit tests isolated from Dataverse by using fakes/mocks instead of live Web API calls.
4. Reports exact failing suites/tests and the command output needed to act on failures.

## Commands

Run all unit tests:

```powershell
npm --prefix DataverseLabelTranslator.WebResource test
```

Run coverage:

```powershell
npm --prefix DataverseLabelTranslator.WebResource run test:coverage
```

Inspect the machine-readable coverage summary:

```powershell
Get-Content DataverseLabelTranslator.WebResource\coverage\coverage-summary.json
```

Open the HTML coverage report after coverage succeeds:

```powershell
start DataverseLabelTranslator.WebResource\coverage\index.html
```

## Current Coverage Target

`DataverseLabelTranslator.WebResource\vitest.config.mjs` currently includes coverage for:

```text
js/GlobalOptionSetHandler.js
js/WebResourceHandler.js
```

Thresholds are 100% for lines, functions, branches, and statements.

## Test Design Rules

- Unit tests must not call real Dataverse, real Xrm, or live browser APIs.
- Fake `WebApiClient`, `XrmTranslator`, `DialogHelper`, `w2ui`, and `window`/`document` objects as needed.
- Prefer small fixtures that model the Dataverse request/response shape used by the handler under test.
- Assert outgoing Web API payloads, validation behavior, grid records, save flow calls, and publish/reload behavior.
- Do not change production code just to satisfy tests unless the production behavior is wrong.
- Do not deploy, commit, or push as part of this skill unless the user explicitly asks for that separate workflow.

## Workflow

1. Check `git status --short` so test changes are not confused with unrelated work.
2. Run `npm --prefix DataverseLabelTranslator.WebResource test` unless the user specifically asked only for coverage.
3. If coverage is requested, run `npm --prefix DataverseLabelTranslator.WebResource run test:coverage`.
4. If a test fails, report the failing file, suite/test name, and the relevant assertion/error.
5. If coverage fails, report the uncovered file/metric from the coverage output.
6. If tests pass, summarize the command and result.

## Dependency Note

If `node_modules` is missing, run:

```powershell
npm --prefix DataverseLabelTranslator.WebResource install
```

Do not commit `node_modules`.
