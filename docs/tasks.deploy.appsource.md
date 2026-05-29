# Tasks - Deploy Dataverse Label Translator To AppSource

Ngày 2026-05-27. File này là backlog triển khai AppSource theo thứ tự để AI có thể làm từng task. File kế hoạch nền: `docs/deploy.appsource.md`.

Mục tiêu cuối: tạo được file ZIP all-in-one up to date với file managed solution do user kiểm soát và sẵn sàng upload lên Azure Blob. Với version hiện tại, output là:

```text
D:\github\DataverseLabelTranslator\release\1.0.0.0\appsource\zip\DataverseLabelTranslator.v.1.0.0.zip
```

Version rule cho các release sau:

- Nếu user/prompt mention version rõ, ví dụ `1.1.0.0`, `$pl-release-appsource` phải dùng đúng version đó.
- Nếu user không mention version, script phải tự chọn latest bằng cách scan folder `release\<version>\dataverse\solutions\DataverseLabelTranslator_managed.zip` và lấy version số lớn nhất.
- Nếu không tìm được managed release folder nào, default/fallback là `1.0.0.0`.
- Marketplace package version lấy 3 số đầu của solution version: `1.0.0.0 -> 1.0.0`, `1.1.0.0 -> 1.1.0`.
- Mỗi version release nằm trong folder riêng: `release\<solution-version>\`, gồm `dataverse\` và `appsource\`.
- Version folder cũ là read-only theo quy ước release. Khi build `1.1.0.0`, không xóa/sửa `release\1.0.0.0`; script chỉ clean `appsource\src` và `appsource\zip` của selected version.
- Nếu muốn tạo version mới, ví dụ `1.0.1.0`, anh Phước/AP copy nguyên folder `release\1.0.0.0` thành `release\1.0.1.0`, thay managed solution trong `release\1.0.1.0\dataverse\solutions\`, rồi gọi `$pl-release-appsource` với version `1.0.1.0`. Script sẽ rebuild package cho selected version và tự đổi tên các file review DOCX/PDF trong selected folder nếu chúng vẫn còn tên version cũ.

Quyết định đã chốt, không hỏi lại:

- Dùng lại Partner Center account/publisher cũ đã publish `Icons and Tooltips with D365`.
- Publisher/display name: `PhuocLe`.
- Publisher ID: `phuocle`.
- Offer ID: `dataverse-label-translator`.
- Listing option: `Get it now (free)` 100%.
- Không dùng `Contact me`, paid plan, trial conversion, marketplace purchase testing hoặc ISV app license management.
- AI-assisted translation là core feature của listing và certification path.

Nguyên tắc an toàn:

- Với `$pl-release-appsource`, luôn trust file managed solution của selected version tại `D:\github\DataverseLabelTranslator\release\<version>\dataverse\solutions\DataverseLabelTranslator_managed.zip` là bản latest/newest do user kiểm soát. Không kiểm Dataverse freshness, không đọc version từ Dataverse, không chạy PAC export, không gợi ý chạy export.
- Không chạy `$pl-export-solution` hoặc skill `pl-export-solution` trong bất kỳ bước `$pl-release-appsource` nào.
- Không upload Azure Blob trong repo task trừ khi user yêu cầu rõ.
- Không commit SAS URL thật. `zip/release.md` chỉ được tạo local/private và phải bị ignore hoặc không stage.
- Không deploy PropertyEditor resources.
- Không hard-code AI endpoint/API key vào source, package hoặc docs commit.
- Không stage/commit/push trừ khi user yêu cầu.

## Phase 0 - Khóa Input Và Baseline

### Task 0.1 - Đọc tài liệu hiện có

Mục tiêu: AI nắm đúng trạng thái và quyết định đã chốt trước khi làm việc.

Actions:

1. Đọc `docs/deploy.appsource.md`.
2. Đọc `README.md`.
3. Đọc `AGENTS.md`.
4. Đọc `.agents/skills/pl-export-solution/SKILL.md` chỉ để học style workflow/output contract. Không chạy export và không bảo user chạy export trong luồng này.
5. Đọc cấu trúc AppSource cũ nếu có quyền:
   - `D:\azure\phuocle\d365icons\D365Icons\src2\AppSource`
   - Tập trung vào version mới nhất, hiện là `1.3.3.0`.
6. Đọc document AppSource hiện có của app này trong release folder:
   - `D:\github\DataverseLabelTranslator\release\1.0.0.0\appsource\Documents\UserGuide.1.0.0.0.docx`
   - `D:\github\DataverseLabelTranslator\release\1.0.0.0\appsource\Test\E2E User Scenario.1.0.0.0.docx`

Acceptance criteria:

- AI có thể nói lại đúng final upload artifact của app mới.
- AI không hỏi lại publisher/free/offer type.
- AI không chạy PAC export.
- AI hiểu `release\1.0.0.0\dataverse\solutions\DataverseLabelTranslator_managed.zip` là nguồn sự thật tuyệt đối cho AppSource release.
- AI hiểu `release\<version>\appsource\Documents` và `release\<version>\appsource\Test` là nơi làm việc chính cho User Guide/E2E Scenario của version đó.

### Task 0.2 - Kiểm tra repo và generated files hiện tại

Mục tiêu: tránh ghi đè thay đổi không liên quan.

Actions:

1. Run:

```powershell
git status --short
```

2. Ghi nhận các file/folder untracked hoặc modified.
3. Nếu có thay đổi của user ngoài phạm vi AppSource, không sửa.
4. Kiểm tra release hiện có:

```powershell
Get-ChildItem release\1.0.0.0\dataverse\solutions -File
```

5. Xác nhận có:

```text
release\1.0.0.0\dataverse\solutions\DataverseLabelTranslator_managed.zip
release\1.0.0.0\dataverse\solutions\DataverseLabelTranslator.zip
```

Acceptance criteria:

- Biết rõ managed solution source đang dùng để build AppSource.
- Không đánh giá file này có mới hơn Dataverse hay không; user/AP sẽ chịu trách nhiệm cập nhật file này trước khi gọi `$pl-release-appsource`.
- Nếu thiếu managed solution, task `$pl-release-appsource` phải stop và báo thiếu file nguồn. Không tự chạy export và không yêu cầu trong cùng luồng `$pl-release-appsource`.

## Phase 1 - Tạo Skill `Release AppSource`

### Task 1.1 - Tạo folder skill

Mục tiêu: có một workflow/skill chính thức, giống style `$pl-export-solution`, để lần sau user gọi `$pl-release-appsource` là AI biết build final ZIP.

Actions:

1. Tạo folder:

```text
D:\github\DataverseLabelTranslator\.agents\skills\pl-release-appsource
```

2. Tạo file:

```text
D:\github\DataverseLabelTranslator\.agents\skills\pl-release-appsource\SKILL.md
```

3. Front matter đề xuất:

```yaml
---
name: "pl-release-appsource"
description: "Build the final AppSource all-in-one Marketplace ZIP for Dataverse Label Translator. Does not upload to Azure."
---
```

4. Tiêu đề skill:

```markdown
# Release AppSource
```

Acceptance criteria:

- Skill mới xuất hiện trong `.agents/skills/pl-release-appsource/SKILL.md`.
- Skill không gọi là `pl-export-solution`.
- Skill không tự upload Azure Blob.

### Task 1.2 - Viết Output Contract cho skill

Mục tiêu: mỗi lần gọi `$pl-release-appsource`, output cuối luôn là final ZIP ready để user upload.

Nội dung cần có trong `SKILL.md`:

```text
Selected Release Version: inferred latest or explicit user version, default fallback `1.0.0.0`
Selected Marketplace Package Version: first three parts of selected release version, e.g. `1.1.0.0 -> 1.1.0`
Final upload ZIP:
D:\github\DataverseLabelTranslator\release\<solution-version>\appsource\zip\DataverseLabelTranslator.v.<package-version>.zip
```

Skill phải cam kết:

1. Consume existing managed solution for the selected version as the source of truth. Treat this exact file as latest/newest without checking Dataverse:

```text
D:\github\DataverseLabelTranslator\release\<solution-version>\dataverse\solutions\DataverseLabelTranslator_managed.zip
```

2. Rebuild all AppSource staging folders for that version.
3. Rebuild nested Package Deployer ZIP:

```text
release\<solution-version>\appsource\src\DataverseLabelTranslator.v.<package-version>\DataverseLabelTranslatorPackage.zip
```

4. Rebuild final all-in-one upload ZIP:

```text
release\<solution-version>\appsource\zip\DataverseLabelTranslator.v.<package-version>.zip
```

5. Verify final ZIP structure.
6. Report final ZIP path and size.
7. Do not stage, commit, push, upload or write SAS URL unless user separately asks.

Acceptance criteria:

- `SKILL.md` has an explicit output contract.
- It states that final ZIP is the file to upload to Azure Storage.
- It states that `DataverseLabelTranslatorPackage.zip` is nested and must not be uploaded directly.
- It states that `release\<solution-version>\dataverse\solutions\DataverseLabelTranslator_managed.zip` is trusted blindly as latest by design.
- It states that old AppSource version folders must not be changed when a newer version is selected.

### Task 1.3 - Viết command workflow trong skill

Mục tiêu: workflow đủ chi tiết để AI có thể chạy lại không cần hỏi.

Skill steps cần có:

1. Confirm repository:

```powershell
git rev-parse --show-toplevel
```

Expected:

```text
D:\github\DataverseLabelTranslator
```

2. Resolve version variables. If the user mentioned a version, pass it to the script. If not, let the script infer latest from `release\<version>\dataverse\solutions\DataverseLabelTranslator_managed.zip`.

```powershell
$RepoRoot = "D:\github\DataverseLabelTranslator"
$SolutionVersion = "<explicit version, inferred latest, or fallback 1.0.0.0>"
$PackageVersion = "<first three parts of SolutionVersion>"
$ManagedSolutionZip = Join-Path $RepoRoot "release\$SolutionVersion\dataverse\solutions\DataverseLabelTranslator_managed.zip"
$AppSourceRoot = Join-Path $RepoRoot "release\$SolutionVersion\appsource"
$PackageProjectDir = Join-Path $AppSourceRoot "src\DataverseLabelTranslatorPackage"
$MarketplaceRoot = Join-Path $AppSourceRoot "src\DataverseLabelTranslator.v.$PackageVersion"
$ZipDir = Join-Path $AppSourceRoot "zip"
$NestedPackageZip = Join-Path $MarketplaceRoot "DataverseLabelTranslatorPackage.zip"
$FinalZip = Join-Path $ZipDir "DataverseLabelTranslator.v.$PackageVersion.zip"
```

3. Validate source managed solution exists. If missing, stop with message using the selected version:

```text
Missing managed solution source:
D:\github\DataverseLabelTranslator\release\<solution-version>\dataverse\solutions\DataverseLabelTranslator_managed.zip
$pl-release-appsource cannot continue without this user-controlled file.
```

4. Recreate only selected-version staging folders:

```text
release\<solution-version>\appsource\src
release\<solution-version>\appsource\zip
```

Do not delete old version folders, for example when selected version is `1.1.0.0`, do not modify:

```text
release\1.0.0.0
```

Also do not delete the whole selected version folder or unrelated repo folders:

```text
release\<solution-version>
docs
js
css
html
img
```

5. Generate/copy AppSource files.
6. Build nested Package Deployer ZIP.
7. Build final all-in-one ZIP.
8. Verify final ZIP entries.
9. Report final path.

Acceptance criteria:

- Skill has enough commands/pseudocode to rebuild the final ZIP.
- Skill explicitly refuses to continue if managed solution is missing.
- Skill does not say it will run `$pl-export-solution`.
- Skill states that `DataverseLabelTranslator_managed.zip` is always trusted as latest/newest for this workflow.

### Task 1.4 - Add trigger note to AGENTS.md if desired

Mục tiêu: future AI sees `$pl-release-appsource` as a supported repo command.

Actions:

1. Add a new command section to `AGENTS.md`:

```markdown
### /pl-release-appsource
Build the final AppSource all-in-one Marketplace ZIP from the existing managed release solution. Always trust `release/1.0.0.0/dataverse/solutions/DataverseLabelTranslator_managed.zip` as the latest user-controlled source. Do not export the Dataverse solution. Output must be `release/<version>/appsource/zip/DataverseLabelTranslator.v.<package-version>.zip`. Do not upload to Azure.
```

2. Keep `$pl-export-solution` unchanged.

Acceptance criteria:

- Repo instructions mention `$pl-release-appsource`.
- It clearly says do not export Dataverse solution.
- It clearly says the existing managed ZIP is trusted as latest.

## Phase 2 - Tạo AppSource Build Script

### Task 2.1 - Tạo script PowerShell `scripts/release-appsource.ps1`

Mục tiêu: skill gọi một script ổn định thay vì AI tự zip thủ công mỗi lần.

Actions:

1. Create:

```text
D:\github\DataverseLabelTranslator\scripts\release-appsource.ps1
```

2. Parameters:

```powershell
param(
    [string]$RepoRoot = "D:\github\DataverseLabelTranslator",
    [string]$SolutionVersion = "",
    [string]$PackageVersion = "",
    [switch]$SkipPackageDeployerBuild
)
```

Version behavior:

- Empty `$SolutionVersion` means infer latest from `release\<version>\dataverse\solutions\DataverseLabelTranslator_managed.zip`.
- Explicit `$SolutionVersion` means use that exact version and fail if its managed solution is missing.
- Empty `$PackageVersion` means derive from `$SolutionVersion` by dropping the fourth part.
- Script must only clean `release\$SolutionVersion\appsource\src` and `release\$SolutionVersion\appsource\zip`.
- Script must never clean or rewrite `release\<other-version>`.

3. Derived paths:

```powershell
$ManagedSolutionZip = Join-Path $RepoRoot "release\$SolutionVersion\dataverse\solutions\DataverseLabelTranslator_managed.zip"
$AppSourceRoot = Join-Path $RepoRoot "release\$SolutionVersion\appsource"
$SrcRoot = Join-Path $AppSourceRoot "src"
$PackageProjectDir = Join-Path $SrcRoot "DataverseLabelTranslatorPackage"
$PkgFolder = Join-Path $PackageProjectDir "PkgFolder"
$MarketplaceRoot = Join-Path $SrcRoot "DataverseLabelTranslator.v.$PackageVersion"
$ZipDir = Join-Path $AppSourceRoot "zip"
$NestedPackageZip = Join-Path $MarketplaceRoot "DataverseLabelTranslatorPackage.zip"
$FinalZip = Join-Path $ZipDir "DataverseLabelTranslator.v.$PackageVersion.zip"
```

4. Fail fast if `$RepoRoot` is not repo root.
5. Fail fast if `$ManagedSolutionZip` is missing.
6. Clean only `$SrcRoot` and `$ZipDir`.
7. Create required directories.

Acceptance criteria:

- Running script without managed solution gives a clear error.
- Script does not modify Dataverse.
- Script does not upload anything.

### Task 2.2 - Add zip helper functions to script

Mục tiêu: build ZIPs deterministically and verify root entries.

Functions needed:

1. `New-CleanDirectory($Path)`
2. `Assert-FileExists($Path, $Message)`
3. `New-ZipFromDirectoryRoot($SourceDir, $ZipPath)`
4. `Get-ZipEntries($ZipPath)`
5. `Assert-ZipHasRootEntries($ZipPath, [string[]]$ExpectedEntries)`
6. `Assert-ZipHasNoParentFolder($ZipPath, $ForbiddenPrefix)`

Implementation requirements:

- Use `[System.IO.Compression.ZipFile]` from .NET.
- Zip contents of source folder, not the folder itself.
- Use `-LiteralPath` for filesystem operations.
- If ZIP exists, delete it before recreating.

Acceptance criteria:

- Final ZIP root contains exactly expected root files.
- No accidental `DataverseLabelTranslator.v.1.0.0/` parent folder inside final ZIP.

### Task 2.3 - Generate `[Content_Types].xml`

Mục tiêu: reuse convention đã pass AppSource package cũ.

Actions:

1. Generate same `[Content_Types].xml` into:

```text
release\1.0.0.0\appsource\src\DataverseLabelTranslatorPackage\[Content_Types].xml
release\1.0.0.0\appsource\src\DataverseLabelTranslator.v.1.0.0\[Content_Types].xml
```

2. Content template:

```xml
<?xml version="1.0" encoding="utf-8"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
  <Default Extension="xml" ContentType="application/octet-stream" />
  <Default Extension="xaml" ContentType="application/octet-stream" />
  <Default Extension="dll" ContentType="application/octet-stream" />
  <Default Extension="zip" ContentType="application/octet-stream" />
  <Default Extension="jpb" ContentType="application/octet-stream" />
  <Default Extension="gif" ContentType="application/octet-stream" />
  <Default Extension="png" ContentType="application/octet-stream" />
  <Default Extension="htm" ContentType="application/octet-stream" />
  <Default Extension="html" ContentType="application/octet-stream" />
  <Default Extension="txt" ContentType="application/octet-stream" />
  <Default Extension="db" ContentType="application/octet-stream" />
  <Default Extension="css" ContentType="application/octet-stream" />
</Types>
```

Acceptance criteria:

- Both files exist.
- Encoding is UTF-8.
- XML is parseable.

### Task 2.4 - Generate `PkgFolder\ImportConfig.xml`

Mục tiêu: Package Deployer imports only the managed solution.

Actions:

1. Create:

```text
release\1.0.0.0\appsource\src\DataverseLabelTranslatorPackage\PkgFolder\ImportConfig.xml
```

2. Content:

```xml
<?xml version="1.0" encoding="utf-16"?>
<configdatastorage xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema"
                   installsampledata="false"
                   waitforsampledatatoinstall="true"
                   agentdesktopzipfile=""
                   agentdesktopexename=""
                   crmmigdataimportfile="">
  <solutions>
    <configsolutionfile solutionpackagefilename="DataverseLabelTranslator_managed.zip" />
  </solutions>
</configdatastorage>
```

3. Copy managed solution into:

```text
release\1.0.0.0\appsource\src\DataverseLabelTranslatorPackage\PkgFolder\DataverseLabelTranslator_managed.zip
```

Acceptance criteria:

- `ImportConfig.xml` exists.
- Managed solution is copied into `PkgFolder`.
- The solution filename in XML exactly matches copied ZIP filename.

### Task 2.5 - Create Package Deployer wizard pages in `PkgFolder`

Mục tiêu: user/reviewer sees clean Package Deployer wizard screens during install, not template placeholders. This is the install wizard content under `PkgFolder\Content\en-us`, matching the app Icons package pattern.

Actions:

1. Create folders:

```text
PkgFolder\Content\en-us\WelcomeHtml\HTML
PkgFolder\Content\en-us\WelcomeHtml\CSS
PkgFolder\Content\en-us\WelcomeHtml\Images
PkgFolder\Content\en-us\EndHtml\HTML
PkgFolder\Content\en-us\EndHtml\CSS
PkgFolder\Content\en-us\EndHtml\Images
```

2. Generate `WelcomeHtml\HTML\Default.htm` with:

- Title: `Dataverse Label Translator package [1.0.0.0]`
- Short description: free model-driven Dataverse admin utility for translating labels and metadata.
- AI note: includes AI-assisted translation; provider credentials are customer-supplied or supplied temporarily for certification.
- Permission note: requires System Administrator or equivalent customization privileges.
- Privacy note: Auto Translate sends selected label/metadata text to configured provider.

3. Generate `EndHtml\HTML\Default.htm` with:

- Title: `Dataverse Label Translator package [1.0.0.0]`
- Thank-you message.
- Next step: open Dataverse Label Translator model-driven app.

4. Generate `WelcomeHtml\CSS\common.css` and `EndHtml\CSS\common.css`.
5. Either make the CSS self-contained with no image references, or include all image assets referenced by CSS.
6. If reusing the app Icons wizard CSS pattern, include these images in both `WelcomeHtml\Images` and `EndHtml\Images`:

```text
body_back.gif
content_back.gif
content_back_orig.gif
contentarea_back.gif
contentArea_back_home.gif
footer_back.gif
header_back.gif
nav_back.gif
nav_list_back.gif
top_item_selected_bg.gif
```

7. Do not reuse product text from app Icons.

Acceptance criteria:

- No leftover text `Template Package Title Here`.
- No `Icons and Tooltips` text.
- `PkgFolder\Content\en-us\WelcomeHtml\HTML\Default.htm` exists.
- `PkgFolder\Content\en-us\EndHtml\HTML\Default.htm` exists.
- `PkgFolder\Content\en-us\WelcomeHtml\CSS\common.css` exists.
- `PkgFolder\Content\en-us\EndHtml\CSS\common.css` exists.
- If `common.css` contains `url(../images/...)`, every referenced file exists under the matching `Images` folder.
- HTML files are valid enough for browser open.

### Task 2.6 - Build or copy Package Deployer DLL

Mục tiêu: create `PL.DataverseLabelTranslator.PackageDeployment.dll` for nested package.

Preferred approach:

1. Investigate if a Package Deployer project already exists in repo or old Icons source outside AppSource folder.
2. If no project exists, create minimal Package Deployer project under:

```text
release\1.0.0.0\appsource\package-project\PL.DataverseLabelTranslator.PackageDeployment
```

3. Use Power Platform Package Deployer template/tooling where available.
4. The package should import only `PkgFolder\ImportConfig.xml`.
5. Avoid custom business logic if not required.

Fallback approach:

1. Create a small documented manual step in the script output:

```text
Missing Package Deployer DLL. Build PL.DataverseLabelTranslator.PackageDeployment.dll before final packaging.
```

2. Stop before final ZIP if DLL is missing.

Build `PL.DataverseLabelTranslator.PackageDeployment.dll` yêu cầu .NET SDK, .NET Framework 4.6.2 reference assemblies, và NuGet package `Microsoft.CrmSdk.XrmTooling.PackageDeployment.Wpf`. `scripts\release-appsource.ps1` phải build DLL tối thiểu nếu project chưa có sẵn; nếu build tooling thiếu thì fail rõ trước khi tạo final ZIP.

Acceptance criteria:

- DLL exists at:

```text
release\1.0.0.0\appsource\src\DataverseLabelTranslatorPackage\PL.DataverseLabelTranslator.PackageDeployment.dll
```

- Script fails clearly if the DLL cannot be built or found.
- No unrelated DLL from Icons package is reused.

### Task 2.7 - Generate `license.md`, `term.md`, and `TermsOfUse.html`

Mục tiêu: AI tạo đầy đủ legal/terms source files cho AppSource và generate HTML terms trong Marketplace package. App này 100% free, deploy/install bao nhiêu environment cũng được, không có publisher backend server, nhưng có AI provider calls do user tự cấu hình.

Actions:

1. Create source/review markdown files. These files are for AppSource/legal review and Partner Center copy source; do not add them to final Marketplace ZIP root unless Microsoft explicitly asks for markdown files:

```text
release\1.0.0.0\appsource\assets\license.md
release\1.0.0.0\appsource\assets\term.md
```

2. Create Marketplace package terms HTML:

```text
release\1.0.0.0\appsource\src\DataverseLabelTranslator.v.1.0.0\TermsOfUse.html
```

3. `license.md` must state:

- Product: `Dataverse Label Translator`.
- Publisher: `PhuocLe`.
- License/cost: free.
- No paid license required.
- No trial expiration.
- Users may install/deploy/use it in any number of Dataverse/Dynamics 365 environments.
- Users may copy/deploy the solution across their own environments.
- No warranty; use at your own risk.
- The app is provided as-is.
- The user is responsible for backing up solutions/environment metadata before making translation changes.
- The user is responsible for compliance with their organization, Microsoft, Dataverse, Dynamics 365, Power Platform, and AI provider terms.

4. `term.md` must state:

- Product: `Dataverse Label Translator`.
- Free use.
- No trial expiration.
- No paid license required.
- Unlimited install/deploy/use across customer-owned Dataverse/Dynamics 365 environments is allowed.
- Requires Dataverse/Dynamics 365 environment.
- Requires System Administrator or equivalent privileges.
- The app does **not** send customer data, labels, metadata, API keys, telemetry, or usage data to a PhuocLe/Dataverse Label Translator publisher server.
- The app has no publisher-controlled backend server for data collection.
- AI-assisted translation sends selected label/metadata text directly from the user's browser/client to the AI provider endpoint configured by the user.
- AI provider endpoint/model/API key are supplied by the user, or by temporary certification test credentials for Microsoft review.
- AI credentials are not hard-coded in the solution.
- AI provider settings may be stored in browser `localStorage` for convenience so the user does not need to re-enter them every time.
- `localStorage` is browser-side storage; users should treat stored endpoint/API key/model values as sensitive according to their organization's policy.
- User is responsible for choosing, configuring, securing, and paying for their AI provider.
- User accepts AI quality, privacy, security, cost, quota, and compliance risks from their selected AI provider. Use it at your own risk.
- Dictionary data is stored in Dataverse as customer-owned data.
- No username/password collection by app.
- No unsupported customization.
- Contact/support section.

5. Generate `TermsOfUse.html` from `term.md` content with simple HTML suitable for Marketplace package.
6. Do not copy old Icons terms because it says trial/paid/no internet access.
7. Keep final Marketplace ZIP root unchanged:

```text
DataverseLabelTranslatorPackage.zip
[Content_Types].xml
input.xml
TermsOfUse.html
logo32x32.png
```

Acceptance criteria:

- `license.md` exists.
- `term.md` exists.
- `TermsOfUse.html` exists.
- Contains no `Icons and Tooltips`.
- Contains no claim that the app does not access internet.
- Contains no trial/paid/purchase language.
- States free/unlimited install/deploy/use.
- States no data is sent to PhuocLe/publisher server.
- Contains explicit AI/external provider disclosure.
- States AI settings may be stored in browser `localStorage`.
- Contains `Use it at your own risk` or equivalent risk wording.

### Task 2.8 - Generate `input.xml`

Mục tiêu: Marketplace root metadata matches final package.

Actions:

1. Create:

```text
release\1.0.0.0\appsource\src\DataverseLabelTranslator.v.1.0.0\input.xml
```

2. Base content:

```xml
<PvsPackageData>
  <ProviderName>PhuocLe</ProviderName>
  <PackageFile>DataverseLabelTranslatorPackage.zip</PackageFile>
  <SolutionAnchorName>DataverseLabelTranslator_managed.zip</SolutionAnchorName>
  <StartDate>01/01/2026</StartDate>
  <EndDate>12/31/2030</EndDate>
  <SupportedCountries>...</SupportedCountries>
  <LearnMoreLink>https://phuocle.github.io/DataverseLabelTranslator/</LearnMoreLink>
  <Locales>
    <PackageLocale Code="1033" IsDefault="true">
       <Logo>logo32x32.png</Logo>
       <Terms>
         <PackageTerm File="TermsOfUse.html" />
       </Terms>
    </PackageLocale>
  </Locales>
</PvsPackageData>
```

3. Use SupportedCountries list from old Icons `input.xml` unless user narrows markets.
4. Use GitHub Pages homepage as final Help/LearnMore URL:

```text
https://phuocle.github.io/DataverseLabelTranslator/
```

5. If the homepage is not live yet, use this URL as the intended final URL in draft assets, but do not submit Partner Center until GitHub Pages is live.

Acceptance criteria:

- `ProviderName` is `PhuocLe`.
- `PackageFile` is `DataverseLabelTranslatorPackage.zip`.
- `SolutionAnchorName` is `DataverseLabelTranslator_managed.zip`.
- `LearnMoreLink` is `https://phuocle.github.io/DataverseLabelTranslator/`.
- XML is parseable.
- `input.xml` lowercase.

### Task 2.9 - Generate `logo32x32.png`

Mục tiêu: Marketplace package has required 32x32 icon.

Actions:

1. Source:

```text
img\app-icon.svg
```

2. Output:

```text
release\1.0.0.0\appsource\src\DataverseLabelTranslator.v.1.0.0\logo32x32.png
release\1.0.0.0\appsource\assets\logo32x32.png
```

3. Prefer a reliable converter available locally:

- ImageMagick if installed.
- Inkscape if installed.
- Browser/canvas rendering script if neither is installed.

4. Verify dimensions are exactly 32x32.

Acceptance criteria:

- PNG exists.
- Dimensions are 32x32.
- File is not empty.

### Task 2.10 - Build nested Package Deployer ZIP

Mục tiêu: create the ZIP referenced by `input.xml`.

Actions:

1. Zip contents of:

```text
release\1.0.0.0\appsource\src\DataverseLabelTranslatorPackage
```

2. Output:

```text
release\1.0.0.0\appsource\src\DataverseLabelTranslator.v.1.0.0\DataverseLabelTranslatorPackage.zip
```

3. Required root entries inside nested ZIP:

```text
[Content_Types].xml
PL.DataverseLabelTranslator.PackageDeployment.dll
PkgFolder/ImportConfig.xml
PkgFolder/DataverseLabelTranslator_managed.zip
PkgFolder/Content/en-us/WelcomeHtml/HTML/Default.htm
PkgFolder/Content/en-us/WelcomeHtml/CSS/common.css
PkgFolder/Content/en-us/WelcomeHtml/Images/body_back.gif
PkgFolder/Content/en-us/WelcomeHtml/Images/content_back.gif
PkgFolder/Content/en-us/WelcomeHtml/Images/footer_back.gif
PkgFolder/Content/en-us/WelcomeHtml/Images/header_back.gif
PkgFolder/Content/en-us/EndHtml/HTML/Default.htm
PkgFolder/Content/en-us/EndHtml/CSS/common.css
PkgFolder/Content/en-us/EndHtml/Images/body_back.gif
PkgFolder/Content/en-us/EndHtml/Images/content_back.gif
PkgFolder/Content/en-us/EndHtml/Images/footer_back.gif
PkgFolder/Content/en-us/EndHtml/Images/header_back.gif
```
Nếu `common.css` tham chiếu wizard images, phải include đủ 10 file GIF trong cả `WelcomeHtml\Images` và `EndHtml\Images`: `body_back.gif`, `content_back.gif`, `content_back_orig.gif`, `contentarea_back.gif`, `contentArea_back_home.gif`, `footer_back.gif`, `header_back.gif`, `nav_back.gif`, `nav_list_back.gif`, `top_item_selected_bg.gif`.

Acceptance criteria:

- Nested ZIP exists.
- Root entries are correct.
- It does not contain parent folder `DataverseLabelTranslatorPackage/`.
- Wizard content under `PkgFolder/Content/en-us` is included in nested ZIP, not only in the source folder.

### Task 2.11 - Build final all-in-one Marketplace ZIP

Mục tiêu: create the upload artifact.

Actions:

1. Zip contents of:

```text
release\1.0.0.0\appsource\src\DataverseLabelTranslator.v.1.0.0
```

2. Output:

```text
release\1.0.0.0\appsource\zip\DataverseLabelTranslator.v.1.0.0.zip
```

3. Required root entries:

```text
DataverseLabelTranslatorPackage.zip
[Content_Types].xml
input.xml
TermsOfUse.html
logo32x32.png
```

4. Forbidden:

```text
DataverseLabelTranslator.v.1.0.0/
DataverseLabelTranslatorPackage/
release/
src/
zip/
```

Acceptance criteria:

- Final ZIP exists.
- Final ZIP contains exactly the required Marketplace root entries.
- Final ZIP does not contain a parent folder.
- Final ZIP is the only file intended for Azure Blob upload.

### Task 2.12 - Validate final release outputs

Mục tiêu: AI can prove the package is upload-ready.

Actions:

1. Print final file path.
2. Print file size.
3. List final ZIP root entries.
4. List nested Package Deployer ZIP root entries.
5. Parse `input.xml`.
6. Verify `PackageFile` points to `DataverseLabelTranslatorPackage.zip`.
7. Verify `SolutionAnchorName` points to `DataverseLabelTranslator_managed.zip`.
8. Verify no files contain:

```text
Icons and Tooltips
D365Icons
Template Package Title Here
trial expires
purchase license
```

Acceptance criteria:

- Validation passes.
- Final report says:

```text
Ready to upload:
D:\github\DataverseLabelTranslator\release\1.0.0.0\appsource\zip\DataverseLabelTranslator.v.1.0.0.zip
```

## Phase 3 - Documentation Assets

### Task 3.1 - Review User Guide in selected release folder

Mục tiêu: dùng User Guide đã có trong selected release folder làm nguồn, review/chỉnh nếu cần, rồi render PDF cùng version.

Review document:

```text
D:\github\DataverseLabelTranslator\release\1.0.0.0\appsource\Documents\UserGuide.1.0.0.0.docx
```

Outputs:

```text
release\1.0.0.0\appsource\Documents\UserGuide.1.0.0.0.docx
release\1.0.0.0\appsource\Documents\UserGuide.1.0.0.0.pdf
```

Current source content already includes:

1. Product overview for Dataverse Label Translator.
2. Requirements and installation.
3. Model-driven app access.
4. Interface overview.
5. Translation scope/component type list.
6. Manual inline translation.
7. All-In-One mode.
8. Search/filter helpers.
9. AI-assisted translation settings and Auto Translate workflow.
10. Translation Dictionary/Glossary.
11. Important form translation/troubleshooting notes.

Actions:

1. Do not overwrite the review file blindly.
2. Review the document for product-specific correctness:
   - no `Icons and Tooltips`,
   - free/no paid/trial wording,
   - AI is core, not optional,
   - no publisher backend server,
   - AI sends selected labels/metadata directly to configured provider,
   - AI settings may use browser `localStorage`,
   - use-at-your-own-risk wording,
   - System Administrator/System Customizer permission requirement.
3. Resolve or leave visible for anh Phước all screenshot placeholders that start with `[📷 HÌNH ẢNH`.
4. Render/export the `.docx` to PDF with the same versioned filename.
5. For a new version, copy the previous version folder first. `$pl-release-appsource` will rename `UserGuide.<old-version>.docx/pdf` to `UserGuide.<selected-version>.docx/pdf` if exact selected-version files do not exist.

Acceptance criteria:

- Review docx exists under `release\1.0.0.0\appsource\Documents\`.
- PDF opens.
- Screenshots are app-specific or screenshot placeholders are clearly left for anh Phước review.
- No text copied from Icons product except generic document structure.
- User Guide has no hidden claim that the app never uses internet, because Auto Translate calls an AI provider.

### Task 3.2 - Review E2E User Scenario document in selected release folder

Mục tiêu: dùng E2E User Scenario đã có trong selected release folder làm certification journey, review/chỉnh nếu cần, rồi render PDF cùng version.

Review document:

```text
D:\github\DataverseLabelTranslator\release\1.0.0.0\appsource\Test\E2E User Scenario.1.0.0.0.docx
```

Outputs:

```text
release\1.0.0.0\appsource\Test\E2E User Scenario.1.0.0.0.docx
release\1.0.0.0\appsource\Test\E2E User Scenario.1.0.0.0.pdf
```

Current source test cases already include:

1. `TEST00` regular/non-admin security requirement check.
2. `TEST01` manual attribute translation.
3. `TEST02` All-In-One bulk translation.
4. `TEST03` AI-assisted bulk translation.
5. `TEST04` Translation Dictionary/Glossary.
6. `TEST05` multi-language form label translation with automatic user language switching.

Actions:

1. Do not overwrite the review file blindly.
2. Review whether the E2E source also needs explicit steps for:
   - installing via the Package Deployer package,
   - opening the model-driven app after install,
   - verifying installed languages,
   - review proposal before save in AI flow,
   - cleanup/uninstall/dictionary data story.
3. Add missing steps to the review document.
4. Resolve or leave visible for anh Phước all screenshot placeholders that start with `[📷 HÌNH ẢNH`.
5. Render/export the `.docx` to PDF with the same versioned filename.
6. For a new version, copy the previous version folder first. `$pl-release-appsource` will rename `E2E User Scenario.<old-version>.docx/pdf` to `E2E User Scenario.<selected-version>.docx/pdf` if exact selected-version files do not exist.

Acceptance criteria:

- E2E PDF has precise step-by-step instructions.
- It includes AI as mandatory path.
- It states needed role: System Administrator or equivalent.
- It includes or explicitly references Package Deployer install path.
- It includes or explicitly references uninstall/cleanup and dictionary data behavior.
- Review docx exists under `release\1.0.0.0\appsource\Test\`.

### Task 3.3 - Create marketing PDF

Mục tiêu: listing supplemental/marketing asset.

Outputs:

```text
release\1.0.0.0\appsource\assets\marketing-onepager.pdf
```

Content:

- Product name.
- Free AppSource install.
- AI-assisted translation.
- Manual review before save.
- Translation dictionary.
- Supports Dataverse metadata labels.
- Admin/security/privacy summary.
- Support/help URL.

Acceptance criteria:

- PDF has product-specific screenshots.
- No unsupported claims.
- No paid/trial text.

## Phase 4 - Listing Assets

### Task 4.1 - Capture screenshots

Mục tiêu: anh Phước chụp screenshot thật từ app/environment thật để dùng cho AppSource listing và certification evidence. AI/tool không được tạo ảnh generated để thay thế các screenshot này.

Outputs:

```text
release\1.0.0.0\appsource\Images\screenshot-01-main-grid.png
release\1.0.0.0\appsource\Images\screenshot-02-ai-settings.png
release\1.0.0.0\appsource\Images\screenshot-03-auto-translate-review.png
release\1.0.0.0\appsource\Images\screenshot-04-dictionary.png
release\1.0.0.0\appsource\Images\screenshot-05-save-result.png
```

Requirements:

- 1280x720 PNG if used for marketplace listing.
- No secrets/API keys visible.
- Use a clean test environment.
- Use English UI if listing is English.

Acceptance criteria:

- Screenshots exist.
- Dimensions are correct.
- No credentials shown.

### Task 4.1b - Generate non-screenshot image assets

Mục tiêu: tạo các ảnh mà AI/tool có thể tạo hợp lệ, không giả làm screenshot sản phẩm thật.

Outputs:

```text
release\1.0.0.0\appsource\assets\logo32x32.png
release\1.0.0.0\appsource\assets\logo-large.png
release\1.0.0.0\appsource\assets\homepage-hero.png
release\1.0.0.0\appsource\assets\appsource-package-flow.png
release\1.0.0.0\appsource\assets\ai-privacy-flow.png
release\1.0.0.0\appsource\assets\install-wizard-visual.png
release\1.0.0.0\appsource\Videos\video-01-thumbnail.png
```

Wizard CSS/image assets generated inside nested Package Deployer package:

```text
PkgFolder\Content\en-us\WelcomeHtml\Images\body_back.gif
PkgFolder\Content\en-us\WelcomeHtml\Images\content_back.gif
PkgFolder\Content\en-us\WelcomeHtml\Images\content_back_orig.gif
PkgFolder\Content\en-us\WelcomeHtml\Images\contentarea_back.gif
PkgFolder\Content\en-us\WelcomeHtml\Images\contentArea_back_home.gif
PkgFolder\Content\en-us\WelcomeHtml\Images\footer_back.gif
PkgFolder\Content\en-us\WelcomeHtml\Images\header_back.gif
PkgFolder\Content\en-us\WelcomeHtml\Images\nav_back.gif
PkgFolder\Content\en-us\WelcomeHtml\Images\nav_list_back.gif
PkgFolder\Content\en-us\WelcomeHtml\Images\top_item_selected_bg.gif
PkgFolder\Content\en-us\EndHtml\Images\body_back.gif
PkgFolder\Content\en-us\EndHtml\Images\content_back.gif
PkgFolder\Content\en-us\EndHtml\Images\content_back_orig.gif
PkgFolder\Content\en-us\EndHtml\Images\contentarea_back.gif
PkgFolder\Content\en-us\EndHtml\Images\contentArea_back_home.gif
PkgFolder\Content\en-us\EndHtml\Images\footer_back.gif
PkgFolder\Content\en-us\EndHtml\Images\header_back.gif
PkgFolder\Content\en-us\EndHtml\Images\nav_back.gif
PkgFolder\Content\en-us\EndHtml\Images\nav_list_back.gif
PkgFolder\Content\en-us\EndHtml\Images\top_item_selected_bg.gif
```

Rules:

- Generated images may be used for homepage, diagrams, package flow, privacy explanation, logos, and optional video thumbnails.
- Generated images must not be submitted as product screenshots if Partner Center expects real app screenshots.
- Screenshots named `screenshot-01-*` through `screenshot-05-*` are owned by anh Phước and must come from the real app.
- `scripts\release-appsource.ps1` should regenerate these non-screenshot assets every release.

Acceptance criteria:

- Generated image files exist after `$pl-release-appsource`.
- `Images\README.md` clearly says real screenshots must be captured by anh Phước.
- No generated image contains secrets, SAS URLs, tenant IDs, or API keys.

### Task 4.2 - Create large logo

Mục tiêu: Partner Center listing needs larger logo assets.

Outputs:

```text
release\1.0.0.0\appsource\assets\logo-large.png
```

Actions:

1. Generate from `img\app-icon.svg`.
2. Verify dimensions required by Partner Center at submission time.
3. Store exact version in assets.

Acceptance criteria:

- Logo is PNG.
- Not blurry.
- Not just 32x32 upscaled.

### Task 4.3 - Draft listing text

Mục tiêu: Partner Center copy is ready and consistent.

Output:

```text
release\1.0.0.0\appsource\assets\partner-center-listing.md
```

Sections:

- Offer name.
- Summary.
- Description.
- Search keywords.
- Categories/products.
- Help URL.
- Privacy URL.
- Support URL.
- Certification notes.
- Screenshot captions.
- Supplemental PDF list.

Content rules:

- State free install.
- State AI-assisted translation as core feature.
- State external AI provider disclosure.
- State admin privileges required.
- Do not mention paid/trial/license-managed.

Acceptance criteria:

- File can be copied into Partner Center fields.
- No unresolved `TODO` except URLs/user-owned values.

### Task 4.4 - Create GitHub source repository and homepage plan

Mục tiêu: public source and homepage are ready for AppSource listing, support, and `input.xml` `LearnMoreLink`.

Fixed decisions:

- Source repository URL:

```text
https://github.com/phuocle/DataverseLabelTranslator
```

- GitHub Pages project site URL:

```text
https://phuocle.github.io/DataverseLabelTranslator/
```

Notes:

- GitHub Pages can publish a project site from a repository.
- Do **not** use this repo's `docs/` folder as the public Pages source, because `docs/` contains internal deployment/AppSource task docs.
- Use a separate public homepage folder, recommended:

```text
site/
```

- Publish `site/` to GitHub Pages using GitHub Actions, or manually configure GitHub Pages after pushing. GitHub Actions is preferred because it can deploy `site/` without exposing internal `docs/`.

Actions:

1. Create public homepage folder:

```text
site/
```

2. Create:

```text
site/index.html
site/assets/
site/assets/app-icon.svg
site/assets/style.css
```

3. Copy or reference `img/app-icon.svg` into `site/assets/app-icon.svg`.
4. Create GitHub Pages workflow:

```text
.github/workflows/pages.yml
```

5. Workflow must publish the `site/` folder only.
6. Add repository/homepage links to generated AppSource assets:

```text
release\1.0.0.0\appsource\assets\partner-center-listing.md
release\1.0.0.0\appsource\src\DataverseLabelTranslator.v.1.0.0\input.xml
```

7. Use homepage URL as `LearnMoreLink` in `input.xml`:

```text
https://phuocle.github.io/DataverseLabelTranslator/
```

8. Use GitHub repo URL in the homepage:

```text
https://github.com/phuocle/DataverseLabelTranslator
```

Homepage content requirements:

- Product name: `Dataverse Label Translator`.
- Clear AppSource/free install positioning.
- GitHub source link.
- AppSource link placeholder until offer is live.
- Main features:
  - AI-assisted translation,
  - manual review before save,
  - translation dictionary,
  - Dataverse metadata labels,
  - model-driven app,
  - no build step for web resources.
- Privacy summary:
  - no data/API key/telemetry sent to PhuocLe/publisher server,
  - Auto Translate sends selected labels/metadata directly to user-configured AI provider,
  - AI settings may be stored in browser `localStorage`,
  - use AI at your own risk.
- Install summary:
  - install from AppSource when live,
  - requires Dataverse/Dynamics 365 and System Administrator or equivalent privileges.
- Links:
  - GitHub repository,
  - README,
  - license,
  - terms,
  - privacy/support URL when available.

Acceptance criteria:

- `site/index.html` exists and opens locally.
- `site/index.html` contains GitHub repo link.
- `site/index.html` contains the free/no-publisher-server/AI-provider/localStorage risk summary.
- `.github/workflows/pages.yml` exists and deploys only `site/`.
- `partner-center-listing.md` includes homepage URL and GitHub repo URL.
- `input.xml` `LearnMoreLink` is set to `https://phuocle.github.io/DataverseLabelTranslator/` when homepage is ready.
- Internal files under `docs/` are not published as the homepage.

## Phase 5 - Privacy, AI, And Certification Inputs

### Task 5.1 - Decide certification AI provider

Mục tiêu: Microsoft reviewer can test AI path.

Needed decision from user:

- Azure Foundry endpoint controlled by publisher, or
- OpenAI-compatible endpoint controlled by publisher.

Actions after decision:

1. Create temporary endpoint/key/model for certification.
2. Set quota and expiry.
3. Store secret outside git.
4. Write certification notes with placeholder:

```text
AI certification provider details are supplied separately in Partner Center notes.
Endpoint: <provided in notes>
Model: <provided in notes>
API key: <provided in notes>
Expiry: <date>
```

Acceptance criteria:

- No secret in repo.
- E2E doc explains where reviewer enters settings.
- Certification notes include enough information for reviewer to test.

### Task 5.2 - Update privacy/support/help URLs

Mục tiêu: listing URLs are final.

Actions:

1. Decide whether to reuse existing domain/process from app Icons.
2. Create product-specific help page.
3. Create or update privacy policy with these exact principles:
   - The app does not send customer data, labels, metadata, API keys, telemetry, or usage data to a PhuocLe/Dataverse Label Translator publisher server.
   - The app has no publisher-controlled backend server for data collection.
   - When the user runs Auto Translate, selected labels/metadata may be sent directly from the user's browser/client to the AI provider endpoint configured by the user.
   - Provider endpoint/key/model are user supplied, except temporary certification test credentials supplied to Microsoft reviewer.
   - Browser `localStorage` may store AI settings/cache for convenience.
   - Users should treat values stored in `localStorage` as sensitive according to their own organization policy.
   - Dictionary data is stored in Dataverse/customer environment.
   - AI output quality, data handling, costs, quota and compliance depend on the user's selected provider. Use it at your own risk.
4. Create support page or support email workflow.

Acceptance criteria:

- URLs are live.
- URLs are HTTPS.
- URLs are not generic placeholders.
- Privacy policy does not contradict AI feature.
- Privacy policy does not imply data is sent to publisher server.

### Task 5.3 - Decide dictionary uninstall story

Mục tiêu: avoid certification ambiguity around customer-owned data.

Current behavior:

- Dictionary data is stored as `pl_/DataverseLabelTranslator/data/TranslationDictionary.xml`.
- Data solution/resource may be customer-owned and remain after uninstall.
Cần xác nhận rõ cơ chế lưu: nếu `TranslationDictionary.xml` được tạo như một Dataverse web resource nằm ngoài managed solution gốc do app tự tạo lúc runtime, nó có thể không bị xóa khi uninstall managed solution. Reviewer Microsoft có thể flag đây là residual data sau uninstall nếu không có hướng dẫn cleanup. Phải xác nhận bằng uninstall test trên clean environment trước khi submit.

Options:

1. Keep behavior and document cleanup steps.
2. Add cleanup command/UI.
3. Move dictionary storage to a managed component with clearer uninstall story.

Acceptance criteria:

- E2E doc states exact cleanup behavior.
- Certification notes mention it if data remains after uninstall.
- User Guide includes cleanup steps.

## Phase 6 - Validation Before Partner Center Upload

### Task 6.1 - Validate managed solution content

Mục tiêu: AppSource package must only include intended solution contents.

Actions:

1. Inspect:

```text
release\1.0.0.0\dataverse\unpack\Other\Solution.xml
```

2. Verify:

- Solution unique name `DataverseLabelTranslator`.
- Version `1.0.0.0`.
- Publisher prefix `pl`.
- Web resources under `pl_/DataverseLabelTranslator/`.
- App module `pl_DataverseLabelTranslator`.
- Sitemap `pl_DataverseLabelTranslator`.
- No PropertyEditor resources.

Acceptance criteria:

- No excluded resources.
- Version matches package version plan.

### Task 6.2 - Run Solution Checker

Mục tiêu: preempt AppSource certification failures.

Actions:

1. Run Power Apps Solution Checker against unmanaged/managed solution using available tooling.
2. Save report under:

```text
release\1.0.0.0\appsource\Validation\solution-checker\
```

3. Review high/critical issues.
4. Fix issues or document accepted risk.

Acceptance criteria:

- Report saved.
- No untriaged critical/high issues.

### Task 6.3 - Test Package Deployer install locally

Mục tiêu: verify nested package works before upload.

Actions:

1. Use a clean Dataverse environment.
2. Run Package Deployer with:

```text
release\1.0.0.0\appsource\src\DataverseLabelTranslator.v.1.0.0\DataverseLabelTranslatorPackage.zip
```

3. Save logs under:

```text
release\1.0.0.0\appsource\Validation\package-deployer\
```

4. Open installed model-driven app.
5. Run smoke tests:
   - load entity list,
   - load attributes,
   - save one harmless translation in test environment,
   - open AI settings,
   - configure test provider,
   - run Auto Translate on selected rows.

Acceptance criteria:

- Install succeeds.
- Smoke test succeeds.
- Logs saved.

### Task 6.4 - Validate final ZIP with script

Mục tiêu: final upload ZIP is structurally correct.

Actions:

1. Run validation command from `scripts/release-appsource.ps1`.
2. Print final ZIP entries.
3. Ensure root entries exactly:

```text
DataverseLabelTranslatorPackage.zip
[Content_Types].xml
input.xml
TermsOfUse.html
logo32x32.png
```

4. Validate nested ZIP entries.

Acceptance criteria:

- Validation passes.
- Ready-to-upload message printed.

## Phase 7 - Azure Blob And Partner Center

### Task 7.1 - Upload final ZIP to Azure Blob

Mục tiêu: create download URL for Partner Center.

Manual/user-controlled task unless user explicitly asks AI to run `$pl-deploy-azure`.

Input:

```text
release\1.0.0.0\appsource\zip\DataverseLabelTranslator.v.1.0.0.zip
```

Actions:

1. Run `$pl-deploy-azure` when the user explicitly asks for Azure upload.
2. Upload only this final ZIP.
3. Generate read-only SAS URL.
4. Expiry at least 1 month in future.
5. Save local/private copy to:

```text
release\1.0.0.0\appsource\zip\release.md
```

6. Do not commit `release.md` with SAS query string.

Acceptance criteria:

- URL downloads the ZIP.
- Partner Center can access it.

### Task 7.2 - Fill Partner Center offer

Mục tiêu: submit preview.

Actions:

1. Create offer under publisher `PhuocLe`.
2. Offer type: Dynamics 365 apps on Dataverse and Power Apps.
3. Offer ID: `dataverse-label-translator`.
4. Listing: `Get it now (free)`.
5. Upload/list final URLs/assets.
6. Technical configuration: CRM package SAS URL.
7. Supplemental content: E2E PDF.
8. Certification notes: include AI provider test instructions.

Acceptance criteria:

- All Partner Center pages show complete.
- Offer is submitted to preview/certification.

### Task 7.3 - Handle certification feedback

Mục tiêu: keep evidence and iterate cleanly.

Actions:

1. If Microsoft reports deploy/certification failure, create:

```text
release\1.0.0.0\appsource\DeployError\<date-or-case-id>\
```

2. Save:

- Microsoft feedback text,
- screenshots,
- Package Deployer logs,
- failed ZIP if relevant,
- fix notes.

3. Fix code/package/docs.
4. Re-run `$pl-release-appsource`.
5. Upload new final ZIP.
6. Resubmit.

Acceptance criteria:

- Every certification issue has traceable evidence and fix.

## Phase 8 - Definition Of Done

The AppSource release is ready for user upload when all are true:

- Existing managed solution exists at `release\1.0.0.0\dataverse\solutions\DataverseLabelTranslator_managed.zip`.
- Public source repository target is `https://github.com/phuocle/DataverseLabelTranslator`.
- GitHub Pages homepage target is `https://phuocle.github.io/DataverseLabelTranslator/`.
- `site/index.html` exists.
- `.github/workflows/pages.yml` exists and publishes `site/`.
- `$pl-release-appsource` skill exists.
- `scripts\release-appsource.ps1` exists and passes validation.
- Final ZIP exists:

```text
release\1.0.0.0\appsource\zip\DataverseLabelTranslator.v.1.0.0.zip
```

- Final ZIP root entries are:

```text
DataverseLabelTranslatorPackage.zip
[Content_Types].xml
input.xml
TermsOfUse.html
logo32x32.png
```

- Nested Package Deployer ZIP includes:

```text
[Content_Types].xml
PL.DataverseLabelTranslator.PackageDeployment.dll
PkgFolder\ImportConfig.xml
PkgFolder\DataverseLabelTranslator_managed.zip
PkgFolder\Content\en-us\WelcomeHtml\HTML\Default.htm
PkgFolder\Content\en-us\WelcomeHtml\CSS\common.css
PkgFolder\Content\en-us\WelcomeHtml\Images\
PkgFolder\Content\en-us\EndHtml\HTML\Default.htm
PkgFolder\Content\en-us\EndHtml\CSS\common.css
PkgFolder\Content\en-us\EndHtml\Images\
```

- `input.xml` references `DataverseLabelTranslatorPackage.zip`.
- `input.xml` references `DataverseLabelTranslator_managed.zip`.
- `input.xml` `LearnMoreLink` references `https://phuocle.github.io/DataverseLabelTranslator/`.
- `release\1.0.0.0\appsource\assets\license.md` exists.
- `release\1.0.0.0\appsource\assets\term.md` exists.
- Generated non-screenshot images exist under `release\1.0.0.0\appsource\assets\`: `logo32x32.png`, `logo-large.png`, `homepage-hero.png`, `appsource-package-flow.png`, `ai-privacy-flow.png`, `install-wizard-visual.png`.
- Real screenshot placeholders are documented under `release\1.0.0.0\appsource\Images\README.md`.
- `TermsOfUse.html` says free and discloses AI/external provider usage.
- `TermsOfUse.html` says the app does not send data to PhuocLe/publisher server.
- `TermsOfUse.html` says AI settings may be stored in browser `localStorage`.
- `TermsOfUse.html` includes use-at-your-own-risk wording for AI/provider usage.
- No generated artifact contains `Icons and Tooltips`.
- No SAS URL/API key is committed.
- User Guide review DOCX exists at `release\1.0.0.0\appsource\Documents\UserGuide.1.0.0.0.docx`.
- E2E Scenario review DOCX exists at `release\1.0.0.0\appsource\Test\E2E User Scenario.1.0.0.0.docx`.
- Staged User Guide PDF exists at `release\1.0.0.0\appsource\Documents\UserGuide.1.0.0.0.pdf`.
- Staged E2E Scenario PDF exists at `release\1.0.0.0\appsource\Test\E2E User Scenario.1.0.0.0.pdf` and includes mandatory AI path.
- Final response from AI says exactly which ZIP to upload.

## Phase 9 - Checklist Cho Anh Phước Review Trước Khi Upload/Submit

Mục tiêu: tách các việc AI có thể build/validate khỏi các quyết định và kiểm tra cuối cần anh Phước tự review. Không upload Azure Blob hoặc submit Partner Center trước khi checklist này được review.

Standalone checklist ngắn hơn cho anh Phước review nằm ở `docs/phuoc.review.md`.

### 9.1 - Review final ZIP artifact

- [ ] Xác nhận file final tồn tại:

```text
D:\github\DataverseLabelTranslator\release\1.0.0.0\appsource\zip\DataverseLabelTranslator.v.1.0.0.zip
```

- [ ] Mở ZIP và kiểm tra root có đúng 5 file:

```text
DataverseLabelTranslatorPackage.zip
[Content_Types].xml
input.xml
TermsOfUse.html
logo32x32.png
```

- [ ] Mở nested `DataverseLabelTranslatorPackage.zip` và kiểm tra có `PkgFolder`.
- [ ] Kiểm tra `PkgFolder\ImportConfig.xml` trỏ đúng `DataverseLabelTranslator_managed.zip`.
- [ ] Kiểm tra wizard install page trong `PkgFolder\Content\en-us\WelcomeHtml\HTML\Default.htm`.
- [ ] Kiểm tra wizard finish page trong `PkgFolder\Content\en-us\EndHtml\HTML\Default.htm`.
- [ ] Kiểm tra wizard pages không còn text `Template Package Title Here`.
- [ ] Kiểm tra wizard pages không còn text `Icons and Tooltips`.

### 9.2 - Review managed solution source

- [ ] Xác nhận file managed solution do anh Phước control là latest:

```text
D:\github\DataverseLabelTranslator\release\1.0.0.0\dataverse\solutions\DataverseLabelTranslator_managed.zip
```

- [ ] Xác nhận không cần export lại solution.
- [ ] Xác nhận version `1.0.0.0` là version muốn submit.
- [ ] Xác nhận solution không chứa PropertyEditor resources.
- [ ] Xác nhận model-driven app `Dataverse Label Translator` mở được sau install.

### 9.3 - Review listing decisions

- [ ] Publisher/account dùng lại account cũ: `PhuocLe`.
- [ ] Publisher ID: `phuocle`.
- [ ] Offer ID: `dataverse-label-translator`.
- [ ] Listing option: `Get it now (free)`.
- [ ] Không bật paid plan.
- [ ] Không bật ISV app license management.
- [ ] Không dùng `Contact me`.
- [ ] Source repo URL đúng: `https://github.com/phuocle/DataverseLabelTranslator`.
- [ ] Homepage URL đúng: `https://phuocle.github.io/DataverseLabelTranslator/`.
- [ ] GitHub Pages đã publish từ `site/`, không publish folder `docs/` nội bộ.
- [ ] Homepage mở được ngoài browser/incognito.
- [ ] Homepage có link GitHub source.
- [ ] Homepage có privacy summary: không gửi data về server publisher, AI gửi trực tiếp tới provider do user cấu hình, `localStorage`, use at your own risk.
- [ ] Chọn applicable products, tối thiểu `Power Apps`.
- [ ] Chọn markets muốn publish.
- [ ] Chọn preview audience/hide key.

### 9.4 - Review AI certification path

- [ ] Chọn provider để Microsoft reviewer test AI: Azure Foundry hoặc OpenAI-compatible endpoint riêng.
- [ ] Tạo endpoint/model/API key test tạm thời.
- [ ] Set quota và expiry cho key test.
- [ ] Đảm bảo key test không nằm trong git.
- [ ] Đảm bảo certification notes có hướng dẫn nhập AI Settings.
- [ ] Test Auto Translate bằng key test trên clean/test environment.
- [ ] Review privacy wording: label/metadata text có thể được gửi tới provider đã cấu hình.
- [ ] Review TermsOfUse có nói rõ AI/external provider usage.

### 9.5 - Review screenshots và media

- [ ] Chụp screenshot main translation grid.
- [ ] Chụp screenshot AI Settings, nhưng che toàn bộ API key/secret.
- [ ] Chụp screenshot Auto Translate proposal/review.
- [ ] Chụp screenshot Dictionary management.
- [ ] Chụp screenshot Save/result hoặc publish success.
- [ ] Kiểm tra screenshots có đúng kích thước Partner Center yêu cầu, ưu tiên 1280 x 720 PNG.
- [ ] Kiểm tra screenshots không lộ environment URL nội bộ nếu không muốn public.
- [ ] Kiểm tra screenshots không lộ user email, tenant info, API key, SAS URL.
- [ ] Review video demo nếu dùng, đảm bảo không lộ secrets.
- [ ] Review logo `logo32x32.png` nhìn rõ ở 32 x 32.
- [ ] Review large logo/listing logo không bị mờ.
- [ ] Review generated homepage hero `assets\homepage-hero.png`.
- [ ] Review generated package flow diagram `assets\appsource-package-flow.png`.
- [ ] Review generated AI privacy flow diagram `assets\ai-privacy-flow.png`.
- [ ] Review generated wizard visual `assets\install-wizard-visual.png`.
- [ ] Confirm các ảnh generated này không được dùng để thay screenshot thật của app.

### 9.6 - Review documents

- [ ] Review User Guide document `D:\github\DataverseLabelTranslator\release\1.0.0.0\appsource\Documents\UserGuide.1.0.0.0.docx`.
- [ ] Review E2E Scenario document `D:\github\DataverseLabelTranslator\release\1.0.0.0\appsource\Test\E2E User Scenario.1.0.0.0.docx`.
- [ ] Review staged PDF `release\1.0.0.0\appsource\Documents\UserGuide.1.0.0.0.pdf`.
- [ ] Review staged PDF `release\1.0.0.0\appsource\Test\E2E User Scenario.1.0.0.0.pdf`.
- [ ] Review all `[📷 HÌNH ẢNH...]` placeholders and replace with real screenshots before final Partner Center submission if needed.
- [ ] E2E doc có bước install package.
- [ ] E2E doc có bước mở model-driven app.
- [ ] E2E doc có bước configure AI provider.
- [ ] E2E doc có bước Auto Translate bắt buộc.
- [ ] E2E doc có bước review proposal trước khi save.
- [ ] E2E doc có bước dictionary.
- [ ] E2E doc có bước uninstall/cleanup.
- [ ] User Guide có nói role cần thiết: System Administrator hoặc quyền tương đương.
- [ ] User Guide có nói dictionary data cleanup/story.

### 9.7 - Review legal/privacy/support URLs

- [ ] Help URL live và đúng app Dataverse Label Translator.
- [ ] `input.xml` `LearnMoreLink` dùng homepage `https://phuocle.github.io/DataverseLabelTranslator/`.
- [ ] Support URL/email live và thuộc quyền kiểm soát của anh Phước.
- [ ] Privacy policy URL live và có AI/external provider disclosure.
- [ ] Terms URL hoặc Partner Center terms text khớp với `TermsOfUse.html`.
- [ ] Review `assets\license.md`.
- [ ] Review `assets\term.md`.
- [ ] `license.md` nói rõ app free, không trial, không paid license, deploy/install bao nhiêu environment cũng được.
- [ ] `term.md` nói rõ app không gửi data/API key/telemetry về server của PhuocLe/publisher.
- [ ] `term.md` nói rõ Auto Translate gửi selected label/metadata trực tiếp tới AI provider do user cấu hình.
- [ ] `term.md` nói rõ AI settings có thể lưu trong browser `localStorage` để tiện sử dụng.
- [ ] `term.md` có câu `Use it at your own risk` hoặc wording tương đương.
- [ ] Không còn text trial/paid/purchase license từ app Icons.
- [ ] Không còn claim “không truy cập internet” vì app này có AI provider calls.
- [ ] Support/contact information đúng người/team sẽ xử lý AppSource customer.

### 9.8 - Review Partner Center listing text

- [ ] Offer name: `Dataverse Label Translator`.
- [ ] Summary nói rõ free AI-assisted Dataverse label translation.
- [ ] Description nói rõ manual review before save.
- [ ] Description nói rõ supported metadata areas.
- [ ] Description nói rõ required permissions/admin role.
- [ ] Keywords phù hợp: Dataverse, translation, localization, labels, metadata, AI.
- [ ] Screenshot captions đúng từng hình.
- [ ] Marketing PDF được upload nếu dùng.
- [ ] Supplemental E2E PDF được upload.
- [ ] Certification notes có AI provider test instructions.

### 9.9 - Review validation evidence

- [ ] Solution Checker report đã được review.
- [ ] Không còn critical/high issue chưa xử lý.
- [ ] Package Deployer install test pass trên clean/test environment.
- [ ] Smoke test load entity/attribute pass.
- [ ] Smoke test save translation pass trên test environment.
- [ ] Smoke test AI Auto Translate pass.
- [ ] Smoke test Dictionary pass.
- [ ] Uninstall test pass hoặc cleanup note đã rõ.
- [ ] Logs/test evidence được lưu dưới `release\1.0.0.0\appsource\Validation\` hoặc `DeployError\` nếu có lỗi.

### 9.10 - Review Azure upload

- [ ] Chỉ upload final all-in-one ZIP:

```text
D:\github\DataverseLabelTranslator\release\1.0.0.0\appsource\zip\DataverseLabelTranslator.v.1.0.0.zip
```

- [ ] Không upload `DataverseLabelTranslator_managed.zip`.
- [ ] Không upload nested `DataverseLabelTranslatorPackage.zip`.
- [ ] SAS URL là read-only.
- [ ] SAS expiry còn ít nhất 1 tháng trong tương lai.
- [ ] Test SAS URL download thành công trong browser/incognito.
- [ ] Lưu `release.md` local nếu cần.
- [ ] Không commit `release.md` có SAS query string.

### 9.11 - Final go/no-go

- [ ] Anh Phước đã review final ZIP.
- [ ] Anh Phước đã review screenshots/media.
- [ ] Anh Phước đã review User Guide.
- [ ] Anh Phước đã review E2E Scenario.
- [ ] Anh Phước đã review Terms/Privacy/Support.
- [ ] Anh Phước đã review Partner Center listing text.
- [ ] Anh Phước đã review AI certification credentials/process.
- [ ] Anh Phước đã upload final ZIP lên Azure Blob.
- [ ] Anh Phước đã paste SAS URL vào Partner Center technical configuration.
- [ ] Anh Phước đã submit preview/certification.
