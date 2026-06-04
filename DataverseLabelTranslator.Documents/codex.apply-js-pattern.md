# Codex Apply JS Handler Pattern

Use this file as the instruction source when rewriting one toolbar handler JavaScript file.

Example prompt:

```text
Read DataverseLabelTranslator.Documents/codex.apply-js-pattern.md and
DataverseLabelTranslator.Documents/codex.handler-architecture-rewrite-plan.md,
then rewrite the requested handler using type 18 as the reference pattern.
```

## Required Handler Shape

Every rewritten handler must follow this shape:

1. Private `GetUpdates()` function.
2. Private `FillTable()` function.
3. Public `Load(lockText)` function.
4. Public `Save()` function.

`Load(lockText)` must lock the grid first:

```js
HandlerName.Load = function (lockText) {
    var app = Helper.GetTranslator();
    app.LockGrid(lockText || Helper.GetOperationLoading());

    return Helper.RunServerLoad({
        app: app,
        actionName: actionName,
        getPayload: function () {
            return BuildLoadInput(app);
        },
        onLoaded: function (output) {
            app.SetMetadata(output.items || []);
            FillTable();
        }
    });
};
```

`Save()` must check no-save-changes before calling the server:

```js
HandlerName.Save = function () {
    var updates = GetUpdates();

    if (HasNoChanges(updates)) {
        return DialogHelper.alert("There are no changes to save.", {
            title: "Handler Title"
        });
    }

    return Helper.RunServerSaveFlow({
        app: Helper.GetTranslator(),
        actionName: actionName,
        getSavePayload: function () {
            return updates;
        },
        getPublishPayload: GetPublishPayload,
        getPublishedPayload: GetPublishPayload,
        shouldReload: ShouldReload,
        reloadAction: function () {
            return HandlerName.Load(Helper.GetOperationReLoading());
        }
    });
};
```

Important:

- Do not create `LoadInternal`.
- Do not use `suppressLoadingMessage`.
- Do not call `EnableLoadAndSave`, `SetLoadButtonDisabled`, or `SetSaveButtonDisabled` from handlers.
- If button state must be restored after a handler dialog, fix core app code such as `XrmTranslator.js`, not the handler.
- Do not show the default status banner for no-change Save when the handler owns no-change behavior; use `DialogHelper.alert(...)`.

## Required Server Check

When the prompt mentions a toolbar `type x`, do not assume the Server project already supports that type.

Before or while rewriting the handler:

1. Resolve `type x` from `AGENTS.md` toolbar map.
2. Inspect the target handler's `actionName` / custom action name.
3. Inspect `DataverseLabelTranslator.Server/CustomActions/`.
4. If the Server custom action for that type is missing, create it correctly.

Use the completed Server patterns for type 17 and type 18 as the reference:

- `DataverseLabelTranslator.Server/CustomActions/ActionNames.cs`
- `DataverseLabelTranslator.Server/CustomActions/PostDataverseLabelTranslatorCustomActionSynchronous.cs`
- `DataverseLabelTranslator.Server/CustomActions/Synchronous/WebResource.cs`
- `DataverseLabelTranslator.Server/CustomActions/Synchronous/GlobalOptionSet.cs`

Required Server shape for a new type:

- add an `ActionNames` constant matching the handler action name
- add dispatcher wiring in `PostDataverseLabelTranslatorCustomActionSynchronous`
- add a class under `DataverseLabelTranslator.Server/CustomActions/Synchronous/`
- implement `ICustomAction` phases: `Loading`, `Saving`, `Publishing`, `Published`, and `Other`
- define phase input/output DTOs in the same file unless the project already has a stronger local pattern
- return the standard custom action response envelope through the existing dispatcher
- keep Dataverse metadata/write/publish behavior on the Server side, not inside the handler

If Server code is added or changed, the handler and Server payload DTOs must match exactly. Do not finish with only client-side code when the requested type has no Server implementation.

## Scope

Work on the requested handler flow. Client-side JavaScript is the default, but Server code is required when the requested toolbar type does not already have a matching Server custom action.

Allowed files:

- target handler JS file under `DataverseLabelTranslator.WebResource/js/Handler/`
- `DataverseLabelTranslator.WebResource/js/Helper.js` only if shared helper functions are missing
- `DataverseLabelTranslator.WebResource/js/EasyTranslator.js` only if the facade is missing
- `DataverseLabelTranslator.WebResource/js/XrmTranslator.js` only for dispatch/core fallback glue
- `DataverseLabelTranslator.WebResource/html/App.html` only when script load order must change
- `DataverseLabelTranslator.Server/CustomActions/ActionNames.cs` when adding a missing Server action
- `DataverseLabelTranslator.Server/CustomActions/PostDataverseLabelTranslatorCustomActionSynchronous.cs` when adding dispatcher wiring
- `DataverseLabelTranslator.Server/CustomActions/Synchronous/<TypeName>.cs` when adding the missing Server implementation

Do not modify unrelated Server files. Do modify the Server files above when `type x` has no Server implementation yet.
Do not implement or analyze JS/C# unit tests.
Do not run unit tests for this instruction.

Handler files live under `DataverseLabelTranslator.WebResource/js/Handler/`.
Shared libraries live under `DataverseLabelTranslator.WebResource/js/lib/`.

## Handler Rules

Handlers must:

- resolve app state with `var app = Helper.GetTranslator();`
- use `app.GetAllRecords()` inside `GetUpdates()`
- use `Helper.ValidateBaseLanguageNotEmpty(...)` for Display Text base language validation
- use `Helper.GetChangedLabels(...)` so intentional clears are preserved
- end `FillTable()` with `Helper.FinalizeGrid(records, app)`
- call custom action phases through `Helper.RunServerLoad(...)` and `Helper.RunServerSaveFlow(...)`
- pass `Helper.GetOperationReLoading()` into `Load(...)` from save reloads

Handlers must not:

- call `WebApiClient`
- call `XrmTranslator.RunTypeSaveFlow`
- call `XrmTranslator.ExecuteChangeSetBatches`
- call any direct publish helper
- call `XrmTranslator.RunAsBaseLanguage`
- mutate Dataverse XML/XAML/package data locally
- expose `SaveOnly`
- disable or enable Load/Save buttons
- use business literal `"Label"` as handler vocabulary

Use component constants:

```js
Helper.ComponentTypes.DisplayText;
Helper.ComponentTypes.Description;
```

Dataverse physical `Label` property mapping belongs in `Helper.GetComponentLocalizedLabels(...)`, not in handlers.

## Helper APIs

Use existing helper functions when present. Add missing ones to `Helper.js`.

Expected helper APIs:

```js
Helper.GetTranslator();
Helper.GetLanguageColumnText(languageCode, grid);
Helper.ApplyPlaceholder(record, editablePlaceholder, basePlaceholder, app);
Helper.AddLocalizedLabelsToRecord(record, localizedLabels);
Helper.GetComponentLocalizedLabels(metadata, component);
Helper.GetChangedLabels(changes, allowEmpty);
Helper.ValidateBaseLanguageNotEmpty(record, changes, options);
Helper.FinalizeGrid(records, app);
Helper.RunServerLoad(options);
Helper.RunServerSaveFlow(options);

Helper.GetOperationLoading();
Helper.GetOperationSaving();
Helper.GetOperationPublishing();
Helper.GetOperationPublished();
Helper.GetOperationReLoading();
```

## Standard Skeleton

```js
(function (HandlerName, undefined) {
    "use strict";

    var actionName = "ActionName";
    var idSeparator = "|";

    function BuildLoadInput(app) {
        return {
            solutionId: app.GetSolution(),
            entityName: app.GetEntity(),
            entityId: app.GetEntityId(),
            component: app.GetComponent()
        };
    }

    function GetComponent(app) {
        return app.IsDescriptionComponent() ? Helper.ComponentTypes.Description : Helper.ComponentTypes.DisplayText;
    }

    function GetRowPath(record) {
        return (record && (record.schemaName || record.recid)) || "(unknown)";
    }

    function HasNoChanges(updates) {
        return !updates || (updates.items || []).length === 0;
    }

    function GetUpdates() {
        var app = Helper.GetTranslator();
        var records = app.GetAllRecords();
        var items = [];

        for (var i = 0; i < records.length; i++) {
            var record = records[i];
            if (!record.w2ui || !record.w2ui.changes) {
                continue;
            }

            Helper.ValidateBaseLanguageNotEmpty(record, record.w2ui.changes, {
                app: app,
                getRowPath: GetRowPath
            });

            var labels = Helper.GetChangedLabels(record.w2ui.changes, true);
            if (labels.length < 1) {
                continue;
            }

            items.push({
                id: record.recid,
                labels: labels
            });
        }

        return { items: items };
    }

    function FillTable() {
        var app = Helper.GetTranslator();
        var grid = app.GetGrid();
        var records = [];

        grid.clear();

        // Build type-specific w2ui records here.

        Helper.FinalizeGrid(records, app);
    }

    function GetPublishPayload(output) {
        var changedIds = (output && output.changedIds) || [];
        return changedIds.length > 0 ? { changedIds: changedIds } : null;
    }

    function ShouldReload(output) {
        return !!(output && output.changedIds && output.changedIds.length > 0);
    }

    HandlerName.Load = function (lockText) {
        var app = Helper.GetTranslator();
        app.LockGrid(lockText || Helper.GetOperationLoading());

        return Helper.RunServerLoad({
            app: app,
            actionName: actionName,
            getPayload: function () {
                return BuildLoadInput(app);
            },
            onLoaded: function (output) {
                app.SetMetadata(output.items || []);
                FillTable();
            }
        });
    };

    HandlerName.Save = function () {
        var updates = GetUpdates();

        if (HasNoChanges(updates)) {
            return DialogHelper.alert("There are no changes to save.", {
                title: "Handler Title"
            });
        }

        return Helper.RunServerSaveFlow({
            app: Helper.GetTranslator(),
            actionName: actionName,
            getSavePayload: function () {
                return updates;
            },
            getPublishPayload: GetPublishPayload,
            getPublishedPayload: GetPublishPayload,
            shouldReload: ShouldReload,
            reloadAction: function () {
                return HandlerName.Load(Helper.GetOperationReLoading());
            }
        });
    };
})((window.HandlerName = window.HandlerName || {}));
```

Adjust only type-specific names, record IDs, output keys, row tree shape, and payload shape.

## Type 18 Reference

Use type 18 as the closest reference implementation.

```text
Handler file: DataverseLabelTranslator.WebResource/js/Handler/GlobalOptionSetHandler.js
Handler object: GlobalOptionSetHandler
Action name: GlobalOptionSet
Input: { solutionId }
Loading output key: optionSets
Saving payload keys:
  component
  optionValueUpdates
  optionSetDescriptionUpdates
Publish target key:
  optionSetNames
```

Type 18 specific rules:

- no-change Save shows `DialogHelper.alert(...)`, not a banner
- `Load(lockText)` locks the grid with `lockText || Helper.GetOperationLoading()`
- save reload calls `Load(Helper.GetOperationReLoading())`
- save payload sends `component` once at top level
- `optionValueUpdates` items do not repeat `component`
- Display Text parent rows are readonly
- Description parent rows are editable

## Final Checks

Before finishing a handler:

- Private `GetUpdates()` exists.
- Private `FillTable()` exists.
- Public `Load(lockText)` exists.
- Public `Save()` exists.
- `Save()` checks no-save-changes before calling the server.
- `Load()` locks the grid first.
- Save reload passes `Helper.GetOperationReLoading()` into `Load(...)`.
- Handler exposes only `Load` and `Save`.
- Handler has no `SaveOnly`.
- Handler has no `WebApiClient`.
- Handler has no `RunTypeSaveFlow`.
- Handler has no direct publish call.
- Handler has no Load/Save button enable-disable calls.
- Handler uses `Helper.GetTranslator()` and avoids direct `XrmTranslator`.
- Handler uses `app.GetAllRecords()` for save extraction.
- `FillTable()` ends with `Helper.FinalizeGrid(records, app)`.
- Empty base-language Display Text is blocked.
- Non-base Display Text and Description clears are preserved as empty strings.
- Handler uses named helper methods for operation text and placeholders.
- Run formatting for changed JS files.
- Run lint for JS changes if practical.
