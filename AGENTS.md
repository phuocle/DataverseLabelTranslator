# AGENTS.md

## Response Completion

When a requested task is complete, the final chat sentence must be: `aP I'm done your tasks ✅`

## Instruction Source

`AGENTS.md` is the single source of truth for shared repository guidance.

- Codex reads this file directly and discovers repo skills from `.agents/skills/`.
- Claude Code reads `CLAUDE.md`; this repo keeps `CLAUDE.md` as a thin `@AGENTS.md` import with only Claude-specific notes.
- GitHub Copilot in VS Code reads this file as always-on agent guidance through workspace setting `chat.useAgentsMdFile=true` in `.vscode/settings.json`. Do not add `.github/copilot-instructions.md`; keep `AGENTS.md` as the shared source of truth. `.github/prompts/*.prompt.md` exposes lightweight Copilot slash commands.
- GitHub Copilot MCP setup for VS Code is documented in `.vscode/mcp.json.example`; copy it to ignored local file `.vscode/mcp.json` and fill credentials locally. The Dataverse DevKit server name is `devkitQuickEdit`.
- Antigravity uses `.agents/skills/` for project skills and `.agents/rules/` for workspace rules. The Antigravity project rule imports this file through `.agents/rules/pl-project.md`. Do not add `.agents/workflows/` unless the workflow is meaningfully different from an existing skill.
- Antigravity IDE MCP config is managed outside the repo at `~/.gemini/antigravity/mcp_config.json`. `.agents/mcp_config.json.example` is a safe placeholder/example; never commit a real `.agents/mcp_config.json` with credentials.
- Do not duplicate shared project rules between `AGENTS.md` and `CLAUDE.md`. Update this file first.

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

Handler-based IIFE modules orchestrated by `XrmTranslator.js`:

```text
User picks Entity -> Type -> Component -> Load
XrmTranslator.SetHandler(type) -> handler.Load()
Grid populated per installed language columns
User edits -> Save -> handler.Save() -> Web API PUT with MergeLabels
```

Every handler: `Load()` fetches metadata and fills the w2ui grid, `Save()` extracts changes and writes them to Dataverse. Handlers read shared state from the `XrmTranslator` global. Key shared modules: `DialogHelper.js`, `TranslationDictionaryService.js`, `TranslationHandler.js`.

## Toolbar Type Files

Use this table first when mapping a numbered toolbar type to code and tests. Type selection and handler dispatch live in `DataverseLabelTranslator.WebResource/js/XrmTranslator.js`.
These numbers are stable shorthand for humans and AI agents; do not reorder, renumber, or reuse them when editing the toolbar.

| #   | Toolbar Type           | Handler JS                     | Test File                              |
| --- | ---------------------- | ------------------------------ | -------------------------------------- |
| 1   | Attributes             | `DataverseLabelTranslator.WebResource/js/Handler/AttributeHandler.js`       | None yet                                                           |
| 2   | Option Sets            | `DataverseLabelTranslator.WebResource/js/EasyTranslatorHandler.js` + `DataverseLabelTranslator.Server/CustomActions/Synchronous/EasyTranslatorAdapters/OptionSetAdapter.cs` | None yet                                                           |
| 3   | Forms                  | `DataverseLabelTranslator.WebResource/js/Handler/FormHandler.js`            | None yet                                                           |
| 4   | Views                  | `DataverseLabelTranslator.WebResource/js/Handler/ViewHandler.js`            | None yet                                                           |
| 5   | Form Metadata          | `DataverseLabelTranslator.WebResource/js/Handler/FormMetaHandler.js`        | None yet                                                           |
| 6   | Entity Metadata        | `DataverseLabelTranslator.WebResource/js/Handler/EntityHandler.js`          | None yet                                                           |
| 7   | Relationships          | `DataverseLabelTranslator.WebResource/js/Handler/RelationshipHandler.js`    | None yet                                                           |
| 8   | Charts                 | `DataverseLabelTranslator.WebResource/js/Handler/ChartHandler.js`           | None yet                                                           |
| 9   | Business Process Flows | `DataverseLabelTranslator.WebResource/js/Handler/BpfHandler.js`             | None yet                                                           |
| 10  | Business Rules         | `DataverseLabelTranslator.WebResource/js/EasyTranslatorHandler.js` + `DataverseLabelTranslator.Server/CustomActions/Synchronous/EasyTranslatorAdapters/BusinessRuleAdapter.cs` | None yet                                                           |
| 11  | Ribbons                | `DataverseLabelTranslator.WebResource/js/Handler/RibbonHandler.js`          | None yet                                                           |
| 12  | Commands               | `DataverseLabelTranslator.WebResource/js/EasyTranslatorHandler.js` + `DataverseLabelTranslator.Server/CustomActions/Synchronous/EasyTranslatorAdapters/CommandAdapter.cs` | None yet                                                           |
| 13  | Entity Messages        | `DataverseLabelTranslator.WebResource/js/EasyTranslatorHandler.js` + `DataverseLabelTranslator.Server/CustomActions/Synchronous/EasyTranslatorAdapters/EntityMessageAdapter.cs` | None yet                                                           |
| 14  | Content Snippets       | `DataverseLabelTranslator.WebResource/js/Handler/ContentSnippetHandler.js`  | None yet                                                           |
| 15  | Sitemap                | `DataverseLabelTranslator.WebResource/js/EasyTranslatorHandler.js` + `DataverseLabelTranslator.Server/CustomActions/Synchronous/EasyTranslatorAdapters/SitemapAdapter.cs` | None yet                                                           |
| 16  | Dashboards             | `DataverseLabelTranslator.WebResource/js/EasyTranslatorHandler.js` + `DataverseLabelTranslator.Server/CustomActions/Synchronous/EasyTranslatorAdapters/DashboardAdapter.cs` | None yet                                                           |
| 17  | Web Resources          | `DataverseLabelTranslator.WebResource/js/EasyTranslatorHandler.js` + `DataverseLabelTranslator.Server/CustomActions/Synchronous/EasyTranslatorAdapters/WebResourceAdapter.cs` | None yet |
| 18  | Global Option Sets     | `DataverseLabelTranslator.WebResource/js/EasyTranslatorHandler.js` + `DataverseLabelTranslator.Server/CustomActions/Synchronous/EasyTranslatorAdapters/GlobalOptionSetAdapter.cs` | None yet |

Dashboards use `DataverseLabelTranslator.WebResource/js/EasyTranslatorHandler.js` with `DashboardAdapter.cs`. Dashboard grids intentionally show only the dashboard parent rows; do not load dashboard tabs, sections, or cells into type 16. Parent dashboard rows are editable directly across language columns. `DataverseLabelTranslator.WebResource/js/Handler/FormHandler.js` is only for type 3 Forms.

## Layout

```text
DataverseLabelTranslator.WebResource/html/  Dataverse HTML web resources
DataverseLabelTranslator.WebResource/js/    Dashboard and library JavaScript web resources
DataverseLabelTranslator.WebResource/css/   CSS web resources
DataverseLabelTranslator.WebResource/img/   Image web resources
DataverseLabelTranslator.WebResource/tests/ Vitest unit tests
DataverseLabelTranslator.Scripts/           Release, packaging, and AI config scripts
DataverseLabelTranslator.Documents/         Repository docs
```

## Skills And Commands

Detailed command workflows live in the agent-native command files:

- Codex/OpenAI skills: `.agents/skills/<name>/SKILL.md`.
- Antigravity skills: `.agents/skills/<name>/SKILL.md`.
- Claude Code project skills: `.claude/skills/<name>/SKILL.md`.
- GitHub Copilot prompt files: `.github/prompts/<name>.prompt.md`.

Keep the roster below aligned with those files. Do not use `.codex/commands/` for new workflows; Codex should use `.agents/skills/`.

## AI Config Sync

`.agents/skills/pl-*/SKILL.md` is the canonical workflow source. Keep downstream adapters generated and checked:

```powershell
powershell -ExecutionPolicy Bypass -File DataverseLabelTranslator.Scripts\sync-ai-config.ps1
powershell -ExecutionPolicy Bypass -File DataverseLabelTranslator.Scripts\check-ai-config.ps1
```

`DataverseLabelTranslator.Scripts\sync-ai-config.ps1` regenerates Claude project skills and GitHub Copilot prompt wrappers from `.agents/skills/`.
`DataverseLabelTranslator.Scripts\check-ai-config.ps1` fails when adapters drift, deprecated files reappear, required local-secret files are not ignored, or tracked files contain DevKit/SAS secret-like values. CI runs the check through `.github/workflows/ai-config.yml`.

## Code Quality

Run quality gates after code edits:

- Run `npm --prefix DataverseLabelTranslator.WebResource run lint` after JavaScript or test changes.
- Run `npm --prefix DataverseLabelTranslator.WebResource run format -- <changed-files>` for files edited in the current task. Paths passed to this command should be relative to `DataverseLabelTranslator.WebResource`. Do not run repo-wide formatting unless the user explicitly asks for it.
- Run `npm --prefix DataverseLabelTranslator.WebResource test` for unit test verification, and `npm --prefix DataverseLabelTranslator.WebResource run test:coverage` when coverage is part of the requested work.

### /pl-ai-sync

Regenerate and validate AI tool adapters from canonical `.agents/skills/pl-*/SKILL.md`. Do not commit or push automatically.

### /pl-unit-tests-webresource

Run Vitest unit tests and optional coverage for Dataverse Label Translator. Use `npm --prefix DataverseLabelTranslator.WebResource test` for tests and `npm --prefix DataverseLabelTranslator.WebResource run test:coverage` for coverage. Unit tests must fake Dataverse/Xrm/browser APIs instead of calling live services. Do not deploy, commit, or push automatically.

### /pl-unit-tests-server

Regenerate early-bound proxy classes with `DataverseLabelTranslator.ProxyTypes\run.bat`, then run server-side MSTest/FakeXrmEasy tests with `dotnet test DataverseLabelTranslator.Test\DataverseLabelTranslator.Test.csproj --configuration Debug`. Tests target `DataverseLabelTranslator.Server`, use `DataverseLabelTranslator.Shared.Test` helpers, and reference `DataverseLabelTranslator.ProxyTypes` for FakeXrmEasy early-bound types. Do not deploy, commit, or push automatically.

### /pl-commit

Full local git workflow: stage all -> commit -> verify clean. Do not push unless the user explicitly asks.

### /pl-deploy-webresource

When files are edited or created under `DataverseLabelTranslator.WebResource/html`, `css`, `js`, or `img`, run `DataverseLabelTranslator.WebResource\deploy.debug.bat` from the repo root and report only the DevKit summary or error.

### /pl-deploy-server

When server code is edited under `DataverseLabelTranslator.Server`, run `DataverseLabelTranslator.Server\deploy.debug.bat` from the repo root and report only the DevKit summary or error.

### /pl-release-0-preflight-deploy

Run the release preflight gate before exporting solutions: run `pl-unit-tests-webresource`, run `pl-unit-tests-server`, require 100% coverage for both, build `DataverseLabelTranslator.Server`, run `pl-deploy-server`, then run `pl-deploy-webresource`.
If tests, coverage, build, or deploy fails, stop and report the failure.
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
