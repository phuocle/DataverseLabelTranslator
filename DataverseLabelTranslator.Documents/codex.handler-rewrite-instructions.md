# Codex Handler Rewrite Instructions

This is the single instruction file for rewriting one Dataverse Label Translator toolbar handler.
It replaces:

- `DataverseLabelTranslator.Documents/codex.apply-js-pattern.md`
- `DataverseLabelTranslator.Documents/codex.handler-architecture-rewrite-plan.md`

Use this file when the user points to a numbered toolbar type, for example:

```text
Read DataverseLabelTranslator.Documents/codex.handler-rewrite-instructions.md,
then rewrite type 1.
```

The AI must be able to read this file plus the requested type number and do the full task end to end.

## First Read

Always read `AGENTS.md` first. It is the source of truth for repository rules, toolbar numbering, quality gates, deployment commands, and final response requirements.

Resolve the requested `type x` through the toolbar map below before touching code. These numbers are stable shorthand; do not reorder, renumber, or reuse them.

## Toolbar Type Map

Types 15, 16, 17, and 18 are already completed server-backed reference types. Do not rewrite them again unless the user explicitly asks for a fix or rework. Use them as implementation references for the remaining types.

| # | Status | Toolbar Type | Handler JS | Test File |
| --- | --- | --- | --- | --- |
| 1 | To rewrite | Attributes | `DataverseLabelTranslator.WebResource/js/Handler/AttributeHandler.js` | None yet |
| 2 | To rewrite | Option Sets | `DataverseLabelTranslator.WebResource/js/Handler/OptionSetHandler.js` | None yet |
| 3 | To rewrite | Forms | `DataverseLabelTranslator.WebResource/js/Handler/FormHandler.js` | None yet |
| 4 | To rewrite | Views | `DataverseLabelTranslator.WebResource/js/Handler/ViewHandler.js` | None yet |
| 5 | To rewrite | Form Metadata | `DataverseLabelTranslator.WebResource/js/Handler/FormMetaHandler.js` | None yet |
| 6 | To rewrite | Entity Metadata | `DataverseLabelTranslator.WebResource/js/Handler/EntityHandler.js` | None yet |
| 7 | To rewrite | Relationships | `DataverseLabelTranslator.WebResource/js/Handler/RelationshipHandler.js` | None yet |
| 8 | To rewrite | Charts | `DataverseLabelTranslator.WebResource/js/Handler/ChartHandler.js` | None yet |
| 9 | To rewrite | Business Process Flows | `DataverseLabelTranslator.WebResource/js/Handler/BpfHandler.js` | None yet |
| 10 | To rewrite | Business Rules | `DataverseLabelTranslator.WebResource/js/Handler/BusinessRuleHandler.js` | None yet |
| 11 | To rewrite | Ribbons | `DataverseLabelTranslator.WebResource/js/Handler/RibbonHandler.js` | None yet |
| 12 | To rewrite | Commands | `DataverseLabelTranslator.WebResource/js/Handler/ModernCommandHandler.js` | None yet |
| 13 | To rewrite | Entity Messages | `DataverseLabelTranslator.WebResource/js/Handler/EntityMessageHandler.js` | None yet |
| 14 | To rewrite | Content Snippets | `DataverseLabelTranslator.WebResource/js/Handler/ContentSnippetHandler.js` | None yet |
| 15 | Done reference | Sitemap | `DataverseLabelTranslator.WebResource/js/Handler/SiteMapHandler.js` | None yet |
| 16 | Done reference | Dashboards | `DataverseLabelTranslator.WebResource/js/Handler/DashboardHandler.js` | None yet |
| 17 | Done reference | Web Resources | `DataverseLabelTranslator.WebResource/js/Handler/WebResourceHandler.js` | `DataverseLabelTranslator.WebResource/tests/WebResourceHandler.test.js` |
| 18 | Done reference | Global Option Sets | `DataverseLabelTranslator.WebResource/js/Handler/GlobalOptionSetHandler.js` | `DataverseLabelTranslator.WebResource/tests/GlobalOptionSetHandler.test.js` |

Type 16 dashboards intentionally show only dashboard parent rows. Do not load dashboard tabs, sections, or cells into type 16. Parent dashboard rows are editable directly across language columns. `DataverseLabelTranslator.WebResource/js/Handler/FormHandler.js` is only for type 3 Forms.

## Completed Reference Types

Use type 18 as the primary reference because it demonstrates component-aware display text and description saves. Use types 15, 16, and 17 for their specialized shapes.

### Type 15: Sitemap

```text
Handler: DataverseLabelTranslator.WebResource/js/Handler/SiteMapHandler.js
Object: SiteMapHandler
Action: SiteMap
Load input: { solutionId }
Load output key: sitemaps
Save payload key: sitemapUpdates
Publish key: sitemapIds
Server: DataverseLabelTranslator.Server/CustomActions/Synchronous/SiteMap.cs
```

Sitemap handler code may parse loaded sitemap XML to build rows, but Dataverse write and publish behavior must stay on the Server side.

### Type 16: Dashboards

```text
Handler: DataverseLabelTranslator.WebResource/js/Handler/DashboardHandler.js
Object: DashboardHandler
Action: Dashboard
Load input: { solutionId }
Load output keys: dashboards, baseLanguage
Save payload key: dashboardUpdates
Publish key: dashboardIds
Server: DataverseLabelTranslator.Server/CustomActions/Synchronous/Dashboard.cs
```

Dashboard grids show only dashboard parent rows. Do not treat dashboard tabs, sections, or cells as type 16 records.

### Type 17: Web Resources

```text
Handler: DataverseLabelTranslator.WebResource/js/Handler/WebResourceHandler.js
Object: WebResourceHandler
Action: WebResource
Load input: { solutionId }
Load output keys: groups, baseLanguage
Save payload key: resourceChanges
Publish key: webresourceIds
Server: DataverseLabelTranslator.Server/CustomActions/Synchronous/WebResource.cs
```

Web resource rows group localized resources by base key. Create/update localized resources through the Server action, not through client-side `WebApiClient`.

### Type 18: Global Option Sets

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
Server: DataverseLabelTranslator.Server/CustomActions/Synchronous/GlobalOptionSet.cs
```

Type 18 specific behavior to preserve where applicable:

- no-change Save shows `DialogHelper.alert(...)`, not a banner
- `Load(lockText)` locks the grid with `lockText || Helper.GetOperationLoading()`
- save reload calls `Load(Helper.GetOperationReLoading())`
- save payload sends `component` once at top level
- child update items do not repeat `component`
- Display Text parent rows are readonly
- Description parent rows are editable when the metadata supports descriptions

## Non-Negotiable Handler Contract

Every rewritten handler must have exactly this public shape:

1. Private `GetUpdates()` function.
2. Private `FillTable()` function.
3. Public `Load(lockText)` function.
4. Public `Save()` function.

`Load(lockText)` must:

- resolve the app with `var app = Helper.GetTranslator();`
- lock the grid first with `app.LockGrid(lockText || Helper.GetOperationLoading());`
- call custom action phase `Loading` through `Helper.RunServerLoad(...)`
- store returned data with `app.SetMetadata(...)` or an existing compatible metadata setter when useful
- fill the grid through private `FillTable()`

Standard load shape:

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

`Save()` must:

- call private `GetUpdates()` first
- check no-save-changes before calling the server
- show `DialogHelper.alert(...)` for no-save-changes
- avoid the default no-change status banner
- call custom action phases `Saving`, `Publishing`, and `Published` through `Helper.RunServerSaveFlow(...)`
- reload with `HandlerName.Load(Helper.GetOperationReLoading())`

Standard save shape:

```js
HandlerName.Save = function () {
    var updates = GetUpdates();

    if (HasNoChanges(updates)) {
        return DialogHelper.alert(userText.noChangesToSave, {
            title: userText.title
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
- Do not expose `SaveOnly`.
- Do not call `EnableLoadAndSave`, `SetLoadButtonDisabled`, or `SetSaveButtonDisabled` from handlers.
- If button state must be restored after a dialog, fix core app code such as `XrmTranslator.js`, not the handler.

## User-Facing Strings

Temporary rule until the app has a base-language translation layer:

- Move every string shown to the user to declarations at the top of the handler `.js` file.
- Put these declarations directly after `"use strict";` and before helper functions.
- A `var` object, individual `var` variables, or `const` declarations are acceptable. Match the file's current JavaScript style.
- Do not leave inline user-facing literals inside `DialogHelper.alert(...)`, lock/status messages, validation text, dialog titles, button labels, or other UI output.
- Do not introduce a new translation framework yet.
- Do not move protocol/internal strings such as `actionName`, DTO property names, component keys, IDs, separators, or Dataverse metadata keys unless they are also displayed to the user.

Example:

```js
(function (HandlerName, undefined) {
    "use strict";

    var userText = {
        title: "Handler Title",
        noChangesToSave: "There are no changes to save.",
        unknownRow: "(unknown)"
    };

    var actionName = "ActionName";
    var idSeparator = "|";
```

## Server Contract

When the prompt says `type x`, do not assume the Server project already supports that type.

Before or while rewriting the handler:

1. Resolve `type x` from the toolbar map in this file and `AGENTS.md`.
2. Inspect the target handler's `actionName` or custom action name.
3. Inspect `DataverseLabelTranslator.Server/CustomActions/ActionNames.cs`.
4. Inspect `DataverseLabelTranslator.Server/CustomActions/PostDataverseLabelTranslatorCustomActionSynchronous.cs`.
5. Inspect `DataverseLabelTranslator.Server/CustomActions/Synchronous/`.
6. If Server custom action support for that type is missing, create it.

Legacy client-side behavior is not Server support. A type is Server-backed only when all of these exist:

- an `ActionNames` constant matching the handler `actionName`
- dispatcher wiring in `PostDataverseLabelTranslatorCustomActionSynchronous`
- a matching `Synchronous/<TypeName>.cs` implementation
- implemented `ICustomAction` phases: `Loading`, `Saving`, `Publishing`, `Published`, and `Other`

Treat handlers that still work through `WebApiClient`, `XrmTranslator.RunTypeSaveFlow`, `XrmTranslator.ExecuteChangeSetBatches`, or direct publish helpers as missing Server support unless the matching Server pieces above already exist.

Required Server shape for a new type:

- add an `ActionNames` constant whose value matches the handler `actionName`
- add dispatcher wiring in `PostDataverseLabelTranslatorCustomActionSynchronous`
- create `DataverseLabelTranslator.Server/CustomActions/Synchronous/<TypeName>.cs`
- implement `ICustomAction` phases: `Loading`, `Saving`, `Publishing`, `Published`, and `Other`
- define phase input/output DTOs in the same file unless the project already has a stronger local pattern
- return the standard custom action response envelope through the existing dispatcher
- keep Dataverse retrieval, metadata mutation, label merge, and publish behavior on the Server side
- keep handler payload and Server DTO names/properties aligned exactly

Do not leave a rewritten handler calling a custom action that does not exist in `DataverseLabelTranslator.Server`.

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

## Architecture Direction

Rewrite handlers as thin type-specific adapters around a smaller app shell.

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

## Scope

Work on the requested handler flow. Client-side JavaScript is the default, but Server code is required when the requested toolbar type does not already have a matching Server custom action.

Allowed files:

- target handler under `DataverseLabelTranslator.WebResource/js/Handler/`
- `DataverseLabelTranslator.WebResource/js/Helper.js` only if shared helper functions are missing or need correction
- `DataverseLabelTranslator.WebResource/js/EasyTranslator.js` only if the facade is missing or needs glue
- `DataverseLabelTranslator.WebResource/js/XrmTranslator.js` only for dispatch/core fallback glue
- `DataverseLabelTranslator.WebResource/html/App.html` only when script load order must change
- `DataverseLabelTranslator.Server/CustomActions/ActionNames.cs` when adding a missing Server action
- `DataverseLabelTranslator.Server/CustomActions/PostDataverseLabelTranslatorCustomActionSynchronous.cs` when adding dispatcher wiring
- `DataverseLabelTranslator.Server/CustomActions/Synchronous/<TypeName>.cs` when adding the missing Server implementation

Do not modify unrelated Server files. Do modify the Server files above when `type x` has no Server implementation yet.

Do not implement or analyze JS/C# unit tests for this instruction unless the user explicitly asks for tests. Do not design the rewrite around current unit tests.

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

Use existing helper functions when present. Add missing helper functions to `Helper.js` only when they are needed by the requested rewrite.

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

Helper.GetPlaceholderDisplayText();
Helper.GetPlaceholderDisplayTextBase();
Helper.GetPlaceholderDescription();
Helper.GetPlaceholderReadonly();
```

Important:

- `Helper.FinalizeGrid(records, app)` must not enable or disable buttons.
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

## Implementation Order For A Type

1. Read `AGENTS.md`.
2. Resolve the requested type number through the toolbar map.
3. Read the target handler.
4. Read the completed reference handler that best matches the target shape, usually type 18.
5. Inspect `ActionNames.cs`, dispatcher wiring, and `Synchronous/` to confirm whether the type already has Server support.
6. If missing, add Server custom action support before finishing the rewrite.
7. Identify target handler load payload, load output key, save payload shape, and publish key.
8. Create or normalize private `FillTable()`.
9. Create or normalize private `GetUpdates()`.
10. Make public `Load(lockText)` lock the grid first and call `Helper.RunServerLoad(...)`.
11. Make public `Save()` check no-save-changes first.
12. Move all user-facing strings to top-of-file declarations.
13. Make save flow call `Helper.RunServerSaveFlow(...)`.
14. Make reload call `Load(Helper.GetOperationReLoading())`.
15. Remove direct Dataverse CRUD, direct publish, and local metadata mutation from the handler.
16. Run formatting for changed JS files when JS files changed:
    `npm --prefix DataverseLabelTranslator.WebResource run format -- <changed-files>`
17. Run lint after JavaScript changes when practical:
    `npm --prefix DataverseLabelTranslator.WebResource run lint`
18. Deploy changed web resources when files under `DataverseLabelTranslator.WebResource/html`, `css`, `js`, or `img` changed:
    `DataverseLabelTranslator.WebResource\deploy.debug.bat`
19. Deploy Server components when files under `DataverseLabelTranslator.Server` changed:
    `DataverseLabelTranslator.Server\deploy.debug.bat`
20. Report only useful verification/deployment results and any blocker.

Do not run unit tests for this instruction unless the user explicitly asks. If the user asks for tests or coverage, follow `AGENTS.md` and the relevant `.agents/skills/` workflow.

## Standard Skeleton

```js
(function (HandlerName, undefined) {
    "use strict";

    var userText = {
        title: "Handler Title",
        noChangesToSave: "There are no changes to save.",
        unknownRow: "(unknown)"
    };

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
        return (record && (record.schemaName || record.recid)) || userText.unknownRow;
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
            return DialogHelper.alert(userText.noChangesToSave, {
                title: userText.title
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

## Final Checklist

- [ ] `AGENTS.md` was read first.
- [ ] Requested type number was resolved through the toolbar map.
- [ ] If the requested type is 15, 16, 17, or 18, current completed code was inspected before deciding whether any change is actually needed.
- [ ] Target handler file was read before editing.
- [ ] Type 18 or the closest completed reference type was read before editing.
- [ ] Server support was checked in `ActionNames.cs`, dispatcher wiring, and `Synchronous/`.
- [ ] Missing Server action support was added when required.
- [ ] Handler has private `GetUpdates()`.
- [ ] Handler has private `FillTable()`.
- [ ] Handler has public `Load(lockText)`.
- [ ] Handler has public `Save()`.
- [ ] `Load()` locks the grid before server loading.
- [ ] `Save()` checks no-save-changes before any server call.
- [ ] No-save Save uses `DialogHelper.alert(...)`.
- [ ] Save reload passes `Helper.GetOperationReLoading()` into `Load(...)`.
- [ ] Handler exposes only `Load` and `Save`.
- [ ] Handler has no `SaveOnly`.
- [ ] Handler has no `WebApiClient`.
- [ ] Handler has no `XrmTranslator.RunTypeSaveFlow`.
- [ ] Handler has no `XrmTranslator.ExecuteChangeSetBatches`.
- [ ] Handler has no direct publish call.
- [ ] Handler has no Load/Save button enable-disable calls.
- [ ] Handler uses `Helper.GetTranslator()` and avoids direct `XrmTranslator` state access.
- [ ] Handler uses `app.GetAllRecords()` for save extraction.
- [ ] `FillTable()` ends with `Helper.FinalizeGrid(records, app)`.
- [ ] Empty base-language Display Text is blocked.
- [ ] Non-base Display Text clears are preserved as empty strings.
- [ ] Description clears are preserved as empty strings.
- [ ] Placeholders are never sent as save values.
- [ ] Handler uses named helper methods for operation text and placeholders.
- [ ] User-facing strings are declared at the top of the handler `.js` file.
- [ ] Handler and Server DTO payload names match exactly.
- [ ] Changed JS files were formatted with the targeted format command.
- [ ] Lint was run after JavaScript changes when practical.
- [ ] Changed web resources were deployed with `DataverseLabelTranslator.WebResource\deploy.debug.bat`.
- [ ] Changed Server components were deployed with `DataverseLabelTranslator.Server\deploy.debug.bat`.
- [ ] No unrelated files were modified.
- [ ] Final response reports the new/changed files, deleted files, and verification/deployment outcome.
