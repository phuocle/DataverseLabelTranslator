# CLAUDE.md

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

## Commands

### /commit
Full local git workflow: stage all -> commit -> verify clean. Do not push unless the user explicitly asks.

### /deploy-web-resource `<local-path>`
Deploy a file to Dataverse using MCP `manage_webresource`:
1. Look up `<local-path>` in `.claude/mapping.xml` to get CRM `UniqueName`.
2. If not in mapping, auto-derive: `pl_/DataverseLabelTranslator/<type>/<basename>` where type = `js|css|html|img`.
3. Try `manage_webresource` with `action=detail`, `web_resource_id=<UniqueName>` to check existence.
4. If exists -> `action=update`, `web_resource_id=<UniqueName>`, `file_path=<local-path>`.
5. If not -> `action=create`, `name=<UniqueName>`, `file_path=<local-path>`, `type=<js|css|html|svg|png>`, `solution_name=DataverseLabelTranslator`.

Do not deploy PropertyEditor resources into this solution.

Do NOT use `devkit` CLI. Use MCP `manage_webresource` directly.

### /export-solution
Export `DataverseLabelTranslator` solution via PAC CLI.
