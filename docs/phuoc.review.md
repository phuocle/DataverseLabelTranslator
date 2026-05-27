# Checklist Anh Phuoc Review Truoc Khi Upload AppSource

Ngay: 2026-05-27

File nay tach rieng cac viec anh Phuoc can tu review truoc khi upload final ZIP len Azure Blob va submit Partner Center. AI co the build/validate package, nhung cac muc duoi day la go/no-go checklist cua anh Phuoc.

## 1. Final ZIP

- [x] Xac nhan final ZIP ton tai:

```text
D:\github\DataverseLabelTranslator\release\1.0.0.0\appsource\zip\DataverseLabelTranslator.v.1.0.0.zip
```

- [x] Chi upload file ZIP all-in-one nay len Azure Blob.
- [x] Khong upload `release\1.0.0.0\dataverse\solutions\DataverseLabelTranslator_managed.zip`.
- [x] Khong upload nested `DataverseLabelTranslatorPackage.zip`.
- [x] Mo final ZIP va kiem tra root co dung 5 file:

```text
DataverseLabelTranslatorPackage.zip
[Content_Types].xml
input.xml
TermsOfUse.html
logo32x32.png
```

- [x] Mo nested `DataverseLabelTranslatorPackage.zip` va kiem tra co `PkgFolder`.
- [x] Kiem tra `PkgFolder\ImportConfig.xml` tro dung `DataverseLabelTranslator_managed.zip`.
- [x] Kiem tra wizard install page: `PkgFolder\Content\en-us\WelcomeHtml\HTML\Default.htm`.
- [x] Kiem tra wizard finish page: `PkgFolder\Content\en-us\EndHtml\HTML\Default.htm`.
- [x] Wizard pages khong con text `Template Package Title Here`.
- [x] Wizard pages khong con text `Icons and Tooltips`.

## 2. Managed Solution Source

- [x] Xac nhan file managed solution do anh Phuoc control la latest:

```text
D:\github\DataverseLabelTranslator\release\1.0.0.0\dataverse\solutions\DataverseLabelTranslator_managed.zip
```

- [x] Xac nhan khong can export lai solution trong luong `Release AppSource`.
- [x] Xac nhan version `1.0.0.0` la version muon submit.
- [x] Xac nhan solution khong chua PropertyEditor resources.
- [x] Test install package vao clean/test environment.
- [x] Xac nhan model-driven app `Dataverse Label Translator` mo duoc sau install.

## 3. Listing Decisions

- [ ] Publisher/account dung lai account cu: `PhuocLe`.
- [ ] Publisher ID: `phuocle`.
- [ ] Offer ID: `dataverse-label-translator`.
- [ ] Listing option: `Get it now (free)`.
- [ ] Khong bat paid plan.
- [ ] Khong bat ISV app license management.
- [ ] Khong dung `Contact me`.
- [ ] Source repo URL dung: `https://github.com/phuocle/DataverseLabelTranslator`.
- [ ] Homepage URL dung: `https://phuocle.github.io/DataverseLabelTranslator/`.
- [ ] GitHub Pages da publish tu folder `site/`, khong publish folder `docs/`.
- [ ] Homepage mo duoc bang browser/incognito.
- [ ] Homepage co link GitHub source.
- [ ] Homepage co privacy summary: khong gui data ve publisher server, AI goi truc tiep provider do user cau hinh, co `localStorage`, co use-at-your-own-risk wording.
- [ ] Chon applicable products, toi thieu `Power Apps`.
- [ ] Chon markets muon publish.
- [ ] Chon preview audience/hide key.

## 4. AI Certification Path

- [ ] Chon provider de Microsoft reviewer test AI: Azure Foundry hoac OpenAI-compatible endpoint rieng.
- [ ] Tao endpoint/model/API key test tam thoi.
- [ ] Set quota va expiry cho key test.
- [ ] Dam bao key test khong nam trong git.
- [ ] Dam bao certification notes co huong dan nhap AI Settings.
- [ ] Test Auto Translate bang key test tren clean/test environment.
- [ ] Review privacy wording: selected label/metadata text co the duoc gui toi AI provider da cau hinh.
- [ ] Review `TermsOfUse.html` co noi ro AI/external provider usage.
- [ ] Review wording noi ro AI output phai duoc user review truoc khi save.

## 5. Screenshots Va Media

- [ ] Chup screenshot main translation grid.
- [ ] Chup screenshot AI Settings, che toan bo API key/secret.
- [ ] Chup screenshot Auto Translate proposal/review.
- [ ] Chup screenshot Dictionary management.
- [ ] Chup screenshot Save/result hoac publish success.
- [ ] Screenshots dung kich thuoc Partner Center yeu cau, uu tien 1280 x 720 PNG.
- [ ] Screenshots khong lo internal environment URL neu khong muon public.
- [ ] Screenshots khong lo user email, tenant info, API key, SAS URL.
- [ ] Review video demo neu dung, dam bao khong lo secrets.
- [ ] Review `logo32x32.png` nhin ro o 32 x 32.
- [ ] Review large logo/listing logo khong bi mo.
- [ ] Review generated homepage hero: `release\1.0.0.0\appsource\assets\homepage-hero.png`.
- [ ] Review generated package flow diagram: `release\1.0.0.0\appsource\assets\appsource-package-flow.png`.
- [ ] Review generated AI privacy flow diagram: `release\1.0.0.0\appsource\assets\ai-privacy-flow.png`.
- [ ] Review generated wizard visual: `release\1.0.0.0\appsource\assets\install-wizard-visual.png`.
- [ ] Confirm generated images khong thay the cho screenshots that cua app.

## 6. Documents

- [ ] Review source User Guide:

```text
D:\github\DataverseLabelTranslator\release\1.0.0.0\appsource\Documents\UserGuide.1.0.0.0.docx
```

- [ ] Review source E2E Scenario:

```text
D:\github\DataverseLabelTranslator\release\1.0.0.0\appsource\Test\E2E User Scenario.1.0.0.0.docx
```

- [ ] Review staged User Guide PDF:

```text
D:\github\DataverseLabelTranslator\release\1.0.0.0\appsource\Documents\UserGuide.1.0.0.0.pdf
```

- [ ] Review staged E2E Scenario PDF:

```text
D:\github\DataverseLabelTranslator\release\1.0.0.0\appsource\Test\E2E User Scenario.1.0.0.0.pdf
```

- [ ] Review tat ca placeholder hinh anh trong DOCX/PDF va thay bang screenshot that neu can.
- [ ] E2E doc co buoc install package.
- [ ] E2E doc co buoc mo model-driven app.
- [ ] E2E doc co buoc configure AI provider.
- [ ] E2E doc co buoc Auto Translate bat buoc.
- [ ] E2E doc co buoc review proposal truoc khi save.
- [ ] E2E doc co buoc dictionary.
- [ ] E2E doc co buoc uninstall/cleanup.
- [ ] User Guide noi ro role can thiet: System Administrator hoac quyen tuong duong.
- [ ] User Guide noi ro dictionary data cleanup/story.

## 7. Legal, Privacy, Support

- [ ] Help URL live va dung app Dataverse Label Translator.
- [ ] `input.xml` `LearnMoreLink` dung homepage `https://phuocle.github.io/DataverseLabelTranslator/`.
- [ ] Support URL/email live va thuoc quyen kiem soat cua anh Phuoc.
- [ ] Privacy policy URL live va co AI/external provider disclosure.
- [ ] Terms URL hoac Partner Center terms text khop voi `TermsOfUse.html`.
- [ ] Review `release\1.0.0.0\appsource\assets\license.md`.
- [ ] Review `release\1.0.0.0\appsource\assets\term.md`.
- [ ] `license.md` noi ro app free, khong trial, khong paid license, deploy/install bao nhieu environment cung duoc.
- [ ] `term.md` noi ro app khong gui data/API key/telemetry ve server cua PhuocLe/publisher.
- [ ] `term.md` noi ro Auto Translate gui selected label/metadata truc tiep toi AI provider do user cau hinh.
- [ ] `term.md` noi ro AI settings co the luu trong browser `localStorage`.
- [ ] `term.md` co cau `Use it at your own risk` hoac wording tuong duong.
- [ ] Khong con text trial/paid/purchase license tu app Icons.
- [ ] Khong con claim "khong truy cap internet" vi app nay co AI provider calls.
- [ ] Support/contact information dung nguoi/team se xu ly AppSource customer.

## 8. Partner Center Listing Text

- [ ] Offer name: `Dataverse Label Translator`.
- [ ] Summary noi ro free AI-assisted Dataverse label translation.
- [ ] Description noi ro manual review before save.
- [ ] Description noi ro supported metadata areas.
- [ ] Description noi ro required permissions/admin role.
- [ ] Keywords phu hop: Dataverse, translation, localization, labels, metadata, AI.
- [ ] Screenshot captions dung tung hinh.
- [ ] Marketing PDF duoc upload neu dung.
- [ ] Supplemental E2E PDF duoc upload.
- [ ] Certification notes co AI provider test instructions.

## 9. Validation Evidence

- [ ] Solution Checker report da duoc review.
- [ ] Khong con critical/high issue chua xu ly.
- [ ] Package Deployer install test pass tren clean/test environment.
- [ ] Smoke test load entity/attribute pass.
- [ ] Smoke test save translation pass tren test environment.
- [ ] Smoke test AI Auto Translate pass.
- [ ] Smoke test Dictionary pass.
- [ ] Uninstall test pass hoac cleanup note da ro.
- [ ] Logs/test evidence duoc luu duoi `release\1.0.0.0\appsource\Validation\` hoac `DeployError\` neu co loi.

## 10. Azure Upload

- [ ] Chi upload final all-in-one ZIP:

```text
D:\github\DataverseLabelTranslator\release\1.0.0.0\appsource\zip\DataverseLabelTranslator.v.1.0.0.zip
```

- [ ] SAS URL la read-only.
- [ ] SAS expiry con it nhat 1 thang trong tuong lai.
- [ ] Test SAS URL download thanh cong trong browser/incognito.
- [ ] Luu `url.txt` local neu can.
- [ ] Khong commit `url.txt` co SAS query string.

## 11. Final Go/No-Go

- [ ] Anh Phuoc da review final ZIP.
- [ ] Anh Phuoc da review screenshots/media.
- [ ] Anh Phuoc da review User Guide.
- [ ] Anh Phuoc da review E2E Scenario.
- [ ] Anh Phuoc da review Terms/Privacy/Support.
- [ ] Anh Phuoc da review Partner Center listing text.
- [ ] Anh Phuoc da review AI certification credentials/process.
- [ ] Anh Phuoc da upload final ZIP len Azure Blob.
- [ ] Anh Phuoc da paste SAS URL vao Partner Center technical configuration.
- [ ] Anh Phuoc da submit preview/certification.
