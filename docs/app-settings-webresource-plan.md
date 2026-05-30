# App Settings Web Resource Plan

## Goal

Move AI Settings out of browser `localStorage` and into Dataverse-owned app settings storage.

The first implementation only covers AI settings, but the UI and storage contract should support future setting groups such as General.

No JavaScript implementation should begin until this plan is reviewed and approved.

## Current State Observed

- `js/TranslationHandler.js` stores AI provider config in `localStorage` keys:
  - `DataverseLabelTranslator_GeminiConfig`
  - `DataverseLabelTranslator_OpenAIConfig`
  - `DataverseLabelTranslator_OpenAICompatibleConfig`
  - `DataverseLabelTranslator_AzureFoundryConfig`
  - `DataverseLabelTranslator_TranslationPrompt`
- `js/TranslationDictionaryService.js` already creates and updates a Dataverse data web resource:
  - `pl_/DataverseLabelTranslator/data/TranslationDictionary.xml`
  - data solution: `DataverseLabelTranslatorData`
  - display name: `Dataverse Label Translator Data`
- Dictionary bootstrapping includes reusable logic that should not remain dictionary-specific:
  - find base solution and publisher
  - create/reuse data publisher
  - create/reuse data solution
  - create/reuse data web resource
  - add web resource to solution
  - base64 encode/decode content
  - publish web resource after update

## Storage Name Recommendation

Recommended Dataverse web resource:

```text
pl_/DataverseLabelTranslator/data/AppSettings.xml
```

Reason: Dataverse web resources do not have a native JSON web resource type in the current codebase pattern. The existing Dictionary storage uses a type `4` data/XML web resource, so the safest implementation is an XML data web resource with a JSON payload in CDATA.

Proposed content:

```xml
<?xml version="1.0" encoding="utf-8"?>
<appSettings contentType="application/json"><![CDATA[
{
  "schemaVersion": 1,
  "updatedOn": "",
  "updatedBy": {
    "id": "",
    "name": ""
  },
  "general": {},
  "ai": {
    "selectedProvider": "google",
    "providers": {
      "google": {
        "enabled": true,
        "baseUrl": "https://generativelanguage.googleapis.com/v1beta",
        "apiKey": "",
        "modelName": "",
        "customPrompt": ""
      },
      "openai": {
        "enabled": true,
        "baseUrl": "https://api.openai.com/v1",
        "apiKey": "",
        "modelName": "",
        "customPrompt": ""
      },
      "azure": {
        "enabled": true,
        "baseUrl": "",
        "apiKey": "",
        "modelName": "",
        "customPrompt": ""
      }
    }
  }
}
]]></appSettings>
```

If a dev Dataverse check proves that `.json` names are accepted for type `4`, we can switch to:

```text
pl_/DataverseLabelTranslator/data/AppSettings.json
```

with raw JSON content. Until that is confirmed, `AppSettings.xml` is the safer plan.

## Security Boundary

This removes AI settings from each browser's `localStorage`. It does not make provider secrets server-side secrets, because the app still runs client-side and must read the key to call the provider directly from the browser.

Practical implication:

- users who can open and use the translation feature can read the configured provider values in the browser
- admins with Dataverse customization/web resource access can read the app settings web resource
- if key-grade secrecy is required later, the architecture must move AI calls behind a server-side component, custom API, connector, or secret-backed environment variable

Within the current client-only architecture, storing the config in Dataverse web resource storage is still better than browser-local storage because it is environment-owned, centrally updateable, and not silently duplicated across user machines.

## Shared Web Resource Storage Helper

Add a new service:

```text
js/DataverseDataWebResourceService.js
```

Suggested public contract:

```javascript
DataverseDataWebResourceService.EnsureDataSolution(forceRefresh)
DataverseDataWebResourceService.EnsureTextWebResource(options)
DataverseDataWebResourceService.ReadText(options)
DataverseDataWebResourceService.WriteText(options)
DataverseDataWebResourceService.ReadJson(options)
DataverseDataWebResourceService.WriteJson(options)
DataverseDataWebResourceService.PublishWebResource(webResourceId)
```

Suggested `options` shape:

```javascript
{
  uniqueName: "pl_/DataverseLabelTranslator/data/AppSettings.xml",
  displayName: "Dataverse Label Translator App Settings",
  description: "Stores environment-owned app settings for Dataverse Label Translator.",
  webResourceType: 4,
  defaultContent: "<xml or json default>",
  solutionUniqueName: "DataverseLabelTranslatorData"
}
```

Rules:

- Use fixed unique names as the source of truth.
- Keep only in-memory cache for the current page session.
- Do not persist setting values or secret-bearing config to `localStorage`.
- Prefer live lookup by web resource name before read/write, like the current Dictionary fixed-name read path.
- Publish the data web resource after write.

Refactor `TranslationDictionaryService` to call this helper for data solution/web resource bootstrap and text read/write. Keep dictionary-specific XML parse, serialize, grid, and matching logic inside `TranslationDictionaryService`.

## App Settings Service

Add a new service:

```text
js/AppSettingsService.js
```

Suggested public contract:

```javascript
AppSettingsService.EnsureInitialized(forceRefresh)
AppSettingsService.GetSettings(forceRefresh)
AppSettingsService.SaveSettings(settings)
AppSettingsService.GetAISettings(forceRefresh)
AppSettingsService.SaveAISettings(aiSettings)
AppSettingsService.GetProviderConfig(providerKey, forceRefresh)
```

Rules:

- Always merge saves against the latest server file so saving AI does not wipe future `general` settings.
- Preserve existing API key when the user leaves a masked key unchanged.
- Clear only in-memory settings cache after save.
- Do not read old AI config from `localStorage`.
- Do not automatically migrate existing local browser secrets into Dataverse.

## UI Plan

Toolbar:

- Rename tooltip from `AI Settings` to `App Settings`.
- Keep the current settings icon unless a better existing icon is already available.
- Route click to the new App Settings dialog.

Popup:

- Title: `App Settings`
- Top-level tabs:
  - `General`
  - `AI`
- `General` tab:
  - blank placeholder for now
  - no fake fields and no explanatory in-app text
- `AI` tab:
  - nested provider tabs:
    - `Google`
    - `OpenAI`
    - `Azure`
  - fields per provider:
    - URL
    - API Key
    - Model Name
    - Custom Prompt

Provider naming:

- Current `Gemini AI` becomes the `Google` tab/provider label in settings UI.
- Current `Azure Foundry` becomes the `Azure` tab/provider label in settings UI.
- Replace visible `OpenAI Compatible` wording with `OpenAI` unless we decide to keep a separate compatibility provider later.

## Runtime Translation Flow

Every Auto Translate apply/propose run must read settings in real time:

1. User opens Auto Translate.
2. User selects provider.
3. Before provider validation and translator creation, call `AppSettingsService.GetAISettings(true)`.
4. Build provider config from the freshly read settings.
5. Validate missing URL/API key/model against that fresh config.
6. Call provider.

This means a settings change in another tab or by another admin is picked up on the next translate run without needing page reload.

The provider list in the Auto Translate prompt should be driven by the same App Settings model. If the file is missing, create it with default blank AI config and show normal validation errors when provider values are empty.

## CRUD Rules

Create:

- On first App Settings open or first translate attempt, ensure `DataverseLabelTranslatorData` and `AppSettings.xml` exist.
- Add the web resource to `DataverseLabelTranslatorData`.

Read:

- Decode web resource content.
- Parse XML wrapper.
- Parse JSON payload.
- Merge with defaults for missing schema fields.

Update:

- Re-read the latest server settings.
- Merge only the submitted section, initially `ai`.
- Serialize XML wrapper with JSON CDATA.
- Update `webresourceset.content`.
- Publish the web resource.

Delete:

- No UI delete for now.
- If content is corrupt, recover by showing an error and offering a reset path only after separate approval.

## Files Expected To Change After Approval

- `html/App.html`
  - add `DataverseDataWebResourceService.js`
  - add `AppSettingsService.js`
- `.codex/mapping.xml`
  - map both new JS web resources
- `js/DataverseDataWebResourceService.js`
  - new shared data web resource helper
- `js/AppSettingsService.js`
  - new app settings read/write service
- `js/TranslationDictionaryService.js`
  - refactor data solution/web resource bootstrap to shared helper
- `js/TranslationHandler.js`
  - remove AI settings `localStorage` read/write
  - make provider config read async from `AppSettingsService`
  - open new App Settings popup
  - stop persisting AI prompt/provider defaults to `localStorage`
- `js/XrmTranslator.js`
  - toolbar tooltip `App Settings`
  - help/about text no longer says AI settings are stored in `localStorage`
- `css/style.css`
  - only if needed for the nested tab layout
- docs/user-guide generation text
  - update later if this feature is approved and implemented

## Non-Goals

- No deployment in this task.
- No Dataverse MCP writes in this task.
- No change to PropertyEditor resources.
- No server-side proxy or custom API for AI calls in this task.
- No automatic migration from browser `localStorage`.
- No change to unrelated `localStorage` usage such as publish job guards unless requested separately.

## Verification Checklist After Implementation

Static:

```powershell
node --check js\DataverseDataWebResourceService.js
node --check js\AppSettingsService.js
node --check js\TranslationDictionaryService.js
node --check js\TranslationHandler.js
node --check js\XrmTranslator.js
```

Functional:

1. Open App Settings in a clean environment.
2. Confirm `DataverseLabelTranslatorData` is created/reused.
3. Confirm `pl_/DataverseLabelTranslator/data/AppSettings.xml` is created/reused and added to the data solution.
4. Save Google/OpenAI/Azure settings.
5. Confirm browser `localStorage` does not receive AI provider config.
6. Reload page and confirm App Settings reads from the web resource.
7. Update the web resource from another session, then run Auto Translate and confirm it reads the latest settings.
8. Confirm masked API key is preserved when unchanged.
9. Confirm Dictionary still loads, saves, and publishes through the shared helper.
10. Confirm help/about text no longer references AI settings in `localStorage`.

