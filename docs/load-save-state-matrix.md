# Load, Edit, Save State Matrix

## Scope

This report covers only the translation types in the main toolbar. Current code exposes menu numbers `0` through `18`:

- `0. All-In-One`
- `1. Attributes`
- `2. Option Sets`
- `3. Forms`
- `4. Views`
- `5. Form Metadata`
- `6. Entity Metadata`
- `7. Relationships`
- `8. Charts`
- `9. Business Process Flows`
- `10. Business Rules`
- `11. Ribbons`
- `12. Commands`
- `13. Entity Messages`
- `14. Content Snippets`
- `15. Sitemap`
- `16. Dashboards`
- `17. Web Resources`
- `18. Global Option Sets`

Out of scope: App Settings, AI Translate, Dictionary, About, and Help.

## Highest Priority: App Loading

Before standardizing any type-specific `Loading/Saving/Publishing/Reloading` flow, the app startup flow must be standardized first.

Current code evidence:

- `App.html` calls `XrmTranslator.Initialize(hasAllowedRole)` on `window.onload`.
- Startup currently can show multiple sequential messages, including:
  - `Loading entities`
  - `Preparing dictionary storage...`
  - publish-status resume/recovery banners from stored operation state

Required startup behavior:

- On `App.html` `window.onload`, show exactly one startup message: `App Loading`.
- The startup message owns the screen until initialization is finished.
- Do not replace `App Loading` with lower-level startup messages such as `Loading entities`, `Preparing dictionary storage...`, `Loading solution entities...`, or publish recovery checks.
- During `App Loading`, grid and toolbar are disabled/locked.
- Load and Save must be disabled during `App Loading`.
- Startup subtasks still run internally, but they do not publish separate user-facing loading messages.
- After app initialization finishes:
  - Hide `App Loading`.
  - Unlock/enable toolbar according to normal initial state.
  - Unlock grid.
  - Save remains disabled.
  - If a real persisted publish operation exists and is still valid, then and only then show/resume the publish banner after `App Loading` has finished.
- If startup fails:
  - Clear `App Loading`.
  - Unlock enough UI for recovery where safe.
  - Show one error banner/dialog with the startup failure.

Implementation rule: `App Loading` has higher priority than every type-specific load message. A user must never see several startup messages flash in sequence before the app is usable.

Implementation status: **Done for this iteration.**

- Added a shared startup state in `XrmTranslator.js`.
- `InitializeGrid()` starts `App Loading`.
- While `App Loading` is active, lower-level `LockGrid(...)` calls keep showing `App Loading`.
- `XrmTranslator.Initialize()` clears `App Loading` only after startup work finishes.
- Startup failure clears `App Loading` before surfacing the error.

## Shared Behavior Observed

The main grid uses `XrmTranslator.LockGrid(message)` and `XrmTranslator.UnlockGrid()` as the common lock mechanism.

When locked:

- The grid shows a lock overlay with the message and spinner.
- `body` gets `xqt-grid-locked`.
- The grid toolbar gets `xqt-toolbar-locked`.
- Toolbar buttons are visually/operationally locked by the shared toolbar lock CSS/logic.

When unlocked:

- The grid lock overlay is removed.
- The toolbar lock class is removed.
- Save is usually disabled immediately after a successful load or reload.
- Save becomes enabled after the user edits a grid cell and `XrmTranslator.HasPendingChanges()` returns true.

Before every normal save:

- The toolbar Save button is disabled immediately.
- Grid changes are normalized.
- Active publish/import operation checks run.
- If there are no pending changes, Save shows `No changes to save.` and returns without calling the handler.
- Save must not be disabled merely because there are no pending changes. Normal state after load, reload, click, search, and grid focus changes is Load enabled and Save enabled.
- Only active operations such as Loading, Saving, Publishing, Published delay, Reloading, App Loading, or stale publish recovery may disable Save.

## Current Matrix

| Type | 1. Load click message | 2. During load toolbar/grid/status | 3. After load toolbar/grid | 4. Save click message | 5. During save toolbar/grid/status | 6. After save action |
|---|---|---|---|---|---|---|
| `0. All-In-One` | `Loading ......` from `AllInOneHandler.Load()` | Grid is cleared and locked. Toolbar is locked. Child handler `grid.unlock()` calls are suppressed while each included type loads sequentially. No status banner. | Grid unlocks, toolbar unlocks, grouped records are shown. Save is disabled until edits are detected. | `Saving ......` | Grid and toolbar are locked. It saves changed groups by calling each child handler `SaveOnly()`, then runs one `PublishAllXmlRequest`. Child progress messages can appear, such as `Saving attribute batches n/m`. | Reloads All-In-One by calling `AllInOneHandler.Load()`. Final state is unlocked grouped grid, Save disabled. |
| `1. Attributes` | `Loading 1. Attributes` | Grid and toolbar are locked. No status banner. Handler retrieves customizable attributes. | `FillTable()` adds records, summary, unlocks grid/toolbar. Load and Save are enabled. | `Saving 1. Attributes`, then `Saving 1. Attributes n/m`, then banner `Publishing 1. Attributes...`, then banner `Published 1. Attributes`, then `Reloading 1. Attributes` | Grid spinner is used for Saving only, then hidden immediately. Publishing runs as a banner with Load/Save disabled and a minimum 5s visible duration. Published stays visible for 5s, then hides before Reloading starts. If there are no pending changes, shared save guard shows `No changes to save.` and does not call the handler. | Reloads Attributes after published banner is hidden. Grid/toolbar unlock after `FillTable()`. Load and Save are enabled. |
| `2. Option Sets` | `Loading <entity> attributes` | Grid and toolbar are locked. No status banner. Handler retrieves picklist, boolean, status, state, and multi-select metadata. | Grid filled and unlocked. Save disabled. | `Saving`, optional `Saving option set batches n/m`, then `Publishing`, then `Reloading` | Grid and toolbar locked. If no updates, skips publish and goes straight to `Reloading`. If updates exist, saves option values, publishes selected entity/global option sets, adds components to solution. | Reloads Option Sets. Grid/toolbar unlock after load. Save disabled. |
| `3. Forms` | `Loading <entity> attributes` | Grid and toolbar are locked. No status banner. Handler loads all forms for the selected entity or dashboard forms when entity is `none`. | Grid filled and unlocked. Save disabled. | `Saving`, `Saving forms 1/1`, then `Publishing`, then `Reloading` | Grid and toolbar locked. Updates form XML, adds system form to solution, publishes selected entity or dashboard XML when entity is `none`. | Reloads Forms. Last form id is preserved for reload selection. Grid/toolbar unlock after load. Save disabled. |
| `4. Views` | `Loading <entity> attributes` | Grid and toolbar locked. No status banner. Handler retrieves savedquery records and loc labels. | Grid filled and unlocked. Save disabled. | `Saving`, `Saving view batches n/m`, then `Publishing`, then `Reloading` | Grid and toolbar locked. Uses `SetLocLabels` batches, adds views to solution, publishes selected entity. | Reloads Views. Grid/toolbar unlock after load. Save disabled. |
| `5. Form Metadata` | `Loading <entity> attributes` | Grid and toolbar locked. No status banner. Handler retrieves active customizable forms and their name labels. | Grid filled and unlocked. Save disabled. | `Saving`, `Saving form metadata batches n/m`, then `Publishing`, then `Reloading` | Grid and toolbar locked. Uses `SetLocLabels`, adds system forms to solution, publishes selected entity or dashboards when entity is `none`. | Reloads Form Metadata. Grid/toolbar unlock after load. Save disabled. |
| `6. Entity Metadata` | `Loading <entity> attributes` | Grid and toolbar locked. No status banner. Handler retrieves `EntityDefinition`. | Grid filled with singular/plural rows and unlocked. Save disabled. | `Saving`, `Saving entity metadata 1/1`, then `Publishing`, then `Reloading` | Grid and toolbar locked. Updates `EntityDefinition` labels with `MSCRM.MergeLabels`, adds entity to solution, publishes selected entity. | Reloads Entity Metadata. Grid/toolbar unlock after load. Save disabled. |
| `7. Relationships` | `Loading <entity> attributes` | Grid and toolbar locked. No status banner. Handler retrieves 1:N, N:1, and N:N relationships and related entity plural names. | Grid filled and unlocked. Save disabled. | `Saving`, `Saving relationship batches n/m`, then `Publishing`, then `Reloading` | Grid and toolbar locked. Updates relationship associated menu labels, adds relationships to solution, publishes selected entity. | Reloads Relationships. Grid/toolbar unlock after load. Save disabled. |
| `8. Charts` | `Loading <entity> attributes` | Grid and toolbar locked. No status banner. Handler retrieves chart records and loc labels. | Grid filled and unlocked. Save disabled. | `Saving`, `Saving chart batches n/m`, then `Publishing`, then `Reloading` | Grid and toolbar locked. Uses `SetLocLabels`, adds charts to solution, publishes selected entity. | Reloads Charts. Grid/toolbar unlock after load. Save disabled. |
| `9. Business Process Flows` | `Loading <entity> attributes` | Grid and toolbar locked. No status banner. Handler retrieves BPF workflows and parses `clientdata`. | Grid filled and unlocked. Save disabled. | `Saving`, `Saving business process flows n/m`, then `Reloading` | Grid and toolbar locked. Deactivates each changed BPF, reads XAML, applies label changes, saves XAML, reactivates, and adds workflows to solution. No explicit publish step. | Reloads BPF. Grid/toolbar unlock after load. Save disabled. |
| `10. Business Rules` | `Loading <entity> attributes` | Grid and toolbar locked. No status banner. Handler loads attribute display names, retrieves business rule workflows, parses XAML/client data. | Grid filled and unlocked. Save disabled. | `Saving`, `Saving business rules n/m`, then `Reloading` | Grid and toolbar locked. For each changed rule, retrieves latest XAML, deactivates if active, applies label changes, saves, reactivates if needed, and adds workflows to solution. No explicit publish step. | Reloads Business Rules. Grid/toolbar unlock after load. Save disabled. |
| `11. Ribbons` | Status banner: `Preparing ribbon labels for <entity>.` then `Preparing ribbon helper solution for <entity>.`, then `Parsing ribbon labels.` | Ribbon skips the generic `Loading <entity> attributes` lock. It disables Load and Save explicitly and uses status banners. Grid can remain visually unlocked during helper solution preparation/export until records are filled. | Grid is filled and unlocked. Status banner shows `Ribbon labels loaded.` briefly. Load is re-enabled after delay. Save remains disabled until edits. | Status banner: `Preparing ribbon backup.` Then after backup choice: `Preparing ribbon import. Save and Load are temporarily disabled.` | Save uses status banners and operation-state blocking. Save/Load are disabled. It creates backup, imports helper solution, starts async PublishAll XML, commits grid changes, and shows publish status. | Grid unlocks after publish job is started, not necessarily after publish completes. Status remains governed by publish job polling/recovery. Save/Load availability follows operation state. |
| `12. Commands` | `Loading <entity> attributes`, then `Loading command labels 0/<count>` | Grid and toolbar locked. Handler resolves solution `appaction` ids, retrieves entity commands, then retrieves labels. | Grid filled and unlocked. Save disabled. | `Saving`, `Saving command labels n/m`, then `Publishing`, then `Reloading` | Grid and toolbar locked. Uses `SetLocLabels` for appaction label properties, then publishes selected entity. | Reloads Commands. Grid/toolbar unlock after load. Save disabled. |
| `13. Entity Messages` | `Loading entity messages` | Grid and toolbar locked. For custom entities, it returns an empty grid immediately. For OOB/system entities, it resolves solution/entity info, queries display string identity, exports translation package, parses `CrmTranslations.xml`. No separate status banner during load. | Grid is filled and unlocked for OOB/system entities, or empty and unlocked for custom entities. Save disabled. | Status banner: `Importing entity message translations. Save and Load are temporarily disabled.` Grid messages: `Re-exporting translations`, `Importing translations`, then status banner `Publishing entity messages for <entity>. Save and Load are temporarily disabled.`, then `Reloading entity messages` | Grid and toolbar locked by grid lock. Save/Load also blocked by operation state. It re-exports a fresh translation package, applies changed rows, imports translations, publishes selected entity. | Clears operation status, reloads Entity Messages, unlocks after load, Save disabled. |
| `14. Content Snippets` | `Loading <entity> attributes` | Grid and toolbar locked. Handler clears/rebuilds language columns with portal languages, retrieves content snippets. | Grid filled and unlocked. Save disabled. | `Saving`, optional `Saving content snippet batches n/m`, then `Reloading` | Grid and toolbar locked. Saves/creates snippet language rows. No explicit publish step. If no updates, reloads immediately. | Reloads Content Snippets. Grid/toolbar unlock after load. Save disabled. |
| `15. Sitemap` | `Loading <entity> attributes` even though this is solution-scoped | Grid and toolbar locked. If no solution is selected or no sitemap exists, handler unlocks and shows alert. Otherwise it loads sitemaps from selected solution. | Grid filled and unlocked. Save disabled. | `Saving`, optional `Saving sitemaps n/m`, then `Publishing`, then `Reloading` | Grid and toolbar locked. Updates sitemap XML, runs `PublishAllXmlRequest`, adds sitemaps to solution. If no sitemap data or no changes, unlocks and returns. | Reloads Sitemap. Grid/toolbar unlock after load. Save disabled. |
| `16. Dashboards` | `Loading <entity> attributes`; routed through `FormHandler` with entity `none` | Grid and toolbar locked. Handler loads dashboard forms. | Grid filled and unlocked. Save disabled. | `Saving`, `Saving forms 1/1`, then `Publishing`, then `Reloading` | Grid and toolbar locked. Updates dashboard form XML, adds system form with root-component settings, publishes dashboard XML through `PublishDashboard()`. | Reloads dashboard forms. Grid/toolbar unlock after load. Save disabled. |
| `17. Web Resources` | `Loading <entity> attributes` even though this is solution/global-scoped | Grid and toolbar locked. Handler gets base language, loads matching localized web resources, parses content. | Grid filled and unlocked. Save disabled. | `Saving`, `Saving web resource batches n/m`, then `Publishing`, then `Reloading` | Grid and toolbar locked. Saves or creates web resources, publishes changed web resources, adds them to solution. | Reloads Web Resources. Grid/toolbar unlock after load. Save disabled. |
| `18. Global Option Sets` | `Loading <entity> attributes` even though this is solution/global-scoped | Grid and toolbar locked. Handler reads global option set component ids from the selected solution, retrieves customizable global option set metadata, and filters/sorts it. | Grid filled and unlocked. Save disabled. | `Saving`, optional `Saving global option set batches n/m`, then `Publishing`, then `Reloading` | Grid and toolbar locked. Saves global option values, publishes only the changed global option sets, adds option sets to solution. If no updates, reloads immediately. | Reloads Global Option Sets. Grid/toolbar unlock after load. Save disabled. |

## Consistency Findings

The current app has several inconsistent loading/saving patterns:

1. Most types show the generic load text `Loading <entity> attributes`, even when the type is not attributes. This affects Option Sets, Forms, Views, Form Metadata, Entity Metadata, Relationships, Charts, BPF, Business Rules, Commands, Content Snippets, Sitemap, Dashboards, and Web Resources.
2. All-In-One uses `Loading ......` and `Saving ......`, which is not consistent with the rest of the app.
3. Ribbons uses status banners and disabled buttons instead of the normal grid lock pattern. This is justified for async import/publish, but load should still disable both toolbar and grid, and should show one merged load state instead of several step-specific banners.
4. Entity Messages uses a good guarded save banner, but load can be slow because it exports a translation package and currently only shows a grid lock message.
5. Some types skip publish by design: BPF, Business Rules, and Content Snippets. Their save flow should explicitly say `Saving ...` then `Reloading ...`, not imply publish.
6. Sitemap, Dashboards, and Web Resources are not entity-attribute loads, but the shared load message says `Loading <entity> attributes`.

## Proposed Standard UX

Use one standard state model for every type after `App Loading` has completed:

### Load

- On click Load:
  - Lock/disable the grid immediately.
  - Lock/disable the toolbar immediately, including Load and Save.
  - Disable Save.
  - Show exactly one type-specific load message: `Loading {ToolbarType}`.
  - `{ToolbarType}` is the same text shown in the toolbar type menu, including the number, for example `Loading 1. Attributes` or `Loading 2. Option Sets`.
- During long sub-steps:
  - Keep the same single load message visible.
  - Do not replace it with second-level load messages such as `Preparing ribbon helper solution...`, `Parsing ribbon labels...`, or `Exporting translation package...`.
  - Special handlers such as Ribbons and Entity Messages can do multiple internal load steps, but the user-facing load state should remain one merged state: `Loading {ToolbarType}`.
  - Use status banner only for cross-request or async operations that may survive page refresh, not for normal load sub-steps.
- After load succeeds:
  - Unlock grid.
  - Unlock toolbar.
  - Re-enable toolbar actions.
  - Hide transient status banner.
  - Enable both Load and Save.
  - Grid contains fresh records or an intentional empty state.
- After load fails:
  - Unlock grid/toolbar.
  - Re-enable toolbar actions except Save.
  - Save disabled unless existing dirty records must be preserved.
  - Show error.

### Edit

- Save is already enabled after load.
- On edit, the normal pending-change tracking still runs so Save can persist changes.
- If the user clicks Save with no pending changes, the shared save guard shows `No changes to save.` for about 3 seconds and returns without saving.

### Save

- On click Save:
  - Disable Save immediately.
  - If no pending changes, refresh grid state, show `No changes to save.` for about 3 seconds, keep Load and Save enabled, and return without calling the handler.
  - If changes exist, lock grid and toolbar.
  - Show exactly one type-specific save message: `Saving {ToolbarType}`.
  - Keep grid and toolbar locked for the whole synchronous save phase.
- During save:
  - Batch saves can show progress using the same type label, for example `Saving 1. Attributes 2/5`.
  - Do not show publish/reload text until the save phase has finished successfully.
- After save succeeds and before reload:
  - Start the publishing phase.
  - Every type must enter a `Publishing {ToolbarType}...` phase, even when there is no real Dataverse publish operation.
  - For types without a real publish step, run a dummy publish phase: show `Publishing {ToolbarType}...`, keep Load and Save disabled, wait about 5 seconds, then show `Published {ToolbarType}`.
  - Prefer async publish/poll behavior for every type that has a real publish operation, using the Ribbon model as the target UX.
  - Unlock the grid only when it is safe; Save and Load remain blocked while real or dummy publish is running.
  - Show a status banner such as `Publishing {ToolbarType}...`.
  - Poll publish state on a fixed schedule, for example wait 30 seconds, check attempt 1, then continue scheduled checks until success/failure/timeout.
  - On publish success, show `Published {ToolbarType}` for about 5 seconds, then hide the banner.
  - On dummy publish success, show `Published {ToolbarType}` for about 5 seconds, then hide the banner and re-enable both Load and Save.
  - On publish failure or timeout, keep a visible error/recovery banner and leave Save/Load guarded by the operation state.
- After publishing finishes:
  - Reload the same type.
  - Show exactly one reload message: `Reloading {ToolbarType}`.
- After save succeeds:
  - Unlock grid and toolbar after fresh data is available.
  - Enable both Load and Save.
- After save fails:
  - Unlock grid/toolbar unless an async operation is still active.
  - Restore Save enabled if pending changes remain.
  - Show error with recovery guidance for XML-mutating types.

## Proposed Message Catalog

| Type | Load message | Save message | Publish message | Reload message |
|---|---|---|---|---|
| `0. All-In-One` | `Loading 0. All-In-One` | `Saving 0. All-In-One` | `Publishing 0. All-In-One...` then `Published 0. All-In-One` | `Reloading 0. All-In-One` |
| `1. Attributes` | `Loading 1. Attributes` | `Saving 1. Attributes` | `Publishing 1. Attributes...` then `Published 1. Attributes` | `Reloading 1. Attributes` |
| `2. Option Sets` | `Loading 2. Option Sets` | `Saving 2. Option Sets` | `Publishing 2. Option Sets...` then `Published 2. Option Sets` | `Reloading 2. Option Sets` |
| `3. Forms` | `Loading 3. Forms` | `Saving 3. Forms` | `Publishing 3. Forms...` then `Published 3. Forms` | `Reloading 3. Forms` |
| `4. Views` | `Loading 4. Views` | `Saving 4. Views` | `Publishing 4. Views...` then `Published 4. Views` | `Reloading 4. Views` |
| `5. Form Metadata` | `Loading 5. Form Metadata` | `Saving 5. Form Metadata` | `Publishing 5. Form Metadata...` then `Published 5. Form Metadata` | `Reloading 5. Form Metadata` |
| `6. Entity Metadata` | `Loading 6. Entity Metadata` | `Saving 6. Entity Metadata` | `Publishing 6. Entity Metadata...` then `Published 6. Entity Metadata` | `Reloading 6. Entity Metadata` |
| `7. Relationships` | `Loading 7. Relationships` | `Saving 7. Relationships` | `Publishing 7. Relationships...` then `Published 7. Relationships` | `Reloading 7. Relationships` |
| `8. Charts` | `Loading 8. Charts` | `Saving 8. Charts` | `Publishing 8. Charts...` then `Published 8. Charts` | `Reloading 8. Charts` |
| `9. Business Process Flows` | `Loading 9. Business Process Flows` | `Saving 9. Business Process Flows` | Dummy `Publishing 9. Business Process Flows...` for about 5s, then `Published 9. Business Process Flows` | `Reloading 9. Business Process Flows` |
| `10. Business Rules` | `Loading 10. Business Rules` | `Saving 10. Business Rules` | Dummy `Publishing 10. Business Rules...` for about 5s, then `Published 10. Business Rules` | `Reloading 10. Business Rules` |
| `11. Ribbons` | `Loading 11. Ribbons` | `Saving 11. Ribbons` | `Publishing 11. Ribbons...` then `Published 11. Ribbons` | `Reloading 11. Ribbons` if a fresh readback is required; otherwise commit in-place after async publish success |
| `12. Commands` | `Loading 12. Commands` | `Saving 12. Commands` | `Publishing 12. Commands...` then `Published 12. Commands` | `Reloading 12. Commands` |
| `13. Entity Messages` | `Loading 13. Entity Messages` | `Saving 13. Entity Messages` | `Publishing 13. Entity Messages...` then `Published 13. Entity Messages` | `Reloading 13. Entity Messages` |
| `14. Content Snippets` | `Loading 14. Content Snippets` | `Saving 14. Content Snippets` | Dummy `Publishing 14. Content Snippets...` for about 5s, then `Published 14. Content Snippets` | `Reloading 14. Content Snippets` |
| `15. Sitemap` | `Loading 15. Sitemap` | `Saving 15. Sitemap` | `Publishing 15. Sitemap...` then `Published 15. Sitemap` | `Reloading 15. Sitemap` |
| `16. Dashboards` | `Loading 16. Dashboards` | `Saving 16. Dashboards` | `Publishing 16. Dashboards...` then `Published 16. Dashboards` | `Reloading 16. Dashboards` |
| `17. Web Resources` | `Loading 17. Web Resources` | `Saving 17. Web Resources` | `Publishing 17. Web Resources...` then `Published 17. Web Resources` | `Reloading 17. Web Resources` |
| `18. Global Option Sets` | `Loading 18. Global Option Sets` | `Saving 18. Global Option Sets` | `Publishing 18. Global Option Sets...` then `Published 18. Global Option Sets` | `Reloading 18. Global Option Sets` |

## Implementation Progress

Completed in this iteration:

| Area | Status | Files | Notes |
|---|---|---|---|
| `App Loading` | Done | `js/XrmTranslator.js` | Startup now has one high-priority message: `App Loading`. Lower-level startup messages are suppressed while app loading is active. |
| `1. Attributes` | Done | `js/XrmTranslator.js`, `js/AttributeHandler.js` | Load now shows `Loading 1. Attributes`. Load and Save are enabled after load. Save spinner shows `Saving 1. Attributes`; after save it unlocks grid, shows `Publishing 1. Attributes...` for at least 5s, then `Published 1. Attributes` for 5s, hides the banner, then shows `Reloading 1. Attributes`. After reload, Load and Save are enabled. |

Not done yet:

- `0. All-In-One`
- `2. Option Sets`
- `3. Forms`
- `4. Views`
- `5. Form Metadata`
- `6. Entity Metadata`
- `7. Relationships`
- `8. Charts`
- `9. Business Process Flows`
- `10. Business Rules`
- `11. Ribbons`
- `12. Commands`
- `13. Entity Messages`
- `14. Content Snippets`
- `15. Sitemap`
- `16. Dashboards`
- `17. Web Resources`
- `18. Global Option Sets`

## Next AI Implementation Guide

Start from `0. All-In-One` and `2. Option Sets` after reviewing the implemented pattern for `1. Attributes`.

### Shared Rules

Use the exact toolbar type text, including number:

```javascript
var toolbarType = XrmTranslator.GetCurrentToolbarTypeText();
```

For each type:

- Load message: `Loading {ToolbarType}`
- Save message: `Saving {ToolbarType}`
- Publish banner: `Publishing {ToolbarType}...`
- Publish success banner: `Published {ToolbarType}` with `autoHideMs: 5000`
- Reload message: `Reloading {ToolbarType}`

During load:

- Lock grid and toolbar immediately.
- Disable Load and Save while loading.
- After load finishes, enable both Load and Save.
- If Save is clicked with no pending changes, the shared guard shows `No changes to save.` and does not call the handler.
- Do not wire grid click/change/focus events to disable Save based on pending changes. Save is intentionally always available outside active operations.
- Do not show sub-step messages to the user.

During save:

- Lock grid and toolbar immediately.
- Disable Save.
- If publish is real, keep Load and Save disabled until publish returns or async publish monitoring takes over.
- If publish is dummy, disable Load and Save during the dummy publish timer, then re-enable both Load and Save.

### How To Implement `0. All-In-One`

File: `js/AllInOneHandler.js`

Current messages:

- `Loading ......`
- `Saving ......`

Target changes:

- In `AllInOneHandler.Load()`, replace the load lock with `Loading 0. All-In-One`.
- Keep child handler unlock suppression.
- Ensure child handler load messages do not replace the parent All-In-One load message.
- In `AllInOneHandler.Save()`, replace save lock with `Saving 0. All-In-One`.
- After child `SaveOnly()` calls finish, show `Publishing 0. All-In-One...`.
- Existing `PublishAllXmlRequest` is a real publish. Wrap it with the shared publish banner behavior.
- On success, show `Published 0. All-In-One` for about 5 seconds.
- Then reload using `Reloading 0. All-In-One`.
- Ensure Load/Save cannot remain disabled if save/publish/reload fails.

### How To Implement `2. Option Sets`

File: `js/OptionSetHandler.js`

Current load message is from `XrmTranslator.TriggerLoading()`:

- `Loading <entity> attributes`

Target changes:

- In `XrmTranslator.TriggerLoading()`, add a branch for `XrmTranslator.GetType() === "options"` and show `Loading 2. Option Sets`.
- In `OptionSetHandler.Save()`, use `Saving 2. Option Sets`.
- Batch progress should use `Saving 2. Option Sets n/m`.
- For real publish, show `Publishing 2. Option Sets...`, keep Load/Save disabled, call existing publish, then show `Published 2. Option Sets` for about 5 seconds.
- Reload using `Reloading 2. Option Sets`.
- If there are no updates, the shared Save click guard should return without saving; Load and Save stay enabled.

## Implementation Recommendation

Add a small shared helper for type state labels, for example:

```javascript
XrmTranslator.TypeStateLabels = {
    attributes: "Attributes",
    options: "Option Sets",
    forms: "Forms",
    views: "Views",
    formMeta: "Form Metadata",
    entityMeta: "Entity Metadata",
    relationships: "Relationships",
    charts: "Charts",
    bpf: "Business Process Flows",
    businessRules: "Business Rules",
    ribbons: "Ribbons",
    commands: "Commands",
    entityMessages: "Entity Messages",
    content: "Content Snippets",
    sitemap: "Sitemap",
    dashboards: "Dashboards",
    webresources: "Web Resources",
    globalOptionSets: "Global Option Sets",
    allInOne: "All-In-One"
};
```

Then expose wrappers:

```javascript
XrmTranslator.StartAppLoading(); // message: "App Loading"
XrmTranslator.ClearAppLoading();
XrmTranslator.LockTypeLoad(); // message: "Loading {ToolbarType}"
XrmTranslator.LockTypeSave();
XrmTranslator.StartTypePublishStatus();
XrmTranslator.PollTypePublishStatus();
XrmTranslator.LockTypeReload();
XrmTranslator.LockTypeProgress(action, current, total);
```

This keeps the handler code readable and prevents future copy/paste messages such as `Loading <entity> attributes` from leaking into unrelated types.

`StartAppLoading()` must be called by `XrmTranslator.Initialize()` before lower-level startup work begins. While app loading is active, calls such as `LockGrid("Loading entities")` or `LockGrid("Preparing dictionary storage...")` should either be suppressed or treated as internal diagnostics only.

Ribbons and Entity Messages should still use operation status banners for long import/publish operations, but their initial load must be merged into the same single `Loading {ToolbarType}` state and must disable both toolbar and grid until load finishes.

Publishing should be implemented as a shared async phase after the synchronous save phase. The target behavior is:

1. Handler finishes data save.
2. App starts publish operation state and shows banner `Publishing {ToolbarType}...`.
3. App waits about 30 seconds before the first poll.
4. If poll attempt 1 succeeds, show `Published {ToolbarType}` for about 5 seconds, then hide the banner.
5. If not complete, continue scheduled polling with Save/Load blocked.
6. If publish fails or times out, keep an error banner and recovery guidance.
7. After publish succeeds, run `Reloading {ToolbarType}` with the same grid/toolbar lock behavior as loading.

## Stale Publish Status Recovery

The publish status must not be able to permanently lock Load and Save if the app is closed, refreshed, crashes, or a browser timer is killed.

Required safeguards:

1. Persist operation state only for real async publish operations that can survive page refresh.
2. Do not persist dummy publish operations. Dummy publish should be timer-only and must auto-clear after about 10 seconds maximum, even if the 5 second success timer fails.
3. Every persisted operation state must include:
   - `type`
   - `toolbarType`
   - `operationId` or publish job id
   - `phase`
   - `createdOn`
   - `updatedOn`
   - `expiresOn`
   - `pollAttempt`
4. On app startup and before every Load/Save click, validate persisted operation state.
5. If `expiresOn` is in the past, clear the operation state, hide the banner, and re-enable both Load and Save.
6. If the operation is a real publish job and `expiresOn` is not expired, resume polling and keep Load/Save blocked.
7. If the job cannot be found after the first resumed poll, show a warning banner and allow the user to clear the stale lock.
8. Add a visible recovery action for stale locks, for example `Clear publish status`, so the user is never trapped.
9. Always call a shared `ClearTypeOperationStatus()` in `finally` paths for success, handled error, and no-op save.
10. Button state must be derived from current operation state, not from pending grid changes or a stale disabled flag left on the toolbar. Outside active operations, Load and Save stay enabled.

Recommended timeout policy:

| Operation kind | Persisted | First poll | Max lock time | Recovery |
|---|---:|---:|---:|---|
| Real async publish | Yes | 30s | 10 minutes unless job is still explicitly running | Resume polling or show stale-lock warning |
| Synchronous publish wrapped as async UX | Optional | 0-5s | 60 seconds | Auto-clear on timeout |
| Dummy publish | No | Not applicable | 10 seconds | Auto-clear unconditionally |
