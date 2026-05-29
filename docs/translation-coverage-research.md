# Dataverse Translation Coverage Research

Date: 2026-05-29

## Summary

The current app already covers most classic Dataverse metadata labels: tables, columns, choices, forms, views, form names, table metadata, relationship menu labels, charts, BPF stage/step labels, sitemap, dashboards, RESX/JSON web resources, global choices, content snippets, and classic ribbon labels.

The biggest confirmed gap is **modern commands**. Microsoft documents modern commands as separate Dataverse solution objects, not classic `RibbonDiffXml`. They support standard solution translation export/import and expose localizable fields on the `appaction` table. This is the likely missing area behind "new/replace ribbon" command customization.

The second confirmed gap is **Entity Messages / Display Strings**. Microsoft lists Entity Messages as exportable localizable solution components, and the Power Apps table designer exposes them under **Messages** for system tables. This app currently does not have a message/display-string handler.

## Current Coverage

| Area from Microsoft docs | Current app support | Notes |
|---|---:|---|
| Entities / tables | Yes | `6. Entity Metadata` loads two rows: `Display Name` and `Collection Name`. |
| Attributes / columns | Yes | `1. Attributes` handles display names and descriptions. |
| Local option sets / state / status | Yes | `2. Option Sets`. |
| Global option sets | Yes | `13. Global Option Sets`. |
| Relationships | Yes | `7. Relationships` handles associated menu labels. |
| Entity forms | Yes | `3. Forms` and `5. Form Metadata`. |
| Entity views / saved queries | Yes | `4. Views`. |
| Charts | Yes | `8. Charts`. |
| Dashboards | Yes | `11. Dashboards`. |
| Sitemap | Yes | `10. Sitemap`. Microsoft documents sitemap titles/descriptions as localized XML. |
| Classic ribbon labels | Yes | `15. Ribbons` reads/saves `RibbonDiffXml` `LocLabels` for entity-level classic ribbon labels. |
| RESX / JSON web resources | Mostly yes | `12. Web Resources` supports localized `.resx` and `.js` resources by LCID naming. |
| Content snippets | Yes, legacy/special | `14. Content Snippets` is portal-specific, not a general Dataverse metadata label type. |

## Confirmed Missing Items

### P0 - Modern Commands

Modern commands are represented by the Dataverse `appaction` table and are managed through command designer. Microsoft says modern command localization is standardized through export/import translations, unlike classic ribbon localization.

Relevant localizable fields from `appaction`:

- `ButtonLabelText`
- `ButtonTooltipTitle`
- `ButtonTooltipDescription`
- `ButtonAccessibilityText`
- `GroupTitle`

Relevant command locations:

- Form
- Main Grid
- Sub Grid
- Associated Grid
- Quick Form
- Global Header
- Dashboard

Recommended app feature:

- Add a new type after `15. Ribbons`, probably **16. Modern Commands**.
- Keep it separate from `15. Ribbons`; do not merge it into the classic ribbon handler.
- Node design should mirror ribbon:
  - parent row: location + command type + command label/name
  - child rows: Text, Title, Description, Accessibility Text, Group Title where applicable
  - always create editable child rows even when values are blank
- Scope:
  - solution-scoped first, because modern commands are solution components
  - optional entity filter when an entity is selected, based on `ContextEntity`, `ContextValue`, and solution membership
- Save strategy needs a spike:
  - Option A: use `SetLocLabels` on localizable `appaction` attributes.
  - Option B: export `CrmTranslations.xml`, edit matching modern-command rows, then use `ImportTranslationRequest`.
  - Microsoft docs are clearest for Option B. Option A is attractive for inline editing, but must be proven against real modern command labels and solution layering.

Why this matters:

- Classic `RibbonDiffXml` does not cover modern command designer objects.
- Replaced/customized OOB commands can become modern commands and will not be handled by `15. Ribbons`.

### P0 - Entity Messages / Display Strings

Microsoft lists **Entity Messages** as exportable localizable solution components. Power Apps also has a table designer **Messages** page for system table messages.

Recommended app feature:

- Add **17. Entity Messages**.
- Load entity-scoped display strings/messages through the official translation export path first, because direct `displaystring` rows do not contain all default text.
- Show rows such as:
  - default display text
  - custom display text
  - published display text
  - comment/key metadata in readonly columns or hidden metadata
- Save through `ImportTranslationRequest` or a proven direct `displaystring` update path.
- Do not include in All-In-One initially; message volume can be large and the UX differs from normal metadata labels.

Why this matters:

- If a system table display name is changed, Microsoft says messages can still contain old names unless those messages are updated.
- This matches the Power Apps **Messages** screen previously seen for OOB tables.

### P1 - Business Rule Error Messages

Business rule "show error message" text is localizable. Microsoft says each message generates a label and can be localized by exporting/importing translations.

Recommended app feature:

- Add a solution/entity-scoped handler only after the export/import translation service exists.
- Candidate type: **18. Business Rule Messages**.
- Read from `CrmTranslations.xml` Display Strings / Localized Labels rows related to business rules.
- Save by importing the updated translation package.

Do not try to hand-edit workflow XAML first unless export/import cannot round-trip the messages reliably.

### P1 - Translation Package Audit Mode

Add an internal/research mode that exports translations for the selected solution, parses `CrmTranslations.xml`, and reports rows not covered by current handlers.

Recommended output:

- component family
- object id / component id
- source worksheet: `Localized Labels` or `Display Strings`
- source text
- whether a current handler owns it

Why this matters:

- Microsoft can add localizable rows for new components over time.
- This gives the project a repeatable way to detect "translator coverage drift" instead of relying on memory.

### P2 - Custom API Labels

Custom APIs, request parameters, and response properties have localizable display names/descriptions. Microsoft documents `SetLocLabels` and `RetrieveLocLabels` for custom API labels.

Recommended app feature:

- Lower priority because these labels are mostly maker/developer-facing, not normal model-driven app UI.
- Add only if target customers build Custom APIs and expect translator coverage.
- Candidate type: **Custom APIs** or include in a future "Developer Components" group.

### P2 - Entity Keys

The Dataverse `Label` complex type is used by `EntityKeyMetadata.DisplayName`, but this app does not currently expose keys.

Recommended app feature:

- Low priority.
- Add only if key display names appear in exported translation files for customer solutions and users care about them.

### P2 - Custom Pages / Canvas RESX Improvements

Current `12. Web Resources` can edit LCID-named RESX web resources. Microsoft custom page localization, however, also depends on custom page resource binding and control expressions.

Recommended app feature:

- Keep current RESX web-resource support.
- Add custom-page-specific guidance or validation only if the product starts targeting custom pages.
- Do not promise automatic custom page label extraction without a separate custom page parser/spike.

## Not Recommended / Out Of Scope

Do not spend time adding multilingual handlers for these unless a customer proves a real need:

- Security roles and field security profiles: Microsoft says these are administrator-only and do not need multiple language versions.
- SDK message processing steps and service endpoints: Microsoft says they do not expose localizable text to users.
- Reports, email templates, mail merge templates, article templates, and dialogs: Microsoft recommends separate components/solutions per language because they can contain a large amount of free-form text.
- OOB Microsoft system labels: language packs handle default Microsoft text. This app should translate customizations and overridden/custom labels, not Microsoft-owned defaults.

## Proposed Implementation Plan

### Phase 1 - Build Translation Package Service

Create a reusable browser-side service around Dataverse translation package operations:

1. Call `ExportTranslationRequest` for the selected unmanaged solution.
2. Use JSZip to read `[Content_Types].xml` and `CrmTranslations.xml`.
3. Parse worksheets needed by Dataverse translation export:
   - `Information`
   - `Display Strings`
   - `Localized Labels`
4. Convert rows into the same grid record model used by existing handlers.
5. On save, update the XML, zip both required root files, call `ImportTranslationRequest`, then publish.
6. Reuse the existing ribbon-style async publish banner/guard for Save/Load.

This service should be private/internal at first, then used by modern commands, entity messages, and business rule messages.

### Phase 2 - Implement 16. Modern Commands

1. Create real test data:
   - one modern command on main grid
   - one modern command on main form
   - one replaced/customized OOB command if possible
   - one dropdown/group command
2. Export translations and confirm rows for label, tooltip, description, accessibility, and group title.
3. Decide save path:
   - Prefer translation package import if rows map cleanly and layering works.
   - Use direct `SetLocLabels` only after verifying Dataverse runtime and solution layering.
4. Implement UI as command parent nodes with child rows.
5. Keep it out of All-In-One until the UX and save behavior are stable.

### Phase 3 - Implement 17. Entity Messages

1. Create/modify an OOB system table message in a dev solution.
2. Export translations and map message rows.
3. Build grid around message key/default/custom/published text.
4. Save through translation package import.
5. Warn users that message text can be broad and should be reviewed carefully before saving.

### Phase 4 - Implement 18. Business Rule Messages

1. Create a business rule with "Show Error Message".
2. Export translations and confirm the generated label rows.
3. Build a handler filtered by selected entity and solution.
4. Save through translation package import.

### Phase 5 - Coverage Audit

Add a developer/admin-only "coverage audit" action:

1. Export translations.
2. Parse all rows.
3. Compare with currently supported handlers.
4. Report uncovered component families and counts.

This becomes the long-term way to keep the translator aligned with Microsoft platform changes.

## Research Sources

- Microsoft Learn: [Create solutions that support multiple languages](https://learn.microsoft.com/en-us/power-platform/alm/create-solutions-support-multiple-languages)
- Microsoft Learn: [Translate localizable text for model-driven apps](https://learn.microsoft.com/en-us/power-apps/maker/model-driven-apps/translate-localizable-text)
- Microsoft Learn: [Modern commanding overview](https://learn.microsoft.com/en-us/power-apps/maker/model-driven-apps/command-designer-overview)
- Microsoft Learn: [Customize the command bar using command designer](https://learn.microsoft.com/en-us/power-apps/maker/model-driven-apps/use-command-designer)
- Microsoft Learn: [Manage commands in solutions](https://learn.microsoft.com/en-us/power-apps/maker/model-driven-apps/manage-commands-in-solutions)
- Microsoft Learn: [A button on the command bar has wrong labels or translations](https://learn.microsoft.com/en-us/troubleshoot/power-platform/power-apps/create-and-use-apps/ribbon-issues-button-wrong-label)
- Microsoft Learn: [Use localized labels with ribbons](https://learn.microsoft.com/en-us/power-apps/developer/model-driven-apps/use-localized-labels-ribbons)
- Microsoft Learn: [App Action table reference](https://learn.microsoft.com/en-us/power-apps/developer/data-platform/reference/entities/appaction)
- Microsoft Learn: [Edit system table messages](https://learn.microsoft.com/en-us/power-apps/maker/data-platform/edit-system-entity-messages)
- Microsoft Learn: [Display String table reference](https://learn.microsoft.com/en-us/power-apps/developer/data-platform/reference/entities/displaystring)
- Microsoft Learn: [Create a business rule in Dataverse](https://learn.microsoft.com/en-us/power-apps/maker/data-platform/data-platform-create-business-rule)
- Microsoft Learn: [Custom API localized label values](https://learn.microsoft.com/en-us/power-apps/developer/data-platform/custom-api#localized-label-values)
- Microsoft Learn: [Label complex type](https://learn.microsoft.com/en-us/power-apps/developer/data-platform/webapi/reference/label?view=dataverse-latest)
- Microsoft Learn: [Localize labels and strings on a custom page](https://learn.microsoft.com/en-us/power-apps/maker/model-driven-apps/custom-page-localize)
- Microsoft Learn: [String RESX web resources](https://learn.microsoft.com/en-us/power-apps/developer/model-driven-apps/resx-web-resources)
