# Dataverse Label Translator - Kế hoạch triển khai AppSource / Microsoft Marketplace

Ngày 2026-05-27. Tài liệu này tổng hợp luồng publish lên AppSource/Microsoft Marketplace cho repo `DataverseLabelTranslator`, dựa trên tài liệu Microsoft hiện hành và trạng thái release hiện tại của project.

## Kết luận ngắn

App này phù hợp nhất với offer type **Dynamics 365 apps on Dataverse and Power Apps** trong Partner Center, vì sản phẩm là một Dataverse managed solution gồm model-driven app, app sitemap và web resources. Tên "AppSource" vẫn xuất hiện trong Power Platform docs, nhưng luồng submit hiện tại đi qua **Microsoft Marketplace / Partner Center**.

Repo hiện đã có release solution:

- Managed solution: `release/1.0.0.0/dataverse/solutions/DataverseLabelTranslator_managed.zip`
- Unmanaged solution: `release/1.0.0.0/dataverse/solutions/DataverseLabelTranslator.zip`
- Solution unique name: `DataverseLabelTranslator`
- Version: `1.0.0.0`
- Publisher prefix: `pl`
- Main app module và sitemap: `pl_DataverseLabelTranslator`
- Web resources: `pl_/DataverseLabelTranslator/...`

Những file trên **chưa đủ để submit AppSource**. Microsoft yêu cầu tạo **Marketplace package** bọc managed solution bằng Package Deployer package, kèm `input.xml`, license terms HTML, icon PNG và content-type metadata. Kết quả cuối cùng phải là **một file ZIP all-in-one** chứa toàn bộ các artifact Marketplace package; file này mới là file pick để upload lên Azure Blob Storage và lấy SAS URL cho Partner Center.

Versioning rule cho `Release AppSource`: nếu user mention version rõ, ví dụ `1.1.0.0`, workflow dùng đúng `release\1.1.0.0\dataverse\solutions\DataverseLabelTranslator_managed.zip` và tạo output mới dưới `release\1.1.0.0\appsource\`. Nếu user không mention version, script tự chọn latest bằng cách scan `release\<version>\dataverse\solutions\DataverseLabelTranslator_managed.zip` và lấy version số lớn nhất. Nếu chưa có release folder nào, fallback là `1.0.0.0`. Mỗi version là một folder self-contained dưới `release\<version>`; version folder cũ là read-only theo quy ước release, nên build version mới không được sửa/xóa `release\1.0.0.0` hoặc bất kỳ version cũ nào.

Quyết định đã chốt: publish dạng **Get it now (free)**. Không dùng `Contact me`, không bán paid plan, không bật ISV app license management trong giai đoạn AppSource này. App sẽ được định vị là free admin utility; mọi nội dung listing, package, certification notes và support flow phải nhất quán với hướng này.

Baseline đã có: dùng lại Partner Center account/publisher hiện có của bạn. Publisher `PhuocLe` đã có offer public/certified trên Microsoft Marketplace là [Icons and Tooltips with D365](https://marketplace.microsoft.com/en-us/product/dynamics365/phuocle.d365-icons-and-tooltips), và bạn đã có trạng thái ISV/certification thành công. Điều này thay đổi trọng tâm kế hoạch: không cần coi Partner Center enrollment, publisher verification hay việc hiểu certification flow là blocker ban đầu nữa. Cần tái sử dụng playbook, contact profile, support/legal URLs, package/certification notes pattern và kinh nghiệm xử lý certification từ offer đã thành công đó. Dataverse Label Translator đã chốt 100% là **Get it now (free)**; không có nhánh paid/contact/license-managed trong kế hoạch này.

Tham chiếu local đã đọc: `D:\azure\phuocle\d365icons\D365Icons\src2\AppSource`. Folder này cho thấy pattern đã pass certification cho app Icons: mỗi version có `src/` chứa package source, `zip/` chứa final upload ZIP và `url.txt`, `Documents/` chứa User Guide, `Test/` chứa E2E scenario PDF/DOCX và screenshots, `Videos/`/`Images/` cho listing media, và `DeployError/` lưu log/screenshot khi certification/deployment lỗi. Kế hoạch bên dưới đã được chỉnh theo pattern thực tế đó.

Tài liệu AppSource của app này được giữ trực tiếp trong release folder theo version, không dùng folder `appsource\Documents` riêng nữa. Với version hiện tại, source/review documents nằm ở:

- `D:\github\DataverseLabelTranslator\release\1.0.0.0\appsource\Documents\UserGuide.1.0.0.0.docx`
- `D:\github\DataverseLabelTranslator\release\1.0.0.0\appsource\Test\E2E User Scenario.1.0.0.0.docx`

Khi tạo version mới, ví dụ `1.0.1.0`, copy nguyên folder `release\1.0.0.0` thành `release\1.0.1.0`, thay managed solution trong `release\1.0.1.0\dataverse\solutions\`, rồi chạy `Release AppSource -SolutionVersion 1.0.1.0`. Script chỉ clean `appsource\src` và `appsource\zip` của selected version, giữ lại `appsource\Documents`, `appsource\Test`, `appsource\Images`, `appsource\Videos`, `appsource\assets`, và tự đổi tên các file review DOCX/PDF versioned trong selected folder nếu chưa có file đúng version.

## Nguồn Microsoft đã đối chiếu

- [Icons and Tooltips with D365 - existing public marketplace offer](https://marketplace.microsoft.com/en-us/product/dynamics365/phuocle.d365-icons-and-tooltips)
- [Publish your app on Marketplace](https://learn.microsoft.com/en-us/power-platform/developer/marketplace/publish-app)
- [Plan a Microsoft Dynamics 365 offer](https://learn.microsoft.com/en-us/partner-center/marketplace-offers/marketplace-dynamics-365)
- [Create a Dynamics 365 apps on Dataverse and Power Apps offer](https://learn.microsoft.com/en-us/partner-center/marketplace-offers/dynamics-365-customer-engage-offer-setup)
- [Create a managed solution for your app](https://learn.microsoft.com/en-us/power-platform/developer/marketplace/create-solution-app)
- [Create a Marketplace package for your app](https://learn.microsoft.com/en-us/power-platform/developer/marketplace/create-package-app)
- [Store your Marketplace Package on Azure Storage and generate a URL with SAS key](https://learn.microsoft.com/en-us/power-platform/developer/marketplace/store-package-azure-storage)
- [Offer properties](https://learn.microsoft.com/en-us/partner-center/marketplace-offers/dynamics-365-customer-engage-properties)
- [Offer listing details](https://learn.microsoft.com/en-us/partner-center/marketplace-offers/dynamics-365-customer-engage-offer-listing)
- [Offer availability](https://learn.microsoft.com/en-us/partner-center/marketplace-offers/dynamics-365-customer-engage-availability)
- [Offer technical configuration](https://learn.microsoft.com/en-us/partner-center/marketplace-offers/dynamics-365-customer-engage-technical-configuration)
- [App certification checklist](https://learn.microsoft.com/en-us/power-platform/developer/marketplace/appendix-app-certification-checklist)
- [Microsoft Marketplace certification policies](https://learn.microsoft.com/en-us/legal/marketplace/certification-policies)
- [Review and publish a Dynamics 365 offer](https://learn.microsoft.com/en-us/partner-center/marketplace-offers/dynamics-365-review-publish)
- [ISV app license management](https://learn.microsoft.com/en-us/partner-center/marketplace-offers/isv-app-license)
- [Create packages for the Package Deployer tool](https://learn.microsoft.com/en-us/power-platform/alm/package-deployer-tool)

## Giai đoạn 0 - Quyết định offer và phạm vi

1. Chọn offer type trong Partner Center: **Dynamics 365 apps on Dataverse and Power Apps**.
2. Listing option đã chốt: **Get it now (free)**.
3. Các hướng `Contact me`, `Buy now`, `Get it now with license management`, paid plan, trial conversion và marketplace purchase test đều nằm ngoài phạm vi kế hoạch này.
4. Chọn applicable products: tối thiểu nên có **Power Apps**; có thể thêm Sales/Customer Service/Field Service nếu marketing định vị rõ app dùng cho các Dynamics 365 app đó.
5. AI-assisted translation là **core feature** của listing, không ghi như optional/advanced feature. App cần được định vị là công cụ dịch label/metadata có AI-assisted translation, dictionary và manual review trước khi save. Trong Partner Center nên đánh giá chọn category/tag **AI Apps and Agents** nếu form offer cho phép, vì AI là một phần chính của value proposition. Khi tạo offer thật, cần verify taxonomy thực tế trên tab Properties vì offer type "Dynamics 365 apps on Dataverse and Power Apps" có thể không hiện cùng category như Azure Marketplace.
6. Vì AI là core feature, certification package phải làm cho reviewer test được AI path:
   - Cung cấp rõ provider được dùng để test certification, ưu tiên Azure Foundry hoặc OpenAI-compatible endpoint do publisher kiểm soát.
   - Cung cấp endpoint/model/API key test ngắn hạn trong **Notes for certification** hoặc kênh Microsoft yêu cầu, không hard-code trong solution.
   - Giới hạn quota/expiry cho key test và ghi rõ label text sẽ được gửi tới external AI endpoint khi reviewer dùng Auto Translate.
   - E2E functional document phải có bước Auto Translate bắt buộc: configure provider -> load labels -> auto translate selected records -> review proposal -> save.
7. Tạo offer dưới cùng publisher/account cũ đã certified: display name `PhuocLe`, publisher ID theo marketplace URL hiện hữu là `phuocle`. Offer ID dùng cho kế hoạch này: `dataverse-label-translator`, để URL marketplace có dạng `phuocle.dataverse-label-translator` và vẫn dưới giới hạn độ dài của Partner Center.

## Giai đoạn 1 - Partner Center readiness

Trạng thái sau cập nhật: phần lớn readiness cấp publisher đã có vì bạn đã từng publish/certify thành công offer `phuocle.d365-icons-and-tooltips`.

Đã có hoặc nên coi là đã có baseline:

- Microsoft Marketplace account trong Partner Center, đã enrolled vào Microsoft Marketplace program.
- Work account của công ty/tổ chức; personal account không dùng cho Partner Center enrollment.
- Publisher account đã verify và có quyền publish offer.
- Người có authority chấp nhận legal agreements.
- Support/engineering contacts đã có pattern từ offer trước.
- Certification workflow, preview review, package feedback loop và resubmission discipline đã có kinh nghiệm thực tế.

Quyết định cố định cho offer mới:

- Dùng cùng Partner Center account/publisher đã publish app Icons.
- Publisher/display name: `PhuocLe`.
- Publisher ID: `phuocle`.
- Offer ID: `dataverse-label-translator`. Offer ID chỉ dùng lowercase letters/numbers, có thể có `-` và `_`, không có space; tổng Publisher ID + Offer ID tối đa 40 ký tự; không đổi được sau khi tạo.
- Offer alias trong Partner Center, ví dụ `Dataverse Label Translator`.
- Listing mode: **Get it now (free)**.
- Không cần pricing page, paid plan, trial conversion flow, marketplace purchase testing hoặc ISV app license management cho submission này.
- Market availability và preview hide key.
- Tax/payout/transactable setup không nằm trong critical path vì offer này free. Không cần lặp lại paid commerce setup của offer khác nếu có.

## Giai đoạn 2 - Product readiness trong solution

Microsoft yêu cầu managed solution cho app. Repo đã có managed solution `release/1.0.0.0/dataverse/solutions/DataverseLabelTranslator_managed.zip`. Trong workflow `Release AppSource`, file managed ZIP trong `release\<version>\dataverse\solutions\` là source of truth do anh Phước/AP kiểm soát; không chạy export lại và không kiểm freshness với Dataverse. Khi muốn release version mới, ví dụ `1.1.0.0`, trước tiên copy folder version cũ thành `release\1.1.0.0`, thay managed solution mới vào `release\1.1.0.0\dataverse\solutions\DataverseLabelTranslator_managed.zip`, sau đó chạy `Release AppSource` cho version đó.

Checklist riêng cho project này:

- Xác nhận solution chỉ gồm dashboard translation, không gồm PropertyEditor resources.
- Xác nhận version tăng mỗi khi submit update. Release hiện tại là `1.0.0.0`; mỗi update sau khi publish phải increment.
- Xác nhận model-driven app chạy trên UCI và Dataverse/Dynamics 365 v9.1+.
- Xác nhận app chỉ dùng public APIs. Các handler hiện gọi Dataverse Web API/metadata endpoints; cần kiểm tra lại không có unsupported/private endpoint.
- Chạy Power Apps Solution Checker trên unmanaged solution trước khi export managed. Microsoft certification sẽ dùng Power Apps Checker cho Dataverse solution.
- Test import managed solution vào clean environment, mở app `Dataverse Label Translator`, load/save từng nhóm chức năng chính.
- Test uninstall managed solution và verify các web resources/app module/app sitemap của solution bị remove.

Rủi ro cần xử lý trước submission:

- **AI credentials**: API key/model settings hiện lưu browser `localStorage` để tiện sử dụng. Listing/privacy/E2E/terms phải nói rõ credentials do user nhập, nằm ở browser/client của user, không hard-code trong solution, và app dùng credentials đó để gọi AI provider khi chạy Auto Translate. Vì AI là core feature, cần chuẩn bị test credentials riêng cho certification.
- **No publisher server data collection**: app không gửi customer data, labels, metadata, API keys, telemetry hoặc usage data về server của PhuocLe/Dataverse Label Translator. App không có publisher-controlled backend server để collect data.
- **External AI transfer**: Auto Translate gửi selected label/metadata text trực tiếp từ browser/client của user tới Google/OpenAI-compatible/Azure Foundry endpoint đã được user cấu hình. Cần disclose rõ trong privacy policy, E2E doc, terms và listing. User chịu trách nhiệm với AI provider đã chọn, bao gồm quality, privacy, security, cost, quota và compliance. Dùng AI với tinh thần **Use it at your own risk**.
- **Dictionary storage**: app tự tạo solution/data web resource `DataverseLabelTranslatorData` khi dùng dictionary. Đây là data customer-owned và có thể còn lại sau khi uninstall app nếu user đã tạo dictionary. Nên thêm hướng dẫn cleanup trong E2E/admin doc, hoặc cân nhắc chuyển sang component managed/data table có uninstall story rõ hơn.
- **Admin permission**: app sửa metadata/publish customization nên cần System Administrator hoặc quyền tương đương. Listing và E2E functional doc phải nói rõ.
- **Content Snippets**: chỉ hỗ trợ legacy Dynamics 365 Portals/Power Pages bằng `adx_contentsnippet`. Nên ghi rõ optional/conditional để reviewer không fail nếu test environment không có portal tables.

## Giai đoạn 3 - Tài sản listing/legal/support

Partner Center yêu cầu hoặc nên có:

- Offer name: `Dataverse Label Translator`.
- Publisher/display name: dùng `PhuocLe`, cùng publisher đã pass certification với offer trước.
- Short summary và description. Description phải nêu full product name; policy cho Dynamics 365 apps on Dataverse and Power Apps yêu cầu plain text hoặc simple HTML.
- Help URL: có thể reuse domain/process từ `Icons and Tooltips with D365`, nhưng nội dung phải riêng cho Dataverse Label Translator.
- Source repository URL: `https://github.com/phuocle/DataverseLabelTranslator`.
- Public homepage: dùng GitHub Pages project site `https://phuocle.github.io/DataverseLabelTranslator/` sau khi source được push lên GitHub. Không publish folder `docs/` vì folder này chứa tài liệu deploy nội bộ; tạo homepage riêng trong `site/` và publish bằng GitHub Actions.
- Privacy policy URL: có thể reuse policy framework đã được duyệt, nhưng phải bổ sung rõ app không gửi data về server publisher, và chỉ gửi selected label/metadata trực tiếp tới AI provider do user cấu hình khi Auto Translate. Privacy wording cũng phải nói AI settings có thể lưu trong browser `localStorage`.
- Legal/terms source files: tạo `license.md` và `term.md` trong AppSource assets để anh Phước review trước khi generate HTML/package terms. Hai file markdown này là source/review artifacts; final Marketplace ZIP root vẫn giữ đúng cấu trúc Microsoft với `TermsOfUse.html`.
- Terms and conditions: URL hoặc text trong Partner Center; cũng cần HTML license terms trong Marketplace package. Có thể reuse layout/template HTML của offer trước, nhưng **không copy nguyên nội dung** vì terms cũ nói trial/paid và không truy cập internet; Dataverse Label Translator là free, cho phép deploy/install/use nhiều environment, không gửi data về server publisher, và có gọi external AI provider do user cấu hình khi Auto Translate.
- Support contact: reuse contact đã pass certification nếu vẫn đúng SLA/owner.
- Engineering contact: reuse contact đã pass certification.
- Ít nhất 1 marketing PDF, tối đa 3 PDF.
- Logo PNG: Partner Center cần large PNG; Marketplace package cần thêm icon 32x32 PNG. Repo hiện chỉ có `img/app-icon.svg`, nên cần generate PNG assets.
- Screenshots: ít nhất 1, tối đa 5, kích thước 1280 x 720 PNG, có caption.
- Optional video demo: tối đa 4 video URL + thumbnail PNG 1280 x 720. Có thể reuse cách tổ chức `Videos/` từ app Icons, nhưng video phải quay workflow dịch label/AI/dictionary của app mới.
- Supplemental content PDF: key usage scenarios/E2E functional journey cho certification team.

Phân loại ảnh cần làm:

- AI/tool có thể tạo các asset không phải screenshot thật: `logo32x32.png`, `logo-large.png`, homepage hero, AppSource package flow, AI privacy flow, Package Deployer wizard visual, video thumbnail và các GIF trang trí cho wizard CSS.
- Anh Phước phải tự chụp screenshot thật từ environment sạch: main translation grid, AI Settings, Auto Translate proposal/review, Dictionary, Save/result, Package Deployer install nếu dùng làm evidence. Không dùng ảnh generated để giả lập product screenshot.
- Screenshot đưa lên Partner Center nên là PNG 1280 x 720, không lộ API key, SAS URL, tenant/internal URL, user email hoặc secret.

Nội dung E2E functional document nên gồm:

- Admin journey: install package, open model-driven app, required roles.
- User journey: select Solution -> Entity -> Type -> Load -> edit labels -> Save.
- Test cases cho Attributes, Option Sets, Forms, Views, Entity Metadata, Relationships, Sitemap, Dashboards/Web Resources/Global Option Sets.
- AI journey bắt buộc: configure provider, translate a small selected set, review proposal, then save.
- Dictionary journey: create entry, apply dictionary, cleanup data storage.
- Uninstall/cleanup notes.
- Known environment requirements: installed languages, Dataverse/Dynamics 365 version, System Administrator role.

## Giai đoạn 4 - Tạo Marketplace package

Microsoft không nhận trực tiếp `DataverseLabelTranslator_managed.zip` trên tab technical configuration. Cần tạo package theo cấu trúc Marketplace.

Đề xuất thư mục build trong repo. Toàn bộ artifact của một version nằm trong `release/<version>/`; khi có version mới thì copy cả folder version cũ rồi đổi tên:

```text
release/1.0.0.0/
  dataverse/
    solutions/
      DataverseLabelTranslator.zip
      DataverseLabelTranslator_managed.zip
    unpack/
  appsource/
    src/
      DataverseLabelTranslatorPackage/
        [Content_Types].xml
        PL.DataverseLabelTranslator.PackageDeployment.dll
        PkgFolder/
          ImportConfig.xml
          DataverseLabelTranslator_managed.zip
          Content/
            en-us/
              WelcomeHtml/
              EndHtml/
      DataverseLabelTranslator.v.1.0.0/
        [Content_Types].xml
        input.xml
        TermsOfUse.html
        logo32x32.png
        DataverseLabelTranslatorPackage.zip
    zip/
      DataverseLabelTranslator.v.1.0.0.zip   <- FINAL UPLOAD ARTIFACT
      url.txt                                <- SAS URL record, keep private
    Documents/
      UserGuide.1.0.0.0.docx
      UserGuide.1.0.0.0.pdf
    Test/
      E2E User Scenario.1.0.0.0.docx
      E2E User Scenario.1.0.0.0.pdf
      screenshots...
    Images/
    Videos/
    DeployError/
```

Package Deployer package:

1. Tạo Package Deployer project bằng Power Platform CLI hoặc Visual Studio/Power Platform Tools.
2. Add `release/1.0.0.0/dataverse/solutions/DataverseLabelTranslator_managed.zip` vào package.
3. Tạo `PkgFolder/ImportConfig.xml` trỏ đúng tới `DataverseLabelTranslator_managed.zip`.
4. Thêm `PkgFolder\Content\en-us\WelcomeHtml` và `PkgFolder\Content\en-us\EndHtml` cho Package Deployer wizard install UI, giống pattern app Icons. Đây là màn hình user/reviewer thấy khi install package, không phải web resource của Dataverse app.
5. Không thêm Package Deployer custom code nếu không cần, để giảm surface security review.
6. Build package và verify package có thể deploy vào clean Dataverse environment.
7. Tạo nested `DataverseLabelTranslatorPackage.zip` theo yêu cầu Marketplace: bên trong có `PL.DataverseLabelTranslator.PackageDeployment.dll`, `PkgFolder/`, managed solution, wizard welcome/end content và `[Content_Types].xml`.

Marketplace package staging root:

- `DataverseLabelTranslatorPackage.zip`: Package Deployer package.
- `[Content_Types].xml`: MIME type metadata cho package root.
- `logo32x32.png`: icon 32x32 PNG/JPG.
- `TermsOfUse.html`: license terms HTML.
- `input.xml`: mô tả asset trong package. App Icons đã pass với tên lowercase `input.xml`; dùng lại convention này để giảm biến số.

Final upload artifact:

- `release/<solution-version>/appsource/zip/DataverseLabelTranslator.v.<package-version>.zip`
- Đây là **file ZIP all-in-one duy nhất** cần upload lên Azure Blob Storage.
- File này phải chứa trực tiếp các file `DataverseLabelTranslatorPackage.zip`, `[Content_Types].xml`, `input.xml`, `logo32x32.png`, `TermsOfUse.html` ở root của ZIP. Không để thêm một folder cha như `DataverseLabelTranslator.v.1.0.0/` bên trong ZIP, vì Microsoft validate package structure rất chặt.
- `DataverseLabelTranslatorPackage.zip` bên trong final ZIP là artifact trung gian Package Deployer, không phải file upload trực tiếp lên Azure Blob.
- Lưu SAS URL sau upload vào `release/<solution-version>/appsource/zip/url.txt` để tracking giống app Icons, nhưng không commit file này nếu nó chứa query string SAS thật.

`input.xml` cần có những trường chính:

- `ProviderName`: dùng `PhuocLe`, cùng provider/publisher name đã pass certification, trừ khi Partner Center/legal profile bắt buộc hiển thị tên pháp lý khác trong package.
- `PackageFile`: `DataverseLabelTranslatorPackage.zip`.
- `SolutionAnchorName`: tên managed solution zip bên trong Package Deployer package, ví dụ `DataverseLabelTranslator_managed.zip`.
- `StartDate` / `EndDate`: format `MM/DD/YYYY`.
- `SupportedCountries`: danh sách country/region code, không chèn space/newline.
- `LearnMoreLink`: dùng homepage `https://phuocle.github.io/DataverseLabelTranslator/` sau khi GitHub Pages live.
- `Locales`: ít nhất `PackageLocale Code="1033" IsDefault="true"` nếu chỉ submit English.
- Logo và terms file cho từng locale.

Lưu ý: Microsoft nhấn mạnh package structure phải đúng chính xác, sai tên file hoặc nested zip sai cấp có thể fail certification.

Các convention rút ra từ package AppSource cũ đã pass:

- Version folder dùng full solution version, ví dụ `1.3.3.0`; final upload ZIP có thể dùng marketing/package version ngắn hơn, ví dụ `D365IconsAndTooltips.v.1.3.3.zip`. Với app này dùng `1.0.0.0/zip/DataverseLabelTranslator.v.1.0.0.zip`.
- Marketplace root dùng `input.xml` lowercase. Dùng lại convention này thay vì đổi qua `Input.xml`.
- `input.xml` cũ đã pass có cấu trúc: `ProviderName`, `PackageFile`, `SolutionAnchorName`, `StartDate`, `EndDate`, `SupportedCountries`, `LearnMoreLink`, `Locales`, `Logo`, `Terms`.
- `[Content_Types].xml` trong cả Marketplace root và Package Deployer package dùng content type `application/octet-stream` cho các extension cần thiết như `xml`, `dll`, `zip`, `png`, `html`, `css`.
- `PkgFolder/ImportConfig.xml` có thể rất tối giản nếu chỉ import một managed solution: `<configsolutionfile solutionpackagefilename="DataverseLabelTranslator_managed.zip" />`.
- Welcome/End HTML pages trong `PkgFolder\Content\en-us` là Package Deployer wizard UI khi user/reviewer install app. Cần tạo nội dung riêng cho Dataverse Label Translator, không để template title và không để text app Icons. Nếu dùng `common.css` có `url(../images/...)`, phải include đủ `Images` assets trong cả `WelcomeHtml` và `EndHtml`.
- `zip/url.txt` trong app cũ lưu SAS URL sau khi upload. Với repo này chỉ giữ local/private; không commit SAS query string.
- `DeployError/<version>/` là pattern hữu ích để lưu Package Deployer log, failed package ZIP và screenshots khi Microsoft báo lỗi certification/deploy.

## Giai đoạn 5 - Upload Azure Blob và SAS URL

1. Dùng Azure Storage account/container đã dùng cho offer trước nếu vẫn phù hợp quyền truy cập và lifecycle, hoặc tạo container riêng cho app này.
2. Upload **file ZIP all-in-one cuối cùng**: `release/1.0.0.0/appsource/zip/DataverseLabelTranslator.v.1.0.0.zip`.
3. Tạo read-only SAS URL cho blob.
4. SAS expiry nên còn ít nhất 1 tháng trong tương lai để tránh publishing block.
5. Lưu URL vào `release/1.0.0.0/appsource/zip/url.txt` để tracking local, giống workflow app Icons. Không commit SAS URL thật.
6. Test URL bằng browser/incognito hoặc `Invoke-WebRequest` để đảm bảo Microsoft có thể download package.

URL này sẽ được nhập vào Partner Center ở **Technical configuration -> CRM package -> URL of your package location**.

## Giai đoạn 6 - Partner Center offer setup

Luồng nhập liệu chính:

1. **Create new offer**
   - Marketplace offers -> New offer -> Dynamics 365 apps on Dataverse and Power Apps.
   - Chọn cùng publisher/account cũ đã certified: `PhuocLe`.
   - Nhập Offer ID: `dataverse-label-translator`.
   - Nhập alias, ví dụ `Dataverse Label Translator`.
2. **Offer setup**
   - Chọn hướng free listing, không sell through Microsoft trong submission này.
   - Chọn `Get it now (free)`.
   - Customer leads optional; có thể dùng Referrals workspace hoặc kết nối Dynamics 365/Marketo/Salesforce/Azure Table/HTTPS Power Automate.
3. **Properties**
   - Categories/subcategories.
   - Applicable products.
   - App version: `1.0.0.0`.
   - Terms and conditions.
4. **Offer listing**
   - Name, summary, description, keywords.
   - Help URL, privacy policy URL.
   - Support/engineering contacts.
   - Marketing PDF, logos, screenshots, optional videos.
5. **Availability**
   - Markets.
   - Preview audience hide key.
6. **Plans**
   - Không cấu hình paid plans.
   - Không bật ISV app license management.
7. **Technical configuration**
   - Nếu Partner Center vẫn yêu cầu base license model, chọn `User`, vì đây là admin tool được người dùng chạy trực tiếp.
   - Không tick S2S outbound/CRM Secure Store nếu app không cần.
   - Để blank Application configuration URL.
   - Nhập CRM package SAS URL.
   - Chọn CRM package availability regions; không chọn sovereign regions bị Microsoft loại trừ.
8. **Supplemental content**
   - Upload PDF key usage scenarios/E2E journey.
9. **Review and publish**
   - Tất cả page phải `Complete`.
   - Điền Notes for certification thật rõ tài khoản/quyền/steps test.
   - Submit `Publish`.

## Giai đoạn 7 - Preview, certification, go live

Trạng thái Partner Center có thể đi qua:

- `Draft`
- `Publish in progress`
- `Attention needed`
- `Preview`
- `Live`

Khi offer vào `Preview`:

- Mở preview link và review listing.
- Test acquisition/install flow bằng preview audience/hide key.
- Không cần paid purchase test vì offer đã chốt là **Get it now (free)**.
- Không cần Contact Me lead-gate test. Nếu vẫn cấu hình customer leads để tracking, chỉ test lead telemetry/referral flow phụ trợ.
- Nếu cần sửa, edit và resubmit preview.
- Khi đạt, chọn `Go live`.

Tất cả Dynamics 365 offers đi qua certification. Nếu fail, Microsoft sẽ gửi issue và guidance; sau khi fix phải rebuild/reupload/resubmit.

## Certification gates cần chuẩn bị

Microsoft certification cho Dataverse/Power Apps app sẽ kiểm:

- **Sanity check**: registration type **free**, package có đủ artifacts, E2E functional document đầy đủ user/admin journey.
- **Code validation**: Power Apps Checker cho Dataverse solution, Marketplace certification ruleset, accessibility/runtime/formula checks nếu có canvas app.
- **Deployment validation**: Package Deployer install được vào Power Apps/Dataverse environment; các tables/web resources/plugins/components có mặt; uninstall managed solution remove đúng components.
- **Functionality validation**: reviewer chạy theo E2E functional doc; tất cả features documented phải pass.
- **Security validation**: external data sources/connections, Package Deployer custom code, customer data access, service account/least privilege nếu có.
- **Policy 1420**: Dynamics 365 v9.1+, UCI for model-driven apps, public APIs only, package đầy đủ artifacts, E2E doc, version increment với mỗi update, recertification trong 6 tháng từ lần publish thành công gần nhất.
- **Sitemap validation**: published customizations không được change/remove OOB sitemap. Project hiện có app-specific sitemap `pl_DataverseLabelTranslator`, điểm này nên ổn nếu không import sửa OOB sitemap.

## Việc cần làm tiếp trong repo

1. Tạo script/build folder cho AppSource package, ví dụ `release/1.0.0.0/appsource`.
2. Generate PNG logos:
   - `logo32x32.png` cho package.
   - Large logo PNG cho Partner Center.
3. Tạo public homepage trong `site/`, publish bằng GitHub Pages/GitHub Actions, expected URL `https://phuocle.github.io/DataverseLabelTranslator/`.
4. Viết `license.md`, `term.md`, generate `TermsOfUse.html`, và chuẩn bị public privacy/support/help URLs.
5. Reuse template/process từ offer `Icons and Tooltips with D365` cho Partner Center listing fields, support contacts, E2E PDF structure, package upload và certification notes.
6. Review hai documents trong release folder: `release\1.0.0.0\appsource\Documents\UserGuide.1.0.0.0.docx` và `release\1.0.0.0\appsource\Test\E2E User Scenario.1.0.0.0.docx`; thay placeholder hình ảnh bằng screenshot thật nếu cần.
7. Render PDF từ hai file DOCX versioned này cho release AppSource.
8. Tạo marketing PDF cho Dataverse Label Translator nếu dùng.
9. Chạy Solution Checker và lưu report.
10. Tạo Package Deployer package từ managed solution.
11. Test package deploy vào clean environment.
12. Test uninstall và cleanup dictionary data story.
13. Upload final all-in-one ZIP `release/1.0.0.0/appsource/zip/DataverseLabelTranslator.v.1.0.0.zip` lên Azure Blob, tạo SAS URL.
14. Tạo Partner Center offer mới dưới publisher đã certified và submit preview.

## Đề xuất nội dung certification notes

```text
Dataverse Label Translator is a model-driven Dataverse admin utility for translating Dataverse labels and metadata. It requires a Dataverse environment with database, installed languages, Unified Client Interface, and a user with System Administrator or equivalent customization privileges.

Main validation path:
1. Install the package into a clean Dataverse/Dynamics 365 environment.
2. Open the Dataverse Label Translator model-driven app.
3. Select a solution and an entity, load Attributes, edit one target-language display label, save, and confirm the label is updated after publish.
4. Repeat a read-only/load path for Option Sets, Forms, Views, Entity Metadata, Relationships, Sitemap, Web Resources, and Global Option Sets as applicable to the environment.
5. AI-assisted translation is a core feature. Use the temporary certification provider details supplied in Partner Center certification notes, open AI Settings, configure endpoint/key/model, run Auto Translate on a small selected set, review the proposed translations, then save.
6. Dictionary storage creates customer-owned dictionary data. Cleanup instructions are included in the E2E document.
7. Uninstall the managed solution and verify DataverseLabelTranslator web resources/app module/app sitemap are removed.
```

## Mở cấu trúc release AppSource đề xuất

```text
release/
  1.0.0.0/
    dataverse/
      solutions/
        DataverseLabelTranslator.zip
        DataverseLabelTranslator_managed.zip
      unpack/
    appsource/
      src/
        DataverseLabelTranslatorPackage/
          [Content_Types].xml
          PL.DataverseLabelTranslator.PackageDeployment.dll
          PkgFolder/
            ImportConfig.xml
            DataverseLabelTranslator_managed.zip
            Content/
              en-us/
                WelcomeHtml/
                  HTML/Default.htm
                  CSS/common.css
                  Images/
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
                EndHtml/
                  HTML/Default.htm
                  CSS/common.css
                  Images/
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
        DataverseLabelTranslator.v.1.0.0/
          [Content_Types].xml
          input.xml
          TermsOfUse.html
          logo32x32.png
          DataverseLabelTranslatorPackage.zip
      zip/
        DataverseLabelTranslator.v.1.0.0.zip    <-- UPLOAD THIS FILE TO AZURE STORAGE
        url.txt                                 <-- SAS URL record; keep private
      assets/
        logo32x32.png
        logo-large.png
        license.md
        term.md
        screenshot-01-main-grid.png
        terms.html
        e2e-functional-scenarios.pdf
        marketing-onepager.pdf
      Documents/
        UserGuide.1.0.0.0.docx
        UserGuide.1.0.0.0.pdf
      Test/
        E2E User Scenario.1.0.0.0.docx
        E2E User Scenario.1.0.0.0.pdf
        screenshots...
      Images/
      Videos/
      DeployError/
```

Nội dung bên trong `DataverseLabelTranslator.v.1.0.0.zip` phải có dạng:

```text
DataverseLabelTranslatorPackage.zip
[Content_Types].xml
input.xml
TermsOfUse.html
logo32x32.png
```

Không upload các file sau lên Azure Blob thay cho final ZIP:

- `release/1.0.0.0/dataverse/solutions/DataverseLabelTranslator_managed.zip`: chỉ là managed solution.
- `release/1.0.0.0/appsource/src/DataverseLabelTranslator.v.1.0.0/DataverseLabelTranslatorPackage.zip`: chỉ là Package Deployer package nested bên trong final ZIP.
- Folder `release/1.0.0.0/appsource/src/DataverseLabelTranslator.v.1.0.0/`: chỉ là staging folder dùng để zip ra final artifact.
- `release/1.0.0.0/appsource/assets/terms.html`: chỉ là draft/review copy của `TermsOfUse.html`, không đưa vào final Marketplace ZIP.

## Câu hỏi còn cần bạn review/chốt

- Chọn provider nào để dùng cho certification test AI: Azure Foundry hay OpenAI-compatible endpoint riêng?
- Privacy policy/support/help URLs sẽ reuse domain/process của offer `Icons and Tooltips with D365`, hay tạo page riêng dưới GitHub Pages homepage?
- Có chấp nhận dictionary data tồn tại sau uninstall không, hay cần thêm cleanup UI/script trước AppSource?
- Markets nào sẽ publish ban đầu?
