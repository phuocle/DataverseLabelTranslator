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
- Web resources under `pl_/DataverseLabelTranslator/`
- Translation dashboard only; PropertyEditor resources are intentionally excluded from this solution.

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

Use this table first when mapping a numbered toolbar type to code and tests. Type selection and handler dispatch live in `js/XrmTranslator.js`.

| #   | Toolbar Type           | Handler JS                     | Test File                              |
| --- | ---------------------- | ------------------------------ | -------------------------------------- |
| 1   | Attributes             | `js/AttributeHandler.js`       | None yet                               |
| 2   | Option Sets            | `js/OptionSetHandler.js`       | None yet                               |
| 3   | Forms                  | `js/FormHandler.js`            | None yet                               |
| 4   | Views                  | `js/ViewHandler.js`            | None yet                               |
| 5   | Form Metadata          | `js/FormMetaHandler.js`        | None yet                               |
| 6   | Entity Metadata        | `js/EntityHandler.js`          | None yet                               |
| 7   | Relationships          | `js/RelationshipHandler.js`    | None yet                               |
| 8   | Charts                 | `js/ChartHandler.js`           | None yet                               |
| 9   | Business Process Flows | `js/BpfHandler.js`             | None yet                               |
| 10  | Business Rules         | `js/BusinessRuleHandler.js`    | None yet                               |
| 11  | Ribbons                | `js/RibbonHandler.js`          | None yet                               |
| 12  | Commands               | `js/ModernCommandHandler.js`   | None yet                               |
| 13  | Entity Messages        | `js/EntityMessageHandler.js`   | None yet                               |
| 14  | Content Snippets       | `js/ContentSnippetHandler.js`  | None yet                               |
| 15  | Sitemap                | `js/SiteMapHandler.js`         | None yet                               |
| 16  | Dashboards             | `js/FormHandler.js`            | None yet                               |
| 17  | Web Resources          | `js/WebResourceHandler.js`     | `tests/WebResourceHandler.test.js`     |
| 18  | Global Option Sets     | `js/GlobalOptionSetHandler.js` | `tests/GlobalOptionSetHandler.test.js` |

Dashboards intentionally share `js/FormHandler.js`; there is no `js/DashboardHandler.js`. Dashboard-only behavior must be gated with `XrmTranslator.GetType() === "dashboards"` so regular Forms behavior does not change.

## Layout

```text
html/  Dataverse HTML web resources
js/    Dashboard and library JavaScript web resources
css/   CSS web resources
img/   Image web resources
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
powershell -ExecutionPolicy Bypass -File scripts\sync-ai-config.ps1
powershell -ExecutionPolicy Bypass -File scripts\check-ai-config.ps1
```

`scripts\sync-ai-config.ps1` regenerates Claude project skills and GitHub Copilot prompt wrappers from `.agents/skills/`.
`scripts\check-ai-config.ps1` fails when adapters drift, deprecated files reappear, required local-secret files are not ignored, or tracked files contain DevKit/SAS secret-like values. CI runs the check through `.github/workflows/ai-config.yml`.

## Code Quality

Run quality gates after code edits:

- Run `npm run lint` after JavaScript or test changes.
- Run `npm run format -- <changed-files>` for files edited in the current task. Do not run repo-wide formatting unless the user explicitly asks for it.
- Run `npm test` for unit test verification, and `npm run test:coverage` when coverage is part of the requested work.

### /pl-ai-sync

Regenerate and validate AI tool adapters from canonical `.agents/skills/pl-*/SKILL.md`. Do not commit or push automatically.

### /pl-unit-tests

Run Vitest unit tests and optional coverage for Dataverse Label Translator. Use `npm test` for tests and `npm run test:coverage` for coverage. Unit tests must fake Dataverse/Xrm/browser APIs instead of calling live services. Do not deploy, commit, or push automatically.

### /pl-commit

Full local git workflow: stage all -> commit -> verify clean. Do not push unless the user explicitly asks.

### /pl-deploy-web-resource `<local-path>`

Deploy a file to Dataverse using MCP `manage_webresource`:

When a file listed in `.codex/mapping.xml` is changed and the user needs to test the app in Dataverse, deploy that changed file again with `/pl-deploy-web-resource <local-path>`. Do not leave mapped web resource changes only on disk when the next expected step is app testing.

1. Look up `<local-path>` in `.codex/mapping.xml` to get CRM `UniqueName`.
2. If not in mapping, auto-derive: `pl_/DataverseLabelTranslator/<type>/<basename>` where type = `js|css|html|img`.
3. Try `manage_webresource` with `action=detail`, `web_resource_id=<UniqueName>` to check existence.
4. If exists -> `action=update`, `web_resource_id=<UniqueName>`, `file_path=<local-path>`.
5. If not -> `action=create`, `name=<UniqueName>`, `file_path=<local-path>`, `type=<js|css|html|svg|png>`, `solution_name=DataverseLabelTranslator`.

Do not deploy PropertyEditor resources into this solution.

Do NOT use `devkit` CLI. Use MCP `manage_webresource` directly.

### /pl-export-solution

Export `DataverseLabelTranslator` solution via PAC CLI.

### /pl-release-appsource

Build the final AppSource all-in-one Marketplace ZIP from the existing managed release solution.
If the user mentions a version, pass it as `-SolutionVersion <version>`.
If the user does not mention a version, infer latest from `release/<version>/dataverse/solutions/DataverseLabelTranslator_managed.zip`; if none exists, default to `1.0.0.0`.
Always trust the selected `release/<version>/dataverse/solutions/DataverseLabelTranslator_managed.zip` as the latest user-controlled source.
Do not export the Dataverse solution.
Output must be `release/<version>/appsource/zip/DataverseLabelTranslator.v.<major.minor.patch>.zip`.
Do not upload to Azure.

### /pl-test-package-deployer

Prepare the local Package Deployer cache for manual `pac tool pd` testing.
Run `scripts/test-package-deployer.ps1`; if the user mentions a version, pass `-SolutionVersion <version>`.
The script must find the active `%LOCALAPPDATA%\Microsoft\PowerPlatform\PD\<version>\tools` folder itself, copy `release/<version>/appsource/src/DataverseLabelTranslatorPackage` into that folder, and verify `PackageDeployer.exe` exists before copying.
Do not export the Dataverse solution.
Do not launch Package Deployer.
After the script succeeds, tell the user to run `pac tool pd`.

### /pl-deploy-azure

Upload the final AppSource all-in-one ZIP to Azure Blob Storage and generate the Partner Center SAS details.
Run `scripts/deploy-azure.ps1`; if the user mentions a version, pass `-SolutionVersion <version>`.
The script must verify Azure CLI is logged in as `sales@d365iconsandtooltips.com`, verify storage account `ple` exists in resource group `SHARED`, create private container `dataverselabeltranslator` if missing, upload only `release/<version>/appsource/zip/DataverseLabelTranslator.v.<major.minor.patch>.zip`, and write sensitive Partner Center details to `release/<version>/appsource/zip/release.md`.
Do not export the Dataverse solution.
Do not run Release AppSource unless the user separately asks.
Do not paste the SAS URL into chat.
Do not commit `release.md`.
