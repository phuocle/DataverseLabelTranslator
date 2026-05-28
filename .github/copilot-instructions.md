# GitHub Copilot Instructions

This file is standalone for GitHub Copilot. Markdown links are references, not Claude-style imports, so keep critical Copilot guidance directly in this file.

`AGENTS.md` remains the shared source of truth for Codex and other agents. In VS Code Copilot, `AGENTS.md` may also be loaded separately when `chat.useAgentsMdFile` is enabled; do not rely on this file to import it.

Project:

- This repository is Dataverse Label Translator: w2ui 2.0, WebApiClient 4.1.6, publisher prefix `pl_`, solution `DataverseLabelTranslator`.
- Web resources live under `pl_/DataverseLabelTranslator/`; local source folders are `html/`, `js/`, `css/`, and `img/`.
- The dashboard has no build step. JavaScript files deploy directly as Dataverse web resources loaded by script tags.
- Runtime architecture is handler-based IIFE modules orchestrated by `XrmTranslator.js`: user selects Entity -> Type -> Component -> Load, then `XrmTranslator.SetHandler(type)` calls `handler.Load()`, edits are saved through `handler.Save()` and Dataverse Web API `MergeLabels`.
- Every handler loads metadata into the w2ui grid and saves changed labels back to Dataverse. Shared modules include `DialogHelper.js`, `TranslationDictionaryService.js`, and `TranslationHandler.js`.

Rules:

- PropertyEditor resources are intentionally excluded from this solution.
- For command-style work in VS Code Copilot, use the prompt files in `.github/prompts/`.
- Prompt files are thin wrappers. Before executing a workflow, read the linked `.agents/skills/<name>/SKILL.md` file and follow it as canonical when the file is available.
- VS Code MCP configuration is local-only. Copy `.vscode/mcp.json.example` to ignored file `.vscode/mcp.json`, then use the configured `devkitQuickEdit` server for Dataverse MCP tools such as `manage_webresource`.
- Do not use the deprecated `.codex/commands/` folder for new workflows.
- Do not paste SAS URLs into chat and do not commit `release.md`.
- Do not push, deploy, export, or upload unless the user explicitly asked for that workflow.

Available Copilot prompt commands:

- `/pl-commit`
- `/pl-deploy-web-resource`
- `/pl-export-solution`
- `/pl-release-appsource`
- `/pl-test-package-deployer`
- `/pl-deploy-azure`
