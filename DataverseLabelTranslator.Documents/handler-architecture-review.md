# Handler Architecture Review — Dataverse Label Translator

> **Date:** 2026-06-03
> **Scope:** 16 handler files in `DataverseLabelTranslator.WebResource/js/` (excluding AllInOneHandler and RibbonHandler)
> **Reference implementations:** `GlobalOptionSetHandler.js` (type 18) and `WebResourceHandler.js` (type 17)
> **Goal:** Identify duplicated code and inconsistencies across all handlers, then propose how to rewrite them for maximum code reuse using the server-owned CRUD convention already proven in types 17 and 18

---

## 1. Context

This is a **pre-production app**. There is no deployed customer base and no backward compatibility requirement. Every handler can be rewritten from scratch.

Two handlers already follow the target convention:

| # | Handler | File | Lines |
|---|---------|------|:-----:|
| 17 | Web Resources | `WebResourceHandler.js` | 300 |
| 18 | Global Option Sets | `GlobalOptionSetHandler.js` | 315 |

These two files are the **gold standard**. All other handlers must be rewritten to follow the same shape. CRUD and publish work lives on the server via `pl_DataverseLabelTranslatorCustomAction`. The JS handler only does three things:

1. **Load** — call `Loading` → transform server output → fill grid
2. **Save** — extract `w2ui.changes` → validate → call `Saving` → `Publishing` → `Published` → reload
3. **FillTable** — build w2ui records from server output

Everything else (Web API queries, batch requests, entity updates, publish XML, language switching) moves to the server.

---

## 2. The Gold Standard Pattern

Both `GlobalOptionSetHandler.js` and `WebResourceHandler.js` follow this exact structure:

```text
┌─────────────────────────────────────────────────────────┐
│  IIFE module attached to window                         │
│                                                         │
│  Constants: idSeparator, actionName                     │
│                                                         │
│  Private functions (type-specific):                     │
│    Validation:   ValidateDisplayTextBaseLanguageChange() │
│    Placeholder:  ApplyEmptyEditablePlaceholder()        │
│    Grid build:   FillTable()                            │
│    Change extract: GetUpdates()                         │
│    Result read:  GetOptionSetNames() / GetWebResourceIds│
│                                                         │
│  Public API (2 methods only):                           │
│    Handler.Load()                                       │
│    Handler.Save()                                       │
└─────────────────────────────────────────────────────────┘
```

### Load flow

```js
Handler.Load = function () {
    return Helper.ExecuteTypedCustomAction(actionName, Helper.CustomActionTypes.Loading, {
        solutionId: XrmTranslator.GetSolution()
    })
    .then(function (result) {
        var output = Helper.GetCustomActionObject(result);
        // transform output → XrmTranslator.metadata
        FillTable();
        return result;
    })
    .catch(function (error) {
        XrmTranslator.UnlockGrid();
        XrmTranslator.errorHandler(error);
    });
};
```

### Save flow

```js
Handler.Save = function () {
    var updates = GetUpdates();      // extract w2ui.changes, validate
    XrmTranslator.LockGrid("Saving ...");

    return Helper.ExecuteTypedCustomAction(actionName, Helper.CustomActionTypes.Saving, updates)
        .then(function (result) {
            var ids = GetChangedIds(result);
            if (ids.length === 0) { unlock; return; }

            XrmTranslator.LockGrid("Publishing ...");
            return Helper.ExecuteTypedCustomAction(actionName, Helper.CustomActionTypes.Publishing, { ids })
                .then(function (pubResult) {
                    XrmTranslator.LockGrid("Published");
                    return Helper.ExecuteTypedCustomAction(actionName, Helper.CustomActionTypes.Published, { ids });
                });
        })
        .then(function (result) {
            if (no changes) return;
            XrmTranslator.LockGrid("Re-Loading ...");
            return Handler.Load().then(function () {
                XrmTranslator.EnableLoadAndSave();
            });
        })
        .catch(function (error) {
            XrmTranslator.UnlockGrid();
            XrmTranslator.EnableLoadAndSave();
            throw error;
        });
};
```

### FillTable ending (identical in both)

```js
XrmTranslator.AddSummary(records);
grid.add(records);
grid.unlock();
XrmTranslator.EnableLoadAndSave();
```

---

## 3. Old Handler Inventory

The remaining 14 handlers all pre-date the server convention. They make direct Web API calls, use varying save patterns, and duplicate code heavily.

| # | Handler | Lines | Save via | Uses `RunTypeSaveFlow` | Has `SaveOnly` |
|---|---------|:-----:|----------|:----------------------:|:--------------:|
| 1 | AttributeHandler | 224 | Web API PUT + MergeLabels | ✅ | ✅ |
| 2 | OptionSetHandler | 296 | `UpdateOptionValue` action | ✅ | ✅ |
| 3 | FormHandler | 811 | Web API PUT formxml | ✅ | ✅ |
| 4 | ViewHandler | 224 | `SetLocLabels` action | ✅ | ✅ |
| 5 | FormMetaHandler | 215 | `SetLocLabels` action | ✅ | ✅ |
| 6 | EntityHandler | 151 | Web API PUT + MergeLabels | ✅ | ✅ |
| 7 | RelationshipHandler | 362 | Web API PUT + MergeLabels | ✅ | ✅ |
| 8 | ChartHandler | 178 | `SetLocLabels` action | ✅ | ✅ |
| 9 | BpfHandler | 495 | XAML string replace | ✅ | ✅ |
| 10 | BusinessRuleHandler | 677 | XAML string replace | ✅ | ✅ |
| 12 | ModernCommandHandler | 483 | `SetLocLabels` action | ✅ | ✅ |
| 13 | EntityMessageHandler | 512 | Translation package import | ✅ | ✅ |
| 14 | ContentSnippetHandler | 261 | Web API Create/Update | ✅ | ✅ |
| 15 | SiteMapHandler | 581 | Web API PATCH sitemapxml | ✅ | ❌ |
| 16 | DashboardHandler | 382 | `SetLocLabels` action | ✅ | ❌ |

**Total old handler code: ~5,851 lines.** After rewrite, each should shrink to ~80–150 lines (FillTable + GetUpdates + Load + Save).

---

## 4. Duplicated Code Catalog

### 4.1 `ApplyChanges()` — 9 copies, 3 with bugs

Merges `w2ui.changes` back into a `LocalizedLabels` array. Found in:

| Handler | Function name | Bug? |
|---------|---------------|:----:|
| AttributeHandler | `ApplyChanges` | ⚠️ Empty-array bug |
| EntityHandler | `ApplyChanges` | ⚠️ Empty-array bug |
| ChartHandler | `ApplyChanges` | ⚠️ Empty-array bug |
| ViewHandler | `ApplyChanges` | ✅ Correct |
| FormMetaHandler | `ApplyChanges` | ✅ Correct |
| RelationshipHandler | `ApplyChanges` | ✅ Correct |
| FormHandler | `ApplyLocalizedLabelChanges` | ✅ Correct |
| ModernCommandHandler | `applyChanges` (lowercase) | ✅ Correct |
| DashboardHandler | `ApplyDashboardLabelChanges` | ✅ Correct |

**The empty-array bug:** Attribute, Entity, and Chart use `if (i === labels.length - 1)` to decide whether to push a new label. When `labels` is empty, the loop never runs and the new label is silently dropped.

**After rewrite:** This function moves to the server. The client sends raw `{LanguageCode, Label}` pairs; the server merges them. No JS copy needed.

### 4.2 FillTable ending — 16 identical copies

Every handler ends FillTable with:

```js
XrmTranslator.AddSummary(records);
grid.add(records);
grid.unlock();
XrmTranslator.EnableLoadAndSave();
```

**After rewrite:** Extract to `Helper.FinalizeGrid(records)`.

### 4.3 Placeholder text — 3 different implementations

| Handler | Function | Style |
|---------|----------|-------|
| GlobalOptionSetHandler | `ApplyEmptyEditablePlaceholder(record, text, baseText)` | Generic, parameterized ✅ |
| WebResourceHandler | `ApplyDisplayTextPlaceholder(record)` | Hardcoded text |
| DashboardHandler | Inline code | No function at all |

**After rewrite:** Use the `GlobalOptionSetHandler` version as `Helper.ApplyPlaceholder(record, text, baseText)`.

### 4.4 Base language validation — 3 different implementations

| Handler | Function | Uses `Helper.*`? |
|---------|----------|:----------------:|
| GlobalOptionSetHandler | `ValidateDisplayTextBaseLanguageChange(record, optionSet, changes)` | ✅ |
| WebResourceHandler | `ValidateDisplayTextBaseLanguageChange(record, group, changes)` | ✅ |
| DashboardHandler | `ValidateBaseLanguageChange(record, changes)` | ❌ re-declares locally |

All three do the same thing: if the changed cell is the base language and the new value is empty, throw an error with row context.

**After rewrite:** Extract to `Helper.ValidateBaseLanguageNotEmpty(changes, baseLanguage, rowPath)`.

### 4.5 `IsEmptyLabelValue` / `GetLanguageColumnText` — local copies

`DashboardHandler` re-declares both functions locally instead of using `Helper.IsEmptyLabelValue()` and `Helper.GetLanguageColumnText()`.

**After rewrite:** Delete local copies, use `Helper.*` everywhere.

### 4.6 `getLocalizedLabel()` — 2 copies

Both `ModernCommandHandler` and `BusinessRuleHandler` implement the same function to extract the best label from a `LocalizedLabels` array (try base language → user language → first available).

**After rewrite:** Not needed in JS. Server returns pre-resolved display text.

### 4.7 `DeactivateWorkflow()` / `ActivateWorkflow()` — 2 identical copies

Both `BpfHandler` and `BusinessRuleHandler` implement the same workflow state toggle.

**After rewrite:** Moves to server. BPF and BusinessRule server actions handle deactivation/activation internally.

### 4.8 `escapeODataString()` — 2 copies

Both `ModernCommandHandler` and `BusinessRuleHandler` implement the same OData string escaper.

**After rewrite:** Not needed in JS. Server builds OData queries internally.

### 4.9 IIFE pattern — 2 different styles

**Pattern A (14 handlers):**
```js
(function (Handler, undefined) { ... })((window.Handler = window.Handler || {}));
```

**Pattern B (2 handlers — GlobalOptionSet, Dashboard):**
```js
window.Handler = Object(window.Handler);
(function (Handler, undefined) { ... })(window.Handler);
```

**After rewrite:** Standardize to Pattern A.

### 4.10 Error handling in `Load()` — 3 different patterns

| Pattern | Handlers |
|---------|----------|
| `.catch(XrmTranslator.errorHandler)` | 11 handlers |
| `.catch(function(e) { UnlockGrid(); errorHandler(e); })` | GOS, WR |
| `.catch(function(e) { grid.unlock(); EnableLoadAndSave(); errorHandler(e); })` | EntityMessage |

**After rewrite:** Standardize to the GOS/WR pattern (unlock grid, then call errorHandler).

### 4.11 `GetUpdates()` record source — inconsistent

| Source | Handlers |
|--------|----------|
| `XrmTranslator.GetGrid().records` (misses tree children) | Attribute, View, FormMeta, Entity, Chart |
| `XrmTranslator.GetAllRecords()` (correct — includes children) | All others |

**After rewrite:** Always use `XrmTranslator.GetAllRecords()`.

---

## 5. Save Pipeline Comparison

### Old handlers: `RunTypeSaveFlow`

```js
Handler.Save = function () {
    return XrmTranslator.RunTypeSaveFlow({
        saveAction:    function () { return Handler.SaveOnly(); },
        publishAction: function () { return XrmTranslator.Publish(); },
        shouldPublish: function (r) { return r !== false; },
        reloadAction:  function () { return Handler.Load(); }
    });
};
```

**Problems with `RunTypeSaveFlow` for the new convention:**
- Adds artificial delays (`publishMinimumMs: 5000`, `publishedMessageMs: 5000`) that conflict with server-side waits
- The server custom action is already synchronous — the server controls timing
- Status banners and operation state tracking are managed by the server flow, not the client

### New handlers (GOS/WR): Direct phase chain

The gold standard does NOT use `RunTypeSaveFlow`. It chains `Saving → Publishing → Published → Load` directly with `Helper.ExecuteTypedCustomAction`, giving the server full control over timing and the client full control over UI lock messages.

**After rewrite:** All handlers use the direct phase chain. `RunTypeSaveFlow` becomes unused for migrated types.

---

## 6. Per-Handler Analysis: What Moves to Server

### 6.1 AttributeHandler (type 1, 224 lines)

| Current JS responsibility | After rewrite |
|---------------------------|---------------|
| `EntityDefinition/Attributes` Web API query | Server `Loading` |
| Filter `AttributeOf` shadow attributes | Server `Loading` |
| `SanitizeForPut()` — remove VersionNumber | Server `Saving` |
| Web API PUT with `?MergeLabels=true` | Server `Saving` |
| `PublishXml` | Server `Publishing` |

**JS after rewrite:** ~80 lines. FillTable builds flat rows with `AddLocalizedLabelsToRecord`. GetUpdates extracts `{attributeId, labels}`.

### 6.2 OptionSetHandler (type 2, 296 lines)

| Current JS responsibility | After rewrite |
|---------------------------|---------------|
| 5 parallel metadata queries (Picklist, MultiSelect, State, Status, Boolean) | Server `Loading` |
| `UpdateOptionValue` action calls | Server `Saving` |
| `PublishXml` with global option set names | Server `Publishing` |

**JS after rewrite:** ~120 lines. FillTable builds parent-child (option set → option values). GetUpdates extracts `{optionSetName, value, component, labels}`. Very similar to GlobalOptionSetHandler.

### 6.3 FormHandler (type 3, 811 lines) — MOST COMPLEX

| Current JS responsibility | After rewrite |
|---------------------------|---------------|
| User language switching per installed language | Server `Loading` |
| FormXML DOM parsing with TreeWalker | Server `Loading` (returns structured JSON) |
| `RemoveOverriddenCellLabels()` | Server `Other` operation |
| Per-form XML label updates | Server `Saving` |
| `PublishXml` | Server `Publishing` |

**JS after rewrite:** ~150 lines. Server returns flat/tree JSON of form labels. FillTable builds tree grid from JSON. GetUpdates extracts `{formId, nodeId, labels}`.

### 6.4 ViewHandler (type 4, 224 lines)

| Current JS responsibility | After rewrite |
|---------------------------|---------------|
| `savedquery` retrieve + `RetrieveLocLabels` per view | Server `Loading` |
| `SetLocLabels` calls via `ExecuteChangeSetBatches` | Server `Saving` |
| `PublishXml` | Server `Publishing` |

**JS after rewrite:** ~80 lines. Same flat grid shape as Attribute.

### 6.5 FormMetaHandler (type 5, 215 lines)

Nearly identical to ViewHandler. Uses `systemform` instead of `savedquery`.

**JS after rewrite:** ~80 lines. If the server returns the same output shape as ViewHandler, the JS FillTable code could be nearly identical.

### 6.6 EntityHandler (type 6, 151 lines) — SIMPLEST

Only 2 rows: "Display Text" and "Collection Name".

**JS after rewrite:** ~60 lines. Minimal handler.

### 6.7 RelationshipHandler (type 7, 362 lines)

| Current JS responsibility | After rewrite |
|---------------------------|---------------|
| 3 queries (1:N, N:1, N:N) + entity plural names | Server `Loading` |
| Switch from `UseCollectionName` to `UseLabel` on save | Server `Saving` |
| Metadata PUT with MergeLabels | Server `Saving` |
| `PublishXml` | Server `Publishing` |

**JS after rewrite:** ~100 lines. Parent-child grid (relationship → label row).

### 6.8 ChartHandler (type 8, 178 lines)

Nearly identical to ViewHandler. Uses `savedqueryvisualization`.

**JS after rewrite:** ~80 lines.

### 6.9 BpfHandler (type 9, 495 lines)

| Current JS responsibility | After rewrite |
|---------------------------|---------------|
| `workflow` clientdata JSON parsing | Server `Loading` |
| `FindStageSteps` recursive extraction | Server `Loading` |
| Deactivate → PATCH xaml → Activate cycle | Server `Saving` |
| XAML string replacement for StepLabels | Server `Saving` |
| Rollback on failure | Server `Saving` |

**JS after rewrite:** ~100 lines. 3-level grid (BPF → Stage → Field). Server returns structured stages/fields with labels.

### 6.10 BusinessRuleHandler (type 10, 677 lines)

| Current JS responsibility | After rewrite |
|---------------------------|---------------|
| XAML regex parsing for `<mcwo:StepLabel>` tags | Server `Loading` |
| Context detection (Error/Recommendation/Detail) | Server `Loading` |
| Attribute display name loading | Server `Loading` |
| Deactivate → PATCH xaml → Activate + rollback | Server `Saving` |

**JS after rewrite:** ~100 lines. Server returns `{workflowId, labelId, kind, sourceField, labels}`. FillTable builds parent-child grid.

### 6.11 ModernCommandHandler (type 12, 483 lines)

| Current JS responsibility | After rewrite |
|---------------------------|---------------|
| Solution component filtering | Server `Loading` |
| `appactions` query + `RetrieveLocLabels` per property | Server `Loading` |
| Command hierarchy path building | Server `Loading` |
| `SetLocLabels` via `ExecuteChangeSetBatches` | Server `Saving` |
| `PublishXml` | Server `Publishing` |

**JS after rewrite:** ~120 lines. Parent-child grid (command → property rows).

### 6.12 EntityMessageHandler (type 13, 512 lines)

| Current JS responsibility | After rewrite |
|---------------------------|---------------|
| Solution unique name resolution | Server `Loading` |
| Translation package export + Excel parsing | Server `Loading` |
| Display string identity matching | Server `Loading` |
| Excel modification + translation package import | Server `Saving` |
| `PublishXml` | Server `Publishing` |

**JS after rewrite:** ~80 lines. Flat grid. Server handles all Excel manipulation.

### 6.13 ContentSnippetHandler (type 14, 261 lines)

| Current JS responsibility | After rewrite |
|---------------------------|---------------|
| Portal language discovery | Server `Loading` |
| `adx_contentsnippet` retrieve with expand | Server `Loading` |
| `XrmTranslator.ClearColumns()` + portal language columns | Server `Loading` (returns column info) |
| Create/Update content snippets | Server `Saving` |

**JS after rewrite:** ~100 lines. Parent-child grid (website → snippet rows). Unique: uses portal languages, not CRM languages.

### 6.14 SiteMapHandler (type 15, 581 lines)

| Current JS responsibility | After rewrite |
|---------------------------|---------------|
| Solution component filtering | Server `Loading` |
| SiteMap XML parsing (Areas/Groups/SubAreas) | Server `Loading` |
| Entity display name fallback lookup | Server `Loading` |
| XML DOM label updates + PATCH sitemapxml | Server `Saving` |
| `PublishAllXml` | Server `Publishing` |

**JS after rewrite:** ~100 lines. Parent-child grid (sitemap → nodes). Server returns structured node tree with labels.

### 6.15 DashboardHandler (type 16, 382 lines)

| Current JS responsibility | After rewrite |
|---------------------------|---------------|
| Solution component filtering | Server `Loading` |
| `RunAsBaseLanguage` language switch | Server `Loading` |
| `RetrieveLocLabels` per dashboard | Server `Loading` |
| `SetLocLabels` per dashboard | Server `Saving` |
| `PublishDashboard` | Server `Publishing` |

**JS after rewrite:** ~80 lines. Flat grid (one row per dashboard). Very similar to EntityHandler.

---

## 7. Shared Code That Should Be in `Helper.js`

After rewrite, the following functions should be shared across all handlers:

### 7.1 Must-have extractions

```js
// Replaces 16 copies of the FillTable ending
Helper.FinalizeGrid = function (records, includeSummaryChildren) {
    var grid = XrmTranslator.GetGrid();
    grid.clear();
    XrmTranslator.AddSummary(records, includeSummaryChildren);
    grid.add(records);
    grid.unlock();
    XrmTranslator.EnableLoadAndSave();
};

// Replaces 3 copies — use GOS version (most generic)
Helper.ApplyPlaceholder = function (record, editablePlaceholder, basePlaceholder) {
    if (!editablePlaceholder) return;
    record._emptyEditablePlaceholder = editablePlaceholder;
    if (basePlaceholder && XrmTranslator.baseLanguage) {
        record._emptyEditablePlaceholders = record._emptyEditablePlaceholders || {};
        record._emptyEditablePlaceholders[String(XrmTranslator.baseLanguage)] = basePlaceholder;
    }
};

// Replaces 3 copies — base language empty check
Helper.ValidateBaseLanguageNotEmpty = function (changes, rowPath) {
    if (!XrmTranslator.IsDisplayTextComponent() || !XrmTranslator.baseLanguage) return;
    var base = String(XrmTranslator.baseLanguage);
    if (!Object.prototype.hasOwnProperty.call(changes, base)) return;
    if (!Helper.IsEmptyLabelValue(changes[base])) return;
    throw new Error(
        "Display Text in the base language (" +
        Helper.GetLanguageColumnText(base) +
        ") cannot be empty.\nRow: " + (rowPath || "(unknown)")
    );
};

// Replaces GOS AddLocalizedLabelsToRecord — useful for all metadata handlers
Helper.AddLocalizedLabelsToRecord = function (record, localizedLabels) {
    localizedLabels = localizedLabels || [];
    for (var i = 0; i < localizedLabels.length; i++) {
        record[localizedLabels[i].LanguageCode.toString()] = localizedLabels[i].Label;
    }
};

// Replaces GOS GetChangedLabels — useful for all metadata handlers
Helper.GetChangedLabels = function (changes, allowEmpty) {
    var labels = [];
    for (var change in changes) {
        if (!changes.hasOwnProperty(change)) continue;
        var label = changes[change];
        if (label == null) { label = allowEmpty ? "" : null; }
        if (!allowEmpty && !label) continue;
        labels.push({ LanguageCode: change, Label: label == null ? "" : label });
    }
    return labels;
};
```

### 7.2 Already in `Helper.js` (keep as is)

- `Helper.CustomActionTypes` — phase name constants
- `Helper.ExecuteTypedCustomAction(functionName, type, input)` — typed custom action call
- `Helper.ExecuteCustomAction(functionName, input)` — raw custom action call
- `Helper.GetCustomActionObject(result)` — safe `result.object` reader
- `Helper.IsEmptyLabelValue(value)` — empty label check
- `Helper.GetLanguageColumnText(languageCode, grid)` — language column display text

---

## 8. Proposals

### 8.1 Extract shared functions to `Helper.js` first

Before rewriting any handler, add these functions to `Helper.js`:

1. `Helper.FinalizeGrid(records, includeSummaryChildren)`
2. `Helper.ApplyPlaceholder(record, editablePlaceholder, basePlaceholder)`
3. `Helper.ValidateBaseLanguageNotEmpty(changes, rowPath)`
4. `Helper.AddLocalizedLabelsToRecord(record, localizedLabels)`
5. `Helper.GetChangedLabels(changes, allowEmpty)`

This gives every handler a shared toolkit without changing any handler yet.

### 8.2 Extract the Save phase chain to `Helper.js`

Both GOS and WR have an identical save chain structure. Extract it:

```js
Helper.RunServerSaveFlow = function (options) {
    var actionName = options.actionName;
    var getUpdates = options.getUpdates;        // function → save payload
    var getChangedIds = options.getChangedIds;   // function(output) → string[]
    var reloadAction = options.reloadAction;     // function → Promise

    var updates = getUpdates();
    XrmTranslator.LockGrid("Saving ...");

    return Helper.ExecuteTypedCustomAction(actionName, Helper.CustomActionTypes.Saving, updates)
        .then(function (result) {
            var output = Helper.GetCustomActionObject(result);
            var ids = getChangedIds(output);

            if (ids.length === 0) {
                XrmTranslator.UnlockGrid();
                XrmTranslator.EnableLoadAndSave();
                return result;
            }

            XrmTranslator.LockGrid("Publishing ...");
            return Helper.ExecuteTypedCustomAction(actionName, Helper.CustomActionTypes.Publishing, {
                changedIds: ids
            }).then(function (pubResult) {
                XrmTranslator.LockGrid("Published");
                return Helper.ExecuteTypedCustomAction(actionName, Helper.CustomActionTypes.Published, {
                    changedIds: getChangedIds(Helper.GetCustomActionObject(pubResult))
                });
            });
        })
        .then(function (result) {
            var output = Helper.GetCustomActionObject(result);
            if (getChangedIds(output).length === 0) return result;

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

With this, the Save method of every handler becomes:

```js
Handler.Save = function () {
    return Helper.RunServerSaveFlow({
        actionName: actionName,
        getUpdates: GetUpdates,
        getChangedIds: function (output) { return output.changedIds || []; },
        reloadAction: function () { return Handler.Load(); }
    });
};
```

**~30 lines of save code per handler → 5 lines.**

### 8.3 Standardize the Load pattern

Extract the Load error handling:

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

With this, every handler's Load becomes:

```js
Handler.Load = function () {
    return Helper.RunServerLoad(actionName, { solutionId: XrmTranslator.GetSolution() }, function (output) {
        XrmTranslator.metadata = output.items;
        FillTable();
    });
};
```

### 8.4 Rewrite order

Rewrite handlers from simplest to most complex. Each rewrite requires a matching server `ICustomAction`:

| Order | Handler | Estimated JS lines | Reason |
|:-----:|---------|:-------------------:|--------|
| ✅ | GlobalOptionSetHandler | 315 (done) | Reference implementation |
| ✅ | WebResourceHandler | 300 (done) | Reference implementation |
| 1 | EntityHandler | ~60 | Simplest: only 2 rows |
| 2 | DashboardHandler | ~80 | Flat grid, already has placeholder/validation |
| 3 | ChartHandler | ~80 | Flat grid, same shape as View |
| 4 | ViewHandler | ~80 | Flat grid |
| 5 | FormMetaHandler | ~80 | Nearly identical to View |
| 6 | AttributeHandler | ~80 | Flat grid, fix ApplyChanges bug |
| 7 | OptionSetHandler | ~120 | Parent-child, similar to GOS |
| 8 | ContentSnippetHandler | ~100 | Parent-child, portal languages (unique columns) |
| 9 | RelationshipHandler | ~100 | Parent-child, relationship-specific labels |
| 10 | SiteMapHandler | ~100 | Parent-child, XML moves to server |
| 11 | ModernCommandHandler | ~120 | Parent-child, multi-property per command |
| 12 | BpfHandler | ~100 | 3-level tree, XAML moves to server |
| 13 | BusinessRuleHandler | ~100 | 3-level tree, complex XAML moves to server |
| 14 | EntityMessageHandler | ~80 | Flat grid, Excel manipulation moves to server |
| 15 | FormHandler | ~150 | Most complex, multi-form, TreeWalker moves to server |

### 8.5 Target architecture after all rewrites

```text
Helper.js (~200 lines)
├── ExecuteTypedCustomAction()        — already exists
├── GetCustomActionObject()           — already exists
├── IsEmptyLabelValue()               — already exists
├── GetLanguageColumnText()           — already exists
├── CustomActionTypes                 — already exists
├── FinalizeGrid()                    — NEW
├── ApplyPlaceholder()                — NEW
├── ValidateBaseLanguageNotEmpty()     — NEW
├── AddLocalizedLabelsToRecord()      — NEW
├── GetChangedLabels()                — NEW
├── RunServerLoad()                   — NEW
└── RunServerSaveFlow()               — NEW

Each handler (~60–150 lines)
├── actionName constant
├── FillTable()      — type-specific grid record building
├── GetUpdates()     — type-specific change extraction + validation
├── Handler.Load()   — calls Helper.RunServerLoad → FillTable
└── Handler.Save()   — calls Helper.RunServerSaveFlow → GetUpdates → Load
```

**Total estimated JS after rewrite:**
- `Helper.js`: ~200 lines (current 101 + ~100 new)
- 16 handlers × ~100 lines avg = ~1,600 lines
- **Total: ~1,800 lines** (down from current ~6,150+ lines, **~70% reduction**)
