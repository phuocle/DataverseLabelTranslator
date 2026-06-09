# Dataverse Label Translator

> Thanks to [XRM-OSS/Xrm-Quick-Edit](https://github.com/XRM-OSS/Xrm-Quick-Edit). This project uses the original idea and a small amount of code from that repo as a foundation, then rebuilds and extends the experience for a cleaner Dataverse label translation workflow.

A Dataverse model-driven app for translating labels and metadata through a single inline grid hosted in the main web resource. It supports manual editing, AI-assisted translation, reusable dictionary entries, and bulk translation workflows.

> **Note:** Use a backup solution before testing metadata changes in a shared environment.

## Features

### Translation App

Translate UI labels for Dataverse components using the same type menu shown in the app:

| Type | Scope | What is translated |
|------|-------|-------------------|
| **1. Attributes** | Entity | Display names and descriptions |
| **2. Option Sets** | Entity | Local option set values, including state/status labels and descriptions |
| **3. Forms** | Entity | Form tabs, sections, field labels, and related form labels |
| **4. Views** | Entity | View display names and descriptions |
| **5. Form Metadata** | Entity | Form display names |
| **6. Entity Metadata** | Entity | Entity display names and collection names |
| **7. Relationships** | Entity | Navigation menu labels for entity relationships |
| **8. Charts** | Entity | Visualization display names |
| **9. Business Process Flows** | Entity | Stage and field labels |
| **10. Business Rules** | Entity | Business rule error messages and recommendation text stored in workflow XAML |
| **11. Ribbons** | Entity | Classic ribbon / command bar button text, tooltip title, and tooltip description labels |
| **12. Commands** | Entity | Modern command designer appaction labels: text, title, description, accessibility text, and group title |
| **13. Entity Messages** | Entity | System table messages/display strings from solution translation packages |
| **15. Sitemap** | None | App navigation areas, groups, and subareas |
| **16. Dashboards** | None | Dashboard form labels |
| **17. Web Resources** | None | Text content within web resources |
| **Global Option Sets** | None | Global option set values independent of an entity |

There is also a special **14. Content Snippets** type for legacy Dynamics 365 Portals / Power Pages content snippets. It appears only when the selected entity is `Adx_contentsnippet`.

### Power Pages Content Snippets

The app still includes legacy content snippet support from the original translator. This is not a general Dataverse label type. It is available only when the environment has the old portal tables and the selected entity is `Adx_contentsnippet`.

When available, **14. Content Snippets** loads `adx_contentsnippet` records grouped by website, uses `adx_websitelanguage` to map portal languages to LCIDs, and saves translated snippet values back to `adx_contentsnippet`. Environments without those portal tables should ignore this type.

### Ribbon Labels

**11. Ribbons** loads classic `RibbonDiffXml` labels for the selected entity only. Each ribbon button is shown as a parent row with `Text`, `Title`, and `Description` child rows, even when a tooltip value is currently blank. Save imports the updated ribbon solution XML and starts Dataverse Publish XML, so the app shows a status banner and temporarily blocks Save/Load until the server job finishes.

### Business Rule Labels

**10. Business Rules** loads Dataverse business rule labels from `workflow.xaml` through the server-side unified handler, including error messages, recommendation titles, and recommendation details. Save temporarily deactivates each changed rule, patches only its `mcwo:StepLabel` entries by `LabelId` and LCID, then reactivates the rule. The runtime flow does not download or persist a backup file; it keeps the original XAML in memory for best-effort rollback during the same save operation.

### Modern Command Labels

**12. Commands** loads modern command designer records from `appaction` components in the selected solution and selected entity. Each command is shown as a parent node with `Text`, `Title`, `Description`, `Accessibility Text`, and `Group Title` child rows. Save writes changed labels with Dataverse `SetLocLabels` and publishes the selected entity once; it does not publish a model-driven app.

### Entity Messages

**13. Entity Messages** loads Dataverse table messages/display strings from the selected solution translation package, filtered to the selected entity. This uses the official translation export/import flow rather than directly patching `displaystring` records. Save re-exports a fresh translation package, applies only changed message rows, imports translations, and publishes the selected entity so the imported messages become visible.

### AI Translation

Translate labels automatically using an enabled AI provider. Select a source language and target language, then click **Auto Translate** to fill missing labels or overwrite selected records.

| Provider | Notes |
|----------|-------|
| **Google** | Gemini-compatible batch mode, custom prompt support, configurable model |
| **OpenAI** | Configurable endpoint URL, API key, model name, and custom prompt |
| **Azure** | OpenAI-compatible chat completions using `api-key` authentication |

Provider credentials and model settings are managed through **App Settings** and stored in a Dataverse app settings web resource: `pl_/DataverseLabelTranslator/data/AppSettings.xml`.

### Translation Dictionary

Maintain a persistent glossary of approved source-to-target translations. Dictionary entries are useful for product names, field names, business terms, acronyms, and phrases that must stay consistent across metadata types.

The dictionary is based on the environment base language as the source language. Each entry stores one source text value and one target value per installed target language. Matching is exact after trimming whitespace, so short generic terms should be added carefully.

You can add entries in two ways:

- **Manage Dictionary**: Open the Dictionary popup, type a source value, fill one or more target language columns, keep **Active** checked, and click **Save**. The grid always keeps a blank row available for the next entry.
- **Add Selected Translation To Dictionary**: Select exactly one translatable row in the main grid, make sure it has source text and at least one translated target value, then use the add-to-dictionary action. Existing dictionary entries with the same source text are updated instead of duplicated.

Dictionary entries can then be used without calling AI:

- **Apply Dictionary** applies matching active entries to the current grid.
- **All Overwrite** replaces matching target values even when a value already exists.
- **All Missing** fills only empty target values.
- **Auto Translate** can use **Use Dictionary as First Priority**. When enabled, dictionary matches are applied first and only unmatched records are sent to the selected AI provider.
- Translation proposal results mark dictionary matches with **From Dictionary** so they can be reviewed before saving.

Dictionary storage is Dataverse-backed, not browser-only:

- Data is saved as `pl_/DataverseLabelTranslator/data/TranslationDictionary.xml`.
- The XML web resource lives inside the unmanaged solution **Dataverse Label Translator Data** (`DataverseLabelTranslatorData`).
- The data solution and dictionary web resource are auto-created on first use.

### Other Capabilities

- **Find and Replace**: Search loaded labels using plain text or JavaScript regular expressions.
- **Untranslated Records Filter**: Show only records with missing translations.
- **Solution filter**: Filter the entity list by solution membership.
- **Solution integration**: Automatically add translated components to a selected solution.
- **Language column toggle**: Show only the current user's language by default and expand when needed.
- **Locked languages**: Prevent editing selected language columns.
- **About / Help**: In-app translation guide.

## Installation

1. Import the solution into a Dynamics 365 / Dataverse environment.
2. Open the **Dataverse Label Translator** model-driven app.
3. Open the main app page hosted by `pl_/html/App.html`.
4. Configure AI provider settings only if AI translation is required.

### System Requirements

- Dynamics CRM 2016 (v8.0) or later / Dynamics 365 / Dataverse
- System Administrator security role for metadata operations

## Usage Notes

### Workflow

1. Select a **Solution** to scope the entity list and solution-level types.
2. Select an **Entity**, or choose **None** for entity-independent types like **Sitemap**, **Dashboards**, **Web Resources**, and **Global Option Sets**.
3. Select a **Type**.
4. Click **Load** to populate the grid.
5. Edit cells inline, use **Auto Translate**, or use **Apply Dictionary**.
6. Click **Save** to write changes back to Dataverse and publish.

For Power Pages content snippets, select entity `Adx_contentsnippet`, choose **14. Content Snippets**, then load and save like the other types.

### Form Translation

Dataverse returns form labels only for the current user's language. To work around this, the tool temporarily switches the user language to each installed language to retrieve all labels, then restores the original language. Do not abort the loading process or your user language may be left in a different state.

The tool also sets the UI language to the base language before publishing to avoid known publishing issues.

### Overridden Attribute Labels In Forms

If translating an attribute does not update its form label, the form likely has overridden labels for that field. Use **Remove Overridden Attribute Labels** inside the form translator to clear them. Export a backup solution before applying this change.

## Architecture

The active web resource codebase lives under `DataverseLabelTranslator.WebResource`. It is a DynamicsCrm.DevKit WebResource `.csproj` for fast local web resource deployment, and it also contains the npm lint/test tooling for the plain JavaScript dashboard.

The Dataverse app hosts one main web resource: `pl_/html/App.html`.

Every handler implements:

- **`Load()`**: Fetch metadata from Dataverse APIs and populate the w2ui grid.
- **`Save()`**: Extract changed grid records, update Dataverse through Web API, and publish.

### Project Structure

```text
DataverseLabelTranslator.WebResource/
  DataverseLabelTranslator.WebResource.csproj
  package.json
  vitest.config.mjs
  eslint.config.mjs
  deploy.debug.bat        # commit-safe DevKit deploy script using root .env
  html/
    App.html
  js/
    XrmTranslator.js
    EasyTranslatorHandler.js
    FormHandler.js
    BpfHandler.js
    RibbonHandler.js
    ContentSnippetHandler.js
    TranslationHandler.js
    TranslationDictionaryService.js
    DialogHelper.js
  css/
    w2ui.css
    style.css
  img/
    app-icon.svg
DataverseLabelTranslator.Scripts/
  release-appsource.ps1
  deploy-azure.ps1
  sync-ai-config.ps1
DataverseLabelTranslator.Documents/
  type-convention-from-global-option-sets.md
```

### Local DevKit Environment

DevKit batch files load local connection settings from the repository root `.env` file. Values in `.env` intentionally override inherited `DEVKIT_*` process environment variables for that batch run. Commit `.env.example`, copy it to `.env`, and fill the local values there. `.env` is ignored by git.

## Tech Stack

| Library | Version | Purpose |
|---------|---------|---------|
| [w2ui](https://github.com/vitmalina/w2ui) | 2.0 | Grid UI framework |
| [WebApiClient](https://github.com/XRM-OSS/Xrm-WebApi-Client) | 4.1.6 | Dataverse Web API wrapper |

No build step is required. JavaScript files deploy directly as Dataverse web resources.

## License

MIT License

## Release And Deployment Flow

Confirmed on May 29, 2026, the production release flow is:

1. Run `$pl-export-solution` to export `DataverseLabelTranslator`, clean labels, and create final Dataverse solution ZIPs under `DataverseLabelTranslator.Release/<version>/dataverse/solutions/`.
2. Run `$pl-release-appsource` to build the AppSource Marketplace upload ZIP from the existing managed solution. This step does not export Dataverse again. The final upload file is `DataverseLabelTranslator.Release/<version>/appsource/zip/DataverseLabelTranslator.v.<major.minor.patch>.zip`.
3. Run `$pl-deploy-azure` to upload only that final AppSource ZIP to Azure Blob Storage and generate Partner Center SAS details in `DataverseLabelTranslator.Release/<version>/appsource/zip/release.md`.

`release.md` contains private SAS details and must stay ignored/uncommitted. Paste the generated SAS package URL into Partner Center, but do not paste it into commits, issues, or chat logs.
