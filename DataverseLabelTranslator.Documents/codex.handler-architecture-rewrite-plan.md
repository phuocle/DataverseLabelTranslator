# Codex Handler Architecture Rewrite Plan

> Repository: `D:\github\DataverseLabelTranslator`
> Scope: requested toolbar type end to end.
> Server rule: if custom action phases do not exist for the requested type, create them.
> Test assumption: do not design around current unit tests.

Use this file together with `codex.apply-js-pattern.md`.

Example prompt:

```text
Read DataverseLabelTranslator.Documents/codex.apply-js-pattern.md and
DataverseLabelTranslator.Documents/codex.handler-architecture-rewrite-plan.md,
then rewrite the requested handler using type 18 as the reference pattern.
```

## Non-Negotiable Handler Contract

Every rewritten handler must have:

1. Private `GetUpdates()`.
2. Private `FillTable()`.
3. Public `Load(lockText)`.
4. Public `Save()`.

`Load(lockText)`:

- resolves the app with `Helper.GetTranslator()`
- locks the grid first with `lockText || Helper.GetOperationLoading()`
- calls custom action phase `Loading`
- stores returned data with `app.SetMetadata(...)` when useful
- fills the grid through private `FillTable()`

`Save()`:

- calls private `GetUpdates()` first
- checks no-save-changes before calling the server
- shows `DialogHelper.alert(...)` for no-save-changes
- does not show the no-change status banner
- calls custom action phases `Saving`, `Publishing`, `Published`
- reloads with `Handler.Load(Helper.GetOperationReLoading())`

Handlers must not call:

- `EnableLoadAndSave`
- `SetLoadButtonDisabled`
- `SetSaveButtonDisabled`
- `WebApiClient`
- `XrmTranslator.RunTypeSaveFlow`
- `XrmTranslator.ExecuteChangeSetBatches`
- direct publish helpers
- `XrmTranslator.RunAsBaseLanguage`

If a button state or generic banner behavior must change, fix core app code such as `XrmTranslator.js`, not the handler.

## Non-Negotiable Server Contract

When the prompt says `type x`, first resolve that number through the toolbar map in `AGENTS.md`. Then verify whether that type already has Server custom action support.

Read the current Server implementation before writing new code. Type 17 and type 18 are the reference implementations:

- `DataverseLabelTranslator.Server/CustomActions/Synchronous/WebResource.cs`
- `DataverseLabelTranslator.Server/CustomActions/Synchronous/GlobalOptionSet.cs`
- `DataverseLabelTranslator.Server/CustomActions/ActionNames.cs`
- `DataverseLabelTranslator.Server/CustomActions/PostDataverseLabelTranslatorCustomActionSynchronous.cs`

If the requested type does not have Server code yet, creating the Server side is required, not optional.

For each new Server-backed toolbar type:

1. Add an `ActionNames` constant whose value matches the handler `actionName`.
2. Wire the constant in `PostDataverseLabelTranslatorCustomActionSynchronous`.
3. Create `DataverseLabelTranslator.Server/CustomActions/Synchronous/<TypeName>.cs`.
4. Implement `ICustomAction` phases:
   - `Loading`
   - `Saving`
   - `Publishing`
   - `Published`
   - `Other`
5. Keep Dataverse retrieval, metadata mutation, label merge, and publish logic on the Server.
6. Keep the handler payload and Server DTO names/properties aligned exactly.

Do not leave a rewritten handler calling a custom action that does not exist in `DataverseLabelTranslator.Server`.

## Architecture Decision

Rewrite handlers as thin adapters around a smaller `EasyTranslator` app shell.

Current transition model:

```text
EasyTranslator.js
  New app facade/shell. It may delegate to XrmTranslator while migration is incomplete.

Helper.js
  Shared labels, placeholders, validation, load/save flow helpers, and translator resolution.

Handler JS
  Type-specific grid shape and payload mapping only.

XrmTranslator.js
  Legacy shell and temporary dispatch/core fallback glue.
```

Handler code should resolve app state through:

```js
var app = Helper.GetTranslator();
```

`Helper.GetTranslator()` should prefer `EasyTranslator` and fall back to `XrmTranslator` during transition.

## Handler Rewrite Scope

When asked to rewrite one toolbar type:

- rewrite only that type's handler unless shared helper/core glue is required
- do not rewrite unrelated handlers
- implement Server code when that type does not already have a matching Server custom action
- do not implement or run unit tests
- do not preserve old client-side Dataverse CRUD behavior in handlers

Allowed files:

- target handler under `DataverseLabelTranslator.WebResource/js/Handler/`
- `DataverseLabelTranslator.WebResource/js/Helper.js`
- `DataverseLabelTranslator.WebResource/js/EasyTranslator.js`
- `DataverseLabelTranslator.WebResource/js/XrmTranslator.js` for dispatch/core fallback glue
- `DataverseLabelTranslator.WebResource/html/App.html` for script load order only
- `DataverseLabelTranslator.Server/CustomActions/ActionNames.cs` for missing Server actions
- `DataverseLabelTranslator.Server/CustomActions/PostDataverseLabelTranslatorCustomActionSynchronous.cs` for missing dispatcher cases
- `DataverseLabelTranslator.Server/CustomActions/Synchronous/<TypeName>.cs` for the requested type's Server implementation

## Custom Action Contract

Handlers call these phases through shared helper flow methods:

```js
Helper.ExecuteTypedCustomAction(actionName, Helper.CustomActionTypes.Loading, input);
Helper.ExecuteTypedCustomAction(actionName, Helper.CustomActionTypes.Saving, input);
Helper.ExecuteTypedCustomAction(actionName, Helper.CustomActionTypes.Publishing, input);
Helper.ExecuteTypedCustomAction(actionName, Helper.CustomActionTypes.Published, input);
```

Expected response envelope:

```json
{
  "ok": true,
  "message": "",
  "type": "Loading",
  "object": {}
}
```

The `object` payload is type-specific.

## Shared Helper Requirements

Use shared helpers instead of handler-local generic logic:

```js
Helper.GetTranslator();
Helper.ApplyPlaceholder(record, editablePlaceholder, basePlaceholder, app);
Helper.AddLocalizedLabelsToRecord(record, localizedLabels);
Helper.GetComponentLocalizedLabels(metadata, component);
Helper.GetChangedLabels(changes, allowEmpty);
Helper.ValidateBaseLanguageNotEmpty(record, changes, options);
Helper.FinalizeGrid(records, app);
Helper.RunServerLoad(options);
Helper.RunServerSaveFlow(options);
```

Important:

- `Helper.FinalizeGrid(records, app)` must not enable/disable buttons.
- `Helper.RunServerSaveFlow(...)` may lock/unlock the grid for progress text.
- Shared helper code may be improved when multiple handlers need the same behavior.

## Grid And Save Rules

Every grid record has:

```js
{
    recid: "unique-id",
    schemaName: "first-column-text"
}
```

Tree records use:

```js
{
    recid: "parent-id",
    schemaName: "parent-name",
    w2ui: {
        editable: false,
        children: []
    }
}
```

Save extraction:

- use `app.GetAllRecords()`
- never use `app.GetGrid().records`
- validate base-language Display Text cannot be empty
- preserve non-base Display Text clears as empty strings
- preserve Description clears as empty strings
- never send placeholders

## Type 18 Reference

Type 18 is the reference implementation.

```text
Handler: DataverseLabelTranslator.WebResource/js/Handler/GlobalOptionSetHandler.js
Object: GlobalOptionSetHandler
Action: GlobalOptionSet
Load input: { solutionId }
Load output key: optionSets
Save payload keys:
  component
  optionValueUpdates
  optionSetDescriptionUpdates
Publish key: optionSetNames
```

Important type 18 behavior to preserve in other handlers where applicable:

- no-change Save is handled before the server call
- no-change Save uses `DialogHelper.alert(...)`
- save reload calls `Load(Helper.GetOperationReLoading())`
- handler has no button enable/disable calls
- handler has no direct `WebApiClient`

## Implementation Order For A Type

1. Read type 18 handler for the current pattern.
2. Identify target handler load payload and custom action output key.
3. Create or normalize private `FillTable()`.
4. Create or normalize private `GetUpdates()`.
5. Make public `Load(lockText)` lock the grid first and call `Helper.RunServerLoad(...)`.
6. Make public `Save()` check no-save-changes first.
7. Make save flow call `Helper.RunServerSaveFlow(...)`.
8. Make reload call `Load(Helper.GetOperationReLoading())`.
9. Remove direct Dataverse CRUD/publish/client mutation from the handler.
10. Format changed JS and run lint if practical.

## Acceptance Criteria

For the target handler:

- Private `GetUpdates()` exists.
- Private `FillTable()` exists.
- Public `Load(lockText)` exists.
- Public `Save()` exists.
- `Save()` checks no-save-changes before server calls.
- `Load()` locks the grid first.
- Save reload passes `Helper.GetOperationReLoading()` into `Load(...)`.
- Handler exposes only `Load` and `Save`.
- Handler has no `SaveOnly`.
- Handler has no `WebApiClient`.
- Handler has no `RunTypeSaveFlow`.
- Handler has no direct publish call.
- Handler has no Load/Save button enable-disable calls.
- Handler uses `Helper.GetTranslator()`.
- Handler uses `app.GetAllRecords()`.
- `FillTable()` ends with `Helper.FinalizeGrid(records, app)`.
- Empty base-language Display Text is blocked.
- Intentional clears are preserved as empty strings.

## Bottom Line

Handlers are not mini apps. They are thin type-specific adapters:

```text
Load -> custom action Loading -> FillTable
Save -> GetUpdates -> no-change dialog or Saving/Publishing/Published -> Load(Re-Loading)
```
