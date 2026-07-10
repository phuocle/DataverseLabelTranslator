# AGENTS.md

## Response Completion

When a requested task is complete, the final chat sentence must be: `aP I'm done your tasks ✅`

## Codex App

Codex App/ChatGPT is the only supported AI client for this repository.

- `AGENTS.md` is the single source of repository guidance and is maintained directly.
- `.codex/workflows/*.md` contains the project workflows. When the user names one, read that file completely before acting.
- `.codex/config.toml.example` is the only tracked MCP client example and uses the fixed process alias `devkit-codex-translate`.
- `.codex/config.toml` is local, may contain credentials, and must never be committed.
- Do not add AI adapters or configuration for Claude, GitHub Copilot, Antigravity, Cursor, or other AI clients.
- Do not add an AI configuration synchronization/generation layer; update Codex files directly.

## Project

This repository is the active codebase for **Dataverse Label Translator**:

- **w2ui** 2.0
- **WebApiClient** 4.1.6
- Publisher prefix `pl_`, solution `DataverseLabelTranslator`
- Deployable dashboard web resources under `pl_/`
- Web resource source lives in `DataverseLabelTranslator.WebResource`, a DynamicsCrm.DevKit WebResource `.csproj` used for fast local web resource deployment.
- DevKit `.bat` files are commit-safe and must read connection values from root `.env` / `DEVKIT_*` environment variables. Commit `.env.example`, never commit `.env`.
- Translation dashboard only.

The HTML dashboard loads JavaScript through `<script>` tags. There is no build step; JavaScript files deploy directly as Dataverse web resources. MCP server connects to the dev Dataverse environment.

## Architecture

Single-dashboard IIFE modules orchestrated by `DataverseLabelTranslator.js`:

```text
User picks Entity -> Type -> Component -> Load
DataverseLabelTranslator.SetHandler(type) -> DataverseLabelTranslatorHandler.Load()
Grid populated per installed language columns
User edits -> Save -> DataverseLabelTranslatorHandler.Save() -> Dataverse custom action -> C# EasyTranslator adapter
```

`DataverseLabelTranslator.WebResource/js/DataverseLabelTranslator.js` is the main dashboard file and contains toolbar orchestration plus the unified JS handler formerly split into `EasyTranslatorHandler.js`. `Helper.js` contains shared helpers and the former `XrmService.js` custom-action wrappers. `DialogHelper.js`, `AIService.js`, `AppService.js`, and `DictionaryService.js` remain separate support modules.

Legacy `XrmTranslator.js` and `TranslationHandler.js` were moved to `DataverseLabelTranslator.WebResource/backup/js/` for code lookup only. They are not loaded by `App.html` and are not part of the active dashboard.

## Toolbar Type Files

Use this table first when mapping a numbered toolbar type to code and tests. Type selection and handler dispatch live in `DataverseLabelTranslator.WebResource/js/DataverseLabelTranslator.js`.
These numbers are stable shorthand for humans and AI agents; do not reorder, renumber, or reuse them when editing the toolbar.

| STT | Name                   | JS Handler                                                        | CS Handler                                                                                      | JS Unit Test | CS Unit Test |
| --- | ---------------------- | ----------------------------------------------------------------- | ----------------------------------------------------------------------------------------------- | ------------ | ------------ |
| 1   | Attributes             | `DataverseLabelTranslator.WebResource/js/DataverseLabelTranslator.js` | `DataverseLabelTranslator.Server/CustomActions/Synchronous/EasyTranslatorAdapters/AttributeAdapter.cs` | none         | none         |
| 2   | Option Sets            | `DataverseLabelTranslator.WebResource/js/DataverseLabelTranslator.js` | `DataverseLabelTranslator.Server/CustomActions/Synchronous/EasyTranslatorAdapters/OptionSetAdapter.cs` | none         | none         |
| 3   | Forms                  | `DataverseLabelTranslator.WebResource/js/DataverseLabelTranslator.js` | `DataverseLabelTranslator.Server/CustomActions/Synchronous/EasyTranslatorAdapters/FormAdapter.cs` | none         | none         |
| 4   | Views                  | `DataverseLabelTranslator.WebResource/js/DataverseLabelTranslator.js` | `DataverseLabelTranslator.Server/CustomActions/Synchronous/EasyTranslatorAdapters/ViewAdapter.cs` | none         | none         |
| 5   | Form Metadata          | `DataverseLabelTranslator.WebResource/js/DataverseLabelTranslator.js` | `DataverseLabelTranslator.Server/CustomActions/Synchronous/EasyTranslatorAdapters/FormMetaAdapter.cs` | none         | none         |
| 6   | Entity Metadata        | `DataverseLabelTranslator.WebResource/js/DataverseLabelTranslator.js` | `DataverseLabelTranslator.Server/CustomActions/Synchronous/EasyTranslatorAdapters/EntityMetadataAdapter.cs` | none         | none         |
| 7   | Relationships          | `DataverseLabelTranslator.WebResource/js/DataverseLabelTranslator.js` | `DataverseLabelTranslator.Server/CustomActions/Synchronous/EasyTranslatorAdapters/RelationshipAdapter.cs` | none         | none         |
| 8   | Charts                 | `DataverseLabelTranslator.WebResource/js/DataverseLabelTranslator.js` | `DataverseLabelTranslator.Server/CustomActions/Synchronous/EasyTranslatorAdapters/ChartAdapter.cs` | none         | none         |
| 9   | Business Process Flows | `DataverseLabelTranslator.WebResource/js/DataverseLabelTranslator.js` | `DataverseLabelTranslator.Server/CustomActions/Synchronous/EasyTranslatorAdapters/BpfAdapter.cs` | none         | none         |
| 10  | Business Rules         | `DataverseLabelTranslator.WebResource/js/DataverseLabelTranslator.js` | `DataverseLabelTranslator.Server/CustomActions/Synchronous/EasyTranslatorAdapters/BusinessRuleAdapter.cs` | none         | none         |
| 11  | Ribbons                | `DataverseLabelTranslator.WebResource/js/DataverseLabelTranslator.js` | `DataverseLabelTranslator.Server/CustomActions/Synchronous/EasyTranslatorAdapters/RibbonAdapter.cs` | none         | none         |
| 12  | Commands               | `DataverseLabelTranslator.WebResource/js/DataverseLabelTranslator.js` | `DataverseLabelTranslator.Server/CustomActions/Synchronous/EasyTranslatorAdapters/CommandAdapter.cs` | none         | none         |
| 13  | Entity Messages        | `DataverseLabelTranslator.WebResource/js/DataverseLabelTranslator.js` | `DataverseLabelTranslator.Server/CustomActions/Synchronous/EasyTranslatorAdapters/EntityMessageAdapter.cs` | none         | none         |
| 14  | Content Snippets       | `DataverseLabelTranslator.WebResource/js/DataverseLabelTranslator.js` | `DataverseLabelTranslator.Server/CustomActions/Synchronous/EasyTranslatorAdapters/ContentSnippetAdapter.cs` | none         | none         |
| 15  | Sitemap                | `DataverseLabelTranslator.WebResource/js/DataverseLabelTranslator.js` | `DataverseLabelTranslator.Server/CustomActions/Synchronous/EasyTranslatorAdapters/SitemapAdapter.cs` | none         | none         |
| 16  | Dashboards             | `DataverseLabelTranslator.WebResource/js/DataverseLabelTranslator.js` | `DataverseLabelTranslator.Server/CustomActions/Synchronous/EasyTranslatorAdapters/DashboardAdapter.cs` | none         | none         |
| 17  | Web Resources          | `DataverseLabelTranslator.WebResource/js/DataverseLabelTranslator.js` | `DataverseLabelTranslator.Server/CustomActions/Synchronous/EasyTranslatorAdapters/WebResourceAdapter.cs` | none         | none         |
| 18  | Global Option Sets     | `DataverseLabelTranslator.WebResource/js/DataverseLabelTranslator.js` | `DataverseLabelTranslator.Server/CustomActions/Synchronous/EasyTranslatorAdapters/GlobalOptionSetAdapter.cs` | none         | none         |

Dashboards use `DataverseLabelTranslator.WebResource/js/DataverseLabelTranslator.js` with `DashboardAdapter.cs`. Dashboard grids intentionally show only the dashboard parent rows; do not load dashboard tabs, sections, or cells into type 16. Parent dashboard rows are editable directly across language columns. Forms use `DataverseLabelTranslator.js` with `FormAdapter.cs`.

## Layout

```text
DataverseLabelTranslator.WebResource/html/  Dataverse HTML web resources
DataverseLabelTranslator.WebResource/js/    Dashboard and library JavaScript web resources
DataverseLabelTranslator.WebResource/css/   CSS web resources
DataverseLabelTranslator.WebResource/img/   Image web resources
DataverseLabelTranslator.WebResource/tests/ Reserved for future Vitest tests; currently empty
DataverseLabelTranslator.Scripts/           Release and packaging scripts
DataverseLabelTranslator.Documents/         Repository docs
```

## Codex Workflows

Detailed recipes live in `.codex/workflows/`. They are instruction files, not generated slash commands. Read the named workflow completely and follow its authorization boundaries.

| Workflow | File |
| --- | --- |
| Commit local changes | `.codex/workflows/pl-commit.md` |
| Deploy server | `.codex/workflows/pl-deploy-server.md` |
| Deploy web resources | `.codex/workflows/pl-deploy-webresource.md` |
| Release 0: preflight and deploy | `.codex/workflows/pl-release-0-preflight-deploy.md` |
| Release 1: export solutions | `.codex/workflows/pl-release-1-export-solutions.md` |
| Release 2: prepare AppSource | `.codex/workflows/pl-release-2-prepare-appsource.md` |
| Release 3: prepare Package Deployer test | `.codex/workflows/pl-release-3-test-package-deployer.md` |
| Release 4: deploy package to Azure | `.codex/workflows/pl-release-4-deploy-azure.md` |
| Deprecated server tests | `.codex/workflows/pl-unit-tests-server.md` |
| Deprecated web-resource tests | `.codex/workflows/pl-unit-tests-webresource.md` |

The two unit-test workflows are retained for historical/reference use only. Do not run or reintroduce tests unless the user explicitly asks.

## Code Quality

Run quality gates after code edits:

- Run `npm --prefix DataverseLabelTranslator.WebResource run lint` after JavaScript or test changes.
- Run `npm --prefix DataverseLabelTranslator.WebResource run format -- <changed-files>` for files edited in the current task. Paths passed to this command should be relative to `DataverseLabelTranslator.WebResource`. Do not run repo-wide formatting unless the user explicitly asks for it.

### Unit Test Status

There are currently no active JavaScript or C# unit tests in this repository. Test folders may exist as placeholders only.

Important AI-agent rule: ignore unit-test work for normal tasks. Do not write new JavaScript or C# unit tests, do not add coverage requirements, and do not run unit-test commands unless the user explicitly asks to reintroduce tests. Use lint, formatting, build/deploy checks, and direct code inspection as the normal verification path.

### /pl-unit-tests-webresource

Deprecated while the project has no active JavaScript unit tests. Do not run this for normal work unless the user explicitly asks to reintroduce webresource tests.

### /pl-unit-tests-server

Deprecated while the project has no active C# unit tests. Do not run this for normal work unless the user explicitly asks to reintroduce server tests.

### /pl-commit

Read and follow `.codex/workflows/pl-commit.md`. Do not run it unless the user explicitly asks to commit. Never push unless the user explicitly asks.

### /pl-deploy-webresource

When files are edited or created under `DataverseLabelTranslator.WebResource/html`, `css`, `js`, or `img`, run `DataverseLabelTranslator.WebResource\deploy.debug.bat` from the repo root and report only the DevKit summary or error.

### /pl-deploy-server

When server code is edited under `DataverseLabelTranslator.Server`, run `DataverseLabelTranslator.Server\deploy.debug.bat` from the repo root and report only the DevKit summary or error.

### /pl-release-0-preflight-deploy

Run the release preflight gate before exporting solutions: skip unit tests/coverage while the project has no active test suites, build `DataverseLabelTranslator.Server`, run `pl-deploy-server`, then run `pl-deploy-webresource`.
If build or deploy fails, stop and report the failure.
Do not export solutions, prepare AppSource, upload to Azure, stage, commit, or push.

### /pl-release-1-export-solutions

Export, clean, pack, and publish the `DataverseLabelTranslator` managed/unmanaged release ZIPs through the DevKit SolutionPackager project.
Use `DataverseLabelTranslator.SolutionPackager\Extract-Both.bat`, clean/stamp the SolutionPackager `Both` folder, use `DataverseLabelTranslator.SolutionPackager\Pack-Both.bat`, then copy the final ZIPs to `DataverseLabelTranslator.Release/<version>/dataverse/solutions`.
Do not use raw `Solutions-Extract` ZIPs as release output.

### /pl-release-2-prepare-appsource

Build the final AppSource Marketplace ZIP from the existing managed release solution.
If the user mentions a version, pass it as `-SolutionVersion <version>`.
If the user does not mention a version, infer latest from `DataverseLabelTranslator.Release/<version>/dataverse/solutions/DataverseLabelTranslator_managed.zip`; if none exists, default to `1.0.0.0`.
Always trust the selected `DataverseLabelTranslator.Release/<version>/dataverse/solutions/DataverseLabelTranslator_managed.zip` as the latest user-controlled source.
Do not export the Dataverse solution.
Output must be `DataverseLabelTranslator.Release/<version>/appsource/zip/DataverseLabelTranslator.v.<major.minor.patch>.zip`.
Do not upload to Azure.

### /pl-release-3-test-package-deployer

Prepare the local Package Deployer cache for manual `pac tool pd` testing.
Run `DataverseLabelTranslator.Scripts/test-package-deployer.ps1`; if the user mentions a version, pass `-SolutionVersion <version>`.
The script must find the active `%LOCALAPPDATA%\Microsoft\PowerPlatform\PD\<version>\tools` folder itself, copy `DataverseLabelTranslator.Release/<version>/appsource/src/DataverseLabelTranslatorPackage` into that folder, and verify `PackageDeployer.exe` exists before copying.
Do not export the Dataverse solution.
Do not launch Package Deployer.
After the script succeeds, tell the user to run `pac tool pd`.

### /pl-release-4-deploy-azure

Upload the final AppSource Marketplace ZIP to Azure Blob Storage and generate the Partner Center SAS details.
Run `DataverseLabelTranslator.Scripts/deploy-azure.ps1`; if the user mentions a version, pass `-SolutionVersion <version>`.
The script must verify Azure CLI is logged in as `sales@d365iconsandtooltips.com`, verify storage account `ple` exists in resource group `SHARED`, create private container `dataverselabeltranslator` if missing, upload only `DataverseLabelTranslator.Release/<version>/appsource/zip/DataverseLabelTranslator.v.<major.minor.patch>.zip`, and write sensitive Partner Center details to `DataverseLabelTranslator.Release/<version>/appsource/zip/release.md`.
Do not export the Dataverse solution.
Do not run Release AppSource unless the user separately asks.
Do not paste the SAS URL into chat.
Do not commit `release.md`.
