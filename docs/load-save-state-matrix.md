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
- Load and Save are enabled after a successful load or reload, unless an active operation is still blocking them.
- Save availability is not tied to `XrmTranslator.HasPendingChanges()` outside active operations.

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
| `0. All-In-One` | `Loading 0. All-In-One` | Grid is cleared and locked. Toolbar is locked. Child handler `grid.unlock()` calls are suppressed while each included type loads sequentially. No status banner. | Grid unlocks, toolbar unlocks, grouped DisplayName records are shown. Load and Save are enabled. | `Saving 0. All-In-One`, then banner `Publishing 0. All-In-One`, then banner `Published 0. All-In-One`, then `Reloading 0. All-In-One` | Grid spinner is used for Saving only. It saves changed groups by calling each child handler `SaveOnly()`, then runs one `PublishAllXmlRequest` in the shared publish phase. Child batch progress messages can appear during the save phase. | Reloads All-In-One after published banner is hidden. Load and Save are enabled. |
| `1. Attributes` | `Loading 1. Attributes` | Grid and toolbar are locked. No status banner. Handler retrieves customizable attributes. | `FillTable()` adds records, summary, unlocks grid/toolbar. Load and Save are enabled. | `Saving 1. Attributes`, then `Saving 1. Attributes n/m`, then banner `Publishing 1. Attributes`, then banner `Published 1. Attributes`, then `Reloading 1. Attributes` | Grid spinner is used for Saving only, then hidden immediately. Publishing runs as a banner with Load/Save disabled and a minimum 5s visible duration. Published stays visible for 5s, then hides before Reloading starts. If there are no pending changes, shared save guard shows `No changes to save.` and does not call the handler. | Reloads Attributes after published banner is hidden. Grid/toolbar unlock after `FillTable()`. Load and Save are enabled. |
| `2. Option Sets` | `Loading 2. Option Sets` | Grid and toolbar are locked. No status banner. Handler retrieves picklist, boolean, status, state, and multi-select metadata. | Grid filled and unlocked. Load and Save are enabled. | `Saving 2. Option Sets`, then banner `Publishing 2. Option Sets`, then banner `Published 2. Option Sets`, then `Reloading 2. Option Sets` | Grid spinner is used for Saving only, then hidden. Publishing runs as a banner with Load/Save disabled and a minimum 5s visible duration. Real publish includes selected entity and changed global option sets. | Reloads Option Sets after published banner is hidden. Load and Save are enabled. |
| `3. Forms` | `Loading 3. Forms` | Grid and toolbar are locked. No status banner. Handler loads all forms for the selected entity or dashboard forms when entity is `none`. This type is scoped to labels inside form XML. | Grid filled and unlocked. Load and Save are enabled. | `Saving 3. Forms`, then banner `Publishing 3. Forms`, then banner `Published 3. Forms`, then `Reloading 3. Forms` | Grid spinner is used for Saving only. Changes update `systemform.formxml`. Publishing runs as a banner with Load/Save disabled and a minimum 5s visible duration. Publishes selected entity or dashboard XML when entity is `none`. | Reloads Forms. Load and Save are enabled. |
| `4. Views` | `Loading 4. Views` | Grid and toolbar locked. No status banner. Handler retrieves savedquery records with loc labels for the selected component: `name` for DisplayName or `description` for Description. | Grid filled with the selected component only and unlocked. Load and Save are enabled. | `Saving 4. Views`, then banner `Publishing 4. Views`, then banner `Published 4. Views`, then `Reloading 4. Views` | Grid spinner is used for Saving only. Uses `SetLocLabels` for the selected savedquery attribute (`name` or `description`), adds changed views to solution, publishes selected entity. | Reloads Views. Load and Save are enabled. |
| `5. Form Metadata` | `Loading 5. Form Metadata` | Grid and toolbar locked. No status banner. Handler retrieves active customizable forms and loc labels for the selected component: `name` for DisplayName or `description` for Description. | Grid filled and unlocked. Load and Save are enabled. | `Saving 5. Form Metadata`, then banner `Publishing 5. Form Metadata`, then banner `Published 5. Form Metadata`, then `Reloading 5. Form Metadata` | Grid spinner is used for Saving only. Uses `SetLocLabels` for the selected systemform attribute (`name` or `description`). Publishing runs as a banner with Load/Save disabled and a minimum 5s visible duration. Publishes selected entity or dashboards when entity is `none`. | Reloads Form Metadata. Load and Save are enabled. |
| `6. Entity Metadata` | `Loading 6. Entity Metadata` | Grid and toolbar locked. No status banner. Handler retrieves `EntityDefinition`. | Grid filled with singular/plural rows and unlocked. Load and Save are enabled. | `Saving 6. Entity Metadata`, then banner `Publishing 6. Entity Metadata`, then banner `Published 6. Entity Metadata`, then `Reloading 6. Entity Metadata` | Grid spinner is used for Saving only. Publishing runs as a banner with Load/Save disabled and a minimum 5s visible duration. Updates `EntityDefinition` labels with `MSCRM.MergeLabels`, adds entity to solution, publishes selected entity. | Reloads Entity Metadata. Load and Save are enabled. |
| `7. Relationships` | `Loading 7. Relationships` | Grid and toolbar locked. No status banner. Handler retrieves 1:N, N:1, and N:N relationships and related entity plural names. | Grid filled and unlocked. Load and Save are enabled. | `Saving 7. Relationships`, then banner `Publishing 7. Relationships`, then banner `Published 7. Relationships`, then `Reloading 7. Relationships` | Grid spinner is used for Saving only. Publishing runs as a banner with Load/Save disabled and a minimum 5s visible duration. Updates relationship associated menu labels, adds relationships to solution, publishes selected entity. | Reloads Relationships. Load and Save are enabled. |
| `8. Charts` | `Loading 8. Charts` | Grid and toolbar locked. No status banner. Handler retrieves chart records and loc labels. | Grid filled and unlocked. Load and Save are enabled. | `Saving 8. Charts`, then banner `Publishing 8. Charts`, then banner `Published 8. Charts`, then `Reloading 8. Charts` | Grid spinner is used for Saving only. Publishing runs as a banner with Load/Save disabled and a minimum 5s visible duration. Uses `SetLocLabels`, adds charts to solution, publishes selected entity. | Reloads Charts. Load and Save are enabled. |
| `9. Business Process Flows` | `Loading 9. Business Process Flows` | Grid and toolbar locked. No status banner. Handler retrieves BPF workflows and parses `clientdata`. | Grid filled and unlocked. Load and Save are enabled. | `Saving 9. Business Process Flows`, then dummy banner `Publishing 9. Business Process Flows`, then banner `Published 9. Business Process Flows`, then `Reloading 9. Business Process Flows` | Grid spinner is used for Saving only. Dummy publishing keeps Load/Save disabled for at least 5s because BPF has no explicit publish step. | Reloads BPF. Load and Save are enabled. |
| `10. Business Rules` | `Loading 10. Business Rules` | Grid and toolbar locked. No status banner. Handler loads attribute display names, retrieves business rule workflows, parses XAML/client data. | Grid filled and unlocked. Load and Save are enabled. | `Saving 10. Business Rules`, then dummy banner `Publishing 10. Business Rules`, then banner `Published 10. Business Rules`, then `Reloading 10. Business Rules` | Grid spinner is used for Saving only. Dummy publishing keeps Load/Save disabled for at least 5s because Business Rules has no explicit publish step. | Reloads Business Rules. Load and Save are enabled. |
| `11. Ribbons` | `Loading 11. Ribbons` | Grid and toolbar are locked. Helper solution reset/export and XML parse sub-steps are internal and do not replace the load message. | Grid is filled and unlocked. Load and Save are enabled. | `Saving 11. Ribbons`, then after backup choice banner/grid state `Publishing 11. Ribbons` | Save prepares/downloads the backup, imports the helper solution, starts async PublishAll XML, commits grid changes, and stores a publish guard labelled `Publishing 11. Ribbons`. | Grid unlocks after the async publish job is started. Publish status/polling blocks Load and Save until completion or recovery. Completion banner is `Published 11. Ribbons`. |
| `12. Commands` | `Loading 12. Commands` | Grid and toolbar locked. Handler resolves solution `appaction` ids, retrieves entity commands, then retrieves labels without replacing the load message. | Grid filled and unlocked. Load and Save are enabled. | `Saving 12. Commands`, then banner `Publishing 12. Commands`, then banner `Published 12. Commands`, then `Reloading 12. Commands` | Grid spinner is used for Saving only. Uses `SetLocLabels` for appaction label properties, then publishes selected entity. | Reloads Commands. Load and Save are enabled. |
| `13. Entity Messages` | `Loading 13. Entity Messages` | Grid and toolbar locked. For custom entities, it returns an empty grid immediately. For OOB/system entities, it resolves solution/entity info, queries display string identity, exports the translation package, and parses `CrmTranslations.xml` without replacing the load message. | Grid is filled and unlocked for OOB/system entities, or empty and unlocked for custom entities. Load and Save are enabled. | `Saving 13. Entity Messages`, then banner `Publishing 13. Entity Messages`, then banner `Published 13. Entity Messages`, then `Reloading 13. Entity Messages` | Grid spinner is used for Saving only. It re-exports a fresh translation package, applies changed rows, imports translations, then publishes selected entity in the shared publish phase. | Reloads Entity Messages. Load and Save are enabled. |
| `14. Content Snippets` | `Loading 14. Content Snippets` | Grid and toolbar locked. Handler clears/rebuilds language columns with portal languages and retrieves content snippets. | Grid filled and unlocked. Load and Save are enabled. | `Saving 14. Content Snippets`, then dummy banner `Publishing 14. Content Snippets`, then banner `Published 14. Content Snippets`, then `Reloading 14. Content Snippets` | Grid spinner is used for Saving only. Saves/creates snippet language rows. Dummy publishing keeps Load/Save disabled for at least 5s because this type has no explicit publish step. | Reloads Content Snippets. Load and Save are enabled. |
| `15. Sitemap` | `Loading 15. Sitemap` | Grid and toolbar locked. If no solution is selected or no sitemap exists, handler unlocks, re-enables Load/Save, and shows an alert. Otherwise it loads sitemaps from the selected solution. | Grid filled and unlocked. Load and Save are enabled. | `Saving 15. Sitemap`, then banner `Publishing 15. Sitemap`, then banner `Published 15. Sitemap`, then `Reloading 15. Sitemap` | Grid spinner is used for Saving only. Updates sitemap XML, runs `PublishAllXmlRequest`, and adds changed sitemaps to the solution in the shared publish phase. | Reloads Sitemap. Load and Save are enabled. |
| `16. Dashboards` | `Loading 16. Dashboards` | Grid and toolbar locked. Routed through `FormHandler` with entity `none`; handler loads dashboard forms. | Grid filled and unlocked. Load and Save are enabled. | `Saving 16. Dashboards`, then banner `Publishing 16. Dashboards`, then banner `Published 16. Dashboards`, then `Reloading 16. Dashboards` | Grid spinner is used for Saving only. Updates dashboard form XML, adds system form with root-component settings, and publishes dashboard XML through `PublishDashboard()`. | Reloads dashboard forms. Load and Save are enabled. |
| `17. Web Resources` | `Loading 17. Web Resources` | Grid and toolbar locked. Handler gets base language, loads matching localized web resources, and parses content. | Grid filled and unlocked. Load and Save are enabled. | `Saving 17. Web Resources`, then banner `Publishing 17. Web Resources`, then banner `Published 17. Web Resources`, then `Reloading 17. Web Resources` | Grid spinner is used for Saving only. Saves or creates web resources, publishes changed web resources, and adds them to the solution in the shared publish phase. | Reloads Web Resources. Load and Save are enabled. |
| `18. Global Option Sets` | `Loading 18. Global Option Sets` | Grid and toolbar locked. Handler reads global option set component ids from the selected solution, retrieves customizable global option set metadata, and filters/sorts it. | Grid filled and unlocked. Load and Save are enabled. | `Saving 18. Global Option Sets`, then banner `Publishing 18. Global Option Sets`, then banner `Published 18. Global Option Sets`, then `Reloading 18. Global Option Sets` | Grid spinner is used for Saving only. Saves global option values, publishes only changed global option sets, and adds them to the solution in the shared publish phase. | Reloads Global Option Sets. Load and Save are enabled. |

## Consistency Findings

The previously inconsistent loading/saving patterns are resolved for toolbar types `0` through `18`:

1. All types now enter load with `Loading {ToolbarType}` instead of generic entity-attribute text or handler-specific sub-step text.
2. All-In-One now uses the shared type save flow around its single final `PublishAllXmlRequest`.
3. Ribbons keeps its async import/publish guard, but normal load sub-steps are internal and the persisted publish status is labelled `Publishing 11. Ribbons`.
4. Entity Messages keeps translation package export/import internal to the save phase and uses the shared publish/reload sequence.
5. Content Snippets uses dummy publishing because it has no explicit Dataverse publish step.
6. Sitemap, Dashboards, Web Resources, and Global Option Sets now use their own toolbar type labels instead of entity-attribute load/save text.

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
  - Every type must enter a `Publishing {ToolbarType}` phase, even when there is no real Dataverse publish operation.
  - For types without a real publish step, run a dummy publish phase: show `Publishing {ToolbarType}`, keep Load and Save disabled, wait about 5 seconds, then show `Published {ToolbarType}`.
  - Prefer async publish/poll behavior for every type that has a real publish operation, using the Ribbon model as the target UX.
  - Unlock the grid only when it is safe; Save and Load remain blocked while real or dummy publish is running.
  - Show a status banner such as `Publishing {ToolbarType}`.
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
| `0. All-In-One` | `Loading 0. All-In-One` | `Saving 0. All-In-One` | `Publishing 0. All-In-One` then `Published 0. All-In-One` | `Reloading 0. All-In-One` |
| `1. Attributes` | `Loading 1. Attributes` | `Saving 1. Attributes` | `Publishing 1. Attributes` then `Published 1. Attributes` | `Reloading 1. Attributes` |
| `2. Option Sets` | `Loading 2. Option Sets` | `Saving 2. Option Sets` | `Publishing 2. Option Sets` then `Published 2. Option Sets` | `Reloading 2. Option Sets` |
| `3. Forms` | `Loading 3. Forms` | `Saving 3. Forms` | `Publishing 3. Forms` then `Published 3. Forms` | `Reloading 3. Forms` |
| `4. Views` | `Loading 4. Views` | `Saving 4. Views` | `Publishing 4. Views` then `Published 4. Views` | `Reloading 4. Views` |
| `5. Form Metadata` | `Loading 5. Form Metadata` | `Saving 5. Form Metadata` | `Publishing 5. Form Metadata` then `Published 5. Form Metadata` | `Reloading 5. Form Metadata` |
| `6. Entity Metadata` | `Loading 6. Entity Metadata` | `Saving 6. Entity Metadata` | `Publishing 6. Entity Metadata` then `Published 6. Entity Metadata` | `Reloading 6. Entity Metadata` |
| `7. Relationships` | `Loading 7. Relationships` | `Saving 7. Relationships` | `Publishing 7. Relationships` then `Published 7. Relationships` | `Reloading 7. Relationships` |
| `8. Charts` | `Loading 8. Charts` | `Saving 8. Charts` | `Publishing 8. Charts` then `Published 8. Charts` | `Reloading 8. Charts` |
| `9. Business Process Flows` | `Loading 9. Business Process Flows` | `Saving 9. Business Process Flows` | Dummy `Publishing 9. Business Process Flows` for about 5s, then `Published 9. Business Process Flows` | `Reloading 9. Business Process Flows` |
| `10. Business Rules` | `Loading 10. Business Rules` | `Saving 10. Business Rules` | Dummy `Publishing 10. Business Rules` for about 5s, then `Published 10. Business Rules` | `Reloading 10. Business Rules` |
| `11. Ribbons` | `Loading 11. Ribbons` | `Saving 11. Ribbons` | `Publishing 11. Ribbons` then `Published 11. Ribbons` | `Reloading 11. Ribbons` if a fresh readback is required; otherwise commit in-place after async publish success |
| `12. Commands` | `Loading 12. Commands` | `Saving 12. Commands` | `Publishing 12. Commands` then `Published 12. Commands` | `Reloading 12. Commands` |
| `13. Entity Messages` | `Loading 13. Entity Messages` | `Saving 13. Entity Messages` | `Publishing 13. Entity Messages` then `Published 13. Entity Messages` | `Reloading 13. Entity Messages` |
| `14. Content Snippets` | `Loading 14. Content Snippets` | `Saving 14. Content Snippets` | Dummy `Publishing 14. Content Snippets` for about 5s, then `Published 14. Content Snippets` | `Reloading 14. Content Snippets` |
| `15. Sitemap` | `Loading 15. Sitemap` | `Saving 15. Sitemap` | `Publishing 15. Sitemap` then `Published 15. Sitemap` | `Reloading 15. Sitemap` |
| `16. Dashboards` | `Loading 16. Dashboards` | `Saving 16. Dashboards` | `Publishing 16. Dashboards` then `Published 16. Dashboards` | `Reloading 16. Dashboards` |
| `17. Web Resources` | `Loading 17. Web Resources` | `Saving 17. Web Resources` | `Publishing 17. Web Resources` then `Published 17. Web Resources` | `Reloading 17. Web Resources` |
| `18. Global Option Sets` | `Loading 18. Global Option Sets` | `Saving 18. Global Option Sets` | `Publishing 18. Global Option Sets` then `Published 18. Global Option Sets` | `Reloading 18. Global Option Sets` |

## Implementation Progress

Completed in this iteration:

| Area | Status | Files | Notes |
|---|---|---|---|
| `App Loading` | Done | `js/XrmTranslator.js` | Startup now has one high-priority message: `App Loading`. Lower-level startup messages are suppressed while app loading is active. |
| `1. Attributes` | Done | `js/XrmTranslator.js`, `js/AttributeHandler.js` | Load now shows `Loading 1. Attributes`. Save uses the shared type save flow with real publish, 5s minimum publishing banner, 5s published banner, then `Reloading 1. Attributes`. After load/reload, Load and Save are enabled. |
| `2. Option Sets` | Done | `js/XrmTranslator.js`, `js/OptionSetHandler.js` | Load now shows `Loading 2. Option Sets`. Save uses the shared type save flow with real publish, 5s minimum publishing banner, 5s published banner, then `Reloading 2. Option Sets`. |
| `3. Forms` | Done | `js/XrmTranslator.js`, `js/FormHandler.js` | Load now shows `Loading 3. Forms`. Forms remains scoped to labels inside form XML; form metadata description belongs to `5. Form Metadata`. Save uses the shared type save flow with real entity/dashboard publish, 5s minimum publishing banner, 5s published banner, then `Reloading 3. Forms`. |
| `4. Views` | Done | `js/XrmTranslator.js`, `js/ViewHandler.js` | Load now shows `Loading 4. Views`. The component toolbar is enabled for Views: `DisplayName` saves `savedquery.name`, `Description` saves `savedquery.description`, including empty description rows for adding new translations. Save uses the shared type save flow with real publish and reload. |
| `5. Form Metadata` | Done | `js/XrmTranslator.js`, `js/FormMetaHandler.js` | Load now shows `Loading 5. Form Metadata`. The component toolbar is enabled for Form Metadata: `DisplayName` saves `systemform.name`, `Description` saves `systemform.description`. Save uses the shared type save flow with real entity/dashboard publish and reload. |
| `6. Entity Metadata` | Done | `js/XrmTranslator.js`, `js/EntityHandler.js` | Load now shows `Loading 6. Entity Metadata`. Save uses the shared type save flow with real publish and reload. |
| `7. Relationships` | Done | `js/XrmTranslator.js`, `js/RelationshipHandler.js` | Load now shows `Loading 7. Relationships`. Save uses the shared type save flow with real publish and reload. |
| `8. Charts` | Done | `js/XrmTranslator.js`, `js/ChartHandler.js` | Load now shows `Loading 8. Charts`. Save uses the shared type save flow with real publish and reload. |
| `9. Business Process Flows` | Done | `js/XrmTranslator.js`, `js/BpfHandler.js` | Load now shows `Loading 9. Business Process Flows`. Save uses the shared type save flow with dummy publishing because this type has no explicit publish step. |
| `10. Business Rules` | Done | `js/XrmTranslator.js`, `js/BusinessRuleHandler.js` | Load now shows `Loading 10. Business Rules`. Save uses the shared type save flow with dummy publishing because this type has no explicit publish step. |
| `0. All-In-One` | Done | `js/XrmTranslator.js`, `js/AllInOneHandler.js` | Load now shows `Loading 0. All-In-One`. Save uses the shared type save flow, saves changed child groups, runs one real `PublishAllXmlRequest`, shows the 5s publishing/published banners, then reloads. |
| `11. Ribbons` | Done | `js/XrmTranslator.js`, `js/RibbonHandler.js` | Load now shows one merged `Loading 11. Ribbons` state. Save uses `Saving 11. Ribbons`, then the async import/publish guard is labelled `Publishing 11. Ribbons` and completion shows `Published 11. Ribbons`. |
| `12. Commands` | Done | `js/XrmTranslator.js`, `js/ModernCommandHandler.js` | Load now shows `Loading 12. Commands` without command-label sub-step text. Save uses the shared type save flow with real entity publish and reload. |
| `13. Entity Messages` | Done | `js/XrmTranslator.js`, `js/EntityMessageHandler.js` | Load now shows `Loading 13. Entity Messages`. Save imports changed translation package rows under `Saving 13. Entity Messages`, then uses the shared publish/reload flow. |
| `14. Content Snippets` | Done | `js/XrmTranslator.js`, `js/ContentSnippetHandler.js` | Load now shows `Loading 14. Content Snippets`. Save uses the shared type save flow with dummy publishing because this type has no explicit publish step. |
| `15. Sitemap` | Done | `js/XrmTranslator.js`, `js/SiteMapHandler.js` | Load now shows `Loading 15. Sitemap`. Save uses the shared type save flow with real `PublishAllXmlRequest`, solution component add, and reload. |
| `16. Dashboards` | Done | `js/XrmTranslator.js`, `js/FormHandler.js` | Load now shows `Loading 16. Dashboards`. FormHandler progress labels use the active toolbar type, so dashboard save/publish/reload now says `16. Dashboards`. |
| `17. Web Resources` | Done | `js/XrmTranslator.js`, `js/WebResourceHandler.js` | Load now shows `Loading 17. Web Resources`. Save uses the shared type save flow with web resource publish, solution component add, and reload. |
| `18. Global Option Sets` | Done | `js/XrmTranslator.js`, `js/GlobalOptionSetHandler.js` | Load now shows `Loading 18. Global Option Sets`. Save uses the shared type save flow with changed option set publish, solution component add, and reload. |

Not done yet:

- None.

## Maintenance Guide

All toolbar types `0` through `18` now follow the standardized load/save state model. Use this guide when adding a new type or changing an existing handler.

### Shared Rules

Use the exact toolbar type text, including number:

```javascript
var toolbarType = XrmTranslator.GetCurrentToolbarTypeText();
```

For each type:

- Load message: `Loading {ToolbarType}`
- Save message: `Saving {ToolbarType}`
- Publish banner: `Publishing {ToolbarType}`
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
- Prefer `XrmTranslator.RunTypeSaveFlow({ saveAction, publishAction, reloadAction })` for normal synchronous save/publish/reload handlers.
- Omit `publishAction` only for dummy publishing types.

### All-In-One Notes

`0. All-In-One` keeps child handler unlock suppression while it loads each included type. The parent type owns the user-facing state messages through `TriggerLoading()` and `RunTypeSaveFlow()`, so child load messages do not replace `Loading 0. All-In-One`.

All-In-One saves changed child groups first, then runs one real `PublishAllXmlRequest` in the shared publish phase. Forms remain DisplayName-only inside All-In-One; form descriptions are handled by standalone `3. Forms` with component `Description`.

### Completed Pattern For All Types

Files:

- `js/AllInOneHandler.js`
- `js/AttributeHandler.js`
- `js/OptionSetHandler.js`
- `js/FormHandler.js`
- `js/ViewHandler.js`
- `js/FormMetaHandler.js`
- `js/EntityHandler.js`
- `js/RelationshipHandler.js`
- `js/ChartHandler.js`
- `js/BpfHandler.js`
- `js/BusinessRuleHandler.js`
- `js/RibbonHandler.js`
- `js/ModernCommandHandler.js`
- `js/EntityMessageHandler.js`
- `js/ContentSnippetHandler.js`
- `js/SiteMapHandler.js`
- `js/WebResourceHandler.js`
- `js/GlobalOptionSetHandler.js`

Implemented pattern:

- `XrmTranslator.TriggerLoading()` shows `Loading {ToolbarType}` for all types `0` through `18`.
- Each `FillTable()` enables both Load and Save after grid unlock.
- Normal synchronous handlers use `XrmTranslator.RunTypeSaveFlow(...)`.
- Real publish types pass `publishAction`; BPF, Business Rules, and Content Snippets omit it, which runs dummy publishing for at least 5 seconds.
- Ribbons keeps async import/publish status handling but uses the same toolbar type labels for load, save, publish, and completion.
- After `Published {ToolbarType}` stays visible for about 5 seconds, the helper hides the banner and runs `Reloading {ToolbarType}`.

## Existing Shared Helpers And Recommendation

Current shared helpers:

- `XrmTranslator.GetCurrentToolbarTypeText()` returns the exact toolbar label including number.
- `XrmTranslator.EnableLoadAndSave()` re-enables both operation buttons after a successful load/reload or handled failure.
- `XrmTranslator.RunTypeSaveFlow({ saveAction, publishAction, reloadAction })` handles:
  - `Saving {ToolbarType}` grid lock
  - `Publishing {ToolbarType}` banner with Load/Save disabled for at least 5 seconds
  - `Published {ToolbarType}` banner for about 5 seconds
  - `Reloading {ToolbarType}` grid lock
  - Load/Save re-enable in success and handled error paths
  - operation-state blocking for Save/Load, so grid click/change events cannot re-enable Save during Publishing, Published, or Reloading phases

The shared type label map now lives in `XrmTranslator.js` as `TYPE_STATE_LABELS` and is exposed through `XrmTranslator.GetTypeStateLabel(type)`.

Potential future wrappers:

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
2. App starts publish operation state and shows banner `Publishing {ToolbarType}`.
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
