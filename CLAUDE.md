@AGENTS.md

## Claude Code

This file intentionally imports `AGENTS.md` so Claude Code and Codex share the same project guidance without duplicated instructions.

Claude-specific command implementations live in `.claude/commands/`:

- `/pl-commit`
- `/pl-deploy-web-resource`
- `/pl-export-solution`
- `/pl-release-appsource`
- `/pl-test-package-deployer`
- `/pl-deploy-azure`

Keep shared project rules in `AGENTS.md`. Add content here only when it is specific to Claude Code.
