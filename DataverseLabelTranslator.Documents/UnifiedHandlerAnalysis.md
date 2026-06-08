# Unified Handler Architecture – Analysis & Plan

> [!CAUTION]
> ## Quy tắc bắt buộc
>
> 1. **KHÔNG viết unit test** — Không tạo, không sửa bất kỳ file `.test.js` hay C# test nào. Test sẽ làm sau khi aP test OK trên Dataverse thật.
> 2. **KHÔNG giữ code cũ** — App chưa release production. Xóa thẳng file cũ, xóa thẳng code cũ. Không "backward compatible", không "keep temporarily", không "song song".
> 3. **KHÔNG update file `.md` khác** — Chỉ focus file này (`UnifiedHandlerAnalysis.md`). Các file `.md` khác (AGENTS.md, README, v.v.) out of date thì kệ.

---

## Bối cảnh vấn đề

Dự án Dataverse Label Translator hiện có **18 handler JS riêng biệt** cho 18 toolbar type. Sau khi refactor CRUD lên server (Custom Action `pl_DataverseLabelTranslatorCustomAction`), logic Load/Save ở client chỉ còn là:

1. Gọi `Helper.RunServerLoad({ actionName, payload })` → nhận data → fill grid
2. User edit trên grid
3. Gọi `Helper.RunServerSaveFlow({ actionName, getSavePayload })` → gửi changes lên server

**Vấn đề**: Mỗi handler file lặp lại cùng 1 pattern nhưng mỗi cái mỗi khác (different payload shape, different FillTable, different GetUpdates) — trong khi server đã xử lý toàn bộ business logic. Client handler chỉ đóng vai trò **pass-through** nhưng lại chứa ~200-300 dòng code riêng biệt mỗi file.

---

## Phân tích chi tiết 4 handler (Type 15-18)

### Pattern chung

Tất cả 4 handler đều tuân theo **cùng 1 cấu trúc**:

```text
IIFE → constants → private helpers → FillTable() → GetUpdates() → Load() → Save()
```

| Bước | Code | Giống nhau? |
|------|------|-------------|
| 1. Lock grid | `app.LockGrid()` | ✅ 100% |
| 2. Call server load | `Helper.RunServerLoad({ actionName, getPayload })` | ✅ 100% |
| 3. Store metadata | `SetMetadata(app, output.xxx)` | ✅ Structure, ❌ field name |
| 4. Fill grid | `FillTable()` | ❌ Khác nhau |
| 5. Get changes | `GetUpdates()` | ❌ Khác nhau |
| 6. Check empty | `HasNoChanges(updates)` | ✅ 100% |
| 7. Call server save | `Helper.RunServerSaveFlow({ actionName, getSavePayload })` | ✅ 100% |
| 8. Publish | `getPublishPayload → getPublishedPayload` | ✅ Structure |
| 9. Reload | `shouldReload → reloadAction` | ✅ 100% |

### Chi tiết khác biệt

#### FillTable — Nơi duy nhất thực sự khác

| Handler | Grid Type | Parent Editable | Data Transform |
|---------|-----------|-----------------|----------------|
| **SiteMap (#15)** | Tree | ❌ (parent = sitemap name) | Parse XML → Area/Group/SubArea nodes |
| **Dashboard (#16)** | Flat | N/A (no tree) | Sort by base label → flat records |
| **WebResource (#17)** | Tree | ❌ (parent = file prefix) | Group by key → content key-value children |
| **GlobalOptionSet (#18)** | Tree | Conditional (Description mode = ✅) | Option sets → option value children |

#### GetUpdates — Cấu trúc payload khác nhau

| Handler | Save Payload | Publish Payload |
|---------|-------------|-----------------|
| **SiteMap** | `{ sitemapUpdates: [{ sitemapId, compositeId, nodeType, component, labels }] }` | `{ sitemapIds }` |
| **Dashboard** | `{ dashboardUpdates: [{ dashboardId, labels }] }` | `{ dashboardIds }` |
| **WebResource** | `{ resourceChanges: [{ webresourceid, contentChanges }] }` | `{ webresourceIds }` |
| **GlobalOptionSet** | `{ component, optionValueUpdates, optionSetDescriptionUpdates }` | `{ optionSetNames }` |

---

## Tại sao hợp nhất vào 1 handler?

### Luận điểm chính

> Server **đã biết** cách load/save cho từng type. Client chỉ cần:
> 1. Gửi `actionName` + `type` number → Server trả data
> 2. Render data vào grid (flat hoặc tree)
> 3. Collect changes từ grid → Gửi nguyên payload lên server

### Vấn đề cốt lõi

Hiện tại mỗi handler tự build payload riêng (`sitemapUpdates`, `dashboardUpdates`, `resourceChanges`, `optionValueUpdates`...). Điều này buộc client phải **hiểu business logic** của từng type — trái ngược với mục tiêu "server handles everything".

### Giải pháp: **Server-Driven Grid**

Thay vì client tự transform data, server nên trả về **grid-ready data** với metadata mô tả cách render:

```json
{
    "gridConfig": {
        "gridType": "tree",
        "actionName": "SiteMap"
    },
    "records": [
        {
            "recid": "abc-123",
            "schemaName": "Site Map",
            "_key": "abc-123",
            "w2ui": { "editable": false, "children": [
                {
                    "recid": "abc-123|area1",
                    "schemaName": "[Area] SFA",
                    "_key": "abc-123|area1",
                    "1033": "Sales",
                    "1036": "Ventes",
                    "w2ui": { "editable": true }
                }
            ]}
        }
    ]
}
```

Khi save, client chỉ cần:
```json
{
    "changes": [
        { "_key": "abc-123|area1", "labels": [{"LanguageCode": "1036", "Label": "Ventes modifié"}] }
    ]
}
```

Server dùng `_key` + `actionName` để biết cách write back.

---

## Phân loại tất cả 18 types theo mức độ khả thi hợp nhất

### Tier 1: Làm ngay (server đã xử lý load/save)

| # | Type | Grid | Notes |
|---|------|------|-------|
| 15 | Sitemap | Tree | XML parsing done on server, nodes returned |
| 16 | Dashboard | Flat | Simplest handler |
| 17 | Web Resources | Tree | Key-value content children |
| 18 | Global Option Sets | Tree | Dual component (DisplayText/Description) |

### Tier 2: Làm tiếp (cần refactor server)

| # | Type | Grid | Notes |
|---|------|------|-------|
| 1 | Attributes | Flat | Entity metadata labels |
| 4 | Views | Flat | View name labels |
| 6 | Entity Metadata | Flat | Entity display names |
| 7 | Relationships | Tree | Relationship labels |
| 8 | Charts | Flat | Chart name labels |

### Tier 3: Làm sau (nhiều logic riêng biệt)

| # | Type | Grid | Notes |
|---|------|------|-------|
| 2 | Option Sets | Tree | Entity-scoped, requires entity selection |
| 3 | Forms | Tree | Tabs/sections/fields hierarchy, deep tree |
| 5 | Form Metadata | Tree | Form-specific metadata |
| 9 | BPF | Tree | Multi-entity process stages |
| 10 | Business Rules | Tree | Rule conditions/actions |
| 11 | Ribbons | Tree | XML-based ribbon definitions |
| 12 | Commands | Tree | Modern command bar |
| 13 | Entity Messages | Flat | SDK message processing |
| 14 | Content Snippets | Tree | KB article snippets |

---

## Proposed Architecture: `EasyTranslatorHandler.js`

### Nguyên tắc thiết kế

1. **Server trả grid-ready records** — Client không transform data, chỉ render
2. **`_key` field ẩn** — Mỗi record có 1 unique key, dùng khi gửi changes lên server
3. **`w2ui.editable` do server quyết định** — Client không hardcode logic parent editable
4. **1 Load(), 1 Save()** — Không phân biệt type ở client
5. **Grid config từ server** — `gridType`, `actionName`

### Mã giả thiết kế

```javascript
(function (EasyTranslatorHandler, undefined) {
    "use strict";

    var gridConfig = null;

    // ========== LOAD ==========
    EasyTranslatorHandler.Load = function (lockText) {
        var app = Helper.GetTranslator();
        var actionName = app.GetCurrentActionName();

        app.LockGrid(lockText || Helper.GetOperationLoading());

        return Helper.RunServerLoad({
            app: app,
            actionName: actionName,
            getPayload: function () {
                return {
                    solutionId: app.GetSolution(),
                    component: app.GetComponent()
                };
            },
            onLoaded: function (output) {
                gridConfig = output.gridConfig || {};

                if (output.baseLanguage) {
                    app.SetBaseLanguage(output.baseLanguage);
                }

                app.SetMetadata(output.records || []);
                FillTable(output.records || []);
            }
        });
    };

    // ========== FILL TABLE (Generic) ==========
    function FillTable(records) {
        var app = Helper.GetTranslator();
        var grid = app.GetGrid();
        grid.clear();

        var processedRecords = [];
        for (var i = 0; i < records.length; i++) {
            processedRecords.push(ApplyRecordPlaceholders(records[i], app));
        }

        Helper.FinalizeGrid(processedRecords, app);
    }

    function ApplyRecordPlaceholders(record, app) {
        var isEditable = !record.w2ui || record.w2ui.editable !== false;

        if (isEditable) {
            Helper.ApplyPlaceholder(
                record,
                app.IsDescriptionComponent()
                    ? Helper.GetPlaceholderDescription()
                    : Helper.GetPlaceholderDisplayText(),
                app.IsDisplayTextComponent()
                    ? Helper.GetPlaceholderDisplayTextBase()
                    : null,
                app
            );
        } else {
            record._emptyReadonlyPlaceholder = Helper.GetPlaceholderReadonly();
        }

        if (record.w2ui && record.w2ui.children) {
            for (var i = 0; i < record.w2ui.children.length; i++) {
                ApplyRecordPlaceholders(record.w2ui.children[i], app);
            }
        }

        return record;
    }

    // ========== SAVE (Generic) ==========
    EasyTranslatorHandler.Save = function () {
        var app = Helper.GetTranslator();
        var changes = CollectAllChanges(app);

        if (changes.length === 0) {
            return DialogHelper.alert("There are no changes to save.", {
                title: app.GetCurrentToolbarTypeText() || "Save"
            });
        }

        var actionName = app.GetCurrentActionName();

        return Helper.RunServerSaveFlow({
            app: app,
            actionName: actionName,
            getSavePayload: function () {
                return {
                    component: app.GetComponent(),
                    changes: changes
                };
            },
            getPublishPayload: function (output) {
                return output && output.publishPayload ? output.publishPayload : null;
            },
            getPublishedPayload: function (output) {
                return output && output.publishPayload ? output.publishPayload : null;
            },
            shouldReload: function (output) {
                return !!(output && output.publishPayload);
            },
            reloadAction: function () {
                return EasyTranslatorHandler.Load(Helper.GetOperationReLoading());
            }
        });
    };

    // ========== COLLECT CHANGES (Generic) ==========
    function CollectAllChanges(app) {
        var records = app.GetAllRecords();
        var changes = [];

        for (var i = 0; i < records.length; i++) {
            var record = records[i];
            if (!record.w2ui || !record.w2ui.changes) {
                continue;
            }

            var labels = Helper.GetChangedLabels(
                record.w2ui.changes,
                app.IsDescriptionComponent() || app.IsDisplayTextComponent(),
                { app: app, includeBaseDisplayText: true, record: record }
            );

            if (labels.length === 0) {
                continue;
            }

            Helper.ValidateBaseLanguageNotEmpty(record, record.w2ui.changes, {
                app: app,
                getRowPath: function (r) {
                    return r && r.schemaName ? String(r.schemaName) : "(unknown)";
                }
            });

            changes.push({
                _key: record._key || record.recid,
                recid: record.recid,
                labels: labels
            });
        }

        return changes;
    }

})((window.EasyTranslatorHandler = window.EasyTranslatorHandler || {}));
```

---

## Server-Side Changes Required

### Response Format chuẩn hóa (Loading)

Mỗi `Loading` response trả về:

```json
{
    "ok": true,
    "type": "Loading",
    "object": {
        "baseLanguage": 1033,
        "gridConfig": {
            "gridType": "tree|flat",
            "actionName": "SiteMap"
        },
        "records": [
            {
                "recid": "...",
                "schemaName": "Display Name",
                "_key": "unique-server-key",
                "1033": "English",
                "1036": "French",
                "w2ui": {
                    "editable": true|false,
                    "children": []
                }
            }
        ]
    }
}
```

### Save Payload chuẩn hóa (Saving)

```json
{
    "type": "Saving",
    "component": "DisplayText|Description",
    "changes": [
        {
            "_key": "unique-server-key",
            "labels": [
                { "LanguageCode": "1033", "Label": "New English" },
                { "LanguageCode": "1036", "Label": "New French" }
            ]
        }
    ]
}
```

### Publish Response chuẩn hóa (Saving output → Publishing input)

Server save trả về `publishPayload` — client pass-through trực tiếp:

```json
{
    "ok": true,
    "type": "Saving",
    "object": {
        "publishPayload": { "sitemapIds": ["abc-123"] }
    }
}
```

Client không cần biết `publishPayload` chứa gì. Chỉ forward nó cho Publishing/Published steps.

### Thay đổi cụ thể cho từng type

| # | Type | Server Load: Build `records` | Server Save: Parse `_key` |
|---|------|------------------------------|---------------------------|
| 15 | SiteMap | Parse XML → build tree records, `_key = sitemapId\|compositeId`, parent `editable: false`, children `editable: true` | Split `_key` by `\|` → `sitemapId` + `compositeId` → update XML node |
| 16 | Dashboard | Flat records, `_key = formid`, tất cả `editable: true` | `_key` = `dashboardId` → `SetLocLabelsRequest` |
| 17 | WebResource | Tree records, parent = group (file prefix) `editable: false`, children = content keys `editable: true`, `_key = groupKey\|property\|lcid` | Split `_key` → find web resource file + content key → update content |
| 18 | GlobalOptionSet | Tree records, parent = option set `editable: isDescription`, children = option values `editable: true`, `_key = MetadataId\|optionValue` | Split `_key` → `optionSetName` + `optionValue` → `UpdateOptionValueRequest` |

---

## Execution Plan

> [!IMPORTANT]
> Xóa thẳng code cũ. Không giữ backward compatibility. Không viết test.

### Step 1: Server — Refactor Loading response cho type 15-18

Sửa 4 file C# server:
- `Synchronous/SiteMap.cs` — `Loading()` trả về `{ records, gridConfig, baseLanguage }` thay vì `{ sitemaps }`
- `Synchronous/Dashboard.cs` — `Loading()` trả về `{ records, gridConfig, baseLanguage }` thay vì `{ dashboards }`
- `Synchronous/WebResource.cs` — `Loading()` trả về `{ records, gridConfig, baseLanguage }` thay vì `{ groups }`
- `Synchronous/GlobalOptionSet.cs` — `Loading()` trả về `{ records, gridConfig }` thay vì `{ optionSets }`

Xóa toàn bộ response shape cũ. Chỉ trả `records` format mới.

### Step 2: Server — Refactor Saving input cho type 15-18

Sửa 4 file C# server:
- Nhận `{ component, changes: [{ _key, labels }] }` thay vì payload riêng
- Parse `_key` trong mỗi handler để biết update record nào
- Trả về `{ publishPayload: { xxxIds: [...] } }` thống nhất

Xóa toàn bộ input model cũ (`SiteMapSaveInput`, `DashboardSaveInput`, v.v.).

### Step 3: Server — Refactor Publishing/Published

Sửa để nhận `publishPayload` pass-through từ client (chính là output của Saving step).

### Step 4: Client — Tạo `EasyTranslatorHandler.js`

Tạo file mới: `DataverseLabelTranslator.WebResource/js/Handler/EasyTranslatorHandler.js`

Nội dung theo mã giả ở trên (~150 dòng).

### Step 5: Client — Xóa 4 handler files cũ

Xóa:
- `js/Handler/SiteMapHandler.js`
- `js/Handler/DashboardHandler.js`
- `js/Handler/WebResourceHandler.js`
- `js/Handler/GlobalOptionSetHandler.js`

Xóa test files:
- `tests/SiteMapHandler.test.js`
- `tests/DashboardHandler.test.js`
- `tests/WebResourceHandler.test.js`
- `tests/GlobalOptionSetHandler.test.js`

### Step 6: Client — Update XrmTranslator dispatch

Sửa `XrmTranslator.js`:
- Type 15 (sitemap) → `EasyTranslatorHandler`
- Type 16 (dashboards) → `EasyTranslatorHandler`
- Type 17 (webresources) → `EasyTranslatorHandler`
- Type 18 (globalOptionSet) → `EasyTranslatorHandler`

Cần thêm `GetCurrentActionName()` vào XrmTranslator/EasyTranslator — map type → server action name:
```javascript
var ACTION_NAME_MAP = {
    "sitemap": "SiteMap",
    "dashboards": "Dashboard",
    "webresources": "WebResource",
    "globalOptionSet": "GlobalOptionSet"
};
```

### Step 7: Client — Update HTML `<script>` tags

Sửa HTML file(s):
- Xóa 4 `<script>` tags cho handler cũ
- Thêm 1 `<script>` tag cho `EasyTranslatorHandler.js`

### Step 8: Client — Update EasyTranslator.js AI translate

Refactor `BuildAiTranslateRows()` trong `EasyTranslator.js`:
- Xóa hardcoded type-name switch (`"sitemap"`, `"dashboards"`, `"web resources"`, `"global option set"`)
- Dùng generic tree/flat detection: nếu record có `w2ui.children` → tree mode, ngược lại → flat mode
- Không cần biết type name cụ thể

---

## So sánh Before / After

### Before (4 files × ~250 lines = ~1,000 lines)

```text
SiteMapHandler.js        336 lines  ← Parse XML, build tree, custom save payload
DashboardHandler.js      194 lines  ← Sort, flat grid, custom save payload
WebResourceHandler.js    289 lines  ← Group by key, content children, custom save payload
GlobalOptionSetHandler.js 251 lines ← Filter/sort, dual component, custom save payload
```

**Mỗi file lặp lại**: `Load()` + `FillTable()` + `GetUpdates()` + `Save()` + `HasNoChanges()` + metadata getter/setter

### After (1 file × ~150 lines)

```text
EasyTranslatorHandler.js  ~150 lines ← Generic Load/FillTable/Save, no type-specific logic
```

**Zero business logic ở client** — Server owns all data transformation

---

## Rủi ro

| Rủi ro | Mức độ | Giải pháp |
|--------|--------|-----------|
| Server response quá lớn nếu include grid-ready records | Trung bình | Server chỉ thêm `_key` + restructure — data size tương đương |
| EasyTranslator.js AI translate logic phụ thuộc type-specific collectors | Trung bình | Refactor `BuildAiTranslateRows` dùng generic tree/flat detection |
| WebResource handler có logic tạo resource mới (`_key = null`) | Thấp | Server xử lý, client gửi `_key` trống → server biết cần create |
| `w2utils.encodeTags/decodeTags` khác nhau giữa handlers | Thấp | Server encode sẵn trong records, client không cần decode |
| `publishPayload` shape khác nhau giữa types | Không có | Client chỉ forward — không cần biết nội dung |

---

## Kết luận

4 handler files hiện tại là duplicate code. Vấn đề cốt lõi:

1. **Server đã xử lý toàn bộ CRUD** nhưng client vẫn tự transform data
2. **Mỗi handler build payload riêng** trong khi server có thể nhận format chung
3. **FillTable logic khác nhau** nhưng nếu server trả grid-ready records thì không cần

Giải pháp: **1 file `EasyTranslatorHandler.js`** + **Server trả grid-ready data** + **Client chỉ render + collect changes**.

> ~1,000 dòng JS client → ~150 dòng. Server restructure output (data size tương đương, chỉ đổi shape).
