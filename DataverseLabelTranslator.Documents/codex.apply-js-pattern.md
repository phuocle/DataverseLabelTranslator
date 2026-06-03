# Codex Apply JS Handler Pattern

Use this file as an instruction prompt for an AI agent when rewriting one toolbar handler JavaScript file.

Example:

```text
Read DataverseLabelTranslator.Documents/codex.apply-js-pattern.md and work on toolbar type 17.
```

The agent should rewrite only the target handler to the current `EasyTranslator` convention, using type 18 (`GlobalOptionSetHandler.js`) as the reference implementation.

## Scope

Work on client-side JavaScript only.

Do not implement or analyze server CRUD.
Do not implement or analyze JS/C# unit tests.
Assume the custom action already exists and returns the expected data.

Allowed files:

- target handler JS file
- `DataverseLabelTranslator.WebResource/js/Helper.js` only if shared helper functions are missing
- `DataverseLabelTranslator.WebResource/js/EasyTranslator.js` only if the facade is missing
- `DataverseLabelTranslator.WebResource/html/App.html` only if `EasyTranslator.js` must be loaded
- `DataverseLabelTranslator.WebResource/js/XrmTranslator.js` only for temporary fallback/delegation or dispatch glue

Do not modify server files unless the user explicitly asks.

## Unit Tests

Static check only; do not run tests for this instruction.

The current tests are not the source of truth for this rewrite. Tests will be rewritten later after the app architecture is correct.

## Button Rule

Load and Save buttons stay enabled.

Do not add calls to:

- `EnableLoadAndSave`
- `SetLoadButtonDisabled`
- `SetSaveButtonDisabled`

Grid lock/unlock is allowed for progress text. It must not be used as button-state logic.

## Toolbar Map

All-In-One is removed. Do not work on type 0.

| # | Toolbar Type | Handler JS | Handler object | Action name |
| --- | --- | --- | --- | --- |
| 1 | Attributes | `AttributeHandler.js` | `AttributeHandler` | `Attribute` |
| 2 | Option Sets | `OptionSetHandler.js` | `OptionSetHandler` | `OptionSet` |
| 3 | Forms | `FormHandler.js` | `FormHandler` | `Form` |
| 4 | Views | `ViewHandler.js` | `ViewHandler` | `View` |
| 5 | Form Metadata | `FormMetaHandler.js` | `FormMetaHandler` | `FormMetadata` |
| 6 | Entity Metadata | `EntityHandler.js` | `EntityHandler` | `EntityMetadata` |
| 7 | Relationships | `RelationshipHandler.js` | `RelationshipHandler` | `Relationship` |
| 8 | Charts | `ChartHandler.js` | `ChartHandler` | `Chart` |
| 9 | Business Process Flows | `BpfHandler.js` | `BpfHandler` | `BusinessProcessFlow` |
| 10 | Business Rules | `BusinessRuleHandler.js` | `BusinessRuleHandler` | `BusinessRule` |
| 11 | Ribbons | `RibbonHandler.js` | `RibbonHandler` | `Ribbon` |
| 12 | Commands | `ModernCommandHandler.js` | `ModernCommandHandler` | `ModernCommand` |
| 13 | Entity Messages | `EntityMessageHandler.js` | `EntityMessageHandler` | `EntityMessage` |
| 14 | Content Snippets | `ContentSnippetHandler.js` | `ContentSnippetHandler` | `ContentSnippet` |
| 15 | Sitemap | `SiteMapHandler.js` | `SiteMapHandler` | `SiteMap` |
| 16 | Dashboards | `DashboardHandler.js` | `DashboardHandler` | `Dashboard` |
| 17 | Web Resources | `WebResourceHandler.js` | `WebResourceHandler` | `WebResource` |
| 18 | Global Option Sets | `GlobalOptionSetHandler.js` | `GlobalOptionSetHandler` | `GlobalOptionSet` |

## Translator Rule

Handler code should resolve app state through:

```js
var app = Helper.GetTranslator();
```

`Helper.GetTranslator()` returns `EasyTranslator` when available and falls back to `XrmTranslator` while migration is incomplete.

Do not call `XrmTranslator` directly from a rewritten handler unless the requested handler cannot work without a temporary compatibility fallback.

## Handler Responsibilities

Handler JS may do only:

1. Build load payload from app state.
2. Call custom action phase `Loading`.
3. Store returned data with `app.SetMetadata(...)` if useful.
4. Build w2ui grid records from custom action output.
5. Collect changed cells from `app.GetAllRecords()`.
6. Validate base-language Display Text.
7. Build save payload.
8. Call custom action phases `Saving`, `Publishing`, `Published`.
9. Reload after successful publish when the server returns publish targets.

Handler JS must not:

- call `WebApiClient`
- call `XrmTranslator.RunTypeSaveFlow`
- call `XrmTranslator.ExecuteChangeSetBatches`
- call any direct publish helper
- call `XrmTranslator.RunAsBaseLanguage`
- mutate Dataverse XML/XAML/package data locally
- expose `SaveOnly`
- disable Load or Save buttons

For type 17 specifically, remove direct Web API save/publish behavior from `WebResourceHandler.js`; the handler should call the `WebResource` custom action phases and trust the returned data.

## Shared Text Convention

All reusable UI text for the rewritten handler lives in `Helper.js`.

Current `Helper.UiText` shape:

```js
Helper.UiText = {
    Placeholders: {
        DisplayText: "Add-display-text",
        DisplayTextBase: "Add-display-text(*)",
        Description: "Add-description",
        Readonly: "-"
    },
    Operations: {
        Loading: "Loading ...",
        Saving: "Saving ...",
        Publishing: "Publishing ...",
        Published: "Published",
        ReLoading: "Re-Loading ..."
    }
};
```

Do not pass text keys from handlers. Use named helper methods:

```js
Helper.GetPlaceholderDisplayText();
Helper.GetPlaceholderDisplayTextBase();
Helper.GetPlaceholderDescription();
Helper.GetPlaceholderReadonly();

Helper.GetOperationLoading();
Helper.GetOperationSaving();
Helper.GetOperationPublishing();
Helper.GetOperationPublished();
Helper.GetOperationReLoading();
```

## Component Convention

Handlers should only use these component names:

```js
Helper.ComponentTypes.DisplayText;
Helper.ComponentTypes.Description;
```

Do not use business wording `"Label"` in the handler.

If Dataverse metadata uses a physical property named `Label`, hide that mapping inside `Helper.js`, for example:

```js
Helper.GetComponentLocalizedLabels = function (metadata, component) {
    var metadataProperty = component === Helper.ComponentTypes.Description ? "Description" : "Label";
    var localizedLabelContainer = metadata && metadata[metadataProperty] ? metadata[metadataProperty] : {};

    return localizedLabelContainer.LocalizedLabels || [];
};
```

The target handler should ask for Display Text or Description only.

## Required Helper APIs

Use existing helper functions when present. Add missing ones to `Helper.js`.

Current expected helper APIs:

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
```

`Helper.FinalizeGrid(records, app)` assumes the handler already called `grid.clear()` when appropriate.

`Helper.ValidateBaseLanguageNotEmpty(...)` receives the record and changes:

```js
Helper.ValidateBaseLanguageNotEmpty(record, changes, {
    app: app,
    getRowPath: function (changedRecord) {
        return GetRowPath(changedRecord);
    }
});
```

## Load Flow

Use `getPayload` and `onLoaded`.

```js
HandlerName.Load = function () {
    var app = Helper.GetTranslator();
    app.LockGrid(Helper.GetOperationLoading());

    return Helper.RunServerLoad({
        app: app,
        actionName: actionName,
        getPayload: function () {
            return {
                solutionId: app.GetSolution(),
                entityName: app.GetEntity(),
                entityId: app.GetEntityId(),
                component: app.GetComponent()
            };
        },
        onLoaded: function (output) {
            app.SetMetadata(output.items || []);
            FillTable();
        }
    });
};
```

## Save Flow

Use `getPublishPayload`, `getPublishedPayload`, and `shouldReload`.

If there is no publish target, return `null` from `getPublishPayload`.

```js
function GetPublishPayload(output) {
    var changedIds = (output && output.changedIds) || [];
    return changedIds.length > 0 ? { changedIds: changedIds } : null;
}

HandlerName.Save = function () {
    var app = Helper.GetTranslator();

    return Helper.RunServerSaveFlow({
        app: app,
        actionName: actionName,
        getSavePayload: GetUpdates,
        getPublishPayload: GetPublishPayload,
        getPublishedPayload: GetPublishPayload,
        shouldReload: function (output) {
            return !!(output && output.changedIds && output.changedIds.length > 0);
        },
        reloadAction: function () {
            return HandlerName.Load();
        }
    });
};
```

`Helper.RunServerSaveFlow(...)` owns the lock text and uses:

- `Helper.GetOperationSaving()`
- `Helper.GetOperationPublishing()`
- `Helper.GetOperationPublished()`
- `Helper.GetOperationReLoading()`

## Standard Handler Skeleton

```js
(function (HandlerName, undefined) {
    "use strict";

    var idSeparator = "|";
    var actionName = "ActionName";

    function GetComponent(app) {
        return app.IsDescriptionComponent() ? Helper.ComponentTypes.Description : Helper.ComponentTypes.DisplayText;
    }

    function GetRowPath(record) {
        return (record && (record.schemaName || record.recid)) || "(unknown)";
    }

    function FillTable() {
        var app = Helper.GetTranslator();
        var grid = app.GetGrid();
        grid.clear();

        var records = [];
        var metadata = app.GetMetadata();
        var component = GetComponent(app);

        for (var i = 0; i < metadata.length; i++) {
            var item = metadata[i];
            var record = {
                recid: item.id,
                schemaName: item.name
            };

            Helper.ApplyPlaceholder(
                record,
                Helper.GetPlaceholderDisplayText(),
                Helper.GetPlaceholderDisplayTextBase(),
                app
            );
            Helper.AddLocalizedLabelsToRecord(record, Helper.GetComponentLocalizedLabels(item, component));
            records.push(record);
        }

        Helper.FinalizeGrid(records, app);
    }

    function GetUpdates() {
        var app = Helper.GetTranslator();
        var records = app.GetAllRecords();
        var updates = [];
        var component = GetComponent(app);
        var allowEmpty = app.IsDisplayTextComponent() || app.IsDescriptionComponent();

        for (var i = 0; i < records.length; i++) {
            var record = records[i];
            if (!record.w2ui || !record.w2ui.changes) {
                continue;
            }

            Helper.ValidateBaseLanguageNotEmpty(record, record.w2ui.changes, {
                app: app,
                getRowPath: GetRowPath
            });

            var labels = Helper.GetChangedLabels(record.w2ui.changes, allowEmpty);
            if (labels.length < 1) {
                continue;
            }

            updates.push({
                id: record.recid,
                labels: labels
            });
        }

        return {
            component: component,
            updates: updates
        };
    }

    function GetPublishPayload(output) {
        var changedIds = (output && output.changedIds) || [];
        return changedIds.length > 0 ? { changedIds: changedIds } : null;
    }

    HandlerName.Load = function () {
        var app = Helper.GetTranslator();
        app.LockGrid(Helper.GetOperationLoading());

        return Helper.RunServerLoad({
            app: app,
            actionName: actionName,
            getPayload: function () {
                return {
                    solutionId: app.GetSolution(),
                    entityName: app.GetEntity(),
                    entityId: app.GetEntityId(),
                    component: app.GetComponent()
                };
            },
            onLoaded: function (output) {
                app.SetMetadata(output.items || []);
                FillTable();
            }
        });
    };

    HandlerName.Save = function () {
        var app = Helper.GetTranslator();

        return Helper.RunServerSaveFlow({
            app: app,
            actionName: actionName,
            getSavePayload: GetUpdates,
            getPublishPayload: GetPublishPayload,
            getPublishedPayload: GetPublishPayload,
            shouldReload: function (output) {
                return !!(output && output.changedIds && output.changedIds.length > 0);
            },
            reloadAction: function () {
                return HandlerName.Load();
            }
        });
    };
})((window.HandlerName = window.HandlerName || {}));
```

Adjust only type-specific names, record IDs, output keys, row tree shape, and save payload shape.

## Type 18 Reference Pattern

For toolbar type 18:

```text
Handler file: DataverseLabelTranslator.WebResource/js/GlobalOptionSetHandler.js
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

Current type 18 rules:

- `GlobalOptionSetHandler.js` has no direct `XrmTranslator` calls.
- `GlobalOptionSetHandler.js` has no business literal `"Label"`.
- The handler uses `Helper.ComponentTypes.DisplayText` and `Helper.ComponentTypes.Description`.
- Dataverse physical `Label` mapping is hidden in `Helper.GetComponentLocalizedLabels(...)`.
- Placeholder calls use named methods such as `Helper.GetPlaceholderDescription()`.
- Operation text uses named methods such as `Helper.GetOperationLoading()`.
- Save payload sends `component` at the top level. `optionValueUpdates` items do not repeat `component`.

Display Text mode:

- parent row is readonly
- parent row empty cells show `Helper.GetPlaceholderReadonly()`
- child rows are editable
- child labels come through `Helper.GetComponentLocalizedLabels(option, Helper.ComponentTypes.DisplayText)`
- base-language empty child Display Text is blocked

Description mode:

- parent row is editable for option set description
- child rows are editable for option descriptions
- parent labels come through `Helper.GetComponentLocalizedLabels(optionSet, Helper.ComponentTypes.Description)`
- child labels come through `Helper.GetComponentLocalizedLabels(option, Helper.ComponentTypes.Description)`
- clearing descriptions is valid

## Type 17 Notes

For toolbar type 17:

```text
Handler file: DataverseLabelTranslator.WebResource/js/WebResourceHandler.js
Handler object: WebResourceHandler
Action name: WebResource
```

When rewriting type 17:

- Keep the existing user-facing behavior: web resource parent/group rows with child key rows.
- Move direct `WebApiClient`, publish, and batch save behavior out of the handler.
- Use custom action phases `Loading`, `Saving`, `Publishing`, `Published`.
- Trust custom action output for groups/resources/publish targets.
- Use the shared placeholder and operation methods from `Helper.js`.
- Use `Helper.ComponentTypes.DisplayText` / `Description`; do not introduce `"Label"` as handler vocabulary.
- Put `component` once at the save payload top level unless the server contract explicitly requires per-item component values.

## Final Checks

Before finishing:

1. Handler exposes only `Load` and `Save`.
2. Handler has no `SaveOnly`.
3. Handler has no `WebApiClient`.
4. Handler has no `RunTypeSaveFlow`.
5. Handler has no direct publish call.
6. Handler uses `Helper.GetTranslator()` and avoids direct `XrmTranslator`.
7. Handler does not call `EnableLoadAndSave`, `SetLoadButtonDisabled`, or `SetSaveButtonDisabled`.
8. Handler uses `app.GetAllRecords()` for save extraction.
9. `FillTable()` ends with `Helper.FinalizeGrid(records, app)`.
10. Empty base-language Display Text is blocked.
11. Non-base Display Text and Description clears are preserved as empty strings.
12. Handler uses named text helpers, not string-key text helpers.
13. Handler does not use business literal `"Label"`.
14. Save payload does not duplicate `component` at both parent and item level.
15. Run formatting for changed JS files.
16. Run lint for JS changes if practical.
