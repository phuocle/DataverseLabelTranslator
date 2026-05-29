# Type 17 Entity Messages Implementation Plan

## Goal

Add a new translation type named **17. Entity Messages** for Dataverse system table messages / display strings.

This type is entity-scoped and solution-scoped, but it must **not** be added to **0. All-In-One**.

Important numbering note: the current codebase may already have a `15. Ribbons` item. This work packet follows the requested future numbering. Before merging code, reconcile the menu numbering with the product owner. Do not delete the existing ribbon handler unless explicitly approved.

## Microsoft References

- [Edit system table messages](https://learn.microsoft.com/en-us/power-apps/maker/data-platform/edit-system-entity-messages)
- [Edit system entity messages and display names](https://learn.microsoft.com/en-us/dynamics365/customerengagement/on-premises/customize/edit-system-entity-messages?view=op-9-1)
- [Display String table reference](https://learn.microsoft.com/en-us/power-apps/developer/data-platform/reference/entities/displaystring)
- [Translate localizable text for model-driven apps](https://learn.microsoft.com/en-us/power-apps/maker/model-driven-apps/translate-localizable-text)
- [Create solutions that support multiple languages](https://learn.microsoft.com/en-us/power-platform/alm/create-solutions-support-multiple-languages)
- [ExportTranslationRequest](https://learn.microsoft.com/en-us/dotnet/api/microsoft.crm.sdk.messages.exporttranslationrequest?view=dataverse-sdk-latest)
- [ImportTranslationRequest](https://learn.microsoft.com/en-us/dotnet/api/microsoft.crm.sdk.messages.importtranslationrequest?view=dataverse-sdk-latest)

Microsoft describes table/entity messages as text shown in the app that may continue to use old system table names after a display name change. These messages are translated through the standard solution translation export/import flow.

## Why Direct `displaystring` Query Is Not Enough

The `displaystring` table stores custom/published display strings, but Microsoft notes that it does not contain all default display strings. Many default messages do not exist as editable `displaystring` records until customized.

Therefore:

- Do not build the first implementation by querying `displaystring` only.
- Use `ExportTranslationRequest` and parse `CrmTranslations.xml`.
- Use `ImportTranslationRequest` to save.

Direct `displaystring` reads may still be useful as supplemental metadata, but not as the source of truth.

## Scope

Translate entity/table message text for the selected entity:

- Default display text from the translation package.
- Existing custom display text.
- Existing published display text where exposed.
- Target language display string cells.

This should primarily help OOB/system tables such as Account, Contact, Lead, Opportunity, Sales Order, etc. Custom tables may have few or no entity messages; the handler should handle that cleanly.

## Required Spike Before Coding

Use a dev solution and an OOB table such as Account:

1. Add the table to an unmanaged solution.
2. Open the Power Apps **Messages** page for the table.
3. Change one message custom display text to a unique value, for example `PL_ENTITY_MESSAGE_TEST_001`.
4. Export translations.
5. Unzip the translation package.
6. Inspect `CrmTranslations.xml`.
7. Record exactly where rows appear:
   - worksheet: likely `Display Strings`
   - row keys
   - columns for default/custom/published/comment
   - columns for target languages
   - whether entity logical name or object id is present

The implementation must be based on the observed row shape.

## Dependency

Reuse `js\TranslationPackageService.js` from Type 15 Business Rules. If Type 15 has not implemented it yet, build it here with the same contract:

- export solution translations
- parse `CrmTranslations.xml`
- update target-language cells
- import translations
- publish safely

Save safety rule:

- Re-export a fresh translation package immediately before save.
- Apply only changed Entity Messages rows to the fresh package.
- Import and publish.

## Handler Design

Create `js\EntityMessageHandler.js`.

Public methods:

- `EntityMessageHandler.Load()`
- `EntityMessageHandler.Save()`
- `EntityMessageHandler.SaveOnly()`

`Load()`:

1. Require selected solution and selected entity.
2. Export translations for the selected solution.
3. Parse `CrmTranslations.xml`.
4. Filter Display String rows belonging to the selected entity.
5. Populate a flat grid or grouped grid depending on export shape.
6. Store original row identifiers in `XrmTranslator.metadata`.

`SaveOnly()`:

1. Collect changed language cells.
2. If there are no changes, return a resolved promise.
3. Re-export a fresh package.
4. Locate the same rows by stable identifiers.
5. Apply changed cells.
6. Import translations.
7. Publish.

`Save()`:

1. Lock grid.
2. Call `SaveOnly()`.
3. Reload after publish completes or after import if publish is async-guarded globally.

## Grid Shape

Recommended flat shape:

```text
<Message display text or message key>
```

Columns:

- `schemaName`: friendly message text/key
- `defaultText`: readonly, if available
- `customText`: readonly or editable only if proven safe
- one column per installed target language

Alternative grouped shape if export rows contain clear categories:

```text
Account message category
  A parent account or parent contact is present.
  Add your Microsoft Outlook contacts to Microsoft Dynamics 365.
```

Hidden metadata on records:

- `_isEntityMessageRow = true`
- `_entityLogicalName`
- `_translationWorksheet`
- `_translationRowKey`
- `_displayStringId` if known
- `_messageKey` if known
- `_comment` if present

Do not use the visible text as the only key because many messages can have similar text.

## UI Rules

- The handler should show source/default text clearly enough that a user understands what they are translating.
- Do not expose technical ids unless needed for debugging.
- Keep message comments/notes if the export has them.
- Very long messages should be supported without breaking the grid.
- No All-In-One integration.

## Files To Update

- `html\App.html`
  - add `EntityMessageHandler.js`
- `.codex\mapping.xml`
  - add `EntityMessageHandler.js`
- `js\XrmTranslator.js`
  - add menu item `17. Entity Messages`
  - add type id to entity-dependent type list
  - route type to `EntityMessageHandler`
  - update help/about text
- `README.md`
  - update after implementation

If not already added by Type 15:

- `js\TranslationPackageService.js`
- `.codex\mapping.xml` mapping for that service
- script tag in `html\App.html`

## Save And Publish UX

Entity Messages use translation import and publish. Reuse the same guarded UX pattern:

- Disable Save and Load while import/publish is running.
- Show status banner.
- Store any async publish job id in `localStorage`.
- Poll until the job completes.
- Unlock automatically when complete.
- Keep the stale-lock recovery mechanism.

Unlike ribbon save, Entity Messages do not need a custom backup download dialog unless the implementation is editing solution XML directly. Export/import translation package is the backup-friendly, official path.

## Explicit Non-Goals

- Do not translate Microsoft-owned default language pack text outside selected solution customizations.
- Do not patch `displaystring` rows directly as the primary save path.
- Do not include Entity Messages in All-In-One.
- Do not add SDK Messages or Custom APIs to this handler.
- Do not alter table display names; this handler is for messages/display strings only.

## Edge Cases

- Custom table has no entity messages.
- OOB table has many default messages but no customized rows.
- A message has default text but no custom/published text.
- A user changed entity display names but messages still use old names.
- A message row changes between load and save.
- Exported translation package does not contain enough row identity to filter by entity; if so, add a spike result and stop before guessing.

## Verification Checklist

Static checks:

```powershell
node --check js\EntityMessageHandler.js
node --check js\XrmTranslator.js
```

Functional checks:

1. Use Account or another OOB table with visible Messages.
2. Load `17. Entity Messages`.
3. Confirm rows match the Power Apps Messages page/export.
4. Edit target language cells.
5. Save.
6. Confirm translation import succeeds and publish completes.
7. Export translations again and verify changed cells.
8. Switch user language and verify visible message text if a runtime scenario is available.
9. Load a custom entity with no messages and confirm graceful empty state.
10. Confirm `0. All-In-One` does not include Entity Messages.

Deployment:

- Use `$pl-deploy-web-resource` for changed web resource files.
- Do not deploy unrelated PropertyEditor resources.
