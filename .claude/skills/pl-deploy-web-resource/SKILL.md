---
name: "PL Deploy Web Resource"
description: Deploy a local Dataverse Label Translator file as a Dataverse web resource through MCP manage_webresource.
argument-hint: "<local-path>"
disable-model-invocation: true
---

# Deploy Web Resource

Deploy a local Dataverse Label Translator file as a web resource using MCP `manage_webresource`.

## Input

`$ARGUMENTS` is the local file path to deploy.

## Steps

**Step 1: Validate**

If `$ARGUMENTS` is empty, stop with: `Usage: /pl-deploy-web-resource <local-path>`.
If the file does not exist, stop with: `File $ARGUMENTS not found.`

**Step 2: Resolve Dataverse unique name**

Read `.claude/mapping.xml`. If `$ARGUMENTS` matches a `LocalPath`, use its `UniqueName`.

If not found, derive from convention:

| File pattern | UniqueName |
|---|---|
| `js/*.js` | `pl_/DataverseLabelTranslator/js/<filename>` |
| `css/*.css` | `pl_/DataverseLabelTranslator/css/<filename>` |
| `html/App.html` | `pl_/DataverseLabelTranslator/html/App.html` |
| `img/*.svg` | `pl_/DataverseLabelTranslator/img/<filename>` |

For new files not matching above, ask the user for the desired Dataverse unique name.

PropertyEditor files are intentionally excluded from this solution and must not be deployed by this command.

**Step 3: Determine web resource type from file extension**

| Ext | Type |
|---|---|
| `.js` | `js` |
| `.css` | `css` |
| `.html` | `html` |
| `.svg` | `svg` |
| `.png` | `png` |

**Step 4: Check if web resource exists**

Call MCP `manage_webresource` with `action=detail`, `web_resource_id=<UniqueName>`.

**Step 5: Create or update**

- If exists: `manage_webresource` with `action=update`, `web_resource_id=<UniqueName>`, `file_path=$ARGUMENTS`.
- If not exists: `manage_webresource` with `action=create`, `name=<UniqueName>`, `file_path=$ARGUMENTS`, `type=<type>`, `solution_name=DataverseLabelTranslator`.

**Step 6: Report**

Success: `Deployed $ARGUMENTS as <UniqueName>.`
Failure: show the error.

## Naming Convention

All web resources for this solution must follow `pl_/DataverseLabelTranslator/<type>/<name.ext>`.
