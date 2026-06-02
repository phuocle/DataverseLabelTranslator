---
name: "PL Deploy Web Resource"
description: "Deploy a local Dataverse Label Translator file as a Dataverse web resource through MCP manage_webresource."
argument-hint: "<local-path>"
disable-model-invocation: true
---

<!-- Generated from ../../.agents/skills/pl-deploy-web-resource/SKILL.md. Do not edit manually; run DataverseLabelTranslator.Scripts/sync-ai-config.ps1. -->

# pl-deploy-web-resource

Use this skill when the user asks to run the Dataverse Label Translator command `pl-deploy-web-resource`.

## Command Template

# Deploy Web Resource

Deploy a local Dataverse Label Translator file as a web resource using MCP `manage_webresource`.

## Input

`$ARGUMENTS` is the local file path to deploy.

## Steps

### Step 1: Validate

If `$ARGUMENTS` is empty, stop with:

```text
Usage: /pl-deploy-web-resource <local-path>
```

If the file does not exist, stop with:

```text
File $ARGUMENTS not found.
```

### Step 2: Resolve Dataverse Unique Name

Read `.codex/mapping.xml`. If `$ARGUMENTS` matches a `LocalPath`, use its `UniqueName`.

If the file is not in the mapping, derive from convention:

| File pattern | UniqueName |
|---|---|
| `DataverseLabelTranslator.WebResource/js/*.js` | `pl_/DataverseLabelTranslator/js/<filename>` |
| `DataverseLabelTranslator.WebResource/css/*.css` | `pl_/DataverseLabelTranslator/css/<filename>` |
| `DataverseLabelTranslator.WebResource/html/App.html` | `pl_/DataverseLabelTranslator/html/App.html` |
| `DataverseLabelTranslator.WebResource/img/*.svg` | `pl_/DataverseLabelTranslator/img/<filename>` |
| `DataverseLabelTranslator.WebResource/img/*.png` | `pl_/DataverseLabelTranslator/img/<filename>` |

For new files that do not match the convention, ask the user for the desired Dataverse unique name.

PropertyEditor files are intentionally excluded from this solution and must not be deployed by this command.

### Step 3: Determine Web Resource Type

Use the file extension:

| Ext | Type |
|---|---|
| `.js` | `js` |
| `.css` | `css` |
| `.html` | `html` |
| `.svg` | `svg` |
| `.png` | `png` |

### Step 4: Check Existence

Call MCP `manage_webresource`:

```text
action=detail
web_resource_id=<UniqueName>
```

### Step 5: Create Or Update

If the web resource exists, call MCP `manage_webresource`:

```text
action=update
web_resource_id=<UniqueName>
file_path=$ARGUMENTS
```

If the web resource does not exist, call MCP `manage_webresource`:

```text
action=create
name=<UniqueName>
file_path=$ARGUMENTS
type=<type>
solution_name=DataverseLabelTranslator
```

Do not use the `devkit` CLI for this command.

### Step 6: Report

On success, report:

```text
Deployed $ARGUMENTS as <UniqueName>.
```

On failure, show the error.

## Naming Convention

All web resources for this solution must follow:

```text
pl_/DataverseLabelTranslator/<type>/<name.ext>
```
