# Handler JS Rewrite Convention

Use this document when rewriting any handler JS file to follow the server-owned CRUD convention proven on types 17 (`WebResourceHandler.js`) and 18 (`GlobalOptionSetHandler.js`).

This document focuses on the **client-side JS** rewrite. For server-side convention, see `type-convention-from-global-option-sets.md`.

## Scope Rule

Change only the requested handler file unless the user explicitly approves a shared change.

Shared helpers live in `DataverseLabelTranslator.WebResource/js/Helper.js`. A function belongs in `Helper.js` only when two or more handlers use the exact same behavior. Do not move type-specific row parsing, payload construction, or grid rendering into `Helper.js`.

## Goal

Every handler JS file must:

1. Delegate all CRUD and publish work to the server via `Helper.ExecuteTypedCustomAction`.
2. Keep only two responsibilities: **fill the grid** from server output and **extract changes** from the grid for server writes.
3. Use shared `Helper.*` functions for common patterns (placeholders, validation, grid finalization).
4. Follow a single structural template so that all 16 handler files look the same.

## File Structure

Every handler file must follow this exact structure:

```js
(function (HandlerName, undefined) {
    "use strict";

    var idSeparator = "|";
    var actionName = "ActionName";

    // --- Private functions (type-specific) ---
    // Validation, placeholder, grid building, change extraction

    // --- Public API ---
    HandlerName.Load = function () { ... };
    HandlerName.Save = function () { ... };
})((window.HandlerName = window.HandlerName || {}));
```

Rules:

- Use the standard IIFE pattern: `(function (X, undefined) { ... })((window.X = window.X || {}));`
- Do not use `window.X = Object(window.X);` before the IIFE. This legacy pattern exists in `GlobalOptionSetHandler.js` and `DashboardHandler.js` and must be replaced.
- Use `"use strict";` as the first statement inside the IIFE.
- Declare `actionName` as a constant string matching the server `ActionNames` constant.
- Declare `idSeparator` only if the handler uses composite record IDs.
- Expose only `Load` and `Save` as public methods on the handler object.
- All other functions are private to the IIFE.

## Shared Helper Functions

### Already in `Helper.js`

| Function | Purpose |
|----------|---------|
| `Helper.ExecuteTypedCustomAction(name, type, input)` | Call server custom action with type verification |
| `Helper.GetCustomActionObject(result)` | Read `result.object` safely |
| `Helper.IsEmptyLabelValue(value)` | Check if a label value is null/empty/whitespace |
| `Helper.GetLanguageColumnText(languageCode, grid)` | Get language column display text for error messages |
| `Helper.CustomActionTypes` | Phase name constants: `Loading`, `Saving`, `Publishing`, `Published`, `Other` |

### Must be added to `Helper.js`

These functions are currently duplicated across handlers. Extract them once and use everywhere.

#### `Helper.FinalizeGrid`

Replaces the 4-line ending found in every handler's `FillTable()`.

```js
Helper.FinalizeGrid = function (records, includeSummaryChildren) {
    var grid = XrmTranslator.GetGrid();
    grid.clear();
    XrmTranslator.AddSummary(records, includeSummaryChildren);
    grid.add(records);
    grid.unlock();
    XrmTranslator.EnableLoadAndSave();
};
```

Every handler's `FillTable()` must end with `Helper.FinalizeGrid(records)` instead of repeating the 4 lines.

#### `Helper.ApplyPlaceholder`

Replaces 3 different placeholder implementations. Based on `GlobalOptionSetHandler.ApplyEmptyEditablePlaceholder`.

```js
Helper.ApplyPlaceholder = function (record, editablePlaceholder, basePlaceholder) {
    if (!editablePlaceholder) {
        return;
    }

    record._emptyEditablePlaceholder = editablePlaceholder;

    if (basePlaceholder && XrmTranslator.baseLanguage) {
        record._emptyEditablePlaceholders = record._emptyEditablePlaceholders || {};
        record._emptyEditablePlaceholders[String(XrmTranslator.baseLanguage)] = basePlaceholder;
    }
};
```

#### `Helper.ValidateBaseLanguageNotEmpty`

Replaces 3 different validation implementations. Checks whether a base-language Display Text cell was cleared to empty.

```js
Helper.ValidateBaseLanguageNotEmpty = function (changes, rowPath) {
    if (!XrmTranslator.IsDisplayTextComponent() || !XrmTranslator.baseLanguage) {
        return;
    }

    var base = String(XrmTranslator.baseLanguage);

    if (!Object.prototype.hasOwnProperty.call(changes, base)) {
        return;
    }

    if (!Helper.IsEmptyLabelValue(changes[base])) {
        return;
    }

    throw new Error(
        "Display Text in the base language (" +
            Helper.GetLanguageColumnText(base) +
            ") cannot be empty.\nRow: " +
            (rowPath || "(unknown)")
    );
};
```

Each handler must call this with a type-specific `rowPath`. For example:

- GlobalOptionSet: `optionSetName + " > " + optionValue`
- WebResource: `groupDisplayName + " > " + keyName`
- Dashboard: `dashboardName`
- Attribute: `schemaName`

#### `Helper.AddLocalizedLabelsToRecord`

Populates a grid record's language columns from a `LocalizedLabels` array. Based on `GlobalOptionSetHandler.AddLocalizedLabelsToRecord`.

```js
Helper.AddLocalizedLabelsToRecord = function (record, localizedLabels) {
    localizedLabels = localizedLabels || [];

    for (var i = 0; i < localizedLabels.length; i++) {
        record[localizedLabels[i].LanguageCode.toString()] = localizedLabels[i].Label;
    }
};
```

#### `Helper.GetChangedLabels`

Extracts language code / label pairs from `w2ui.changes`. Based on `GlobalOptionSetHandler.GetChangedLabels`.

```js
Helper.GetChangedLabels = function (changes, allowEmpty) {
    var labels = [];

    for (var change in changes) {
        if (!changes.hasOwnProperty(change)) {
            continue;
        }

        var label = changes[change];

        if (label == null) {
            if (!allowEmpty) {
                continue;
            }

            label = "";
        }

        if (!allowEmpty && !label) {
            continue;
        }

        labels.push({ LanguageCode: change, Label: label });
    }

    return labels;
};
```

#### `Helper.RunServerLoad`

Standardizes the Load pattern including error handling.

```js
Helper.RunServerLoad = function (actionName, input, onSuccess) {
    return Helper.ExecuteTypedCustomAction(actionName, Helper.CustomActionTypes.Loading, input)
        .then(function (result) {
            var output = Helper.GetCustomActionObject(result);
            onSuccess(output);
            return result;
        })
        .catch(function (error) {
            XrmTranslator.UnlockGrid();
            XrmTranslator.errorHandler(error);
        });
};
```

#### `Helper.RunServerSaveFlow`

Standardizes the Save → Publishing → Published → Reload chain.

```js
Helper.RunServerSaveFlow = function (options) {
    var actionName = options.actionName;
    var savePayload = options.savePayload;
    var getChangedIds = options.getChangedIds;
    var publishPayloadKey = options.publishPayloadKey || "changedIds";
    var reloadAction = options.reloadAction;

    if (!savePayload || (typeof savePayload === "object" && Object.keys(savePayload).length === 0)) {
        return Promise.resolve();
    }

    XrmTranslator.LockGrid("Saving ...");

    return Helper.ExecuteTypedCustomAction(actionName, Helper.CustomActionTypes.Saving, savePayload)
        .then(function (result) {
            var output = Helper.GetCustomActionObject(result);
            var ids = getChangedIds(output);

            if (!ids || ids.length === 0) {
                XrmTranslator.UnlockGrid();
                XrmTranslator.EnableLoadAndSave();
                return result;
            }

            XrmTranslator.LockGrid("Publishing ...");
            var publishPayload = {};
            publishPayload[publishPayloadKey] = ids;

            return Helper.ExecuteTypedCustomAction(
                actionName,
                Helper.CustomActionTypes.Publishing,
                publishPayload
            ).then(function (pubResult) {
                var pubOutput = Helper.GetCustomActionObject(pubResult);
                XrmTranslator.LockGrid("Published");
                var publishedPayload = {};
                publishedPayload[publishPayloadKey] = getChangedIds(pubOutput);

                return Helper.ExecuteTypedCustomAction(
                    actionName,
                    Helper.CustomActionTypes.Published,
                    publishedPayload
                );
            });
        })
        .then(function (result) {
            if (!result) {
                return result;
            }

            var output = Helper.GetCustomActionObject(result);
            var ids = getChangedIds(output);

            if (!ids || ids.length === 0) {
                return result;
            }

            XrmTranslator.LockGrid("Re-Loading ...");

            return reloadAction().then(function () {
                XrmTranslator.EnableLoadAndSave();
                return result;
            });
        })
        .catch(function (error) {
            XrmTranslator.UnlockGrid();
            XrmTranslator.EnableLoadAndSave();
            throw error;
        });
};
```

## Handler Template

After `Helper.js` has all shared functions, every handler must follow this template:

```js
(function (HandlerName, undefined) {
    "use strict";

    var idSeparator = "|";
    var actionName = "TypeName";

    // === Type-specific private functions ===

    function GetRowPath(record) {
        // Return a human-readable path for validation error messages
        return record.schemaName || record.recid || "(unknown)";
    }

    function GetUpdates() {
        var records = XrmTranslator.GetAllRecords();
        var updates = [];

        for (var i = 0; i < records.length; i++) {
            var record = records[i];

            if (!record.w2ui || !record.w2ui.changes) {
                continue;
            }

            var changes = record.w2ui.changes;
            Helper.ValidateBaseLanguageNotEmpty(changes, GetRowPath(record));

            var labels = Helper.GetChangedLabels(changes, XrmTranslator.IsDescriptionComponent());

            if (labels.length < 1) {
                continue;
            }

            updates.push({
                id: record.recid,
                labels: labels
            });
        }

        return updates;
    }

    function FillTable(items) {
        var records = [];

        for (var i = 0; i < items.length; i++) {
            var item = items[i];
            var record = {
                recid: item.id,
                schemaName: item.name
            };

            Helper.ApplyPlaceholder(record, "Add display text", "Add display text (*)");
            Helper.AddLocalizedLabelsToRecord(record, item.labels);
            records.push(record);
        }

        Helper.FinalizeGrid(records);
    }

    // === Public API ===

    HandlerName.Load = function () {
        return Helper.RunServerLoad(
            actionName,
            { solutionId: XrmTranslator.GetSolution(), entityName: XrmTranslator.GetEntity() },
            function (output) {
                XrmTranslator.metadata = output.items || [];
                FillTable(XrmTranslator.metadata);
            }
        );
    };

    HandlerName.Save = function () {
        return Helper.RunServerSaveFlow({
            actionName: actionName,
            savePayload: GetUpdates(),
            getChangedIds: function (output) {
                return output.changedIds || [];
            },
            publishPayloadKey: "changedIds",
            reloadAction: function () {
                return HandlerName.Load();
            }
        });
    };
})((window.HandlerName = window.HandlerName || {}));
```

## Load Convention

### Input

Every handler sends at minimum:

```json
{ "solutionId": "guid-or-all" }
```

Entity-scoped handlers also send:

```json
{ "solutionId": "guid", "entityName": "account" }
```

The handler must not send `entityName` if the type is not entity-scoped. The server action decides whether `entityName` is required.

### Server output

The server `Loading` phase returns a JSON object. The handler reads it via `Helper.GetCustomActionObject(result)`. The shape of the object is type-specific, but the handler always transforms it into grid records.

### Grid population

1. Call `Helper.RunServerLoad(actionName, input, onSuccess)`.
2. Inside `onSuccess`, store the output in `XrmTranslator.metadata`.
3. Call `FillTable()` to build grid records from the stored metadata.

### Error handling

`Helper.RunServerLoad` handles errors: unlock the grid and call `XrmTranslator.errorHandler`. The handler must not add its own `.catch()`.

## Save Convention

### Change extraction

1. Call `XrmTranslator.GetAllRecords()` — not `GetGrid().records`. `GetAllRecords` includes tree children; `.records` does not.
2. For each record with `record.w2ui.changes`:
   a. Call `Helper.ValidateBaseLanguageNotEmpty(changes, rowPath)`.
   b. Call `Helper.GetChangedLabels(changes, allowEmpty)` to build label pairs.
   c. Push a type-specific update object.
3. Return the updates as the save payload.

### Validation

Call `Helper.ValidateBaseLanguageNotEmpty` before building the label payload. This must happen in `GetUpdates()`, not in `Save()`. If validation throws, the save stops and the error is shown.

The `rowPath` parameter must include enough context for the user to find the bad cell. Prefer a path such as `parentName > rowName`. Examples:

```text
pl_globalchoice > 222220000
pl_/html/XrmTranslator.html > Title
Dashboard: Active Sales > (row)
```

### Empty value handling

Use `Helper.GetChangedLabels(changes, allowEmpty)`:

- For `Display Text` component: pass `allowEmpty = true` because clearing a non-base-language Display Text is valid and must be saved as empty string.
- For `Description` component: pass `allowEmpty = true` because clearing a description is always valid.
- The base-language check in `ValidateBaseLanguageNotEmpty` has already run before this point.

Do not use `if (!value) continue` to skip empty values. This filters out intentional clears.

### Save flow

1. Call `Helper.RunServerSaveFlow(options)`.
2. The shared function handles the full chain: `Saving → Publishing → Published → Re-Load`.
3. The handler only provides: `actionName`, `savePayload`, `getChangedIds`, `publishPayloadKey`, `reloadAction`.
4. If `savePayload` is empty (no changes), the flow resolves immediately without calling the server.

### Error handling

`Helper.RunServerSaveFlow` handles errors: unlock the grid, enable buttons, rethrow. The handler must not add its own `.catch()`.

## FillTable Convention

### Grid record shape

Every grid record must have at minimum:

```js
{
    recid: "unique-id",
    schemaName: "display-name-for-first-column"
}
```

### Parent-child records

For handlers that display grouped data (option set → option values, BPF → stages → fields):

```js
var parent = {
    recid: parentId,
    schemaName: parentName,
    w2ui: {
        editable: false,    // or true for Description component
        children: []
    }
};

// Non-editable parents with no language values:
parent._emptyReadonlyPlaceholder = "-";

// Editable parents (Description component):
Helper.ApplyPlaceholder(parent, "Add description", null);
Helper.AddLocalizedLabelsToRecord(parent, parentLabels);
```

### Placeholders

Use `Helper.ApplyPlaceholder(record, editablePlaceholder, basePlaceholder)`:

| Component | Editable placeholder | Base placeholder |
|-----------|---------------------|------------------|
| Display Text | `"Add display text"` | `"Add display text (*)"` |
| Description | `"Add description"` | `null` |

For readonly cells (non-editable parent rows in Display Text mode), set:

```js
record._emptyReadonlyPlaceholder = "-";
```

### Language values

Use `Helper.AddLocalizedLabelsToRecord(record, localizedLabels)` to populate language columns from a `LocalizedLabels` array.

### Grid finalization

End every `FillTable()` with `Helper.FinalizeGrid(records)`. Do not call `grid.clear()`, `grid.add()`, `grid.unlock()`, or `XrmTranslator.EnableLoadAndSave()` directly — `Helper.FinalizeGrid` does all four.

## Do Not Use

When rewriting a handler, do NOT use the following patterns from old handlers:

| Old pattern | Replacement |
|-------------|-------------|
| `XrmTranslator.RunTypeSaveFlow(...)` | `Helper.RunServerSaveFlow(...)` |
| `XrmTranslator.ExecuteChangeSetBatches(...)` | Server `Saving` phase |
| `WebApiClient.Retrieve(...)` for loading | Server `Loading` phase |
| `WebApiClient.Update(...)` / `Create(...)` / `SendRequest(...)` for saving | Server `Saving` phase |
| `WebApiClient.Execute(PublishXmlRequest)` | Server `Publishing` phase |
| `XrmTranslator.Publish()` | Server `Publishing` phase |
| `XrmTranslator.RunAsBaseLanguage(...)` | Server handles language switching |
| Local `ApplyChanges(changes, labels)` | Server merges labels |
| Local `IsEmptyLabelValue(value)` | `Helper.IsEmptyLabelValue(value)` |
| Local `GetLanguageColumnText(code)` | `Helper.GetLanguageColumnText(code)` |
| `GetGrid().records` for change extraction | `XrmTranslator.GetAllRecords()` |
| `window.X = Object(window.X);` before IIFE | Remove; use standard IIFE pattern |

## Naming Convention

| Scope | Rule | Example |
|-------|------|---------|
| Public methods | PascalCase, attached to handler | `Handler.Load`, `Handler.Save` |
| Private functions | PascalCase, inside IIFE | `FillTable`, `GetUpdates`, `GetRowPath` |
| Constants | camelCase | `actionName`, `idSeparator` |
| Shared helpers | PascalCase, on `Helper` | `Helper.FinalizeGrid`, `Helper.ApplyPlaceholder` |
| Server action name | PascalCase, singular | `"Attribute"`, `"WebResource"`, `"GlobalOptionSet"` |

## Implementation Checklist

When rewriting a handler to this convention:

1. Read the existing handler and identify all Web API calls, data transforms, and grid logic.
2. Decide what stays in JS (FillTable, GetUpdates) and what moves to server (everything else).
3. Add the server `ICustomAction` implementation with `Loading`, `Saving`, `Publishing`, `Published`, `Other`.
4. Replace the handler JS file using the template above.
5. Replace `FillTable` ending with `Helper.FinalizeGrid(records)`.
6. Replace placeholder logic with `Helper.ApplyPlaceholder(record, text, baseText)`.
7. Replace base language validation with `Helper.ValidateBaseLanguageNotEmpty(changes, rowPath)`.
8. Replace label-to-record population with `Helper.AddLocalizedLabelsToRecord(record, labels)`.
9. Replace change extraction with `Helper.GetChangedLabels(changes, allowEmpty)`.
10. Replace Load body with `Helper.RunServerLoad(...)`.
11. Replace Save body with `Helper.RunServerSaveFlow(...)`.
12. Remove all `WebApiClient.*` calls from the handler.
13. Remove all `RunTypeSaveFlow` usage.
14. Remove all `RunAsBaseLanguage` usage.
15. Remove all local copies of `IsEmptyLabelValue`, `GetLanguageColumnText`, `ApplyChanges`.
16. Use the standard IIFE pattern.
17. Verify: the handler must have exactly 2 public methods (`Load`, `Save`) and no Web API calls.
18. Build the server project.
19. Run `npm --prefix DataverseLabelTranslator.WebResource run lint`.
20. Run `npm --prefix DataverseLabelTranslator.WebResource run format -- <changed-files>`.
21. Deploy server when server code changed.
22. Deploy web resources when JS changed.

## Per-Type Notes

### Type 1: Attributes

- Server action name: `Attribute`.
- Flat grid: one row per attribute.
- Server filters shadow attributes (`AttributeOf`).
- Server handles `SanitizeForPut` and `MergeLabels`.
- Component: `DisplayText` maps to metadata `DisplayName`, `Description` maps to metadata `Description`.

### Type 2: Option Sets (Entity-scoped)

- Server action name: `OptionSet`.
- Parent-child grid: option set → option values.
- Very similar to `GlobalOptionSetHandler`. FillTable and GetUpdates should look nearly identical.
- Server queries all 5 option set types (Picklist, MultiSelect, State, Status, Boolean).
- Component: `DisplayText` maps to option `Label`, `Description` maps to option `Description`.

### Type 3: Forms

- Server action name: `Form`.
- Tree grid: form → tabs → sections → cells.
- Server parses FormXML and returns structured JSON tree.
- Server handles language switching (currently done client-side per installed language).
- `RemoveOverriddenCellLabels` uses `Other` operation.
- Most complex handler. Rewrite last.

### Type 4: Views

- Server action name: `View`.
- Flat grid: one row per view.
- Server queries `savedquery` and calls `RetrieveLocLabels`.
- Component: `DisplayText` maps to `name`, `Description` maps to `description`.

### Type 5: Form Metadata

- Server action name: `FormMeta`.
- Flat grid: one row per form.
- Nearly identical to View. Server queries `systemform` instead of `savedquery`.
- Needs special publish: `PublishDashboard` when entity is `"none"`.

### Type 6: Entity Metadata

- Server action name: `Entity`.
- Only 2 rows: "Display Text" and "Collection Name".
- Simplest handler. Rewrite first.
- Component: `DisplayText` maps to `DisplayName` / `DisplayCollectionName`.

### Type 7: Relationships

- Server action name: `Relationship`.
- Parent-child grid: relationship → label row.
- Server queries 1:N, N:1, N:N relationships.
- Server handles `UseCollectionName` → `UseLabel` switch on save.

### Type 8: Charts

- Server action name: `Chart`.
- Flat grid: one row per chart.
- Nearly identical to View. Server queries `savedqueryvisualization`.

### Type 9: Business Process Flows

- Server action name: `Bpf`.
- 3-level tree grid: BPF → Stage → Field.
- Server parses `clientdata` JSON and returns structured stages/fields.
- Server handles Deactivate → PATCH XAML → Activate cycle with rollback.

### Type 10: Business Rules

- Server action name: `BusinessRule`.
- Parent-child grid: rule → label rows.
- Server parses XAML for `<mcwo:StepLabel>` tags with context detection.
- Server handles Deactivate → PATCH XAML → Activate cycle with rollback.
- Server loads attribute display names for field context.

### Type 12: Commands (Modern)

- Server action name: `ModernCommand`.
- Parent-child grid: command → property rows (Text, Title, Description, Accessibility, Group Title).
- Server queries `appactions` with solution filtering and calls `RetrieveLocLabels` per property.
- Server builds command hierarchy path for display.

### Type 13: Entity Messages

- Server action name: `EntityMessage`.
- Flat grid: one row per display string.
- Server handles translation package export, Excel parsing, row matching, modification, and import.
- Unique data source (not metadata labels).

### Type 14: Content Snippets

- Server action name: `ContentSnippet`.
- Parent-child grid: website → snippet rows.
- Uses portal languages, not CRM installed languages.
- Server discovers portal languages and returns column configuration.
- JS may need to call `XrmTranslator.ClearColumns()` and set portal language columns.

### Type 15: Sitemap

- Server action name: `SiteMap`.
- Parent-child grid: sitemap → Areas/Groups/SubAreas.
- Server parses SiteMap XML and returns structured node tree.
- Server handles XML label updates and `PublishAllXml`.

### Type 16: Dashboards

- Server action name: `Dashboard`.
- Flat grid: one row per dashboard.
- Server queries solution-filtered dashboards with `RetrieveLocLabels`.
- Server handles `PublishDashboard` instead of `PublishXml`.
- Simple handler similar to Entity.
