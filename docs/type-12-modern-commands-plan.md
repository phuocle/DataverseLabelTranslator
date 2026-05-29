# Type 12 Commands Implementation Plan

## Goal

Add a new translation type named **12. Commands** for Power Apps command designer commands stored as Dataverse `appaction` records.

This type is entity/solution scoped, but it must **not** be added to **0. All-In-One**.

Implementation result: `12. Commands` uses direct Dataverse Web API label actions on `appaction` records. It does not export/import translation packages and does not publish a model-driven app.

Numbering note: classic ribbon labels are `11. Ribbons`; modern command designer labels are the separate `12. Commands` type.

## Microsoft References

- [Modern commanding overview](https://learn.microsoft.com/en-us/power-apps/maker/model-driven-apps/command-designer-overview)
- [Customize the command bar using command designer](https://learn.microsoft.com/en-us/power-apps/maker/model-driven-apps/use-command-designer)
- [Manage commands in solutions](https://learn.microsoft.com/en-us/power-apps/maker/model-driven-apps/manage-commands-in-solutions)
- [App Action table reference](https://learn.microsoft.com/en-us/power-apps/developer/data-platform/reference/entities/appaction)
- [A button on the command bar has wrong labels or translations](https://learn.microsoft.com/en-us/troubleshoot/power-platform/power-apps/create-and-use-apps/ribbon-issues-button-wrong-label)
- [Translate localizable text for model-driven apps](https://learn.microsoft.com/en-us/power-apps/maker/model-driven-apps/translate-localizable-text)

Microsoft documents modern commands separately from classic ribbon XML. Classic ribbon labels live in `RibbonDiffXml` `LocLabels`; modern command designer commands are `appaction` solution components.

## Scope

Translate localizable text on `appaction` records:

- `ButtonLabelText`
- `ButtonTooltipTitle`
- `ButtonTooltipDescription`
- `ButtonAccessibilityText`
- `GroupTitle`

Relevant command locations include:

- Form
- Main Grid
- Sub Grid
- Associated Grid
- Quick Form
- Global Header
- Dashboard

Do not merge this into `11. Ribbons`. `11. Ribbons` remains classic `RibbonDiffXml`. Commands is a separate handler.

## Completed Spike

Test commands were created on `pl_salesorder` in the dev solution. The observed metadata shape is:

- Table: `appaction`
- Entity filter: `contextvalue eq 'pl_salesorder'`
- Solution component type: `10298`
- App actions may have `_appmoduleid_value = null`; do not require app publish.
- Parent relationships use `_parentappactionid_value`.
- Location/type values follow the command MCP code:
  - location `0` Form, `1` Main Grid, `2` Sub Grid, `3` Associated Grid, `4` Quick Form, `5` Global Header, `6` Dashboard.
  - type `0` Button, `1` Dropdown, `2` Split Button, `3` Group.

Confirmed localizable fields:

- `buttonlabeltext`
- `buttontooltiptitle`
- `buttontooltipdescription`
- `buttonaccessibilitytext`
- `grouptitle`

`RetrieveLocLabels` worked for the above fields. Save uses `SetLocLabels` with the full localized label collection for the changed field.

## Handler Design

Created `js\ModernCommandHandler.js`.

Public methods:

- `ModernCommandHandler.Load()`
- `ModernCommandHandler.Save()`
- `ModernCommandHandler.SaveOnly()`

`Load()`:

1. Require selected solution.
2. If an entity is selected, filter commands to that entity where possible.
3. Retrieve solution component ids where `componenttype eq 10298` for the selected solution.
4. Retrieve each matching `appaction` for `contextvalue eq <selected entity>`.
5. For each localizable property, retrieve all language labels.
6. Build parent rows for commands and child rows for label fields.

`SaveOnly()` direct-label path:

1. Collect changed child rows.
2. Group changes by `appactionid` and field name.
3. For each changed field, call `SetLocLabels` with the full label collection for that field.
4. Publish the selected entity once with `XrmTranslator.Publish()`.

Translation package export/import is not used for Commands.

## Grid Shape

Modern commands should use the same node style as classic ribbons:

```text
Main Grid / Button: <label or unique name>
  Text
  Title
  Description
  Accessibility Text
  Group Title
```

Parent row:

- `schemaName`: `<Location> / <CommandKind>: <display text>`
- non-editable
- expanded by default if current UX wants it

Child rows:

- `Text` maps to `ButtonLabelText`
- `Title` maps to `ButtonTooltipTitle`
- `Description` maps to `ButtonTooltipDescription`
- `Accessibility Text` maps to `ButtonAccessibilityText`
- `Group Title` maps to `GroupTitle`

Always create all child rows for every command, even when a value is blank. This matches the ribbon behavior and lets users create missing translations.

Hidden metadata on child rows:

- `_isModernCommandLabelRow = true`
- `_appActionId`
- `_appActionUniqueName`
- `_propertyName`
- `_labelCollection`

## Query Strategy

Solution-scoped query:

1. Use `solutioncomponent` for the selected solution.
2. Filter to component type `10298`.
3. Retrieve matching active `appaction` records for the selected entity using `contextvalue`.

If the component type is not available in the existing `XrmTranslator.ComponentType` map, add it after verifying the numeric value from Dataverse metadata/export.

Entity filter:

- Use selected entity to filter after records are loaded.
- Candidate fields to inspect during spike:
  - `ContextEntity`
  - `ContextValue`
  - app/action location fields
  - any command designer metadata columns returned by `appaction`

Verified in the target environment. Do not use `contextentity` in Web API filters; lookup values are exposed as `_contextentity_value`, and the practical entity logical-name filter is `contextvalue`.

## Files To Update

- `html\App.html`
  - load `ModernCommandHandler.js`
- `.codex\mapping.xml` and `.claude\mapping.xml`
  - map `ModernCommandHandler.js`
- `js\XrmTranslator.js`
  - route type `12. Commands` to `ModernCommandHandler`
  - add `XrmTranslator.ComponentType.AppAction = 10298`
  - update help text
- `README.md`
  - document Type 12 as implemented

## Save And Publish UX

Modern Commands can affect command bar runtime behavior, but this implementation deliberately uses a simple entity-scoped publish:

- Save changed fields using `SetLocLabels`.
- Publish once at selected entity level with `PublishXml`.
- Do not publish appmodule because this project/test setup does not require an app-scoped command publish.
- Do not use `PublishAllXml` for Commands.

## Explicit Non-Goals

- Do not edit classic `RibbonDiffXml`.
- Do not manage command actions, JavaScript, Power Fx formulas, visibility rules, or enable rules.
- Do not create/delete commands.
- Do not add this type to All-In-One.
- Do not include unmanaged solution mutation beyond label updates.

## Edge Cases

- Command exists in solution but not for selected entity.
- Command label is inherited from classic/OOB command and has no `appaction` localizable row.
- User edits a blank tooltip/description and creates a new localized label.
- Command was deleted or moved between load and save.
- Command designer creates rows with duplicate display labels; use stable ids, not text, as keys.

## Verification Checklist

Static checks:

```powershell
node --check js\ModernCommandHandler.js
node --check js\XrmTranslator.js
```

Functional checks:

1. Create a modern main grid command.
2. Load `12. Commands`.
3. Confirm parent/child node shape.
4. Edit target language `Text`, `Title`, and `Description`.
5. Save.
6. Confirm entity publish completes and Save/Load re-enable.
7. Switch language and verify command bar text.
8. Test a blank description value can be created.
9. Test a second command with same label does not collide.
10. Confirm `0. All-In-One` does not include Modern Commands.

Deployment:

- Use `$pl-deploy-web-resource` for changed web resource files.
- Do not use MCP `manage_ribbon`; that tool is for classic ribbon XML, not modern commands.
