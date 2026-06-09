# EasyTranslator Unified Handler Plan

Ngay: 2026-06-09

## Trang Thai Hien Tai

Doc nay da duoc sync theo code hien tai. No khong con la plan ban dau cho type 15-18 nua. Kien truc unified handler da duoc implement cho nhieu type hon:

- `sitemap`
- `dashboards`
- `webresources`
- `globalOptionSet`
- `entityMeta`
- `views`
- `formMeta`
- `relationships`
- `charts`
- `entityMessages`
- `commands`

Client toolbar load/save cua cac type tren di qua:

```text
DataverseLabelTranslator.WebResource/js/EasyTranslatorHandler.js
```

Server custom action chung la:

```text
DataverseLabelTranslator.Server/CustomActions/Synchronous/EasyTranslator.cs
```

Domain logic nam trong adapter rieng:

```text
DataverseLabelTranslator.Server/CustomActions/Synchronous/EasyTranslatorAdapters/
```

## Muc Tieu Kien Truc

Client chi lam grid shell:

- Build generic load/save payload tu state hien tai.
- Render flat/tree rows tu server.
- Apply placeholder theo component.
- Collect changed rows.
- Validate base language.
- Goi `Loading`, `Saving`, `Publishing`, `Published` cua action `EasyTranslator`.

Server so huu domain logic:

- Dataverse query.
- Metadata/query XML parsing.
- Web resource content parsing.
- Label merge.
- `SetLocLabelsRequest`, metadata update, web resource create/update.
- Publish target mapping.

Ranh gioi can giu:

```text
one generic client handler
one generic grid row contract
one generic changedRows save payload
one server adapter per Dataverse domain
```

## Client Routing

`XrmTranslator.SetHandler()` hien route cac type sau vao `EasyTranslatorHandler`:

| Toolbar Type | translatorType | Client Handler |
| --- | --- | --- |
| Views | `views` | `EasyTranslatorHandler` |
| Form Metadata | `formMeta` | `EasyTranslatorHandler` |
| Entity Metadata | `entityMeta` | `EasyTranslatorHandler` |
| Relationships | `relationships` | `EasyTranslatorHandler` |
| Charts | `charts` | `EasyTranslatorHandler` |
| Entity Messages | `entityMessages` | `EasyTranslatorHandler` |
| Commands | `commands` | `EasyTranslatorHandler` |
| Sitemap | `sitemap` | `EasyTranslatorHandler` |
| Dashboards | `dashboards` | `EasyTranslatorHandler` |
| Web Resources | `webresources` | `EasyTranslatorHandler` |
| Global Option Sets | `globalOptionSet` | `EasyTranslatorHandler` |

`EasyTranslatorHandler.IsUnifiedType()` phai stay in sync voi danh sach tren. Hien tai no dung chinh danh sach nay de AI toolbar path biet day la unified type.

`App.html` load `EasyTranslatorHandler.js` truc tiep. Cac old handler files cho `EntityMessageHandler.js`, `ModernCommandHandler.js`, `SiteMapHandler.js`, `DashboardHandler.js`, `WebResourceHandler.js`, va `GlobalOptionSetHandler.js` da bi xoa khoi source web resource de `deploy.debug.bat` khong deploy chung nua.

## Server Adapter Registry

`EasyTranslator.cs` resolve adapter bang `translatorType`:

| translatorType | Adapter |
| --- | --- |
| `sitemap` | `SitemapAdapter` |
| `dashboards` | `DashboardAdapter` |
| `webresources` | `WebResourceAdapter` |
| `globalOptionSet` | `GlobalOptionSetAdapter` |
| `entityMeta` | `EntityMetadataAdapter` |
| `views` | `ViewAdapter` |
| `formMeta` | `FormMetaAdapter` |
| `relationships` | `RelationshipAdapter` |
| `charts` | `ChartAdapter` |
| `entityMessages` | `EntityMessageAdapter` |
| `commands` | `CommandAdapter` |

Unknown or blank `translatorType` fails in `EasyTranslator.ResolveAdapter()`.

## Shared Contract

Load input:

```json
{
  "translatorType": "views",
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
    "title": "Views",
    "rows": []
  }
}
```

Every server row uses this generic shape:

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

Save input:

```json
{
  "translatorType": "views",
  "solutionId": "GUID-or-all",
  "entityName": "account",
  "entityId": "GUID",
  "component": "DisplayText",
  "baseLanguage": "1033",
  "changedRows": [
    {
      "gridKey": "views|00000000-0000-0000-0000-000000000000|name|account",
      "recid": "views:00000000-0000-0000-0000-000000000000",
      "rowType": "views.row",
      "changes": {
        "1041": "Translated text"
      }
    }
  ]
}
```

Save output:

```json
{
  "changed": true,
  "changedRowCount": 1,
  "publishTargets": [
    {
      "kind": "entity",
      "id": "account"
    }
  ]
}
```

Publishing input reuses `publishTargets`:

```json
{
  "translatorType": "views",
  "publishTargets": [
    {
      "kind": "entity",
      "id": "account"
    }
  ]
}
```

## Contract Rules

- `recid` is UI identity for w2ui only.
- `gridKey` is server identity and must be stable enough for save.
- `gridKey` parts are URI-encoded by `BuildGridKey()` and decoded by `ParseGridKey()`.
- `schemaName` is display text only.
- `rowType` helps diagnostics and save validation.
- `isEditable=false` means client skips save and renders readonly placeholders.
- `isTranslatable=false` and `ai.include=false` mean AI workspace skips the row.
- `children` is optional. If present, client maps it to `w2ui.children`.
- Server must not send placeholders as real values.
- Save sends only changed LCID fields. Non-LCID fields are ignored.
- `EasyTranslatorHandler.NormalizeSaveValue()` strips display text, base display text, description, and readonly placeholders before save.

## Type Matrix

| Toolbar Type | translatorType | Adapter | Load Shape | Publish Kind |
| --- | --- | --- | --- | --- |
| Views | `views` | `ViewAdapter` | tree for all, flat for entity | `entity` |
| Form Metadata | `formMeta` | `FormMetaAdapter` | tree for all, flat for entity | `entity`, `dashboard` |
| Entity Metadata | `entityMeta` | `EntityMetadataAdapter` | tree | `entity` |
| Relationships | `relationships` | `RelationshipAdapter` | tree for all, flat for entity | `entity` |
| Charts | `charts` | `ChartAdapter` | tree for all, flat for entity | `entity` |
| Entity Messages | `entityMessages` | `EntityMessageAdapter` | flat | `entity` |
| Commands | `commands` | `CommandAdapter` | tree | `entity` |
| Sitemap | `sitemap` | `SitemapAdapter` | tree | `sitemap`, `appmodule` |
| Dashboards | `dashboards` | `DashboardAdapter` | flat | `dashboard` |
| Web Resources | `webresources` | `WebResourceAdapter` | tree for Display Text, flat for Description | `webresource` |
| Global Option Sets | `globalOptionSet` | `GlobalOptionSetAdapter` | tree | `globalOptionSet` |

## Type-Specific Notes

### Views

- Reads `savedquery`.
- `name` or `description` is selected by `component`.
- For all entities, parent rows group by entity and are not editable.
- For one entity, rows are flat.
- Save updates loc labels on `savedquery`.
- Publish targets are entity logical names.

### Form Metadata

- Reads `systemform`.
- `name` or `description` is selected by `component`.
- For all entities, parent rows group by `objecttypecode`.
- For one entity, rows are flat.
- Dashboard-like forms use dashboard publish targets.
- Entity forms use entity publish targets.
- `systemform.type` must be read through `GetOptionValue(form, "type")` because Dataverse returns `OptionSetValue`. Do not cast it directly to `int`.
- `formid` is preferred, but code falls back to `form.Id` when `formid` is not present.

### Entity Metadata

- Reads entity metadata.
- Returns parent entity rows with child rows for description, display name, and collection name.
- Save maps grid keys back to entity metadata labels.
- Publish targets are entity logical names.

### Relationships

- Reads one-to-many, many-to-one, and many-to-many relationship metadata.
- Returns relationship rows for associated menu labels.
- For all entities, parent rows group by entity.
- For one entity, rows are flat.
- Save updates relationship menu configuration labels.
- Publish targets are entity logical names.

### Charts

- Reads `savedqueryvisualization`.
- For all entities, parent rows group by entity.
- For one entity, rows are flat.
- Save updates loc labels on chart `name`.
- Publish targets are entity logical names.

### Entity Messages

- Reads Dataverse translation package data from `CrmTranslations.xml`.
- Requires a selected solution and selected non-custom entity.
- Filters Display Strings worksheet rows to the selected entity via `displaystringmap` identity data.
- Grid is flat.
- Load exports the selected solution translation ZIP on the server, reads `CrmTranslations.xml`, and returns normalized rows.
- Save re-exports a fresh translation package on the server, applies `changedRows`, writes `CrmTranslations.xml` back into the ZIP, imports translations, then returns an entity publish target.
- Server ZIP processing is intentionally traced because Dataverse sandbox support for framework compression APIs must be verified in the target environment.
- The browser stays generic and does not parse Display Strings rows, write translation ZIPs, or apply entity-message domain changes.

### Commands

- Reads `appaction` rows from the selected solution and selected entity.
- Parent rows represent the modern command path by location and command hierarchy.
- Child rows represent `Text`, `Title`, `Description`, `Accessibility Text`, and `Group Title`.
- Save updates loc labels on `appaction` with `SetLocLabelsRequest`.
- Publish targets are entity logical names.

### Sitemap

- Reads sitemap records and sitemap XML.
- Root row is readonly sitemap name.
- Child rows are Area, Group, and SubArea nodes.
- Save updates `sitemapxml`.
- If a sitemap belongs to app modules, save returns `appmodule` publish targets.
- If no app module is found, save returns `sitemap` publish target.

### Dashboards

- Reads dashboard `systemform` rows.
- Grid is flat.
- Type 16 intentionally shows only parent dashboard rows.
- It does not load dashboard tabs, sections, or cells.
- Save updates loc labels on `systemform.name`.
- Publish targets are dashboard form ids.

### Web Resources

- Reads localizable JavaScript and RESX web resources.
- Display Text mode returns group parent rows plus child key rows.
- Description mode returns flat web resource description rows.
- Save can update an existing localized web resource or create a localized resource from the base resource.
- Publish targets are web resource ids.

### Global Option Sets

- Reads global option set metadata from solution membership.
- Parent rows represent option sets.
- Child rows represent option values.
- Display Text mode edits option value labels.
- Description mode can edit option set descriptions and option value descriptions.
- Publish targets are option set names.

## EasyTranslatorHandler Responsibilities

Current implemented responsibilities:

- Build operation payload from `translatorType`, `solutionId`, `entityName`, `entityId`, and `component`.
- Call `Helper.RunServerLoad()` with action `EasyTranslator`.
- Store server grid output in app metadata through `app.SetMetadata(output.grid || {})`.
- Recursively map server `children` to `w2ui.children`.
- Default missing `recid` to `gridKey` or `schemaName`.
- Set row editability from `isEditable`.
- Mark group nodes from `isTranslatable=false` or `ai.include=false`.
- Apply generic placeholders.
- Collect changed rows from `app.GetAllRecords()`.
- Skip summary rows, readonly rows, rows without `gridKey`, and rows without `w2ui.changes`.
- Validate base language using `Helper.ValidateBaseLanguageNotEmpty()`.
- Send `changedRows` with `gridKey`, `recid`, `rowType`, and LCID changes.
- Show a generic no-change dialog.
- Call `Helper.RunServerSaveFlow()`.
- Publish/reload only when save output has `publishTargets`.

It must stay generic and must not:

- Parse sitemap XML.
- Parse RESX or JSON web resource content.
- Know option set DTO names.
- Know dashboard systemform update details.
- Build legacy payloads such as `sitemapUpdates`, `dashboardUpdates`, `resourceChanges`, or `optionValueUpdates`.
- Call `WebApiClient` directly.
- Expose `SaveOnly`.

## AI Translate

`EasyTranslator.BuildAiTranslateDataSource()` is generic for unified rows. It builds AI rows from:

- language columns
- current cell values
- `gridKey`
- `isEditable`
- `isTranslatable`
- `ai.include`
- `ai.location`
- child rows

`EasyTranslator.ShowAITranslate()` uses the generic AI datasource when it has translatable rows. If there are no generic rows, it falls back to the legacy AI prompt path.

## Remaining Gaps

### All-In-One

`AllInOneHandler.js` is not fully synced with unified adapters.

It still references these handler globals:

```text
ViewHandler
FormMetaHandler
EntityHandler
RelationshipHandler
ChartHandler
```

Those files are not loaded by `App.html`, and several do not exist under `js/Handler`. Before All-In-One can be considered supported for unified types, it must call the unified server flow or a dedicated all-in-one server flow instead of these old globals.

### Legacy Files

Legacy client files for migrated unified types have been removed from `js/Handler`:

```text
EntityMessageHandler.js
ModernCommandHandler.js
SiteMapHandler.js
DashboardHandler.js
WebResourceHandler.js
GlobalOptionSetHandler.js
```

`TranslationPackageService.js` was also removed because ZIP handling for Entity Messages moved to the server adapter path.

Legacy server action classes also still exist:

```text
SiteMap.cs
Dashboard.cs
WebResource.cs
GlobalOptionSet.cs
```

The current toolbar path uses `EasyTranslator` for the unified types. Keep the legacy classes only while tests or migration safety still need them.

### Tests

Old JS tests still cover legacy handler files for web resources, global option sets, and sitemap. That is historical coverage, not proof that the unified handler path is fully covered.

Server tests cover several legacy custom actions and at least one current unified Form Metadata regression path. Unified adapters should get targeted tests as they stabilize:

- load row shape
- save `gridKey` parsing
- publish target output
- invalid key failures
- option set and systemform `OptionSetValue` handling

## Implementation Rules Going Forward

When migrating a new toolbar type to the unified architecture:

1. Add or update a server adapter.
2. Return normalized `EasyTranslatorGridRowOutput` rows.
3. Use `BuildGridKey()` for all server-stable keys.
4. Accept `changedRows`.
5. Route the type in `XrmTranslator.SetHandler()` to `EasyTranslatorHandler`.
6. Add the type to `EasyTranslatorHandler.IsUnifiedType()` when AI should use generic row collection.
7. Keep domain-specific Dataverse names out of client save payloads.
8. Add targeted tests when behavior is stable enough to protect.

Do not add another browser JavaScript handler just to support a new Dataverse metadata type. The default path is:

```text
new server adapter + existing EasyTranslatorHandler
```

## Acceptance Criteria

The architecture is in good shape when:

- All unified toolbar types route through `EasyTranslatorHandler`.
- Server adapter registry and client `IsUnifiedType()` stay in sync.
- Every editable row has a stable `gridKey`.
- Client save payload shape is identical across unified types.
- Client does not parse Dataverse XML, RESX, JSON resource content, option metadata, chart metadata, relationship metadata, view metadata, or dashboard metadata.
- Server returns publish targets in generic `{ kind, id }` form.
- AI row collection uses row flags instead of type-specific row builders.
- All-In-One no longer references missing legacy handler globals.
- Legacy handler files are removed or clearly isolated from runtime.

## Bottom Line

The target architecture remains:

```text
Client = generic grid and interaction shell
Server = Dataverse domain adapters
```

Keep the client contract small and boring. Put Dataverse-specific read, merge, save, and publish logic in server adapters.
