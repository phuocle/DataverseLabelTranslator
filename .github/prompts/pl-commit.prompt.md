---
name: pl-commit
description: Stage local changes and create one local git commit. Never push.
argument-hint: '[-m "commit message"]'
agent: agent
---

Run the Dataverse Label Translator commit workflow.

Before acting, read and follow [AGENTS.md](../../AGENTS.md) and the canonical workflow in [pl-commit skill](../../.agents/skills/pl-commit/SKILL.md).

Use any text supplied after `/pl-commit` as the command arguments. If a `-m` message is provided, use it unless it is empty or misleading. If no message is provided, inspect the staged diff and generate one clear commit message.

Hard rules:

- Create exactly one local commit.
- Do not push.
- Do not create a pull request.
- Do not deploy or export.
- Do not use `--no-verify`.
- If there are unrelated or risky changes, stop and ask before committing.
