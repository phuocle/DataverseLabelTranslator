# AGENTS.md

## Instruction Source

`AGENTS.md` is the single source of truth for shared repository guidance.

- Codex reads this file directly and discovers repo skills from `.agents/skills/`.
- Claude Code reads `CLAUDE.md`; this repo keeps `CLAUDE.md` as a thin `@AGENTS.md` import with only Claude-specific notes.
- GitHub Copilot in VS Code can read this file as always-on agent guidance when `chat.useAgentsMdFile` is enabled. `.github/copilot-instructions.md` is a standalone Copilot entry point, not an import wrapper, and `.github/prompts/*.prompt.md` exposes lightweight Copilot slash commands.
- GitHub Copilot MCP setup for VS Code is documented in `.vscode/mcp.json.example`; copy it to ignored local file `.vscode/mcp.json` and fill credentials locally. The Dataverse DevKit server name is `devkitQuickEdit`.
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
- Claude Code slash commands: `.claude/commands/<name>.md`.
- GitHub Copilot prompt files: `.github/prompts/<name>.prompt.md`.

Keep the roster below aligned with those files. Do not use `.codex/commands/` for new workflows; Codex should use `.agents/skills/`.

### /pl-commit
Full local git workflow: stage all -> commit -> verify clean. Do not push unless the user explicitly asks.

### /pl-deploy-web-resource `<local-path>`
Deploy a file to Dataverse using MCP `manage_webresource`:
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
