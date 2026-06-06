---
name: "pl-release-0-preflight-deploy"
display-name: "PL Release 0 Preflight Deploy"
description: "Run 100% coverage release preflight checks, then deploy server and web resources to the dev Dataverse environment."
---

# PL Release 0 Preflight Deploy

Use this skill when the user asks to run `/pl-release-0-preflight-deploy`, release preflight, or the release step before exporting solutions.

## What This Skill Does

1. Runs the WebResource unit test workflow with 100% Vitest coverage.
2. Runs the server unit test workflow with 100% server handler coverage.
3. Stops immediately if tests fail or any coverage target is below 100%.
4. Builds the server project after coverage passes.
5. Deploys the server components through `pl-deploy-server`.
6. Deploys the web resources through `pl-deploy-webresource`.
7. Reports the full result summary to chat.

This is release step 0. It validates and deploys the current code to the dev Dataverse environment before `/pl-release-1-export-solutions`.

## Required Skill Order

Run these existing repository skills in this exact order:

```text
pl-unit-tests-webresource
pl-unit-tests-server
pl-deploy-server
pl-deploy-webresource
```

Do not continue to a later step after any failed command.

## Workflow

### Step 1: Confirm Repository

Run from:

```powershell
D:\github\DataverseLabelTranslator
```

Verify the current folder:

```powershell
git rev-parse --show-toplevel
git status --short
```

Expected repository root:

```text
D:\github\DataverseLabelTranslator
```

If the repository root is different, stop.

### Step 2: Run WebResource Unit Tests And Coverage

Follow the canonical `pl-unit-tests-webresource` workflow:

```powershell
npm --prefix DataverseLabelTranslator.WebResource run test:coverage
Get-Content DataverseLabelTranslator.WebResource\coverage\coverage-summary.json
```

The configured coverage thresholds must be 100% for lines, functions, branches, and statements. If tests fail or coverage is below 100%, report the failing test or coverage metric and stop.

### Step 3: Run Server Unit Tests And Coverage

Follow the canonical `pl-unit-tests-server` workflow:

```powershell
DataverseLabelTranslator.ProxyTypes\run.bat
dotnet test DataverseLabelTranslator.Test\DataverseLabelTranslator.Test.csproj --configuration Debug --collect:"Code Coverage" --results-directory DataverseLabelTranslator.Test\TestResults
powershell -ExecutionPolicy Bypass -File .agents\skills\pl-unit-tests-server\report-coverage.ps1
```

The server coverage report must be 100% for all tracked handler classes. If proxy generation fails, tests fail, or coverage is below 100%, report the exact failure and stop.

### Step 4: Build Server Project

After both coverage gates pass, build the server project:

```powershell
dotnet build DataverseLabelTranslator.Server\DataverseLabelTranslator.Server.csproj --configuration Debug
```

If the build fails, report the build error and stop. Do not deploy after a failed build.

### Step 5: Deploy Server

Follow the canonical `pl-deploy-server` workflow:

```powershell
DataverseLabelTranslator.Server\deploy.debug.bat
```

Run from the repository root. Report only the DevKit summary or error for this deploy step.

If server deploy fails, report the failure and stop. Do not deploy web resources after a failed server deploy.

### Step 6: Deploy WebResource

Follow the canonical `pl-deploy-webresource` workflow:

```powershell
DataverseLabelTranslator.WebResource\deploy.debug.bat
```

Run from the repository root. Report only the DevKit summary or error for this deploy step.

If web resource deploy fails, report the failure and stop.

### Step 7: Report Result

Report all of these items to chat:

- WebResource test result and 100% coverage confirmation.
- Server test result and 100% coverage confirmation.
- Server build result.
- Server deploy summary.
- WebResource deploy summary.
- Whether release step 0 passed or failed.
- The next intended release step: `/pl-release-1-export-solutions`, only when all steps passed.

## Hard Rules

- Do not run `/pl-release-1-export-solutions`.
- Do not export Dataverse solutions.
- Do not prepare AppSource.
- Do not upload to Azure.
- Do not stage files.
- Do not commit.
- Do not push.
- Do not continue after any failed test, coverage, build, or deploy step.
- Do not hide warnings that are relevant to test, coverage, build, or deploy success.
