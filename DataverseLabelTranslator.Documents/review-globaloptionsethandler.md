# Review: GlobalOptionSetHandler.js

> Date: 2026-06-03
> Reviewed against:
> - `codex.apply-js-pattern.md`
> - `codex.handler-architecture-rewrite-plan.md`
>
> Status: Handler passes all acceptance criteria. Items below are minor code-quality and doc-sync fixes.

---

## Fixes Needed in GlobalOptionSetHandler.js

### 1. Redundant `else if` condition (line 132)

Current:

```js
} else if (!isDescription) {
    parent._emptyReadonlyPlaceholder = Helper.GetPlaceholderReadonly();
}
```

This is inside the `else` branch of `if (isDescription)` at line 124, so `!isDescription` is always true here. Simplify to `} else {`.

### 2. Unnecessary double negation (line 136)

Current:

```js
if (!!optionSet.TrueOption) {
```

`if` already coerces to boolean. Change to:

```js
if (optionSet.TrueOption) {
```

### 3. Duplicate `var options` declaration (lines 137 and 150)

`var options` is declared twice in the same function scope (ES5 `var` is function-scoped). It works but is confusing. Declare once before the `if`/`else`:

```js
var options;
if (optionSet.TrueOption) {
    options = [optionSet.TrueOption, optionSet.FalseOption];
} else {
    options = optionSet.Options;
    ...
}
```

### 4. Duplicated child-building loop (lines 138-148 vs 156-166)

The Boolean branch (`TrueOption`/`FalseOption`) and the regular `Options` branch build children with identical logic:

```js
var child = {
    recid: optionSet.MetadataId + idSeparator + option.Value,
    schemaName: option.Value.toString()
};
Helper.ApplyPlaceholder(child, editablePlaceholder, baseEditablePlaceholder, app);
Helper.AddLocalizedLabelsToRecord(child, labels);
parent.w2ui.children.push(child);
```

Extract a shared inner function, for example `BuildChildAndPush(parent, optionSet, option, ...)`, to DRY the two loops into one.

### 5. `component` sent at two levels in save payload (lines 88 and 221)

Each item in `optionValueUpdates` already carries `component` (line 88), and the top-level save payload also sends `component` (line 221). Confirm with the server whether both are needed. If the server reads only the top-level key, remove `component` from individual update objects, or vice versa.

---

## Fixes Needed in codex.handler-architecture-rewrite-plan.md

> The architecture plan was written earlier than `codex.apply-js-pattern.md`. Several examples drifted from the API that was actually implemented. The handler follows the newer `codex.apply-js-pattern.md` correctly; the older plan doc needs updating.

### 6. Helper function signatures out of date (lines 305-311)

Current in architecture doc:

```js
Helper.FinalizeGrid(records, includeSummaryChildren)
Helper.ValidateBaseLanguageNotEmpty(changes, rowPath)
```

Actual implemented API (matching `codex.apply-js-pattern.md`):

```js
Helper.FinalizeGrid(records, app)
Helper.ValidateBaseLanguageNotEmpty(record, changes, { app: app, getRowPath: fn })
```

Update the architecture doc signatures to match reality.

### 7. Handler Contract example calls `EasyTranslator` directly (lines 214-227)

Current in architecture doc:

```js
function BuildLoadInput() {
    return { solutionId: EasyTranslator.GetSolution() };
}
// ...
var records = EasyTranslator.GetAllRecords();
```

All rewritten handlers use `Helper.GetTranslator()` instead. The doc itself prescribes this at line 292-297. Update the Handler Contract example to use `var app = Helper.GetTranslator();` consistently.

### 8. `RunServerLoad` option keys out of date (lines 253-260)

Current in architecture doc:

```js
Helper.RunServerLoad({
    actionName: actionName,
    input: BuildLoadInput(),        // eager
    onSuccess: function (output) {} // old name
});
```

Actual implemented API:

```js
Helper.RunServerLoad({
    app: app,
    actionName: actionName,
    getPayload: function () { ... }, // lazy
    onLoaded: function (output) {}   // current name
});
```

Update: `input` → `getPayload` (function), `onSuccess` → `onLoaded`, add `app` option.

### 9. `RunServerSaveFlow` option keys out of date (lines 263-277)

Current in architecture doc:

```js
Helper.RunServerSaveFlow({
    actionName: actionName,
    getSavePayload: GetUpdates,
    getPublishPayload: function (output) { ... },
    hasPublishTargets: function (payload) { ... },
    reloadAction: function () { ... }
});
```

Actual implemented API:

```js
Helper.RunServerSaveFlow({
    app: app,
    actionName: actionName,
    getSavePayload: GetUpdates,
    getPublishPayload: fn,
    getPublishedPayload: fn,       // missing in doc
    shouldReload: fn,              // replaces hasPublishTargets
    reloadAction: fn
});
```

Update: add `app`, add `getPublishedPayload`, rename `hasPublishTargets` → `shouldReload`.
