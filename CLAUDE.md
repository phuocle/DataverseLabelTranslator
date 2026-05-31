@AGENTS.md

## Claude Code

This file intentionally imports `AGENTS.md` so Claude Code and Codex share the same project guidance without duplicated instructions.

Claude-specific project skills live in `.claude/skills/` and are generated from canonical `.agents/skills/` by `scripts/sync-ai-config.ps1`:

- `/pl-ai-sync`
- `/pl-commit`
- `/pl-deploy-web-resource`
- `/pl-export-solution`
- `/pl-release-appsource`
- `/pl-test-package-deployer`
- `/pl-deploy-azure`
- `/pl-unit-tests`

Keep shared project rules in `AGENTS.md`. Add content here only when it is specific to Claude Code.
