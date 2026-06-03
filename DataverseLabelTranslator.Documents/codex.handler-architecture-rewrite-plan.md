# Codex Handler Architecture Rewrite Plan

> Date: 2026-06-03
> Repository: `D:\github\DataverseLabelTranslator`
> Scope: client-side app and handler JavaScript only.
> Server assumption: CRUD, metadata mutation, publish, XML/XAML/package work, and custom action dispatch are already solved elsewhere. JS only calls the custom action and consumes returned data.
> Test assumption: do not design around current unit tests yet. Tests will be rebuilt after the app shape is correct.

## Executive Decision

Rewrite the client architecture around a smaller `EasyTranslator` app shell.

Keep `XrmTranslator` temporarily so the app still loads while the rewrite is happening. New/reworked handler code should call `EasyTranslator`; during transition, `EasyTranslator` may delegate to existing `XrmTranslator` functions until those functions are moved.

The final client model:

```text
EasyTranslator.js
  Small app shell:
  toolbar state, solution/entity/type/component state, language columns,
  grid setup, record flattening, custom action UX messages, and handler dispatch.

Handler JS
  Thin view adapter:
  Load -> call custom action Loading -> FillTable
  Save -> collect changed cells -> validate visible rules
       -> call Saving/Publishing/Published -> reload

XrmTranslator.js
  Legacy shell kept temporarily only so existing app still loads.
  Move behavior out gradually after type 18 works on EasyTranslator.
```

## Current Unit Test Compatibility Check

Static check only; no tests were run.

`codex.apply-js-pattern.md` is not fully compatible with the two existing unit test files as they stand today.

| Test file | Static compatibility issue |
| --- | --- |
| `GlobalOptionSetHandler.test.js` | The harness mocks `XrmTranslator`, not `EasyTranslator`. It also mocks only old helper methods, not `Helper.RunServerLoad`, `Helper.RunServerSaveFlow`, `Helper.FinalizeGrid`, `Helper.ApplyPlaceholder`, `Helper.GetChangedLabels`, or `Helper.ValidateBaseLanguageNotEmpty`. It expects `EnableLoadAndSave()` calls, but the new design keeps Load/Save buttons always enabled. |
| `WebResourceHandler.test.js` | The harness still expects the old client-side Web API flow: `WebApiClient.Retrieve`, `ExecuteChangeSetBatches`, `RunTypeSaveFlow`, and `PublishWebResources`. That test file does not match the custom-action handler pattern. |

Conclusion: do not bend the new docs to keep these tests green. When the UI shape is correct, rewrite tests around `EasyTranslator` and the custom action contract.

## Hard Changes

### Remove All-In-One Completely

Type 0 All-In-One should be removed from the app.

Remove or stop using:

- `AllInOneHandler.js`
- `allInOne` toolbar type item
- `XrmTranslator.showAllInOneType`
- `TYPE_STATE_LABELS.allInOne`
- `SetHandler()` branch for `allInOne`
- help text and README feature text that describes All-In-One
- any app code that swaps `grid.records` into child handlers

There is no reason to preserve `SaveOnly()`.

### Keep Load And Save Buttons Enabled

New code should not disable Load or Save buttons.

Remove from the new pattern:

- `EnableLoadAndSave()`
- `SetLoadButtonDisabled(...)`
- `SetSaveButtonDisabled(...)`
- operation guards whose only purpose is disabling Load/Save

It is still acceptable to show progress text through a grid lock/overlay while a request is running, but button enabled/disabled state is no longer part of the handler contract.

### Ignore Server Implementation

Do not design server services here.

Assume each custom action phase exists and returns the shape the handler expects.

### Defer Unit Tests

Do not make architecture decisions to satisfy the current unit tests. They target old assumptions and should be rewritten later.

## EasyTranslator Transition Plan

Create `DataverseLabelTranslator.WebResource/js/EasyTranslator.js` as the new app shell/facade.

Phase 1: facade mode for type 18.

```js
(function (EasyTranslator, undefined) {
    "use strict";

    function Legacy() {
        return window.XrmTranslator;
    }

    EasyTranslator.GetGrid = function () {
        return Legacy().GetGrid();
    };

    EasyTranslator.GetSolution = function () {
        return Legacy().GetSolution();
    };

    EasyTranslator.GetEntity = function () {
        return Legacy().GetEntity();
    };

    EasyTranslator.GetEntityId = function () {
        return Legacy().GetEntityId();
    };

    EasyTranslator.GetComponent = function () {
        return Legacy().GetComponent();
    };

    EasyTranslator.IsDisplayTextComponent = function () {
        return EasyTranslator.GetComponent() === "DisplayText";
    };

    EasyTranslator.IsDescriptionComponent = function () {
        return EasyTranslator.GetComponent() === "Description";
    };

    EasyTranslator.GetAllRecords = function () {
        return Legacy().GetAllRecords();
    };

    EasyTranslator.AddSummary = function (records, includeSummaryChildren) {
        return Legacy().AddSummary(records, includeSummaryChildren);
    };

    EasyTranslator.LockGrid = function (message) {
        return Legacy().LockGrid(message);
    };

    EasyTranslator.UnlockGrid = function () {
        return Legacy().UnlockGrid();
    };

    EasyTranslator.errorHandler = function (error) {
        return Legacy().errorHandler(error);
    };

    EasyTranslator.GetBaseLanguage = function () {
        return EasyTranslator.baseLanguage || (Legacy() && Legacy().baseLanguage);
    };

    EasyTranslator.SetBaseLanguage = function (languageCode) {
        EasyTranslator.baseLanguage = languageCode;
        if (Legacy()) {
            Legacy().baseLanguage = languageCode;
        }
    };

    EasyTranslator.SetMetadata = function (metadata) {
        EasyTranslator.metadata = metadata;
        if (Legacy()) {
            Legacy().metadata = metadata;
        }
    };

    EasyTranslator.GetMetadata = function () {
        return EasyTranslator.metadata || (Legacy() && Legacy().metadata) || [];
    };
})((window.EasyTranslator = window.EasyTranslator || {}));
```

Phase 2: make type 18 use `EasyTranslator` only.

Phase 3: move shell features from `XrmTranslator` to `EasyTranslator` gradually.

Phase 4: delete old `XrmTranslator` pieces after all toolbar types use `EasyTranslator`.

## What Must Be Removed From Handler JS

Remove these patterns from toolbar handlers:

- direct `WebApiClient.Retrieve`, `Create`, `Update`, `Execute`, `SendBatch`
- `RetrieveLocLabels` and `SetLocLabels`
- metadata PUT/PATCH with `MSCRM.MergeLabels`
- `UpdateOptionValue`
- `PublishXmlRequest`, `PublishAllXmlRequest`, `PublishDashboard`, `PublishWebResources`
- client-side `RunAsBaseLanguage`
- client-side XML/XAML mutation
- translation package export/import from handler code
- `SaveOnly()`
- `XrmTranslator.RunTypeSaveFlow()`
- handler-local copies of generic helper logic

Every rewritten toolbar handler exposes only:

```js
Handler.Load = function () { ... };
Handler.Save = function () { ... };
```

## Handler Contract

Every handler should use `EasyTranslator` and shared `Helper` methods.

```js
(function (Handler, undefined) {
    "use strict";

    var actionName = "GlobalOptionSet";
    var idSeparator = "|";

    function BuildLoadInput() {
        return {
            solutionId: EasyTranslator.GetSolution()
        };
    }

    function FillTable(output) {
        var records = [];
        // Build type-specific w2ui records from output.
        Helper.FinalizeGrid(records);
    }

    function GetUpdates() {
        var records = EasyTranslator.GetAllRecords();
        var updates = [];

        for (var i = 0; i < records.length; i++) {
            var record = records[i];
            if (!record.w2ui || !record.w2ui.changes) {
                continue;
            }

            Helper.ValidateBaseLanguageNotEmpty(record.w2ui.changes, GetRowPath(record));

            var labels = Helper.GetChangedLabels(record.w2ui.changes, true);
            if (labels.length < 1) {
                continue;
            }

            updates.push({
                id: record.recid,
                labels: labels
            });
        }

        return { updates: updates };
    }

    Handler.Load = function () {
        return Helper.RunServerLoad({
            actionName: actionName,
            input: BuildLoadInput(),
            onSuccess: function (output) {
                EasyTranslator.SetMetadata(output.items || []);
                FillTable(output);
            }
        });
    };

    Handler.Save = function () {
        return Helper.RunServerSaveFlow({
            actionName: actionName,
            getSavePayload: GetUpdates,
            getPublishPayload: function (output) {
                return { changedIds: output.changedIds || [] };
            },
            hasPublishTargets: function (payload) {
                return (payload.changedIds || []).length > 0;
            },
            reloadAction: function () {
                return Handler.Load();
            }
        });
    };
})((window.SomeHandler = window.SomeHandler || {}));
```

Rules:

- Standard IIFE only.
- No `window.X = Object(window.X);`
- `"use strict";` first inside IIFE.
- Expose only `Load` and `Save`.
- No Dataverse CRUD in handler JS.
- No button enable/disable calls.

## Helper Contract

`Helper.js` should resolve the active app shell through `EasyTranslator`, with `XrmTranslator` fallback only during transition.

```js
Helper.GetTranslator = function () {
    return window.EasyTranslator || window.XrmTranslator;
};
```

Any existing helper that reads app/grid state must use `Helper.GetTranslator()`. In particular, `Helper.GetLanguageColumnText` should resolve the grid through `EasyTranslator` instead of calling `XrmTranslator.GetGrid()` directly.

Shared helper behavior:

```js
Helper.FinalizeGrid(records, includeSummaryChildren)
Helper.ApplyPlaceholder(record, editablePlaceholder, basePlaceholder)
Helper.ValidateBaseLanguageNotEmpty(changes, rowPath)
Helper.AddLocalizedLabelsToRecord(record, localizedLabels)
Helper.GetChangedLabels(changes, allowEmpty)
Helper.RunServerLoad(options)
Helper.RunServerSaveFlow(options)
```

Important: `Helper.FinalizeGrid` must not call `EnableLoadAndSave()`.

```js
Helper.FinalizeGrid = function (records, includeSummaryChildren) {
    var app = Helper.GetTranslator();
    var grid = app.GetGrid();
    grid.clear();
    app.AddSummary(records, includeSummaryChildren);
    grid.add(records);
    grid.unlock();
};
```

Important: `Helper.RunServerSaveFlow` may lock/unlock the grid for progress text, but it must not disable or enable Load/Save buttons.

## Custom Action Contract

JS calls:

```js
Helper.ExecuteTypedCustomAction(actionName, Helper.CustomActionTypes.Loading, input)
Helper.ExecuteTypedCustomAction(actionName, Helper.CustomActionTypes.Saving, input)
Helper.ExecuteTypedCustomAction(actionName, Helper.CustomActionTypes.Publishing, input)
Helper.ExecuteTypedCustomAction(actionName, Helper.CustomActionTypes.Published, input)
Helper.ExecuteTypedCustomAction(actionName, Helper.CustomActionTypes.Other, input)
```

Expected envelope:

```json
{
  "ok": true,
  "message": "",
  "type": "Loading",
  "object": {}
}
```

The `object` shape is type-specific.

## Type Map

All-In-One is intentionally removed.

| # | Toolbar Type | Handler JS | Action name |
| --- | --- | --- | --- |
| 1 | Attributes | `AttributeHandler.js` | `Attribute` |
| 2 | Option Sets | `OptionSetHandler.js` | `OptionSet` |
| 3 | Forms | `FormHandler.js` | `Form` |
| 4 | Views | `ViewHandler.js` | `View` |
| 5 | Form Metadata | `FormMetaHandler.js` | `FormMetadata` |
| 6 | Entity Metadata | `EntityHandler.js` | `EntityMetadata` |
| 7 | Relationships | `RelationshipHandler.js` | `Relationship` |
| 8 | Charts | `ChartHandler.js` | `Chart` |
| 9 | Business Process Flows | `BpfHandler.js` | `BusinessProcessFlow` |
| 10 | Business Rules | `BusinessRuleHandler.js` | `BusinessRule` |
| 11 | Ribbons | `RibbonHandler.js` | `Ribbon` |
| 12 | Commands | `ModernCommandHandler.js` | `ModernCommand` |
| 13 | Entity Messages | `EntityMessageHandler.js` | `EntityMessage` |
| 14 | Content Snippets | `ContentSnippetHandler.js` | `ContentSnippet` |
| 15 | Sitemap | `SiteMapHandler.js` | `SiteMap` |
| 16 | Dashboards | `DashboardHandler.js` | `Dashboard` |
| 17 | Web Resources | `WebResourceHandler.js` | `WebResource` |
| 18 | Global Option Sets | `GlobalOptionSetHandler.js` | `GlobalOptionSet` |

## Grid Rules

Every record has:

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

Editable placeholders:

| Component | Placeholder | Base-language placeholder |
| --- | --- | --- |
| Display Text | `Add display text` | `Add display text (*)` |
| Description | `Add description` | none |

Readonly empty cells:

```js
record._emptyReadonlyPlaceholder = "-";
```

End every `FillTable()` with:

```js
Helper.FinalizeGrid(records);
```

## Save Rules

Use:

```js
var records = EasyTranslator.GetAllRecords();
```

Do not use:

```js
EasyTranslator.GetGrid().records
```

Validation:

- base-language Display Text cannot be empty
- non-base Display Text can be cleared and must be sent as empty string
- Description can be cleared and must be sent as empty string
- placeholders are UI only and must never be sent

## Type 18 First

Do type 18 first so `EasyTranslator` is proven on the smallest real custom-action-backed handler.

Type 18 requirements:

- handler: `GlobalOptionSetHandler.js`
- action name: `GlobalOptionSet`
- loading input: `{ solutionId: EasyTranslator.GetSolution() }`
- loading output key: `optionSets`
- save payload keys:
  - `component`
  - `optionValueUpdates`
  - `optionSetDescriptionUpdates`
- publish target key: `optionSetNames`
- parent rows readonly in Display Text mode
- parent rows editable only in Description mode
- child rows editable for Display Text and Description

## Implementation Order

1. Remove All-In-One from UI and dispatch.
2. Create `EasyTranslator.js` facade over `XrmTranslator`.
3. Update `App.html` load order so `EasyTranslator.js` is available before rewritten handlers run.
4. Update `Helper.js` to use `Helper.GetTranslator()`.
5. Rewrite type 18 to use `EasyTranslator`.
6. Move required app-shell functions from `XrmTranslator` into `EasyTranslator`.
7. Rewrite remaining handler JS files one by one.
8. Delete unused old APIs from `XrmTranslator.js` after no rewritten code depends on them.

## Acceptance Criteria For A Handler

1. Uses `EasyTranslator`, not direct `XrmTranslator`, except through the temporary facade.
2. Exposes only `Load` and `Save`.
3. Does not call `WebApiClient`.
4. Does not call `RunTypeSaveFlow`.
5. Does not call any publish helper.
6. Does not call `EnableLoadAndSave`, `SetLoadButtonDisabled`, or `SetSaveButtonDisabled`.
7. Uses `Helper.ExecuteTypedCustomAction` through shared load/save flow helpers.
8. Uses `EasyTranslator.GetAllRecords()` for changed rows.
9. Ends `FillTable()` with `Helper.FinalizeGrid(records)`.
10. Blocks empty base-language Display Text.
11. Preserves intentional clears for non-base Display Text and Description.

## Bottom Line

The rewrite is client-side only:

- remove All-In-One
- ignore server implementation details
- do not design around current unit tests
- keep Load and Save buttons always enabled
- introduce `EasyTranslator` as the new small shell
- make type 18 work first on `EasyTranslator`
- move remaining behavior from `XrmTranslator` to `EasyTranslator` gradually
- delete `SaveOnly`
- delete `RunTypeSaveFlow`
- remove direct Dataverse CRUD from handler JavaScript
