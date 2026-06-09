# Ribbon Unified Handler Migration Solution

Ngay: 2026-06-09

## Pham vi

Tai lieu nay chi dua tren 2 file da duoc yeu cau doc:

- `DataverseLabelTranslator.Documents/easytranslator-unified-handler-plan.md`
- `DataverseLabelTranslator.WebResource/js/Handler/RibbonHandler.js`

Tai lieu nay da duoc cap nhat sau khi migration ribbon vao unified handler path.

## Ket luan ngan

`RibbonHandler.js` la source-of-truth nghiep vu luc phan tich migration: export solution ZIP, doc `customizations.xml`, parse `RibbonDiffXml`, hien thi label rows, merge thay doi vao LocLabels, import solution lai, roi start `PublishAllXmlAsync`.

Van de khong nam o logic nghiep vu sai. Van de la logic ribbon dang nam o browser JavaScript, trong khi kien truc unified handler muon:

```text
Client = generic grid and interaction shell
Server = Dataverse domain adapters
```

Viec migration ribbon len server tao `RibbonAdapter` cho `EasyTranslator`, khong viet them client handler moi. Server da process duoc ZIP trong path Entity Messages, nen ribbon dung cung huong: export ZIP tren server, sua `customizations.xml`, import ZIP async, start publish async, tra job status ve client.

## Legacy RibbonHandler.js source behavior

`RibbonHandler.Load()` da lam cac viec domain-specific sau tren browser:

- Bat buoc user chon solution.
- Export selected unmanaged solution bang `ExportSolution`.
- Dung `JSZip` doc `customizations.xml`.
- Parse XML bang `DOMParser`.
- Tim entity node theo logical name.
- Lay `RibbonDiffXml`.
- Build map `LocLabel` theo LCID.
- Tim cac control node `Button`, `SplitButton`, `FlyoutAnchor`.
- Tao tree rows: parent control readonly, child label rows editable.
- Child label rows gom `LabelText`, `ToolTipTitle`, `ToolTipDescription`.

`RibbonHandler.Save()` da lam cac viec domain-specific sau tren browser:

- Collect changed LCID cells.
- Sua `customizations.xml`:
  - Tim dung control node.
  - Dam bao attribute tro ve `$LocLabels:{id}`.
  - Tao `LocLabel`, `Titles`, `Title languagecode`.
  - Ghi `description` moi.
- Tao import solution ZIP.
- Goi `ImportSolution()`.
- Goi `PublishAllXmlAsync()`.
- Luu publish job bang `XrmTranslator.StorePublishXmlJob()`.
- Show publish status va khoa Load/Save.

Day la behavior tot de lay lam source of truth khi migrate. Migration khong thay doi y nghia row, y nghia label, hay cach merge LocLabels.

## Migration target

Them translator type moi:

```text
translatorType: ribbons
adapter: RibbonAdapter
toolbar type: 11 Ribbons
```

Client routing sau migration:

```text
XrmTranslator.SetHandler("ribbons") -> EasyTranslatorHandler
EasyTranslatorHandler.IsUnifiedType() includes "ribbons"
```

Server adapter moi:

```text
DataverseLabelTranslator.Server/CustomActions/Synchronous/EasyTranslatorAdapters/RibbonAdapter.cs
```

`RibbonAdapter` so huu toan bo logic:

- Validate selected solution.
- Validate selected entity.
- Export selected solution ZIP.
- Extract `customizations.xml`.
- Parse XML.
- Find entity `RibbonDiffXml`.
- Build generic grid rows.
- Accept generic `changedRows`.
- Re-export fresh solution during save.
- Apply changes to latest `customizations.xml`.
- Write updated XML back into ZIP.
- Start async solution import.
- Return import job metadata.
- Start `PublishAllXmlAsync` in a second request, not inside the save request.

## Grid contract de xuat

Parent row readonly cho tung ribbon control:

```json
{
  "recid": "ribbons:control:{index}",
  "gridKey": "ribbons|control|{entity}|{surface}|{component}|{controlId}",
  "schemaName": "Homepage Grid / Button: New",
  "rowType": "ribbons.control",
  "isEditable": false,
  "isTranslatable": false,
  "ai": {
    "include": false
  },
  "children": []
}
```

Child row editable cho tung property:

```json
{
  "recid": "ribbons:label:{index}:{property}",
  "gridKey": "ribbons|label|{entity}|{surface}|{component}|{controlId}|{property}|{locLabelId}",
  "schemaName": "Text",
  "rowType": "ribbons.label",
  "isEditable": true,
  "isTranslatable": true,
  "ai": {
    "include": true,
    "location": "Homepage Grid / Button: New"
  },
  "1033": "New",
  "1041": "New translated"
}
```

`gridKey` phai duoc encode/decode bang shared `BuildGridKey()` va `ParseGridKey()` pattern trong unified plan. Khong nen gui raw object rieng nhu `controlId`, `surface`, `property` tu client save payload nua. Nhung cac gia tri nay co the nam trong `gridKey`.

## Save contract de xuat

Client chi gui generic `changedRows`:

```json
{
  "translatorType": "ribbons",
  "solutionId": "GUID",
  "entityName": "account",
  "entityId": "GUID",
  "component": "DisplayText",
  "baseLanguage": "1033",
  "changedRows": [
    {
      "gridKey": "ribbons|label|account|form|Button|Mscrm.Form.account.New|LabelText|Mscrm.Form.account.New.LabelText",
      "recid": "ribbons:label:1:LabelText",
      "rowType": "ribbons.label",
      "changes": {
        "1041": "Translated text"
      }
    }
  ]
}
```

Server save:

1. Validate `changedRows` chi gom `rowType = ribbons.label`.
2. Decode `gridKey`.
3. Re-export selected solution de lay `customizations.xml` moi nhat.
4. Tim lai entity va control tu XML moi.
5. Apply tung LCID change vao LocLabels.
6. Start `ImportSolutionAsync` cho updated solution ZIP.
7. Return import job id va `needsPublish = true`.

De tranh stale data, save khong nen dung ZIP da load truoc do tu browser. Save nen export fresh solution tren server, roi apply changes vao file moi.

## PublishAllXml wait and lock design

Ribbon publish khac cac type khac vi entity-scoped publish khong du. Sau import solution, phai start `PublishAllXmlAsync`. Vi day la async operation, user can thay trang thai "dang publish" va UI phai khoa hop ly.

### Timeout-safe import and publish flow

Dung async import + async publish de tranh timeout 2 phut. Import solution ZIP lon khong nen chay bang import sync trong save request.

```text
Request 1: Save changes, build ZIP, start ImportSolutionAsync, return import job id
Client: Persist import lock, show Importing..., then poll import job status
Request 2: After import succeeded, start PublishAllXmlAsync, return publish job id
Client: Persist publish lock, show Publishing..., then poll publish job status
```

Request 1 la `Save` cua `EasyTranslator` va phai return nhanh sau khi queue import:

- Server export fresh solution ZIP.
- Server apply `changedRows` vao `customizations.xml`.
- Server goi SDK `ImportSolutionAsyncRequest` hoac Web API `ImportSolutionAsync`.
- Server return `AsyncOperationId` cua import job ve client.
- Server khong wait den khi import xong.

Client poll import job:

- Khi import Running/Waiting/InProgress: tiep tuc show `Importing...`, khoa Load/Save.
- Khi import Failed/Canceled: clear lock, show error, khong start publish.
- Khi import Succeeded: chuyen sang request 2.

Request 2 la operation rieng de start publish va phai return nhanh sau khi queue publish:

- Client goi custom action/custom API server-side async publish.
- Server chi call `PublishAllXmlAsync`.
- Server return `AsyncOperationId`/job id ve client cang som cang tot.
- Server khong wait den khi publish xong.

Sau request 2, client chua phai la `Published`. Client dang o trang thai `Publishing...`. Chi khi polling publish job status tra terminal success thi moi chuyen sang `Published`.

Neu "custom action async" la async plugin/action that khong the return output parameter ngay, thi khong dung no de return child import/publish job id truc tiep. Khi do co 2 cach:

- Preferred: dung custom action/custom API synchronous rat nhe, chi start `ImportSolutionAsync` hoac `PublishAllXmlAsync` va return job id ngay.
- Alternative: async action ghi job id vao persisted status record, client poll action job/status record truoc roi moi poll import/publish job.

Muc tieu chinh la khong co request nao wait den luc import solution hoac PublishAllXml xong.

Muc tieu:

- User thay ro rang app dang `Importing...` hoac `Publishing...`, khong tuong app bi treo.
- Load/Save bi khoa khi import solution hoac PublishAllXml dang chay.
- F5 hoac Ctrl+F5 van khoi phuc dung trang thai `Importing...` hoac `Publishing...` neu job cu dang running.
- `Publishing...` phai treo den khi server bao job terminal: succeeded, failed, hoac canceled.
- Khi publish succeeded thi hien `Published`.
- Neu user van dang o ribbon grid/context cu thi hien `Reloading...` va load lai ribbon grid.
- Neu user da refresh va app dang o main grid hoac context khac thi chi done, khong tu dong reload ribbon grid.
- Khong co lock vinh vien khi job id stale, not found, hoac khong the confirm trang thai sau nhieu lan retry.

### User-visible flow

Flow dung sau migration:

```text
Loading... -> fill grid -> translate / AI translate -> Saving... -> Importing... -> Publishing... -> Published
```

Neu `Published` xay ra khi user van dang xem dung ribbon grid da save:

```text
Published -> Reloading... -> fill grid
```

Neu `Published` xay ra sau khi user F5/Ctrl+F5 va app quay ve main grid:

```text
Published -> done
```

Khong nen reload ribbon grid trong truong hop app khong con o ribbon context, vi user luc do dang o mot context khac.

### Save output can co

Request 1 save/start import tra ve:

```json
{
  "changed": true,
  "changedRowCount": 3,
  "needsPublish": true,
  "import": {
    "mode": "async",
    "kind": "importSolution",
    "operationId": "IMPORT-ASYNC-OPERATION-GUID",
    "importJobId": "IMPORT-JOB-GUID",
    "translatorType": "ribbons",
    "entityName": "account",
    "startedOn": "2026-06-09T00:00:00Z"
  },
  "publishKind": "publishAllXml"
}
```

Request 2 start publish tra ve publish job object:

```json
{
  "mode": "async",
  "kind": "publishAllXml",
  "operationId": "ASYNC-OPERATION-GUID",
  "importJobId": "IMPORT-JOB-GUID",
  "translatorType": "ribbons",
  "entityName": "account",
  "startedOn": "2026-06-09T00:00:00Z",
  "lastKnownRunningOn": "2026-06-09T00:00:00Z"
}
```

Neu `PublishAllXmlAsync` khong tra duoc `AsyncOperationId`, khong nen xem nhu publish tracked thanh cong. Server/client nen tao warning ro rang va chi dung short uncertainty lock, vi neu khong co job id thi khong the poll chinh xac.

Tuong tu, neu `ImportSolutionAsync` khong tra duoc `AsyncOperationId`, khong nen start publish. Client can show import error/uncertain state thay vi chuyen sang `Publishing...`.

### Client lock persistence

Client nen luu import lock va publish lock vao `localStorage`, khong dung `sessionStorage`, vi F5 va Ctrl+F5 van giu `localStorage`.

Publish lock object de xuat:

```json
{
  "version": 1,
  "kind": "publishAllXml",
  "translatorType": "ribbons",
  "operationId": "ASYNC-OPERATION-GUID",
  "importJobId": "IMPORT-JOB-GUID",
  "orgUrl": "https://org.crm.dynamics.com",
  "solutionId": "GUID",
  "entityName": "account",
  "startedOn": "2026-06-09T00:00:00Z",
  "lastCheckedOn": "2026-06-09T00:00:20Z",
  "lastKnownRunningOn": "2026-06-09T00:00:20Z",
  "consecutiveUnknownChecks": 0
}
```

Storage key nen scope theo org va publish kind:

```text
DataverseLabelTranslator:importLock:{orgUrl}:importSolution
DataverseLabelTranslator:publishLock:{orgUrl}:publishAllXml
```

`PublishAllXml` la org-level publish, nen lock nen duoc xem la org-level customization lock. Toi thieu phai khoa Ribbon Load/Save. Tot hon la khoa cac save flow co publish metadata cho den khi job xong, de tranh import/publish race.

### Startup and refresh behavior

Khi app load hoac user refresh browser:

1. Doc local import lock va publish lock.
2. Neu co publish lock, resume publish truoc.
3. Neu khong co publish lock nhung co import lock, resume import.
4. Neu khong co lock, enable UI binh thuong.
5. Neu co import lock:
   - Show lai `Importing...` ngay.
   - Khoa Load/Save.
   - Poll import async operation.
   - Neu import Succeeded, clear import lock va goi request 2 start publish.
   - Neu import Failed/Canceled, clear import lock, show error, enable UI.
6. Neu co publish lock:
   - Show lai `Publishing...` ngay, truoc khi status API tra ve, de user thay app dang resume publish state.
   - Khoa Load/Save.
   - Goi server status operation bang `operationId`.
7. Neu publish status la Running/Waiting/InProgress:
   - Tiep tuc khoa Load/Save.
   - Tiep tuc show `Publishing...`.
   - Cap nhat `lastKnownRunningOn`.
   - Khong auto-unlock theo age neu server van confirm job dang chay.
8. Neu publish status la Succeeded:
   - Clear lock.
   - Show `Published`.
   - Neu current UI context van la ribbon grid cua cung `solutionId + entityName`, show `Reloading...` va load lai ribbon grid.
   - Neu current UI context la main grid hoac context khac, enable UI va khong reload.
9. Neu publish status la Failed/Canceled:
   - Clear lock.
   - Show error.
   - Enable UI.

Neu user Ctrl+F5 luc import hoac publish dang chay, app se doc lai local lock, poll server, thay job dang running va tiep tuc khoa.

### Current context check

Khi publish terminal success, client chi duoc reload neu current UI van la dung ribbon grid cua publish job.

Dieu kien de xem la same ribbon grid:

- Current toolbar type la `ribbons`.
- Current selected solution id trung voi `lock.solutionId`.
- Current selected entity logical name trung voi `lock.entityName`.
- Grid hien tai dang la ribbon grid, khong phai main entity/component grid.

Neu mot trong cac dieu kien tren sai, client chi show `Published`, clear lock, enable UI, va khong goi load lai ribbon.

### Polling behavior

Khuyen nghi polling:

```text
Import job 0-60 seconds: poll moi 5 seconds
Import job >60 seconds: poll moi 15 seconds
Publish job 0-60 seconds: poll moi 5 seconds
Publish job 1-5 minutes: poll moi 15 seconds
Publish job >5 minutes: poll moi 30 seconds
Confirmed running import/publish: no hard max age
```

Nut/action nen co:

- `Check status now`: poll ngay lap tuc.
- `Reload after publish`: chi enable khi status Succeeded va current UI context la ribbon grid can reload.
- Khong can nut force unlock mac dinh trong normal path.

### Stale lock rules

Nguyen tac chinh: neu Dataverse van confirm async job dang running thi tiep tuc treo `Publishing...`. Khong clear lock chi vi job chay lau.

De thong minh va khong khoa hoai khi lock stale:

| Status check result | Lock behavior |
| --- | --- |
| Import Succeeded | Clear import lock, start request 2 PublishAllXmlAsync |
| Import Failed/Canceled | Clear import lock, show error, no publish |
| Succeeded, current context is same ribbon grid | Clear lock, show `Published`, then `Reloading...`, reload grid |
| Succeeded, current context is main grid or different context | Clear lock, show `Published`, enable UI, no reload |
| Failed/Canceled | Clear lock, show error, enable Load/Save |
| Running/Waiting/InProgress | Keep lock |
| Not found, lock age < 2 minutes | Keep lock, vi async job co the chua visible |
| Not found, 3 checks lien tiep va lock age >= 2 minutes | Clear lock with warning |
| Status API error, lock age < 30 minutes | Keep lock, show retry state |
| Status API error, lock age >= 30 minutes | Clear lock with warning |

Neu lock bi clear do stale nhung Dataverse publish thuc te van dang chay, lan save tiep theo nen check active publish job mot lan truoc khi import solution moi.

### Server async operation status

Can them generic server operation cho EasyTranslator, vi client khong nen query Dataverse asyncoperation truc tiep theo tung domain. Operation nay dung cho ca import job va publish job:

```text
operation: AsyncStatus
translatorType: ribbons
operationId: asyncoperation GUID
```

Output:

```json
{
  "operationId": "GUID",
  "kind": "importSolution or publishAllXml",
  "state": "Running",
  "status": "InProgress",
  "message": "Importing solution or publishing customizations",
  "startedOn": "2026-06-09T00:00:00Z",
  "completedOn": null,
  "isTerminal": false,
  "isSuccess": false
}
```

Voi import job, terminal success nghia la duoc phep start request 2 `PublishAllXmlAsync`. Voi publish job, terminal success nghia la duoc show `Published`. Client co the goi optional `Published` operation de server trace/cleanup, hoac chi clear local lock neu server khong can cleanup.

## Anh huong khi move tu JS len server

### Anh huong tot

- Browser khong can parse ZIP/XML lon bang JSZip/DOMParser.
- Client khong con biet chi tiet `RibbonDiffXml`, `LocLabel`, `Title`, `languagecode`.
- Save payload giong cac unified type khac.
- Server co the trace chi tiet hon khi export/import/publish loi.
- Reuse duoc ZIP processing pattern da dung cho Entity Messages.
- Giam stale browser state vi save re-export fresh solution.

### Anh huong can canh giac

- Export/import solution co the cham. Neu synchronous server action bi gioi han timeout, khong nen import sync hoac wait publish trong server action.
- `PublishAllXmlAsync` chi nen start trong request 2 va tra `AsyncOperationId`; khong wait den published trong save call.
- Import solution trong server action la buoc rui ro nhat ve timeout neu dung import sync. Dung `ImportSolutionAsync` va poll import job truoc khi start publish.
- Error messages chuyen tu JS exception sang server exception, can map thanh message user-friendly.
- Privileges khong giam: user van can quyen export/import/publish customizations.
- PublishAllXml la org-level side effect. Lock nen duoc doi xu nghiem tuc hon entity publish.

## De xuat migration theo phase

### Phase 1: Server adapter parity

- `RibbonAdapter` implemented.
- Load rows follow legacy `RibbonHandler.Load()` behavior.
- Save merge follows legacy `RibbonHandler.Save()` behavior, without backup.
- Save re-exports fresh ZIP.
- Save starts `ImportSolutionAsync` for updated solution ZIP.
- Save returns import job id va `needsPublish = true`.

### Phase 2: Generic publish lock

- Import lock and publish lock use the existing operation-state recovery path.
- Request 2 starts `PublishAllXmlAsync` after import job Succeeded.
- Client receives import/publish start output.
- Client persists lock state and polls import/publish status.
- Client rehydrates lock on app startup/refresh.
- Client shows `Importing...` immediately when rehydrating import lock.
- Client shows `Publishing...` immediately when rehydrating publish lock.
- Load/Save stay blocked while import or publish is running.
- When publish status Succeeded, client reloads only if current UI context is still the same ribbon grid.

### Phase 3: Route toolbar type 11

- `ribbons` routes to `EasyTranslatorHandler`.
- `ribbons` is included in `EasyTranslatorHandler.IsUnifiedType()`.
- `App.html` no longer loads `RibbonHandler.js`.

### Phase 4: Cleanup

- Legacy `DataverseLabelTranslator.WebResource/js/Handler/RibbonHandler.js` is removed.
- Unified plan/type matrix is updated.
- Ribbon publish target remains `publishAllXml`, not entity publish.

## Acceptance criteria

Migration duoc xem la dung khi:

- Ribbon Load qua `EasyTranslatorHandler` tra dung tree rows nhu old handler.
- Ribbon Save chi gui generic `changedRows`.
- Server apply LocLabels dung cho `LabelText`, `ToolTipTitle`, `ToolTipDescription`.
- Save request start `ImportSolutionAsync`, return import job id va `needsPublish = true`, khong wait import xong.
- Client poll import job; import success moi start publish.
- Publish request rieng start `PublishAllXmlAsync` va tra publish job id.
- UI hien trang thai dang publish, khoa Load/Save.
- F5/Ctrl+F5 trong luc import van khoi phuc `Importing...`, khoa Load/Save, va tiep tuc poll.
- F5/Ctrl+F5 trong luc publish van khoi phuc `Publishing...`, khoa Load/Save, va tiep tuc poll.
- Publish success thi tu unlock.
- Publish success khi user van o ribbon grid thi show `Published`, show `Reloading...`, roi load lai grid.
- Publish success khi app dang o main grid/context khac thi show `Published`, done, khong reload ribbon grid.
- Publish failed/canceled thi unlock kem error.
- Confirmed running job khong bi auto-unlock theo time; stale lock chi clear khi job not found/error theo retry rules.
- Client khong con parse ZIP, `customizations.xml`, hay `RibbonDiffXml`.

## Bottom line

Khong can "sua lai" ribbon logic. Can di chuyen dung logic do len server adapter, start import bang `ImportSolutionAsync`, poll import den success, sau do moi start publish bang request rieng, va them generic async lock cho ca import lan publish.

Server da process ZIP duoc trong Entity Messages, nen phan ZIP cua ribbon la kha thi. Diem phai thiet ke ky hon la import/publish wait lock: import request phai return import job id nhanh, publish request phai return publish job id nhanh, job id phai duoc persist, status phai poll duoc sau refresh, confirmed running job phai giu `Importing...` hoac `Publishing...`, va stale lock phai duoc clear bang retry rules de khong khoa user vinh vien.
