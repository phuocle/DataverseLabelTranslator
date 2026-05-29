# Type 16 Modern Commands Implementation Plan

## Goal

Add a new translation type named **16. Modern Commands** for Power Apps command designer commands stored as Dataverse `appaction` records.

This type is entity/solution scoped, but it must **not** be added to **0. All-In-One**.

Important numbering note: the current codebase may already have a `15. Ribbons` item. This work packet follows the requested future numbering. Before merging code, reconcile the menu numbering with the product owner. Do not delete the existing ribbon handler unless explicitly approved.

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

Do not merge this into `15. Ribbons`. `15. Ribbons` remains classic `RibbonDiffXml`. Modern Commands is a separate handler.

## Required Spike Before Coding

Create test commands in a dev solution:

1. Main grid command with label only.
2. Main form command with label and tooltip.
3. Dropdown/group command if possible.
4. Replaced/customized OOB command if possible.

For each command:

1. Query the `appaction` record.
2. Test `RetrieveLocLabels` for each candidate localizable field.
3. Test `SetLocLabels` on one non-base language field.
4. Publish and verify the command designer/runtime shows the translated value.
5. Export translations and verify the same command labels appear in `CrmTranslations.xml`.

Decision point:

- Prefer direct `RetrieveLocLabels` / `SetLocLabels` if it works reliably for `appaction` localizable fields and respects solution layering.
- Use translation package export/import only if direct label actions are incomplete or unstable.

Record the chosen path in the implementation PR.

## Handler Design

Create `js\ModernCommandHandler.js`.

Public methods:

- `ModernCommandHandler.Load()`
- `ModernCommandHandler.Save()`
- `ModernCommandHandler.SaveOnly()`

`Load()`:

1. Require selected solution.
2. If an entity is selected, filter commands to that entity where possible.
3. Retrieve solution component ids for `appaction` records in the selected solution.
4. Retrieve each `appaction` record with the fields needed for display and filtering.
5. For each localizable property, retrieve all language labels.
6. Build parent rows for commands and child rows for label fields.

`SaveOnly()` direct-label path:

1. Collect changed child rows.
2. Group changes by `appactionid` and field name.
3. For each changed field, call `SetLocLabels` with the full label collection for that field.
4. Publish using the existing async publish guard when required.

`SaveOnly()` translation-package path:

1. Re-export the selected solution translation package immediately before save.
2. Apply only changed command rows to the fresh package.
3. Import the translation package.
4. Publish using the existing async publish guard.

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
- `_appActionLocation`
- `_propertyName`
- `_labelCollection`

## Query Strategy

Start with solution-scoped query:

1. Use `solutioncomponent` for the selected solution.
2. Filter to the component type used by `appaction`.
3. Retrieve matching `appaction` records.

If the component type is not available in the existing `XrmTranslator.ComponentType` map, add it after verifying the numeric value from Dataverse metadata/export.

Entity filter:

- Use selected entity to filter after records are loaded.
- Candidate fields to inspect during spike:
  - `ContextEntity`
  - `ContextValue`
  - app/action location fields
  - any command designer metadata columns returned by `appaction`

Do not rely on field names without verifying them in the target environment.

## Files To Update

- `html\App.html`
  - add `ModernCommandHandler.js`
- `.codex\mapping.xml`
  - add `ModernCommandHandler.js`
- `js\XrmTranslator.js`
  - add menu item `16. Modern Commands`
  - add type id to the correct visibility list
  - route type to `ModernCommandHandler`
  - update help/about text
- `README.md`
  - update after implementation

If the implementation chooses translation package import:

- Reuse `js\TranslationPackageService.js` from Type 15 Business Rules.

## Save And Publish UX

Modern Commands can affect command bar runtime behavior. Use the same defensive UX principles as ribbon save:

- Disable Save and Load while the handler is importing or publishing.
- Show a banner during async publish.
- Store publish job id in `localStorage`.
- Poll until complete.
- Clear stale lock after the same recovery rules already used by ribbon.

If direct `SetLocLabels` does not return a publish job id, call the existing publish helper that gives the safest user feedback. Prefer async `PublishAllXml` if command changes are not visible with scoped publish.

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
2. Load `16. Modern Commands`.
3. Confirm parent/child node shape.
4. Edit target language `Text`, `Title`, and `Description`.
5. Save.
6. Confirm publish completes and Save/Load re-enable.
7. Switch language and verify command bar text.
8. Test a blank description value can be created.
9. Test a second command with same label does not collide.
10. Confirm `0. All-In-One` does not include Modern Commands.

Deployment:

- Use `$pl-deploy-web-resource` for changed web resource files.
- Do not use MCP `manage_ribbon`; that tool is for classic ribbon XML, not modern commands.
