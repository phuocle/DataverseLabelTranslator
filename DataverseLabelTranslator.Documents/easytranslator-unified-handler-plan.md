# EasyTranslator Unified Handler Plan

Ngay: 2026-06-08

## Dieu Kien Quan Trong

Doc nay la file plan chinh cho huong unified handler. Khi lam theo doc nay, uu tien cac dieu kien sau truoc moi phan khac:

1. Khong lam unit test JS/C# trong giai do rewrite nay.
   - Tests se lam sau, chi sau khi aP test manual ok.
   - Khong them test moi.
   - Khong sua coverage gate.
   - Khong bien unit test hien tai thanh constraint thiet ke.
2. Khong can giu compatibility voi code/action/handler cu.
   - App chua release production.
   - Duoc phep xoa code cu khi generic flow da thay the.
   - Khong de fallback tam thoi neu fallback do lam kien truc roi rac.
   - Khong giu old custom actions chi de "compatibility".
   - Final implementation phai la clean replacement, khong phai wrapper goi lai old handlers/actions.
3. Khong update cac file `.md` khac trong task nay.
   - Neu docs khac out of date thi de nguyen.
   - Chi focus file nay: `DataverseLabelTranslator.Documents/easytranslator-unified-handler-plan.md`.
   - Sau khi aP review va approve plan nay moi tinh den viec sync docs khac neu can.

## Muc Tieu

Refactor lai client architecture de type 15, 16, 17, 18 va cac type sau nay khong can moi type mot JavaScript handler rieng.

Huong dung:

```text
Client EasyTranslatorHandler.js
  - biet render grid
  - biet render tree/flat rows
  - biet hidden key nao gui lai server
  - biet collect changed grid payload
  - biet goi Loading/Saving/Publishing/Published
  - biet AI translate row eligibility theo flag chung

Server EasyTranslator custom action
  - biet type nao dang load/save
  - biet Dataverse CRUD / metadata / XML / web resource content
  - biet map domain object thanh grid rows
  - biet map changed grid rows thanh Dataverse update
  - biet publish target nao can publish
```

Ket luan kien truc: user nhan xet dung. Khi Read/Update da dua len server, client khong nen con 4 file `SiteMapHandler.js`, `DashboardHandler.js`, `WebResourceHandler.js`, `GlobalOptionSetHandler.js` cung tu parse, tu group, tu gom DTO save theo 4 kieu khac nhau. Do la trang thai refactor nua duong: CRUD da len server, nhung grid projection va save payload mapping van nam o client.

## Van De Hien Tai

Type 15-18 hien da dung custom action server, nhung client van con type-specific handler:

- Type 15 `SiteMapHandler.js` van parse sitemap XML trong browser, build node tree, gan `sitemapId`, `compositeId`, `nodeType`, va tao `sitemapUpdates`.
- Type 16 `DashboardHandler.js` build flat dashboard rows va tao `dashboardUpdates`.
- Type 17 `WebResourceHandler.js` build resource group tree, parse group metadata trong client, va tao `resourceChanges`.
- Type 18 `GlobalOptionSetHandler.js` build option set tree, tu quyet parent editable theo component, va tao `optionValueUpdates` / `optionSetDescriptionUpdates`.

Tat ca cung lam chung mot viec o client:

1. Load data tu server.
2. Bien raw metadata thanh rows cua w2ui.
3. Dan label values vao language columns.
4. Dat parent/child/editable/placeholder.
5. Tim rows co `w2ui.changes`.
6. Validate base language.
7. Convert changed labels thanh save payload.
8. Goi save/publish/reload.

Khac nhau thuc su khong nam o client. Khac nhau thuc su la domain mapping:

- Sitemap: XML node path va publish sitemap id.
- Dashboard: systemform name label va publish dashboard id.
- Web Resource: localized JS/RESX content key va publish webresource id.
- Global Option Set: option set description / option value label/description va publish option set name.

Nhung domain mapping nay nen o server, vi server moi la noi dang quan ly Dataverse Read/Update.

## Nguyen Tac Moi

Client khong con type-specific CRUD vocabulary.

Client chi biet vocabulary chung:

- `translatorType`
- `component`
- `gridKey`
- `schemaName`
- `rows`
- `children`
- `isEditable`
- `isTranslatable`
- `changes`
- `publishTargets`

Server moi biet vocabulary rieng cua Dataverse:

- `sitemapId`
- `compositeId`
- `nodeType`
- `dashboardId`
- `webresourceid`
- `baseWebresourceid`
- `optionSetName`
- `optionValue`
- `Label`
- `Description`
- XML / RESX / JSON parsing

`recid` la UI identity cho w2ui. `gridKey` la server identity. Save khong duoc phu thuoc vao text hien thi nhu `schemaName`.

## De Xuat File Moi

Tao mot client handler chung:

```text
DataverseLabelTranslator.WebResource/js/EasyTranslatorHandler.js
```

Handler nay thay the client dispatch cho it nhat:

- Type 15: `sitemap`
- Type 16: `dashboards`
- Type 17: `webresources`
- Type 18: `globalOptionSet`

Sau khi on dinh, type 1-14 se migrate dan vao cung handler.

Khong nen tao mot server class khong lo chua tat ca logic trong mot file. Server nen co mot action chung, nhung domain logic nen nam trong adapter rieng theo type:

```text
DataverseLabelTranslator.Server/CustomActions/Synchronous/EasyTranslator.cs
DataverseLabelTranslator.Server/CustomActions/Synchronous/EasyTranslatorAdapters/SitemapAdapter.cs
DataverseLabelTranslator.Server/CustomActions/Synchronous/EasyTranslatorAdapters/DashboardAdapter.cs
DataverseLabelTranslator.Server/CustomActions/Synchronous/EasyTranslatorAdapters/WebResourceAdapter.cs
DataverseLabelTranslator.Server/CustomActions/Synchronous/EasyTranslatorAdapters/GlobalOptionSetAdapter.cs
```

Ly do: client chi can mot handler, nhung server van can tach domain de code de doc, review, va maintain.

## Generic Load Contract

Client goi mot action chung, vi du action name:

```text
EasyTranslator
```

Load input:

```json
{
  "translatorType": "sitemap",
  "solutionId": "GUID-or-all",
  "entityName": "account",
  "entityId": "GUID",
  "component": "DisplayText"
}
```

Load output:

```json
{
  "baseLanguage": "1033",
  "grid": {
    "mode": "tree",
    "title": "Sitemap",
    "rows": [
      {
        "recid": "sitemap:00000000-0000-0000-0000-000000000000",
        "gridKey": "sitemap|00000000-0000-0000-0000-000000000000",
        "schemaName": "Sales Hub",
        "rowType": "sitemap.root",
        "isEditable": false,
        "isTranslatable": false,
        "children": [
          {
            "recid": "sitemap:00000000-0000-0000-0000-000000000000:area:Sales",
            "gridKey": "sitemap|00000000-0000-0000-0000-000000000000|Area|Sales|DisplayText",
            "schemaName": "[Area] Sales",
            "rowType": "sitemap.node",
            "isEditable": false,
            "isTranslatable": false,
            "1033": "Sales"
          }
        ]
      }
    ]
  }
}
```

Notes:

- Server returns neutral `children`; client converts to `w2ui.children`.
- Server decides `isEditable` and `isTranslatable`.
- Server returns language values directly by LCID field name.
- Client adds placeholders generically based on `component`, `baseLanguage`, `isEditable`, and row flags.
- Client adds a hidden `gridKey` column or hidden record field. This key must always be sent back on save.

## Generic Save Contract

Client sends only changed rows.

Save input:

```json
{
  "translatorType": "sitemap",
  "solutionId": "GUID-or-all",
  "entityName": "account",
  "entityId": "GUID",
  "component": "DisplayText",
  "baseLanguage": "1033",
  "changedRows": [
    {
      "gridKey": "sitemap|00000000-0000-0000-0000-000000000000|SubArea|Sales|DisplayText",
      "recid": "sitemap:00000000-0000-0000-0000-000000000000:subarea:Sales",
      "rowType": "sitemap.node",
      "changes": {
        "1033": "Sales",
        "1041": "Sales JP"
      }
    }
  ]
}
```

Save output:

```json
{
  "changed": true,
  "publishTargets": [
    {
      "kind": "sitemap",
      "id": "00000000-0000-0000-0000-000000000000"
    }
  ]
}
```

Publishing input should reuse the same generic publish target shape:

```json
{
  "translatorType": "sitemap",
  "publishTargets": [
    {
      "kind": "sitemap",
      "id": "00000000-0000-0000-0000-000000000000"
    }
  ]
}
```

Server adapter maps `publishTargets` to the correct `PublishXmlRequest` XML.

## Generic Row Contract

Every row returned by server should follow this shape:

```json
{
  "recid": "ui-unique-id",
  "gridKey": "server-stable-key",
  "schemaName": "text shown in first column",
  "rowType": "domain.row.type",
  "isEditable": true,
  "isTranslatable": true,
  "ai": {
    "include": true,
    "location": "optional parent text"
  },
  "1033": "Base value",
  "1041": "Translated value",
  "children": []
}
```

Rules:

- `recid` only needs to be unique in the current grid.
- `gridKey` must be stable enough for server save.
- `schemaName` is display text only.
- `rowType` helps server validate that a key is used with the expected adapter.
- `isEditable=false` means client must render readonly cells.
- `isTranslatable=false` means AI workspace must skip the row.
- `children` is optional. If present, client renders a tree.
- Server should not send placeholders as real values.

## Type-Specific Shape Rules

### Type 15: Sitemap

Server load:

- Parent root row is sitemap name.
- Children are sitemap nodes: Area, Group, SubArea.
- Server parses XML, not browser JavaScript.
- Server returns entity display label fallback and XML Title fallback when needed.
- Server computes `gridKey` from sitemap id, node type, composite path, and component.

Edit rules:

- Sitemap root row is not editable.
- Leaf nodes are editable when the component supports editing.
- Parent node editability is a server flag:
  - Description can be editable for parent nodes when Dataverse node supports descriptions.
  - Display Text can be readonly for parent/grouping nodes when the product decision is that only leaf nodes should be edited.
- Client must not hardcode Area/Group/SubArea edit rules. It only respects `isEditable`.

Save:

- Client sends changed rows by `gridKey`.
- Server finds XML node from `gridKey`, merges labels, updates `sitemapxml`, and returns sitemap publish target.
- Server must return changed target only when actual XML was updated. If key cannot resolve, fail clearly instead of pretending save succeeded.

### Type 16: Dashboards

Server load:

- Flat rows only.
- Each row is one dashboard.
- No dashboard tabs, sections, or cells in type 16.
- Row `gridKey` maps to systemform id and name label component.

Edit rules:

- Dashboard rows are editable directly across language columns.

Save:

- Server receives changed rows.
- Server maps `gridKey` to dashboard id and calls localized label update for `systemform.name`.
- Server returns dashboard publish targets.

### Type 17: Web Resources

Server load:

- Parent rows are web resource groups.
- Child rows are localizable keys inside JS/RESX content.
- Server parses localized JS/RESX content and groups resources.
- Parent row display text is only grouping/read-only.
- Child rows are editable.

Edit rules:

- Parent group rows are `isEditable=false`, `isTranslatable=false`.
- Child key rows are `isEditable=true`, `isTranslatable=true`.

Save:

- Client sends changed child rows by `gridKey`.
- Server maps key to existing localized web resource, or creates one from base resource when missing.
- Server updates content and returns webresource publish targets.

### Type 18: Global Option Sets

Server load:

- Parent rows are global option sets.
- Child rows are option values.
- Server decides whether parent row has labels for current component.

Edit rules:

- Display Text:
  - Parent option set rows are readonly and not AI translatable.
  - Child option rows are editable and AI translatable.
- Description:
  - Parent option set rows can be editable for option set description.
  - Child option rows can be editable for option description.

Save:

- Client sends changed rows by `gridKey`.
- Server maps parent row changes to option set description update.
- Server maps child row changes to option value label/description update.
- Server returns option set publish targets.

## EasyTranslatorHandler Responsibilities

`EasyTranslatorHandler.js` should expose only:

```javascript
EasyTranslatorHandler.Load(lockText)
EasyTranslatorHandler.Save()
```

Internal responsibilities:

- Build generic load payload from current app state.
- Call `Helper.RunServerLoad(...)` with action `EasyTranslator`.
- Store loaded rows/metadata only as generic grid state.
- Build w2ui rows recursively from server neutral rows.
- Add generic placeholders.
- Add summary row through existing app helper.
- Collect changed rows through `app.GetAllRecords()`.
- Ignore summary rows and readonly rows.
- Validate base language Display Text cannot be empty.
- Strip placeholders before save.
- Send `changedRows` with `gridKey`, `rowType`, and LCID changes.
- Show a generic no-change dialog before server save.
- Call `Helper.RunServerSaveFlow(...)`.
- Reload through `EasyTranslatorHandler.Load(Helper.GetOperationReLoading())`.

It must not:

- Parse sitemap XML.
- Parse RESX/JSON web resource content.
- Know option set DTO names.
- Know dashboard systemform update details.
- Build `sitemapUpdates`, `dashboardUpdates`, `resourceChanges`, or `optionValueUpdates`.
- Call `WebApiClient` directly.
- Call direct publish helpers.
- Expose `SaveOnly`.

## AI Translate Implication

AI Translate should stop switching by type name for row collection.

Instead, it should read generic row flags:

- `isTranslatable`
- `isEditable`
- `ai.include`
- `ai.location`
- language columns

Current special rules can be expressed by server row flags:

- Sitemap: include editable node rows, skip sitemap root.
- Dashboard: include dashboard rows.
- Web Resources: include child key rows, skip group parent rows.
- Global Option Set Display Text: include option child rows only.
- Global Option Set Description: include parent description rows and option child rows.

This makes AI workspace generic too. The workspace should not need to know `Sitemap`, `Dashboards`, `Web Resources`, or `Global Option Set`.

## Migration Plan

### Phase 0 - Freeze Current Reference Behavior

Before rewriting, write down current expected behavior for 15-18:

- which rows show
- which rows are editable
- which rows AI includes
- what publish targets are needed
- how empty values and base language validation behave

Use this document as the new direction. Existing docs that say 15-18 are "completed reference types" are now historical transition docs, not final architecture.

Do not update those old docs in this phase. They are allowed to stay out of date while this file is being reviewed.

### Phase 1 - Add Shared Server DTOs

Add common DTO classes:

- `EasyTranslatorLoadInput`
- `EasyTranslatorLoadOutput`
- `EasyTranslatorGridOutput`
- `EasyTranslatorGridRowOutput`
- `EasyTranslatorSaveInput`
- `EasyTranslatorChangedRowInput`
- `EasyTranslatorSaveOutput`
- `EasyTranslatorPublishTarget`

Acceptance:

- DTO names are generic.
- DTOs contain no `sitemapUpdates`, `dashboardUpdates`, `resourceChanges`, or `optionValueUpdates`.
- DTOs can represent tree and flat grids.

### Phase 2 - Add Server Adapter Interface

Create an adapter interface like:

```csharp
public interface IEasyTranslatorTypeAdapter
{
    EasyTranslatorLoadOutput Load(EasyTranslatorLoadInput input);
    EasyTranslatorSaveOutput Save(EasyTranslatorSaveInput input);
    EasyTranslatorSaveOutput Publish(EasyTranslatorPublishInput input);
    EasyTranslatorSaveOutput Published(EasyTranslatorPublishInput input);
}
```

Then implement adapters for 15-18 by reusing existing server logic.

Acceptance:

- Existing `SiteMap.cs`, `Dashboard.cs`, `WebResource.cs`, and `GlobalOptionSet.cs` logic is moved or delegated without changing behavior unexpectedly.
- Server adapters return normalized rows.
- Server adapters accept normalized changed rows.

### Phase 3 - Add Generic Server Action

Add action name:

```csharp
public const string EasyTranslator = "EasyTranslator";
```

Wire it in `PostDataverseLabelTranslatorCustomActionSynchronous`.

`EasyTranslator.cs` should:

- deserialize generic input
- resolve `translatorType`
- delegate to the matching adapter
- preserve existing phase names: `Loading`, `Saving`, `Publishing`, `Published`, `Other`
- return the normal custom action envelope

Acceptance:

- One action can load/save all migrated types.
- Unknown `translatorType` fails clearly.
- Old type-specific custom actions must not remain as compatibility fallback after the matching type is migrated.
- Do not commit a finished migration where old custom actions are still wired as fallback.

### Phase 4 - Add `EasyTranslatorHandler.js`

Create the generic client handler.

Update script load order in `DataverseLabelTranslator.WebResource/html/App.html`.

Update `XrmTranslator.SetHandler()` so these types route to the same handler:

```text
sitemap          -> EasyTranslatorHandler
dashboards       -> EasyTranslatorHandler
webresources     -> EasyTranslatorHandler
globalOptionSet  -> EasyTranslatorHandler
```

Acceptance:

- Type 15-18 all load through one JS handler.
- Save payload for all four types has `changedRows`.
- There is exactly one client save extraction path.

### Phase 5 - Move AI Row Collection To Generic Flags

Update `EasyTranslator.BuildAiTranslateDataSource()` so it does not use type-specific collection functions for 15-18.

Acceptance:

- AI row collection reads generic row flags.
- No switch on type display names is needed for row shape.
- The AI workspace remains unaware of domain types.

### Phase 6 - Retire Old Type-Specific Client Handlers

After generic type 15-18 flow is implemented and manually accepted:

- Stop loading `SiteMapHandler.js`, `DashboardHandler.js`, `WebResourceHandler.js`, and `GlobalOptionSetHandler.js`.
- Remove old files and references. Do not keep fallback dispatch to the old handlers.
- Remove or stop wiring old type-specific server actions once the generic server action covers those types.
- Do not update `AGENTS.md` or other `.md` files in this phase. If they are out of date, leave them out of date until aP separately asks for doc sync.

Acceptance:

- No dead script references.
- No duplicate handler dispatch.
- No compatibility fallback to old JS handlers.
- No compatibility fallback to old server actions.
- No `.md` churn outside this plan file.

### Phase 7 - Migrate Types 1-14 Incrementally

For each remaining type:

1. Add server adapter.
2. Return normalized grid rows.
3. Accept `changedRows`.
4. Route the type to `EasyTranslatorHandler`.
5. Remove old client save/load logic for that type.

Do not migrate all remaining types in one huge change. The server domain rules differ, but the client contract should remain unchanged.

## Testing Strategy

Important: this section is for the later test pass, not for the first rewrite implementation. Do not implement JS/C# unit tests until aP has manually tested the new flow and confirmed it is OK.

Server tests:

- Load adapter returns expected rows, keys, editability, and publish target metadata.
- Save adapter maps `gridKey` to the correct Dataverse update.
- Invalid or stale `gridKey` fails clearly.
- Save output includes publish target only when data changed.

Client tests:

- `EasyTranslatorHandler.Load()` sends generic load payload.
- `EasyTranslatorHandler.Load()` renders flat and tree rows.
- `EasyTranslatorHandler.Save()` sends only changed rows.
- Readonly rows are not sent.
- Base language Display Text cannot be cleared.
- Non-base Display Text clears are preserved as empty strings.
- Description clears are preserved as empty strings.
- Placeholders are not sent as values.
- AI datasource includes only rows with `isTranslatable` / `ai.include`.

Manual validation:

- Type 15 sitemap tree row editability matches product decision.
- Type 16 dashboard flat grid still saves.
- Type 17 web resource child key save creates/updates localized resources.
- Type 18 Display Text excludes parent rows from editing/AI.
- Type 18 Description includes parent rows when editable.
- Publish/reload occurs only when server reports changed publish targets.

## Acceptance Criteria For The New Architecture

- There is one client handler for 15-18.
- Client save payload shape is identical for 15-18.
- Client load path is identical for 15-18.
- No client handler parses Dataverse XML, RESX, JSON resource content, option metadata, or dashboard metadata.
- Server owns every Read/Update domain operation.
- Every grid row has a stable hidden `gridKey`.
- `recid` is treated as UI-only.
- Row editability and AI eligibility come from server flags.
- Publish targets come from server save output.
- Old handler files are deleted or no longer loaded after parity.
- Future type migration adds a server adapter, not a new client handler.

## Bottom Line

The target architecture is not "one file controls everything everywhere".

The target architecture is:

```text
one generic client handler
one generic client grid contract
one generic save payload
server adapter per Dataverse domain
```

That is the clean boundary. Client becomes a professional grid/app shell. Server becomes the only place that knows how Dataverse objects are read, changed, merged, and published.
