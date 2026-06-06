# Code Review Type 15 Sitemap

Ngay review: 2026-06-06

## Ket Luan

AI da resolve dung `AGENTS.md`: type 15 la `Sitemap`, handler chinh la `DataverseLabelTranslator.WebResource/js/Handler/SiteMapHandler.js`.

AI cung da lam dung mot phan lon handler contract trong `codex.apply-js-pattern.md` va `codex.handler-architecture-rewrite-plan.md`:

- Co private `GetUpdates()`.
- Co private `FillTable()`.
- Co public `Load(lockText)`.
- Co public `Save()`.
- `Load()` lock grid truoc khi goi `Helper.RunServerLoad(...)`.
- `Save()` check no-change va dung `DialogHelper.alert(...)` truoc khi goi server.
- Handler khong con `WebApiClient`, `XrmTranslator.RunTypeSaveFlow`, direct publish helper, `SaveOnly`, hoac enable/disable Load/Save button.
- Server action `SiteMap` da duoc them vao `ActionNames.cs`, dispatcher, va `DataverseLabelTranslator.Server/CustomActions/Synchronous/SiteMap.cs`.

Nhung ket qua chua dat de merge. Co loi coverage gate chac chan fail, co regression hien thi Display Text cua sitemap, va co save flow co the bao thanh cong/publish du khong update XML nao.

## Findings

### P1 - Them SiteMap vao coverage target nhung test chua du, lam quality gates fail

Thay doi AI folder va config da dua `SiteMap` vao cac target coverage 100%:

- `.agents/skills/pl-unit-tests-server/report-coverage.ps1:58`
- `.agents/skills/pl-unit-tests-server/SKILL.md:77`
- `.claude/skills/pl-unit-tests-server/SKILL.md:79`
- `DataverseLabelTranslator.WebResource/vitest.config.mjs:12`

Nhung tests moi chua cover du:

- `npm --prefix DataverseLabelTranslator.WebResource run test:coverage` fail: global branch coverage `94.17%`, `SiteMapHandler.js` branch coverage `77.77%`.
- `.agents\skills\pl-unit-tests-server\report-coverage.ps1` fail: `SiteMap` line coverage `38.4%`, trong khi script exit non-zero neu class nao duoi 100%.

Day la loi chac chan voi workflow `/pl-unit-tests-webresource` va `/pl-unit-tests-server`. Neu muon them `SiteMap` vao danh sach coverage target, can bo sung test dat 100%. Neu chua co y dinh bat gate cho type 15, khong nen sua `.agents`, `.claude`, va `vitest.config.mjs` trong rewrite nay.

### P2 - Regression: Sitemap Display Text khong con fallback ve entity labels/default title

Code moi chi copy label tu `<Titles>` hoac `<Descriptions>`:

- `DataverseLabelTranslator.WebResource/js/Handler/SiteMapHandler.js:47`
- `DataverseLabelTranslator.WebResource/js/Handler/SiteMapHandler.js:49`

Code cu co fallback quan trong cho Display Text:

- Neu node co `Entity`, load `EntityDefinitions.DisplayName` va gan vao `node.entityLabels`.
- Neu khong co entity label, fallback sang XML `Title` attribute.

Reference code cu:

- `HEAD:DataverseLabelTranslator.WebResource/js/Handler/SiteMapHandler.js:196`
- `HEAD:DataverseLabelTranslator.WebResource/js/Handler/SiteMapHandler.js:204`
- `HEAD:DataverseLabelTranslator.WebResource/js/Handler/SiteMapHandler.js:302`
- `HEAD:DataverseLabelTranslator.WebResource/js/Handler/SiteMapHandler.js:324`

Tac dong: nhieu `SubArea Entity="account"` trong sitemap Dataverse khong co explicit `<Titles>` van hien dung trong app bang entity display name. Handler moi se hien blank placeholder, lam nguoi dung khong thay gia tri hien tai va co the bi base-language validation ep tao label moi khi chi muon edit node. Theo server contract, phan retrieve entity display labels nen duoc dua vao `SiteMap.cs` Loading DTO, khong nen dua lai `WebApiClient` vao handler.

### P2 - Save co the publish/reload du khong update sitemap XML

`SiteMap.Saving()` luon add `sitemapId` vao output sau khi xu ly update, ke ca khi XML khong doi:

- `DataverseLabelTranslator.Server/CustomActions/Synchronous/SiteMap.cs:50`
- `DataverseLabelTranslator.Server/CustomActions/Synchronous/SiteMap.cs:52`
- `DataverseLabelTranslator.Server/CustomActions/Synchronous/SiteMap.cs:58`

`UpdateSitemapXml()` tra ve XML goc khi khong tim thay node hoac khi parse/update bi exception:

- `DataverseLabelTranslator.Server/CustomActions/Synchronous/SiteMap.cs:255`
- `DataverseLabelTranslator.Server/CustomActions/Synchronous/SiteMap.cs:291`

Tac dong: handler nhan `sitemapIds`, tiep tuc `Publishing`, `Published`, va reload nhu save thanh cong, trong khi thuc te khong co `serviceAdmin.Update(...)`. Nen chi return id khi update that su duoc ghi, hoac throw/tra loi loi ro rang khi composite id/node bi loi.

### P2 - Scope rewrite bi vuot qua: docs noi khong implement JS/C# unit tests

Hai file instruction deu noi khi rewrite mot handler thi khong implement/analyze JS/C# unit tests:

- `DataverseLabelTranslator.Documents/codex.apply-js-pattern.md`
- `DataverseLabelTranslator.Documents/codex.handler-architecture-rewrite-plan.md`

Nhung diff da them:

- `DataverseLabelTranslator.WebResource/tests/SiteMapHandler.test.js`
- `DataverseLabelTranslator.Test/CustomActions/Synchronous/SiteMapTests.cs`
- Coverage target changes trong `.agents`, `.claude`, va `vitest.config.mjs`

Neu day la task rewrite type 15 theo hai docs tren, cac thay doi test/coverage nay la ngoai scope. Neu project muon giu lai test thi can nang coverage cho dat gate, con neu chi muon handler rewrite thi nen revert phan test/coverage config.

### P3 - Published phase cua SiteMap khong co wait nhu cac type reference

Type reference `Dashboard`, `WebResource`, va `GlobalOptionSet` deu co wait trong `Published` phase truoc khi reload. `SiteMap.Published()` chi validate va return ids:

- `DataverseLabelTranslator.Server/CustomActions/Synchronous/SiteMap.cs:90`

Day khong nhat thiet lam sai data reload vi handler load lai `sitemapxml` tu DB, nhung no lech voi pattern server reference va co the khien UI bao published/reload qua som so voi Dataverse publish propagation.

## AI Folder Review

Da review cac thay doi trong:

- `.agents/skills/pl-unit-tests-server/SKILL.md`
- `.agents/skills/pl-unit-tests-server/report-coverage.ps1`
- `.agents/skills/pl-unit-tests-webresource/SKILL.md`
- `.claude/skills/pl-unit-tests-server/SKILL.md`
- `.claude/skills/pl-unit-tests-webresource/SKILL.md`

Nhan xet:

- `.agents` va `.claude` dang sync ve noi dung them `SiteMap` vao coverage target.
- Tuy nhien viec them target nay lam coverage workflow fail nhu P1.
- Khong thay thay doi trong `.github/prompts`.
- Khong thay folder `.agentts`; folder dung trong repo la `.agents`.
- `powershell -ExecutionPolicy Bypass -File DataverseLabelTranslator.Scripts\check-ai-config.ps1` hien fail vi co folder root `coverage`. Folder nay dang ignored va khong nam trong git status, nhung no block check AI config: `Unexpected file exists: coverage`.

## Verification Da Chay

- PASS: `npm --prefix DataverseLabelTranslator.WebResource test -- SiteMapHandler.test.js`
  - 4 tests passed.
- PASS: `dotnet test DataverseLabelTranslator.Test\DataverseLabelTranslator.Test.csproj --configuration Debug --filter SiteMapTests`
  - 2 tests passed.
  - Co warning `NU1900` do khong doc duoc package vulnerability data tu NuGet/Azure feed.
- FAIL: `npm --prefix DataverseLabelTranslator.WebResource run test:coverage`
  - Tests passed, coverage fail vi branch coverage duoi 100%.
- FAIL: `powershell -ExecutionPolicy Bypass -File .agents\skills\pl-unit-tests-server\report-coverage.ps1`
  - `SiteMap` line coverage `38.4%`.
- FAIL: `powershell -ExecutionPolicy Bypass -File DataverseLabelTranslator.Scripts\check-ai-config.ps1`
  - `Unexpected file exists: coverage`.

## De Xuat Fix

1. Chon mot trong hai huong coverage:
   - Bo `SiteMap` khoi coverage target va AI skill docs neu rewrite scope khong bao gom tests.
   - Hoac giu `SiteMap` trong target va them tests dat 100% branch/line cho JS va C#.
2. Phuc hoi Display Text fallback:
   - Server `Loading` nen parse sitemap nodes va bo sung entity display labels/default title vao DTO.
   - Handler chi nen fill grid tu DTO, khong goi `WebApiClient`.
3. Sua `SiteMap.Saving()`:
   - Chi add `sitemapId` vao output khi `serviceAdmin.Update(...)` that su chay.
   - Throw hoac return error khi XML invalid hoac node khong tim thay, de user khong thay save/publish gia.
4. Can nhac them wait vao `Published` phase cho giong reference server types.
