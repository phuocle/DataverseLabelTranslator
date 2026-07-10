---
name: "pl-release-0-preflight-deploy"
display-name: "PL Release 0 Preflight Deploy"
description: "Build, deploy, and verify server and web resources before solution export."
---

# PL Release 0 Preflight Deploy

Use this workflow when the user asks for `/pl-release-0-preflight-deploy`, release preflight, or the release step before exporting solutions.

There are currently no active JavaScript or C# unit-test suites. Do not run deprecated test or coverage workflows as part of this preflight unless the user explicitly asks to reintroduce tests.

## Workflow

Run from `D:\github\DataverseLabelTranslator` and stop immediately after any failure.

### 1. Confirm repository and working state

```powershell
git rev-parse --show-toplevel
git status --short
```

The expected root is `D:\github\DataverseLabelTranslator`. Preserve unrelated working-tree changes.

### 2. Build the server

```powershell
dotnet build DataverseLabelTranslator.Server\DataverseLabelTranslator.Server.csproj --configuration Debug
```

If the build fails, report the relevant error and stop. Do not deploy.

### 3. Deploy the server

Read and follow `.codex/workflows/pl-deploy-server.md`, which runs:

```powershell
DataverseLabelTranslator.Server\deploy.debug.bat
```

Report only the DevKit summary or error. If deployment fails, stop and do not deploy web resources.

### 4. Deploy web resources

Read and follow `.codex/workflows/pl-deploy-webresource.md`, which runs:

```powershell
DataverseLabelTranslator.WebResource\deploy.debug.bat
```

Report only the DevKit summary or error.

### 5. Report result

Report the server build result, server deployment summary, web-resource deployment summary, and whether release step 0 passed. Mention `/pl-release-1-export-solutions` as the next intended step only when every step passed.

## Hard Rules

- Do not run unit tests or coverage checks while the repository has no active test suites.
- Do not run `/pl-release-1-export-solutions`.
- Do not export solutions, prepare AppSource, or upload to Azure.
- Do not stage, commit, or push.
- Do not continue after a failed build or deployment.
