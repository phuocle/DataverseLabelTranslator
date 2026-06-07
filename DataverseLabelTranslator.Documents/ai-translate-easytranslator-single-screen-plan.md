# AI Translate Single-Screen Redesign Via EasyTranslator

## Summary

Create a new AI Translate workspace under `EasyTranslator` and leave the old `TranslationHandler` popup flow intact. On the main toolbar AI Translate click, route by the current Type menu display name:

- `Sitemap`
- `Dashboards`
- `Web Resources`
- `Global Option Set`

Those four type display names open the new one-screen UI. All other types continue to call the existing AI Translate flow unchanged.

The first implementation pass must be code + deploy + manual validation only. Do not write unit tests until anh P has manually tested and confirmed the flow.

## Current Flow To Replace For Types 15-18

Current AI Translate flow:

1. Show AI translate options popup.
2. User selects source, target, provider, mode, dictionary option.
3. User clicks OK.
4. Show record selector popup.
5. User clicks OK.
6. Run translate.
7. Show translated results popup.
8. User clicks Apply.
9. Fill translated values back into the main grid.

New flow for types 15-18:

1. User clicks AI Translate.
2. Open one maximized AI workspace popup with a spinner and `Loading ...`.
3. Preload language columns, provider list, prepared datasource rows, and workspace session state.
4. After preload completes, render the AI workspace grid and toolbar.
5. User sets options in the workspace toolbar.
6. User reviews and edits rows in the workspace grid.
7. User clicks Translate to fill suggested target-language cells.
8. User clicks Apply to copy changed workspace cells back to the main grid.

## Routing Rules

Add a new `EasyTranslator.ShowAITranslate()` entrypoint.

The main toolbar AI Translate button should call only this new entrypoint. `EasyTranslator.ShowAITranslate()` decides whether to use the new or old flow:

- Normalize the selected Type menu display name.
- If the normalized display name is `Sitemap`, `Dashboards`, `Web Resources`, or `Global Option Set`, open the new workspace.
- Otherwise call the existing `TranslationHandler.ShowTranslationPrompt()`.

Important constraints:

- Use the Type menu display name, not the internal type id, for routing.
- Strip ordering prefixes, compacted text, parenthetical suffixes, and HTML tags before comparison.
- Do not rewrite or refactor old `TranslationHandler` AI flow internals.
- The only allowed old-flow touchpoint is the minimal toolbar dispatcher change needed to reach `EasyTranslator.ShowAITranslate()`.

## New Client Module

Create a new web resource JavaScript module:

```text
DataverseLabelTranslator.WebResource/js/AiTranslate.js
```

Load it after `EasyTranslator.js` and before or near `TranslationHandler.js` in `html/App.html`.

The new module is a black-box AI translate workspace. It accepts a prepared datasource and owns only workspace behavior:

- popup creation
- workspace grid creation
- workspace toolbar
- translation validation
- calling the new server-side translate custom action
- writing suggested values into the workspace grid
- returning workspace changes through the datasource apply callback

The new module must not contain type-specific logic or hardcoded references to `Sitemap`, `Dashboards`, `Web Resources`, `Global Option Set`, option sets, dashboards, web resources, or sitemap rows. It should not know where records came from.

Type-specific collection belongs outside the workspace, in the EasyTranslator entrypoint/datasource builder. That builder prepares a neutral datasource with only rows, language columns, and apply callbacks.

Keep `TranslationHandler.ShowTranslationPrompt`, `ShowRecordSelector`, `ProposeTranslations`, `ShowTranslationResults`, and `ApplyTranslations` untouched unless a later task explicitly asks to refactor them.

## EasyTranslator Wrappers

Extend `EasyTranslator.js` only with thin wrapper methods required by the new module. These wrappers should call existing `XrmTranslator` methods and should not add business behavior.

Required wrapper capabilities:

- get current main grid
- get current type id
- get current type display text
- get current component
- get current columns
- get all records
- get selected record ids
- get record by `recid`
- get attribute/metadata by id when needed for global option set labels
- apply a changed value to the main grid
- refresh a main-grid row
- refresh the main grid
- set main Save button disabled/enabled
- check pending main-grid changes
- get base language
- lock/unlock the main grid
- call the shared error handler

## Server-Side Translate Custom Action

Move AI provider execution and API key access to the server.

Reason:

- API keys must not be exposed to browser/client JavaScript.
- The client should send only translation request data and provider selection.
- The server reads provider settings/API keys and calls the provider API from server-side code.

Create a new server-side custom action handler. Preferred shape:

- Add a new action name, for example `AiTranslate`, to `DataverseLabelTranslator.Server/CustomActions/ActionNames.cs`.
- Register it in `PostDataverseLabelTranslatorCustomActionSynchronous`.
- Implement `DataverseLabelTranslator.Server/CustomActions/Synchronous/AiTranslate.cs`.
- Use the existing typed custom action pattern: `type = "Other"` and `operation = "Translate"`.

Client request payload:

```json
{
  "type": "Other",
  "operation": "Translate",
  "provider": "google",
  "fromLanguage": "en",
  "toLanguage": "ja",
  "fromLcid": "1033",
  "toLcid": "1041",
  "useDictionary": true,
  "items": [
    {
      "text": "Name"
    },
    {
      "text": "Description"
    }
  ]
}
```

Server response payload:

```json
{
  "operation": "Translate",
  "translations": ["Translated name", "Translated description"],
  "results": [
    {
      "translation": "Translated name",
      "usedDictionary": true
    },
    {
      "translation": "Translated description",
      "usedDictionary": false
    }
  ]
}
```
```

Server behavior:

- Validate operation is `Translate`.
- Validate provider, source language, target language, source LCID, target LCID, and items.
- Load provider configuration and API key server-side.
- Load dictionary web resource server-side.
- If request-level `useDictionary` is true, match dictionary first by source text and target LCID.
- Remove dictionary matches from the AI batch before calling the provider.
- Preserve original item order in the final `translations` and `results` response.
- Reject disabled or incomplete provider configuration with a clear error.
- Call the selected provider from server-side code.
- Return translations in the same order as the input items.
- Throw a clear error when provider response count does not match phrase count.

Provider support:

- Google/Gemini
- OpenAI-compatible
- Azure/OpenAI-compatible with `api-key` header

Client behavior:

- The new EasyTranslator workspace calls this custom action instead of calling provider URLs directly.
- Client must not read or send API keys.
- Client may still read non-secret provider display/selection data if needed for dropdown labels, but the actual credential is server-only.
- Dictionary lookup must also run server-side so the AI workspace does not preload dictionary data or send dictionary cache back to the server.

## New Workspace UI

Open one `w2popup` and maximize it immediately on open.

Initial popup state:

- show only a centered spinner/loading state
- loading text must be exactly `Loading ...`
- do not render the final toolbar/grid until all required workspace data is loaded
- if preload fails, keep the popup open and show the error through the existing alert/error style

Inside the popup, render a single `w2grid` named specifically for this feature, for example `easyAiTranslateGrid`.

Workspace toolbar controls:

- Source language dropdown
- Target language dropdown
- Provider dropdown
- All Missing toggle
- Use Dictionary toggle
- right-aligned `Translate` button
- `Apply` button beside Translate
- `Close` button

Defaults:

- All Missing toggle is off.
- Off means All Overwrite.
- Use Dictionary toggle is on by default.
- Apply is disabled until the workspace grid has changed LCID cells.

Workspace grid columns:

1. `Location`
2. `Schema Name`
3. Base language LCID column
4. All non-base language LCID columns
5. `Use Dictionary` readonly checkbox column, populated from server response `usedDictionary`

Dictionary usage for the Translate request is controlled only by the toolbar toggle. The grid column is an output indicator that tells the client which returned text actually used dictionary.

Language columns are editable, including the base language column and all non-base language columns.

## Startup Loading Flow

When the new workspace opens, preload everything required for a complete first render before showing the actual grid.

Required preload data:

- current main-grid records and current type/component state
- base language
- non-base language columns
- full language column display names
- available AI providers, including Azure provider if configured
- selected/default provider
- prepared neutral datasource rows
- temporary in-memory workspace state for row mapping, row eligibility, and pending workspace changes

The final workspace grid should appear only after this preload completes.

Temporary memory/cache expectations:

- keep an in-memory map from workspace row `recid` to original main-grid `recid`
- keep an in-memory map of LCID columns and display names
- keep current toolbar state in memory for source language, target language, provider, All Missing, and Use Dictionary
- cache is popup-session scoped only; closing the popup clears it

## Record Collection Rules

The new workspace only handles type display names 15-18.

### Type 15: Sitemap

Include translatable sitemap node rows.

Exclude sitemap parent container rows.

Both Display Text and Description should use sitemap node rows, because the nodes are the actual editable rows.

### Type 16: Dashboards

Include dashboard rows directly.

Dashboards intentionally do not load child rows in the current handler. The parent dashboard rows are the editable translation rows.

### Type 17: Web Resources

Include web resource key child rows only.

Exclude web resource group parent rows.

### Type 18: Global Option Set

For Display Text:

- include option item child rows only
- exclude logical-name parent rows

For Description:

- include logical-name parent rows
- include option item child rows

This preserves the current distinction where Global Option Set Description has editable parent rows but Display Text should not treat parent rows as translatable labels.

## Filtering Rules

The workspace grid should rebuild or filter when these toolbar values change:

- Source language
- Target language
- All Missing toggle
- current main type/component state, when the popup is reopened

All Overwrite mode:

- include rows with source text

All Missing mode:

- include rows with source text
- include only rows where the selected target language is empty

Use Dictionary is a single workspace option:

- the toolbar toggle controls dictionary usage for the current Translate run
- default value is checked
- when unchecked, the server skips dictionary lookup and sends all eligible text to AI

## Translate Flow

Validate before translating:

- main grid is loaded
- source language is selected
- target language is selected
- source and target are different
- target language column exists in the workspace grid
- provider is selected
- selected provider is enabled and configured on the server
- at least one eligible row exists after filtering

If validation fails, show an error using `Helper.ShowError` and do not call the server.

Translate behavior:

- translate one target language per click
- if user has three target languages, they select one target and click Translate three separate times
- read source text from the selected source language column
- skip rows with empty source text
- send every eligible row to the server as an item with `text`, plus request-level `useDictionary`
- server performs dictionary-first matching
- server removes dictionary matches from the AI provider batch
- when `Use Dictionary` is unchecked, all eligible rows go directly to AI inside the server action
- server returns final translations and `usedDictionary` flags in item order
- AI results write the selected target language cell in the workspace grid
- server `usedDictionary` flags update the readonly `Use Dictionary` checkbox column
- translated values are stored as workspace grid changes, not directly in the main grid

Provider execution must happen through the new server-side translate custom action. The new module should not call AI provider endpoints directly and should not read API keys.

Token reduction strategy:

- never send text already translated by dictionary to AI
- client sends request-level dictionary intent only
- server groups only unmatched rows into the provider request
- server preserves original item order so returned translations map back correctly
- if every checked row is translated by dictionary, server skips the provider call entirely
- if some rows are dictionary matches and some are misses, server combines dictionary results and AI results before returning

## Apply Flow

`Apply` reads changed LCID cells from the workspace grid.

It must apply both:

- base language edits
- non-base language edits

Apply behavior:

- ignore non-LCID fields like `location` and `schemaName`
- find the original main-grid record by stored target `recid`
- call the main-grid change API for each changed LCID cell
- refresh affected main-grid rows
- refresh the main grid
- enable main Save when pending changes exist
- clear applied changes in the workspace grid
- keep the popup open so the user can translate another target language

Do not save to Dataverse from the AI workspace. The normal main-grid Save remains responsible for Dataverse save/publish behavior.

## Public Interfaces

New client public API:

```javascript
EasyTranslator.ShowAITranslate()
EasyTranslator.OpenAiTranslateWorkspace(dataSource)
EasyTranslator.BuildAiTranslateDataSource()
```

Neutral datasource shape:

```javascript
{
    languages: [{ lcid: "1033", text: "English (en) (1033)" }],
    baseLcid: "1033",
    rows: [
        {
            recid: "easy_ai_0",
            targetRecid: "original-main-grid-recid",
            location: "Parent or grouping text",
            schemaName: "Editable row key",
            "1033": "Name",
            "1041": ""
        }
    ],
    applyChanges: function (changes) {}
}
```

New server API:

```javascript
Helper.ExecuteTypedCustomAction("AiTranslate", Helper.CustomActionTypes.Other, {
    operation: "Translate",
    provider: providerId,
    fromLanguage: fromIso,
    toLanguage: targetIso,
    fromLcid: sourceLcid,
    toLcid: targetLcid,
    useDictionary: useDictionary,
    items: items
})
```

Optional test-facing API, only after manual testing is approved:

```javascript
EasyTranslator.AiTranslateDataSource
```

No Dataverse table schema changes are required.

## Implementation And Manual Verification First

Do not write unit tests in the first implementation pass.

First implementation pass should do only:

- code the new client workspace
- code the new server-side translate custom action
- run local build/lint checks needed to avoid obvious syntax/build failures
- deploy server components
- deploy web resources
- ask anh P to test manually

After anh P confirms manual testing is OK, write unit tests in a later task.

Manual test request after deploy:

```text
anh P test láº¡i giÃºp em mÃ n AI Translate má»›i cho Sitemap, Dashboards, Web Resources, Global Option Set.
```

Manual scenarios for anh P:

- AI Translate opens new single-screen workspace for type display names `Sitemap`, `Dashboards`, `Web Resources`, and `Global Option Set`.
- Popup initially shows spinner text `Loading ...`, then shows the workspace only after preload completes.
- Workspace preload includes base language, non-base language columns, provider list, Azure provider if configured, prepared datasource rows, and popup-session state.
- Other type display names still open the old AI Translate flow.
- Source/Target/Provider validation reports clear errors.
- All Overwrite is default.
- All Missing toggle filters only missing target rows.
- Use Dictionary is checked by default and applies to the whole Translate run.
- Dictionary matches are resolved server-side and are not sent to the provider AI batch.
- Translate fills only the selected target language column in the workspace.
- Apply copies changed base and non-base language cells back to the main grid.
- Main grid Save then persists the changes through existing save flow.
- Type 18 Display Text excludes logical-name parent rows.
- Type 18 Description includes logical-name parent rows.

Quality gates before asking anh P to test:

```powershell
npm --prefix DataverseLabelTranslator.WebResource run format -- js/EasyTranslator.js js/AiTranslate.js html/App.html
npm --prefix DataverseLabelTranslator.WebResource run lint
dotnet build DataverseLabelTranslator.Server\DataverseLabelTranslator.Server.csproj --configuration Debug
```

Because server and web resource files will change during implementation, run:

```powershell
DataverseLabelTranslator.Server\deploy.debug.bat
DataverseLabelTranslator.WebResource\deploy.debug.bat
```

Do not run or create unit tests until anh P finishes manual validation and asks for tests.

## Later Unit Test Pass

Only after manual validation is approved, add unit tests for:

- routing by Type menu display name
- unsupported display names call the old flow
- display name normalization
- record collection for Sitemap, Dashboards, Web Resources, and Global Option Set
- default workspace state
- validation errors
- dictionary checked/unchecked behavior
- server translate custom action request payload shape
- Apply copying changed base and non-base LCID cells back to the main grid

## Implementation Boundaries

In scope:

- new `EasyTranslator` AI workspace module
- thin `EasyTranslator` wrapper additions
- new script include
- minimal AI Translate toolbar dispatcher route
- new server custom action for safe server-side AI provider calls
- server-side provider/API-key handling

Out of scope:

- rewriting old `TranslationHandler` popup flow
- changing existing server CRUD/save custom actions
- changing save/publish behavior
- converting types other than 15-18 to the new workspace
- changing Display Text behavior for Global Option Set parent rows
- writing unit tests before manual validation is approved

## Assumptions

- "EasyTranslate" means the existing `window.EasyTranslator` module.
- "Do not touch old code" means do not modify old AI flow internals; one minimal toolbar click routing change is acceptable.
- The first implementation should use Type menu display names as the supported-type switch because that is the requested behavior.
- Dictionary matching belongs to the new server custom action. The new workspace should not depend on client dictionary services or private functions inside `TranslationHandler`.
- AI provider API keys must be read and used server-side only.
