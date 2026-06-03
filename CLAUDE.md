@AGENTS.md

## Claude Code

This file intentionally imports `AGENTS.md` so Claude Code and Codex share the same project guidance without duplicated instructions.

Claude-specific project skills live in `.claude/skills/` and are generated from canonical `.agents/skills/` by `DataverseLabelTranslator.Scripts/sync-ai-config.ps1`:

- `/pl-ai-sync`
- `/pl-commit`
- `/pl-deploy-server`
- `/pl-deploy-webresource`
- `/pl-release-1-export-solutions`
- `/pl-release-2-prepare-appsource`
- `/pl-release-3-test-package-deployer`
- `/pl-release-4-deploy-azure`
- `/pl-unit-tests-server`
- `/pl-unit-tests-webresource`

Keep shared project rules in `AGENTS.md`. Add content here only when it is specific to Claude Code.
