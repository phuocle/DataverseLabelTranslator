# Handler Architecture Review — Dataverse Label Translator

> **Date:** 2026-06-03
> **Scope:** All 18 handler files in `DataverseLabelTranslator.WebResource/js/`
> **Goal:** Identify inconsistencies, code duplication, and propose standardization with a shared handler interface

---

## Table of Contents

1. [Executive Summary](#executive-summary)
2. [Current Architecture Overview](#current-architecture-overview)
3. [Handler Classification](#handler-classification)
4. [Universal Lifecycle Pattern](#universal-lifecycle-pattern)
5. [Inconsistency Catalog](#inconsistency-catalog)
6. [Duplicated Code Analysis](#duplicated-code-analysis)
7. [Shared Functions That Should Be in Helper](#shared-functions-that-should-be-in-helper)
8. [Per-Handler Detailed Analysis](#per-handler-detailed-analysis)
9. [Proposed Handler Interface Contract](#proposed-handler-interface-contract)
10. [Server-Side CRUD Migration Impact](#server-side-crud-migration-impact)
11. [Refactoring Priorities](#refactoring-priorities)
12. [Appendix: Method Inventory Matrix](#appendix-method-inventory-matrix)

---

## 1. Executive Summary

The project has **18 handler files** (including `AllInOneHandler.js`) that all follow the same conceptual flow — **Load → Fill Grid → Edit → Save → Publish → Reload** — but each one is implemented independently with no shared base class or interface contract. This has led to:

- **~12 distinct copies** of `ApplyChanges()` / `ApplyLocalizedLabelChanges()` — nearly identical label merging logic
- **~6 distinct copies** of `IsEmptyLabelValue()` / `GetLanguageColumnText()` — already in `Helper.js` but not used everywhere
- **3 different IIFE initialization patterns** for attaching handlers to `window`
- **2 fundamentally different Save architectures**: client-side Web API (old handlers) vs. server-side custom action (new handlers)
- **No standard method naming**: some use `SaveOnly()`, some don't; some have `FillTable()`, others `fillTable()`
- **No shared FillTable pattern**: each handler re-implements grid clearing, record building, and summary attachment
- **Placeholder text inconsistency**: "Add display text" / "Add description" implemented differently across handlers

The review below catalogs every inconsistency and proposes a standardized handler interface that will simplify all handlers to focus only on their data-specific transformations while inheriting common lifecycle behavior.

---

## 2. Current Architecture Overview

```
User picks Entity → Type → Component → Click Load
    ↓
XrmTranslator.SetHandler(type) → assigns currentHandler
    ↓
currentHandler.Load()
    ↓
    ├── Fetch data (Web API or Custom Action)
    ├── Transform to metadata
    ├── FillTable() → build records → grid.add()
    ↓
User edits grid cells
    ↓
currentHandler.Save()
    ↓
    ├── GetUpdates() → extract w2ui.changes
    ├── Write to server (Web API PUT/POST/PATCH or Custom Action)
    ├── Publish
    └── Reload
```

### Orchestrator: `XrmTranslator.js`

- **`SetHandler()`** (line 1262): Giant if/else chain mapping type strings to handler globals
- **`RunTypeSaveFlow(options)`** (line 1459): Standardized save pipeline accepting `saveAction`, `publishAction`, `shouldPublish`, `reloadAction`
- **`ExecuteChangeSetBatches()`**: Shared batch execution utility used by most metadata handlers
- **`AddSummary()`**: Adds summary row to records
- **`EnableLoadAndSave()`**: Enables toolbar buttons
- **`LockGrid()` / `UnlockGrid()`**: Spinner control
- **`Publish()`**: Entity-specific publish
- **`RunAsBaseLanguage()`**: User language switching wrapper

### Shared Modules

| Module | Purpose |
|--------|---------|
| `Helper.js` | `ExecuteCustomAction()`, `ExecuteTypedCustomAction()`, `GetCustomActionObject()`, `IsEmptyLabelValue()`, `GetLanguageColumnText()`, `CustomActionTypes` enum |
| `DialogHelper.js` | `alert()`, `confirm()`, `prompt()` |
| `TranslationHandler.js` | AI translation, dictionary, portal language management |
| `TranslationDictionaryService.js` | Translation memory/dictionary |
| `TranslationPackageService.js` | CRM translation package export/import |
| `DataverseDataWebResourceService.js` | Data web resource operations |
| `AppSettingsService.js` | App settings management |

---

## 3. Handler Classification

### Group A: Server-Side Custom Action Handlers (New Pattern)

These handlers delegate all CRUD operations to the server via `Helper.ExecuteTypedCustomAction()`.

| # | Handler | Action Name | Save Pipeline |
|---|---------|-------------|---------------|
| 17 | `WebResourceHandler` | `"WebResource"` | Loading → Saving → Publishing → Published → Reload |
| 18 | `GlobalOptionSetHandler` | `"GlobalOptionSet"` | Loading → Saving → Publishing → Published → Reload |

**Key characteristics:**
- Server returns structured data via custom action output
- Save sends changes as JSON payload to server
- Server handles entity operations, handler just transforms UI ↔ payload
- Uses `Helper.ExecuteTypedCustomAction()` with typed lifecycle phases
- Self-contained save pipeline (NOT using `RunTypeSaveFlow`)

### Group B: Client-Side Metadata API Handlers (Old Pattern — Using `RunTypeSaveFlow`)

These handlers make direct Web API calls from JavaScript.

| # | Handler | Data Source | Save Method |
|---|---------|-------------|-------------|
| 1 | `AttributeHandler` | `EntityDefinition/.../Attributes` | Web API PUT with MergeLabels |
| 2 | `OptionSetHandler` | `EntityDefinition/.../PicklistAttribute...` | `UpdateOptionValue` action |
| 4 | `ViewHandler` | `savedquery` + `RetrieveLocLabels` | `SetLocLabels` action |
| 5 | `FormMetaHandler` | `systemform` + `RetrieveLocLabels` | `SetLocLabels` action |
| 6 | `EntityHandler` | `EntityDefinition` direct | Web API PUT with MergeLabels |
| 7 | `RelationshipHandler` | `EntityDefinition/.../Relationships` | Web API PUT with MergeLabels |
| 8 | `ChartHandler` | `savedqueryvisualization` + `RetrieveLocLabels` | `SetLocLabels` action |
| 12 | `ModernCommandHandler` | `appactions` + `RetrieveLocLabels` | `SetLocLabels` action |
| 14 | `ContentSnippetHandler` | `adx_contentsnippet` | Web API Create/Update |

### Group C: Client-Side XML/XAML Handlers (Complex — Using `RunTypeSaveFlow`)

These handlers parse XML/XAML strings and apply text replacement for label updates.

| # | Handler | Format | Complexity |
|---|---------|--------|------------|
| 3 | `FormHandler` | FormXML DOM | High — TreeWalker, language switching, multi-form |
| 9 | `BpfHandler` | Workflow XAML string replacement | High — Deactivate/Activate cycle, rollback |
| 10 | `BusinessRuleHandler` | Workflow XAML string replacement | Very High — regex XAML parsing, multi-label context |
| 11 | `RibbonHandler` | CRM Ribbon XML via solution import | Extreme — full solution export/import cycle |
| 15 | `SiteMapHandler` | SiteMap XML DOM | Medium — DOM-based label updates |

### Group D: Translation Package Handler (Unique)

| # | Handler | Mechanism |
|---|---------|-----------|
| 13 | `EntityMessageHandler` | Export/modify/import CRM translation package (Excel) |

### Group E: Dashboard Handler (Hybrid)

| # | Handler | Pattern |
|---|---------|---------|
| 16 | `DashboardHandler` | Direct `SetLocLabels` but with custom save logic (no `ExecuteChangeSetBatches`) |

---

## 4. Universal Lifecycle Pattern

Despite implementation differences, every handler follows this lifecycle:

```
┌─────────────┐    ┌──────────────┐    ┌───────────┐    ┌──────────┐    ┌────────────┐
│  Load()     │ →  │ FillTable()  │ →  │ User Edit │ →  │ Save()   │ →  │ Reload()   │
│             │    │              │    │           │    │          │    │            │
│ Fetch data  │    │ Clear grid   │    │ w2ui      │    │ Extract  │    │ Re-run     │
│ from server │    │ Build records│    │ changes   │    │ changes  │    │ Load()     │
│ Set metadata│    │ AddSummary() │    │           │    │ Write    │    │            │
│             │    │ grid.add()   │    │           │    │ Publish  │    │            │
│             │    │ grid.unlock()│    │           │    │          │    │            │
│             │    │ EnableLoad.. │    │           │    │          │    │            │
└─────────────┘    └──────────────┘    └───────────┘    └──────────┘    └────────────┘
```

### Standard Methods Every Handler Should Implement

| Method | Purpose | Current Status |
|--------|---------|---------------|
| `Load()` | Fetch data, populate grid | ✅ All handlers have this |
| `Save()` | Extract changes, persist, publish, reload | ✅ All handlers have this |
| `SaveOnly()` | Persist changes without publish/reload | ⚠️ Only 14 of 18 handlers |
| `FillTable()` | Transform metadata → grid records | ✅ All have (private function) |
| `GetUpdates()` | Extract w2ui.changes → update payload | ✅ All have (private function) |

---

## 5. Inconsistency Catalog

### 5.1 IIFE Module Pattern (3 Variations)

**Pattern A — Standard (most handlers):**
```js
(function (HandlerName, undefined) {
    "use strict";
    // ...
})((window.HandlerName = window.HandlerName || {}));
```
Used by: `AttributeHandler`, `OptionSetHandler`, `FormHandler`, `ViewHandler`, `FormMetaHandler`, `EntityHandler`, `ChartHandler`, `BpfHandler`, `RibbonHandler`, `ModernCommandHandler`, `ContentSnippetHandler`, `SiteMapHandler`, `EntityMessageHandler`, `WebResourceHandler`

**Pattern B — Pre-initialize on window:**
```js
window.GlobalOptionSetHandler = Object(window.GlobalOptionSetHandler);
(function (GlobalOptionSetHandler, undefined) {
    // ...
})(window.GlobalOptionSetHandler);
```
Used by: `GlobalOptionSetHandler`, `DashboardHandler`

**Pattern C — Mixed:**
```js
(function (RelationshipHandler, undefined) {
    // ...
})((window.RelationshipHandler = window.RelationshipHandler || {}));
```
Used by: `RelationshipHandler`, `BusinessRuleHandler`

> **Impact:** Functionally equivalent but creates confusion during code review. Should standardize.

### 5.2 Error Handling in Load() (4 Variations)

| Pattern | Handlers |
|---------|----------|
| `.catch(XrmTranslator.errorHandler)` | Attribute, OptionSet, Entity, Chart, ViewHandler, FormMeta, Relationship, BpfHandler, ModernCommand, BusinessRule, ContentSnippet |
| `.catch(function(error) { XrmTranslator.UnlockGrid(); XrmTranslator.errorHandler(error); })` | WebResourceHandler |
| `.catch(function(error) { grid.unlock(); EnableLoadAndSave(); errorHandler(error); })` | EntityMessageHandler |
| `.catch(XrmTranslator.errorHandler)` after custom handling | DashboardHandler, SiteMapHandler |

> **Impact:** Some handlers unlock the grid on error, others don't. Some enable load/save, others leave buttons disabled. This creates inconsistent UX on errors.

### 5.3 Save Pipeline (3 Architectures)

**Architecture 1 — `RunTypeSaveFlow` (standardized):**
```js
Handler.Save = function() {
    return XrmTranslator.RunTypeSaveFlow({
        saveAction: function() { return Handler.SaveOnly(); },
        publishAction: function() { return XrmTranslator.Publish(); },
        reloadAction: function() { return Handler.Load(); }
    });
};
```
Used by: Attribute, OptionSet, View, FormMeta, Entity, Relationship, Chart, Form, BpfHandler, BusinessRule, ModernCommand, ContentSnippet, Dashboard, SiteMap, EntityMessage

**Architecture 2 — Custom action pipeline (server-side):**
```js
Handler.Save = function() {
    return Helper.ExecuteTypedCustomAction(name, "Saving", payload)
        .then(function(result) {
            return Helper.ExecuteTypedCustomAction(name, "Publishing", ...);
        })
        .then(function(result) {
            return Helper.ExecuteTypedCustomAction(name, "Published", ...);
        })
        .then(function() { return Handler.Load(); });
};
```
Used by: WebResourceHandler, GlobalOptionSetHandler

**Architecture 3 — Solution import (extreme):**
```js
// RibbonHandler: Export solution → modify XML → import → publish
```
Used only by: RibbonHandler

> **Impact:** WebResource and GlobalOptionSet handlers don't use `RunTypeSaveFlow`, so they miss standardized status banners, operation state tracking, and the consistent save→publish→reload flow.

### 5.4 FillTable() Ending Pattern (Inconsistencies)

**Standard ending:**
```js
XrmTranslator.AddSummary(records);
grid.add(records);
grid.unlock();
XrmTranslator.EnableLoadAndSave();
```

| Handler | `AddSummary` | `grid.add` | `grid.unlock` | `EnableLoadAndSave` |
|---------|:---:|:---:|:---:|:---:|
| AttributeHandler | ✅ | ✅ | ✅ | ✅ |
| OptionSetHandler | ✅ | ✅ | ✅ | ✅ |
| ViewHandler | ✅ | ✅ | ✅ | ✅ |
| FormMetaHandler | ✅ | ✅ | ✅ | ✅ |
| EntityHandler | ✅ | ✅ | ✅ | ✅ |
| ChartHandler | ✅ | ✅ | ✅ | ✅ |
| RelationshipHandler | ✅ | ✅ | ✅ | ✅ |
| BpfHandler | ✅ | ✅ | ✅ | ✅ |
| BusinessRuleHandler | ✅ | ✅ | ✅ | ✅ |
| ModernCommandHandler | ✅ | ✅ | ✅ | ✅ |
| ContentSnippetHandler | ✅ | ✅ | ✅ | ✅ |
| SiteMapHandler | ✅ | ✅ | ✅ | ✅ |
| EntityMessageHandler | ✅ | ✅ | ✅ | ✅ |
| WebResourceHandler | ✅ | ✅ | ✅ | ✅ |
| GlobalOptionSetHandler | ✅ | ✅ | ✅ | ✅ |
| DashboardHandler | ✅ | ✅ | ✅ | ✅ |
| FormHandler | ✅ (with `true`) | ✅ | ✅ | ✅ |

> All handlers have the same ending pattern, confirming it should be extracted to a shared function.

### 5.5 Placeholder Text Pattern (Inconsistencies)

**WebResourceHandler:**
```js
function ApplyDisplayTextPlaceholder(record) {
    record._emptyEditablePlaceholder = "Add display text";
    if (XrmTranslator.baseLanguage) {
        record._emptyEditablePlaceholders = record._emptyEditablePlaceholders || {};
        record._emptyEditablePlaceholders[String(XrmTranslator.baseLanguage)] = "Add display text (*)";
    }
}
```

**GlobalOptionSetHandler:**
```js
function ApplyEmptyEditablePlaceholder(record, editablePlaceholder, basePlaceholder) {
    if (!editablePlaceholder) return;
    record._emptyEditablePlaceholder = editablePlaceholder;
    if (basePlaceholder && XrmTranslator.baseLanguage) {
        record._emptyEditablePlaceholders = record._emptyEditablePlaceholders || {};
        record._emptyEditablePlaceholders[String(XrmTranslator.baseLanguage)] = basePlaceholder;
    }
}
```

**DashboardHandler:**
```js
// Inline, not a function:
record._emptyEditablePlaceholder = "Add display text";
if (XrmTranslator.baseLanguage) {
    record._emptyEditablePlaceholders = {};
    record._emptyEditablePlaceholders[String(XrmTranslator.baseLanguage)] = "Add display text (*)";
}
```

> **3 different implementations of the same concept.** `GlobalOptionSetHandler` has the most generic version. Should be extracted to `Helper.ApplyPlaceholder()`.

### 5.6 Base Language Validation (Inconsistencies)

**WebResourceHandler:**
```js
function ValidateDisplayTextBaseLanguageChange(record, group, changes) {
    // Validates that base language display text is not cleared to empty
}
```

**GlobalOptionSetHandler:**
```js
function ValidateDisplayTextBaseLanguageChange(record, optionSet, changes) {
    // Same validation, different second parameter
}
```

**DashboardHandler:**
```js
function ValidateBaseLanguageChange(record, changes) {
    // Same concept, local IsEmptyLabelValue and GetLanguageColumnText instead of Helper.*
}
```

> **3 copies with slightly different signatures.** `DashboardHandler` even re-declares `IsEmptyLabelValue()` and `GetLanguageColumnText()` locally instead of using `Helper.IsEmptyLabelValue()` and `Helper.GetLanguageColumnText()`.

### 5.7 ApplyChanges() — The Most Duplicated Function

This function merges user edits from `w2ui.changes` back into `LocalizedLabels` arrays. There are **at least 7 distinct copies**:

| Handler | Function Name | New label push behavior |
|---------|---------------|------------------------|
| AttributeHandler | `ApplyChanges` | Push `{ LanguageCode, Label }` (no HasChanged, no parseInt) |
| EntityHandler | `ApplyChanges` | Same as Attribute (copy-paste) |
| ChartHandler | `ApplyChanges` | Same as Attribute (copy-paste) |
| ViewHandler | `ApplyChanges` | Push with `parseInt(change, 10)` + `HasChanged: true` |
| FormMetaHandler | `ApplyChanges` | Same as View (copy-paste) |
| RelationshipHandler | `ApplyChanges` | Push with `parseInt(change, 10)`, uses `found` variable |
| FormHandler | `ApplyLocalizedLabelChanges` | Push with `parseInt(change, 10)`, no `HasChanged` on push |
| ModernCommandHandler | `applyChanges` (lowercase!) | Push with `parseInt(change, 10)` + `HasChanged: true` |
| DashboardHandler | `ApplyDashboardLabelChanges` | Completely different signature, operates on array of updates |

**Critical differences:**
1. Some versions push `LanguageCode` as string, others as `parseInt(change, 10)`
2. Some set `HasChanged: true`, others don't
3. Attribute/Entity/Chart have a bug-prone pattern: `if (i === labels.length - 1)` check — only pushes new label if it's the last iteration, which fails silently if the array is empty!
4. View/FormMeta/Relationship use a `found` flag pattern — correct behavior
5. Function naming: `ApplyChanges`, `applyChanges`, `ApplyLocalizedLabelChanges`, `ApplyDashboardLabelChanges`

> **This is the #1 candidate for extraction to `Helper.js`.**

### 5.8 GetUpdates() / GetChangedLabels() — Also Highly Duplicated

Every handler has its own version of extracting changes from `w2ui.changes`. The pattern is always:

```js
for (var i = 0; i < records.length; i++) {
    if (record.w2ui && record.w2ui.changes) {
        // handler-specific transformation
    }
}
```

| Handler | Function | Gets records from |
|---------|----------|-------------------|
| AttributeHandler | `GetUpdates` | `XrmTranslator.GetGrid().records` |
| OptionSetHandler | `GetUpdates(records)` | parameter |
| ViewHandler | `GetUpdates` | `XrmTranslator.GetGrid().records` |
| FormMetaHandler | `GetUpdates` | `XrmTranslator.GetGrid().records` |
| EntityHandler | `GetUpdates` | `XrmTranslator.GetGrid().records` |
| ChartHandler | `GetUpdates` | `XrmTranslator.GetGrid().records` |
| RelationshipHandler | `GetUpdates` | `XrmTranslator.GetGrid().records` |
| BpfHandler | `GetUpdates(records)` | parameter |
| BusinessRuleHandler | `GetUpdates(records)` | parameter or default `GetAllRecords` |
| ModernCommandHandler | `GetUpdates(records)` | parameter or default `GetAllRecords` |
| ContentSnippetHandler | `GetUpdates(records)` | parameter |
| SiteMapHandler | Inline in Save() | `GetAllRecords` |
| EntityMessageHandler | `collectChanges(records)` | parameter |
| WebResourceHandler | `GetUpdates(records)` | parameter |
| GlobalOptionSetHandler | `GetUpdates` | `GetAllRecords` inside function |
| DashboardHandler | `GetUpdates` | `GetAllRecords` inside function |

> Some get records from `GetGrid().records`, others from `GetAllRecords()`. The latter is correct because `GetAllRecords()` includes child records in tree grids, while `.records` does not.

### 5.9 `getLocalizedLabel()` — Duplicated Between Handlers

Both `ModernCommandHandler` and `BusinessRuleHandler` implement their own `getLocalizedLabel()` function to extract the best label from a `LocalizedLabels` array with base language preference:

```js
function getLocalizedLabel(labelCollection) {
    var labels = labelCollection && labelCollection.LocalizedLabels ? ... ;
    // try baseLanguage, then uilanguageid, then first available
}
```

> This should be in `Helper.js`.

### 5.10 `DeactivateWorkflow()` / `ActivateWorkflow()` — Duplicated

Both `BpfHandler` and `BusinessRuleHandler` implement identical:
```js
function DeactivateWorkflow(workflowId) { ... }
function ActivateWorkflow(workflowId) { ... }
```

> Should be in `Helper.js` or a shared `WorkflowHelper`.

---

## 6. Duplicated Code Analysis — Summary Table

| Duplicated Code | # of Copies | Current Location | Proposed Location |
|-----------------|:-----------:|------------------|-------------------|
| `ApplyChanges()` / label merge | 9 | Every handler | `Helper.ApplyLabelChanges()` |
| FillTable ending (`AddSummary` + `add` + `unlock` + `Enable`) | 18 | Every handler | `Helper.FinalizeGrid(records)` or `XrmTranslator.FinalizeGrid()` |
| Placeholder text (`_emptyEditablePlaceholder`) | 3 | WR, GOS, Dashboard | `Helper.ApplyPlaceholder(record, text, baseText)` |
| Base language validation | 3 | WR, GOS, Dashboard | `Helper.ValidateBaseLanguageNotEmpty(record, changes, rowPath)` |
| `IsEmptyLabelValue()` | 2 | Helper.js + Dashboard (local) | Use `Helper.IsEmptyLabelValue()` everywhere |
| `GetLanguageColumnText()` | 2 | Helper.js + Dashboard (local) | Use `Helper.GetLanguageColumnText()` everywhere |
| `getLocalizedLabel()` | 2 | ModernCommand, BusinessRule | `Helper.GetLocalizedLabel()` |
| `DeactivateWorkflow()` / `ActivateWorkflow()` | 2 | BpfHandler, BusinessRuleHandler | `Helper.DeactivateWorkflow()` |
| `escapeODataString()` | 2 | BusinessRule, ModernCommand | `Helper.EscapeODataString()` |
| `GetGroupKey()` / `GetRecordId()` / ID splitting | 5+ | OptionSet, WR, GOS, Bpf, BusinessRule | `Helper.GetRecordIdPart(id, separator, index)` |
| Error catch with grid unlock | 3+ | WR, EntityMessage, Dashboard | Standard in `RunTypeSaveFlow` |

---

## 7. Shared Functions That Should Be in Helper

### Must-Have Extractions (High Impact)

```js
// 1. Generic label merge — replaces 9 copies
Helper.ApplyLabelChanges = function(changes, labels, options) {
    // options: { parseInt: true/false, setHasChanged: true/false, allowEmpty: false }
    // Returns the modified labels array
};

// 2. Grid finalization — replaces 18 copies
Helper.FinalizeGrid = function(records, options) {
    var grid = XrmTranslator.GetGrid();
    grid.clear();
    XrmTranslator.AddSummary(records, options && options.includeSummaryChildren);
    grid.add(records);
    grid.unlock();
    XrmTranslator.EnableLoadAndSave();
};

// 3. Placeholder application — replaces 3 copies
Helper.ApplyPlaceholder = function(record, text, baseText) {
    if (!text) return;
    record._emptyEditablePlaceholder = text;
    if (baseText && XrmTranslator.baseLanguage) {
        record._emptyEditablePlaceholders = record._emptyEditablePlaceholders || {};
        record._emptyEditablePlaceholders[String(XrmTranslator.baseLanguage)] = baseText;
    }
};

// 4. Base language empty validation — replaces 3 copies
Helper.ValidateBaseLanguageNotEmpty = function(record, changes, rowPath, fieldLabel) {
    if (!XrmTranslator.baseLanguage) return;
    var base = String(XrmTranslator.baseLanguage);
    if (!Object.prototype.hasOwnProperty.call(changes, base)) return;
    if (!Helper.IsEmptyLabelValue(changes[base])) return;
    throw new Error(
        (fieldLabel || "Display Text") + " in the base language (" +
        Helper.GetLanguageColumnText(base) + ") cannot be empty.\nRow: " +
        (rowPath || "(unknown)")
    );
};

// 5. Best localized label extraction — replaces 2 copies
Helper.GetLocalizedLabel = function(labelCollection) {
    // Try baseLanguage, then uilanguageid, then first available
};

// 6. Workflow state management — replaces 2 copies
Helper.DeactivateWorkflow = function(workflowId) { ... };
Helper.ActivateWorkflow = function(workflowId) { ... };

// 7. Changed records extraction — replaces ~16 copies of iteration
Helper.GetChangedRecords = function(records) {
    records = records || XrmTranslator.GetAllRecords();
    return records.filter(function(r) { return r.w2ui && r.w2ui.changes; });
};
```

---

## 8. Per-Handler Detailed Analysis

### 8.1 AttributeHandler.js (224 lines)

**Classification:** Group B — Client-side metadata API

**Methods:**
| Method | Visibility | Purpose |
|--------|-----------|---------|
| `GetMetadataComponent()` | private | Maps "DisplayText" → "DisplayName" |
| `ApplyChanges(changes, labels)` | private | ⚠️ Has empty-array bug |
| `SanitizeForPut(obj)` | private | Removes VersionNumber, unsafe integers |
| `GetUpdates()` | private | Extracts changes for metadata PUT |
| `IsShadowAttribute(attribute)` | private | Checks AttributeOf |
| `FillTable()` | private | Builds flat record list |
| `Load()` | **public** | Fetches EntityDefinition/Attributes |
| `SaveOnly()` | **public** | Batch PUT with MergeLabels |
| `Save()` | **public** | `RunTypeSaveFlow` wrapper |

**Issues:**
- `ApplyChanges` has the **empty-array bug**: `if (i === labels.length - 1)` only works when `labels.length > 0`
- `GetUpdates()` uses `GetGrid().records` instead of `GetAllRecords()`
- `SanitizeForPut()` is unique to this handler but could benefit other metadata PUT handlers
- Same `GetMetadataComponent()` exists in `EntityHandler`

---

### 8.2 OptionSetHandler.js (296 lines)

**Classification:** Group B — Client-side metadata API

**Methods:**
| Method | Visibility | Purpose |
|--------|-----------|---------|
| `GetRecordId(id)` | private | Splits composite ID |
| `GetComponent()` | private | Maps "DisplayText" → "Label" |
| `GetOptionValueUpdate(...)` | private | Builds UpdateOptionValue payload |
| `GetUpdateIds(records)` | private | Collects changed option set IDs |
| `GetUpdates(records)` | private | Builds label update array |
| `SaveOptionValueUpdates(updates)` | private | Batch POST UpdateOptionValue |
| `HandleOptionSets(attribute, options, records)` | private | Builds parent-child grid records |
| `FillTable()` | private | Populates grid with option sets |
| `Load()` | **public** | 5 parallel queries for all option set types |
| `SaveOnly()` | **public** | Save without publish |
| `Save()` | **public** | `RunTypeSaveFlow` wrapper |

**Issues:**
- `GetUpdates(records)` takes `records` as parameter (good) but `Save()` calls `GetAllRecords()` and `GetUpdates()` separately, leading to potential double-computation
- Inline label building instead of using a shared `ApplyChanges`
- `GetComponent()` duplicates `GlobalOptionSetHandler`'s component mapping logic

---

### 8.3 FormHandler.js (811 lines) — MOST COMPLEX

**Classification:** Group C — Client-side XML handler

**Unique complexity:**
- Uses `TreeWalker` to parse FormXML DOM
- Switches user language per installed language to get per-language FormXML
- Supports both single-form and multi-form (all-forms) modes
- Has `RemoveOverriddenCellLabels()` — unique feature
- `LoadAllForms()` exposed for `AllInOneHandler`
- `ProcessSelection()` exposed for `AllInOneHandler`
- Multi-form save: iterates per form, restores/strips prefixed record IDs

**Issues:**
- Mixing of `var` and `const`/`let` (line 622-648 uses `const`)
- Has its own `ApplyLocalizedLabelChanges` instead of shared `ApplyChanges`
- 811 lines — should be split into FormParser + FormHandler

---

### 8.4 ViewHandler.js (224 lines)

**Classification:** Group B — Client-side RetrieveLocLabels/SetLocLabels

**Issues:**
- `ApplyChanges` uses `found` flag — **correct** pattern
- Nearly identical to `FormMetaHandler` — both use `RetrieveLocLabels` + `SetLocLabels`
- Has unused variables `languages` and `initialLanguage` in `Load()` (lines 147-148)

---

### 8.5 FormMetaHandler.js (215 lines)

**Classification:** Group B — Client-side RetrieveLocLabels/SetLocLabels

**Issues:**
- Almost identical to `ViewHandler` — only entity type and form type map differ
- `ApplyChanges` is copy-paste of `ViewHandler.ApplyChanges`
- Special case: `publishAction` checks for `"none"` entity to use `PublishDashboard`

---

### 8.6 EntityHandler.js (151 lines) — SIMPLEST

**Classification:** Group B — Client-side metadata PUT

**Issues:**
- `ApplyChanges` has the **empty-array bug** (same as AttributeHandler)
- `GetMetadataComponent()` duplicates `AttributeHandler.GetMetadataComponent()`
- Only 2 rows ever: "Display Text" and "Collection Name"
- Hardcoded check for `record.schemaName === "Display Text"` / `"Collection Name"`

---

### 8.7 RelationshipHandler.js (362 lines)

**Classification:** Group B — Client-side metadata PUT

**Unique features:**
- Fetches plural entity names to show as default labels for "UseCollectionName" relationships
- Handles 1:N, N:1, and N:N relationships with different menu configuration properties
- Switches relationship behavior from "UseCollectionName" to "UseLabel" on save

**Issues:**
- `ApplyChanges` uses `found` flag — correct pattern, but still duplicated
- Two `var menuConfig` declarations in `SaveOnly` (line 295 and 304) — redeclaration

---

### 8.8 ChartHandler.js (178 lines)

**Classification:** Group B — Client-side RetrieveLocLabels/SetLocLabels

**Issues:**
- `ApplyChanges` has the **empty-array bug** (same as Attribute/Entity)
- Has unused variables `languages` and `initialLanguage` in `Load()` (lines 102-103)
- Nearly identical pattern to ViewHandler

---

### 8.9 BpfHandler.js (495 lines)

**Classification:** Group C — Workflow XAML string replacement

**Unique features:**
- BPF XAML string replacement with detailed documentation
- Deactivate → Fetch XAML → Update → Activate → Reload cycle
- Error recovery with XAML rollback
- Supports both stage-level and field-level labels
- 3-level grid: BPF → Stage → Field

**Issues:**
- `DeactivateWorkflow()`/`ActivateWorkflow()` duplicated in `BusinessRuleHandler`
- No `publishAction` in `Save()` — BPF activation regenerates clientdata automatically
- `FindStageSteps` recursive function could be more readable

---

### 8.10 BusinessRuleHandler.js (677 lines)

**Classification:** Group C — Workflow XAML string replacement

**Unique features:**
- Most complex XAML parsing — uses regex to find all `<mcwo:StepLabel>` tags
- Context-aware labels: "Error message", "Recommendation title", "Recommendation detail"
- Loads attribute display names for field-level context
- Multi-pass XML encoding/decoding
- Rollback with original XAML restoration

**Issues:**
- `DeactivateWorkflow()`/`ActivateWorkflow()` duplicated from `BpfHandler`
- `escapeODataString()` duplicated from `ModernCommandHandler`
- `getLocalizedLabel()` duplicated from `ModernCommandHandler`
- `encodeXmlAttribute()` / `decodeXmlAttribute()` — shared XML helpers could be extracted

---

### 8.11 RibbonHandler.js (34,523 bytes) — LARGEST

**Classification:** Group C — Solution export/import

**Not analyzed in full detail due to size, but key characteristics:**
- Full solution export → modify ribbon XML → import → publish
- Uses operation state tracking with import job polling
- Most complex save pipeline in the project

---

### 8.12 ModernCommandHandler.js (483 lines)

**Classification:** Group B — Client-side RetrieveLocLabels/SetLocLabels

**Unique features:**
- Handles 5 translatable properties per command (buttonlabeltext, buttontooltiptitle, etc.)
- Parent-child grid: Command → Property rows
- Solution component filtering
- Complex sorting by command hierarchy path

**Issues:**
- `getLocalizedLabel()` duplicated from `BusinessRuleHandler`
- `escapeODataString()` duplicated from `BusinessRuleHandler`
- `applyChanges` (lowercase!) — inconsistent naming
- `cloneLabelCollection()` could be shared

---

### 8.13 EntityMessageHandler.js (512 lines)

**Classification:** Group D — Translation package

**Unique features:**
- Uses CRM translation package (Excel) export/import
- Parses Excel worksheets to find "Display Strings" sheet
- Maps rows to entity via displaystringid/displaystringkey/objecttypecode
- Modifies Excel cells and re-imports

**Issues:**
- Completely different data model — not metadata labels
- `fillTable` is lowercase (inconsistent with other handlers)
- Unique `commitChanges()` pattern to update grid in-place

---

### 8.14 ContentSnippetHandler.js (261 lines)

**Classification:** Group B — Client-side CRUD (adx_contentsnippet)

**Unique features:**
- Portal-specific: works with `adx_contentsnippet` entity
- Custom columns from portal languages (not CRM languages)
- Sets `XrmTranslator.columnRestoreNeeded = true`
- Creates new records if content snippet doesn't exist for a language

**Issues:**
- Uses `let` instead of `var` (lines 3-6) — inconsistent with rest of codebase
- `find()` usage (ES6) — not available in all target browsers

---

### 8.15 SiteMapHandler.js (581 lines)

**Classification:** Group C — XML DOM manipulation

**Unique features:**
- Parses SiteMap XML into Areas/Groups/SubAreas
- Composite IDs for hierarchical node addressing
- Fetches entity display names for SubArea fallback labels
- XML serialization after label updates
- Solution-scoped loading

**Issues:**
- Inline label extraction in `Save()` instead of separate `GetUpdates()`
- Long save chain with closure-wrapped sequential processing

---

### 8.16 DashboardHandler.js (382 lines)

**Classification:** Group E — Hybrid (SetLocLabels but custom pipeline)

**Unique features:**
- Solution-filtered dashboard loading
- Uses `RunAsBaseLanguage()` for initial query
- Sequential save per dashboard with `SetLocLabels`
- Dashboard-specific `PublishDashboard()` publish method

**Issues:**
- **Re-declares** `IsEmptyLabelValue()` and `GetLanguageColumnText()` locally instead of using `Helper.*`
- Inline placeholder application (not using a shared function)
- Uses `RunTypeSaveFlow` correctly but with custom `shouldPublish` and `publishAction`

---

### 8.17 WebResourceHandler.js (300 lines)

**Classification:** Group A — Server-side custom action

**Key pattern (new):**
```js
Load: Helper.ExecuteTypedCustomAction("WebResource", "Loading", { solutionId })
Save: Helper.ExecuteTypedCustomAction("WebResource", "Saving", { resourceChanges })
  → Helper.ExecuteTypedCustomAction("WebResource", "Publishing", { webresourceIds })
  → Helper.ExecuteTypedCustomAction("WebResource", "Published", { webresourceIds })
```

**Issues:**
- Does NOT use `RunTypeSaveFlow` — has its own save pipeline chain
- Inline error handling differs from the standard pattern
- `ApplyDisplayTextPlaceholder` is handler-specific version of a pattern that should be shared

---

### 8.18 GlobalOptionSetHandler.js (315 lines)

**Classification:** Group A — Server-side custom action

**Key pattern (new):**
```js
Load: Helper.ExecuteTypedCustomAction("GlobalOptionSet", "Loading", { solutionId })
Save: Helper.ExecuteTypedCustomAction("GlobalOptionSet", "Saving", { ... })
  → Helper.ExecuteTypedCustomAction("GlobalOptionSet", "Publishing", { optionSetNames })
  → Helper.ExecuteTypedCustomAction("GlobalOptionSet", "Published", { optionSetNames })
```

**Issues:**
- Does NOT use `RunTypeSaveFlow` — has its own save pipeline chain
- `ApplyEmptyEditablePlaceholder()` — generic version, but duplicated with WebResource and Dashboard
- `ValidateDisplayTextBaseLanguageChange()` checks `XrmTranslator.IsDisplayTextComponent()` — unique to this handler
- `GetChangedLabels(changes, allowEmpty)` — reusable, should be shared
- `AddLocalizedLabelsToRecord()` — reusable, used in FillTable to populate grid from `LocalizedLabels`

---

## 9. Proposed Handler Interface Contract

Every handler should implement this interface:

```js
var HandlerInterface = {
    // ===== REQUIRED =====

    /** Fetch data from server and populate the grid */
    Load: function() {
        // 1. Show spinner via XrmTranslator.LockGrid()
        // 2. Fetch data (Custom Action or Web API)
        // 3. Transform response to metadata
        // 4. Build grid records
        // 5. Call Helper.FinalizeGrid(records)
        // Returns: Promise
    },

    /** Full save pipeline: extract → persist → publish → reload */
    Save: function() {
        return XrmTranslator.RunTypeSaveFlow({
            saveAction:   function() { return Handler.SaveOnly(); },
            publishAction: function() { return Handler.GetPublishAction(); },
            shouldPublish: function(result) { return result !== false; },
            reloadAction:  function() { return Handler.Load(); }
        });
    },

    // ===== RECOMMENDED =====

    /** Persist changes without publish/reload (used by AllInOneHandler) */
    SaveOnly: function() {
        // 1. Call Handler.GetUpdates()
        // 2. Send changes to server
        // Returns: Promise<result|false>
    },

    // ===== INTERNAL (should follow naming convention) =====

    /** Build grid records from metadata — private */
    // _fillTable: function() { ... },

    /** Extract w2ui.changes into update payload — private */
    // _getUpdates: function() { ... },

    // ===== OPTIONAL OVERRIDES =====

    /** Custom publish behavior (default: XrmTranslator.Publish()) */
    // GetPublishAction: function() { ... },
};
```

### Standardized Naming Convention

| Current (varies) | Proposed Standard |
|-------------------|-------------------|
| `FillTable()`, `fillTable()` | `_fillTable()` (private) |
| `GetUpdates()`, `collectChanges()` | `_getUpdates(records)` (private) |
| `ApplyChanges()`, `applyChanges()`, etc. | `Helper.ApplyLabelChanges()` (shared) |
| `ApplyDisplayTextPlaceholder()`, etc. | `Helper.ApplyPlaceholder()` (shared) |
| `ValidateDisplayTextBaseLanguageChange()` | `Helper.ValidateBaseLanguageNotEmpty()` (shared) |
| `GetMetadataComponent()`, `GetComponent()` | `Helper.GetMetadataComponentName()` (shared) |

---

## 10. Server-Side CRUD Migration Impact

### Current State

Only 2 handlers (WebResource, GlobalOptionSet) use the server-side custom action pattern. As more handlers migrate to server-side CRUD, the following will change:

### What Moves to Server

| Concern | Current (Client) | Future (Server) |
|---------|-------------------|-----------------|
| Data fetching | Web API `Retrieve()`, `RetrieveLocLabels()` | Custom Action `Loading` phase |
| Data transformation | Handler-specific JS | Server-side C# |
| Label update logic | `ApplyChanges()` in JS | Server-side C# |
| Entity update | Web API `PUT`/`POST`/`SetLocLabels` | Custom Action `Saving` phase |
| Publishing | `PublishAllXml`/`PublishXml` from JS | Custom Action `Publishing` phase |
| Post-publish verification | JS polling | Custom Action `Published` phase |

### What Stays in Client

| Concern | Stays in Client |
|---------|-----------------|
| Grid record building | `FillTable()` transforms server output → w2ui records |
| Change extraction | `GetUpdates()` reads `w2ui.changes` → JSON payload |
| Placeholder application | `ApplyPlaceholder()` for empty cell UX |
| Base language validation | Pre-save validation in `GetUpdates()` |
| Spinner/progress | `LockGrid()` / `UnlockGrid()` |

### Simplified Handler Template (Post-Migration)

```js
(function (MyHandler, undefined) {
    "use strict";

    var actionName = "MyType";

    function FillTable(output) {
        var records = [];
        // Transform output.items → grid records
        // Use Helper.ApplyPlaceholder() for editable rows
        Helper.FinalizeGrid(records);
    }

    function GetUpdates() {
        var records = XrmTranslator.GetAllRecords();
        return Helper.GetChangedRecords(records).map(function(record) {
            var changes = record.w2ui.changes;
            Helper.ValidateBaseLanguageNotEmpty(record, changes, record.schemaName);
            return {
                id: record.recid,
                labels: Helper.BuildLabelPayload(changes)
            };
        });
    }

    MyHandler.Load = function () {
        return Helper.ExecuteTypedCustomAction(actionName, Helper.CustomActionTypes.Loading, {
            solutionId: XrmTranslator.GetSolution(),
            entityName: XrmTranslator.GetEntity()
        })
        .then(function (result) {
            var output = Helper.GetCustomActionObject(result);
            FillTable(output);
        })
        .catch(Helper.HandleLoadError);
    };

    MyHandler.Save = function () {
        var updates = GetUpdates();
        XrmTranslator.LockGrid("Saving ...");

        return Helper.ExecuteTypedCustomAction(actionName, Helper.CustomActionTypes.Saving, {
            updates: updates
        })
        .then(Helper.RunPublishPipeline(actionName))
        .then(function() { return MyHandler.Load(); })
        .catch(Helper.HandleSaveError);
    };
})((window.MyHandler = window.MyHandler || {}));
```

> With server-side CRUD, each handler shrinks to **~50–80 lines** (FillTable + GetUpdates + Load + Save).

---

## 11. Refactoring Priorities

### Priority 1: Quick Wins (Low Risk, High Impact)

1. **Extract `ApplyLabelChanges()` to `Helper.js`** — Eliminates 9 copies, fixes empty-array bug
2. **Extract `ApplyPlaceholder()` to `Helper.js`** — Eliminates 3 copies
3. **Extract `ValidateBaseLanguageNotEmpty()` to `Helper.js`** — Eliminates 3 copies
4. **Remove local `IsEmptyLabelValue()` / `GetLanguageColumnText()` from `DashboardHandler`** — Use `Helper.*`
5. **Extract `FinalizeGrid()` to `Helper.js` or `XrmTranslator`** — Eliminates 18 copies of the same 4-line ending

### Priority 2: Standardize Save Pipeline (Medium Risk)

6. **Migrate WebResource/GlobalOptionSet Save to `RunTypeSaveFlow`** — Achieves consistent status banner, operation state, button management
7. **Extract `DeactivateWorkflow()` / `ActivateWorkflow()` to `Helper.js`** — Shared by BPF + BusinessRule
8. **Extract `getLocalizedLabel()` to `Helper.js`** — Shared by ModernCommand + BusinessRule

### Priority 3: Architecture Alignment (Higher Risk)

9. **Add `SaveOnly()` to handlers that lack it** — Required for AllInOneHandler support
10. **Standardize IIFE pattern** to Pattern A across all handlers
11. **Standardize function naming** (PascalCase for public, camelCase for private)

### Priority 4: Server-Side Migration (Long Term)

12. **Migrate remaining handlers to custom action pattern** one-by-one, starting with simpler ones:
    - EntityHandler (simplest)
    - ChartHandler
    - ViewHandler
    - FormMetaHandler
    - AttributeHandler
    - DashboardHandler
    - OptionSetHandler
    - ContentSnippetHandler
    - RelationshipHandler
    - ModernCommandHandler
    - SiteMapHandler
    - BpfHandler
    - BusinessRuleHandler
    - EntityMessageHandler
    - FormHandler (most complex — last)
    - RibbonHandler (extreme — last)

---

## Appendix: Method Inventory Matrix

| Handler | Load | Save | SaveOnly | FillTable | GetUpdates | ApplyChanges | Placeholder | BaseValidation |
|---------|:----:|:----:|:--------:|:---------:|:----------:|:------------:|:-----------:|:--------------:|
| Attribute | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ (buggy) | ❌ | ❌ |
| OptionSet | ✅ | ✅ | ✅ | ✅ | ✅ | Inline | ❌ | ❌ |
| Form | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ (own name) | ❌ | ❌ |
| View | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ (correct) | ❌ | ❌ |
| FormMeta | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ (correct) | ❌ | ❌ |
| Entity | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ (buggy) | ❌ | ❌ |
| Relationship | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ (correct) | ❌ | ❌ |
| Chart | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ (buggy) | ❌ | ❌ |
| BPF | ✅ | ✅ | ✅ | ✅ | ✅ | N/A (XAML) | ❌ | ❌ |
| BusinessRule | ✅ | ✅ | ✅ | ✅ | ✅ | N/A (XAML) | ❌ | ❌ |
| Ribbon | ✅ | ✅ | ✅ | ✅ | N/A | N/A (XML) | ❌ | ❌ |
| ModernCmd | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ (correct, lowercase) | ❌ | ❌ |
| EntityMsg | ✅ | ✅ | ✅ | ✅ | ✅ (own name) | N/A (Excel) | ❌ | ❌ |
| ContentSnippet | ✅ | ✅ | ✅ | ✅ | ✅ | N/A (CRUD) | ❌ | ❌ |
| SiteMap | ✅ | ✅ | ❌ | ✅ | Inline | N/A (XML) | ❌ | ❌ |
| Dashboard | ✅ | ✅ | ❌ | ✅ | ✅ | ✅ (own name) | ✅ (inline) | ✅ (local) |
| WebResource | ✅ | ✅ | ❌ | ✅ | ✅ | N/A (server) | ✅ (own fn) | ✅ (own fn) |
| GlobalOptionSet | ✅ | ✅ | ❌ | ✅ | ✅ | N/A (server) | ✅ (own fn) | ✅ (own fn) |

**Legend:**
- ✅ = Present
- ❌ = Missing
- ✅ (buggy) = Present but has the empty-array bug
- ✅ (correct) = Present with correct `found` flag pattern
- ✅ (own fn) = Present as dedicated private function
- ✅ (inline) = Present as inline code, not a function
- ✅ (own name) = Present with a different function name
- N/A = Not applicable for this handler's data model

---

## Appendix: Data Source Comparison

| # | Handler | Data Source | API Pattern | Publish Method |
|---|---------|------------|-------------|----------------|
| 1 | Attribute | `EntityDefinition/Attributes` | Web API GET → PUT | `PublishXml` (entity) |
| 2 | OptionSet | `EntityDefinition/Attributes/...Metadata` (×5) | Web API GET → `UpdateOptionValue` | `PublishXml` (entity + global OS) |
| 3 | Form | `systemform` (per language) | Web API GET → PUT formxml | `PublishXml` (entity) |
| 4 | View | `savedquery` + `RetrieveLocLabels` | Web API GET → `SetLocLabels` | `PublishXml` (entity) |
| 5 | FormMeta | `systemform` + `RetrieveLocLabels` | Web API GET → `SetLocLabels` | `PublishXml` (entity) or `PublishDashboard` |
| 6 | Entity | `EntityDefinition` | Web API GET → PUT | `PublishXml` (entity) |
| 7 | Relationship | `EntityDefinition/...Relationships` (×3) | Web API GET → PUT | `PublishXml` (entity) |
| 8 | Chart | `savedqueryvisualization` + `RetrieveLocLabels` | Web API GET → `SetLocLabels` | `PublishXml` (entity) |
| 9 | BPF | `workflow` (clientdata JSON) | Deactivate → PATCH xaml → Activate | N/A (Activate regenerates) |
| 10 | BusinessRule | `workflow` (xaml) | Deactivate → PATCH xaml → Activate | N/A (Activate regenerates) |
| 11 | Ribbon | Solution export/import | Full cycle | `PublishAllXmlAsync` |
| 12 | ModernCmd | `appactions` + `RetrieveLocLabels` | Web API GET → `SetLocLabels` | `PublishXml` (entity) |
| 13 | EntityMsg | Translation package (Excel) | Export → modify → Import | `PublishXml` (entity) |
| 14 | ContentSnippet | `adx_contentsnippet` | Web API GET → Create/Update | N/A (no publish needed) |
| 15 | SiteMap | `sitemap` entity | Web API GET → PATCH sitemapxml | `PublishAllXml` |
| 16 | Dashboard | `systemform` + `RetrieveLocLabels` | Web API GET → `SetLocLabels` | `PublishDashboard` |
| 17 | WebResource | **Custom Action** | `pl_DataverseLabelTranslatorCustomAction` | Server-side publish |
| 18 | GlobalOptionSet | **Custom Action** | `pl_DataverseLabelTranslatorCustomAction` | Server-side publish |
