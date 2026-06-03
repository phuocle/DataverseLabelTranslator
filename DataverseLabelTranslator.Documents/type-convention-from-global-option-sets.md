# Type Convention From Global Option Set

Use this document when applying the convention proven on toolbar type 18 (`Global Option Set`) to another toolbar type.

Next planned type: `17. Web Resources`.

## Scope Rule

Change only the requested toolbar type unless the user explicitly approves a shared app-wide change.

Shared helpers are allowed when they define the cross-type contract. Current approved shared areas are:

- `DataverseLabelTranslator.WebResource/js/Helper.js` for typed custom action calls and small label/grid helpers.
- `DataverseLabelTranslator.Server/CustomActions/ICustomAction.cs` for the required server phase contract.
- `DataverseLabelTranslator.Server/CustomActions/CustomActionModels.cs` for shared custom action input/output models and type constants.

Do not move type-specific row parsing, payload construction, or grid rendering into shared helpers unless another type actually needs the same behavior.

The app reads existing Dataverse solution contents. Do not auto-create solution membership and do not automatically add components to a solution. If a missing component must be included, the user will add it.

## Server-Owned CRUD Convention

For migrated types, CRUD and publish work must live on the server. The client handler should call the synchronous custom action and use the returned object.

Client side:

- Keep the same Dataverse custom action operation: `pl_DataverseLabelTranslatorCustomAction`.
- Keep `f` as the server action name, for example `GlobalOptionSet`.
- Call `Helper.ExecuteTypedCustomAction(functionName, type, input)`.
- Do not call Dataverse Web API directly for migrated type load/save/publish operations.
- Do not call `XrmTranslator.RunTypeSaveFlow` for migrated type 18-style flows.
- The client may still build grid update payloads from changed cells before sending `Saving`.

Server side:

- Add an action name constant in `ActionNames.cs`.
- Register dispatch in `PostDataverseLabelTranslatorCustomActionSynchronous.cs`.
- Implement `ICustomAction`.
- Put type-specific synchronous action code under `DataverseLabelTranslator.Server/CustomActions/Synchronous/`.
- Return phase-specific objects through the shared output envelope.

## Required Phases

Every `ICustomAction` implementation must implement all five methods:

```csharp
object Loading(...);
object Saving(...);
object Publishing(...);
object Published(...);
object Other(...);
```

This is intentional. If a future type such as type 17 misses one method, the server project must fail to build.

Use these exact type constants:

```text
Loading
Saving
Publishing
Published
Other
```

`Other` is a controlled extension hook. Use `type: "Other"` plus `operation: "<operation-name>"` when a type needs extra server reads or commands that do not fit the four normal phases. Do not use `Other` as an unvalidated catch-all. Each action should validate `operation` before doing anything.

## Shared Models

Shared server models live in `DataverseLabelTranslator.Server/CustomActions/CustomActionModels.cs`.

`CustomActionInput` is the base input model:

```csharp
internal class CustomActionInput
{
    public string type { get; set; }
    public string operation { get; set; }
}
```

Every type-specific input class should inherit from `CustomActionInput`, for example:

```csharp
internal class LoadingGlobalOptionSetInput : CustomActionInput
{
    public string solutionId { get; set; }
}
```

The shared output envelope is:

```csharp
internal class CustomActionOutput
{
    public bool ok { get; set; } = true;
    public string message { get; set; } = string.Empty;
    public string type { get; set; } = string.Empty;
    public object @object { get; set; }
}
```

The custom action response must return JSON equivalent to:

```json
{
  "ok": true,
  "message": "",
  "type": "Saving",
  "object": {
    "optionSetNames": ["pl_globalchoice"]
  }
}
```

The dispatcher sets `type` and wraps the phase return value in `object`.

## Helper.js Convention

Use `Helper.CustomActionTypes` for phase names:

```javascript
Helper.CustomActionTypes.Loading;
Helper.CustomActionTypes.Saving;
Helper.CustomActionTypes.Publishing;
Helper.CustomActionTypes.Published;
Helper.CustomActionTypes.Other;
```

Use `Helper.ExecuteTypedCustomAction(functionName, type, input)` so the client always sends `input.type` and verifies the returned `result.type`.

Use `Helper.GetCustomActionObject(result)` to read the returned phase object safely.

Approved shared label helpers:

- `Helper.IsEmptyLabelValue(value)`
- `Helper.GetLanguageColumnText(languageCode, grid)`

Keep type-specific helpers local. For type 18, these remain local because they are Global Option Set-specific:

- `GetDisplayTextRowPath`
- `BuildOptionSetDescriptionUpdate`
- `GetUpdates`
- `FillTable`
- `GetOptionSetNames`

## Phase Responsibilities

### Loading

Server reads Dataverse data and returns the type-specific metadata needed to fill the grid.

For type 18, `Loading`:

- Receives `solutionId`.
- Reads `solutioncomponent` records for global option sets.
- Retrieves global option set metadata.
- Filters customizable global option sets.
- Returns `optionSets`.

Client then fills the grid from `result.object.optionSets`.

### Saving

Server performs Dataverse writes.

For type 18, client still extracts changed grid cells and sends:

- `component`
- `optionValueUpdates`
- `optionSetDescriptionUpdates`

Server saves option set descriptions and option value labels/descriptions, then returns changed option set names.

If there are no changes, `Saving` should return an empty update list, and the client should unlock without calling `Publishing`.

### Publishing

Server performs publish work.

For type 18, `Publishing` receives `optionSetNames` and runs `PublishXmlRequest` when there are names.

Timing rule:

- If real publish work ran, do not add artificial wait.
- If no publish work ran, wait using the server-level publishing wait variable.

For type 18 this wait is currently `PublishingWaitMilliseconds = 10000`.

### Published

Server runs post-publish wait/hook logic before the client reloads.

For type 18, `Published` currently waits using `PublishedWaitMilliseconds = 10000` and returns the option set names.

This wait is deliberately server-side because the custom action is synchronous. When the server waits, the client naturally waits.

### Other

Use only for named extra server operations:

```json
{
  "type": "Other",
  "operation": "SomeOperation"
}
```

The action implementation must validate `operation`. Placeholder implementations may echo the operation or throw `InvalidPluginExecutionException` until the operation is implemented.

## Component Names

Use these user-facing component names:

- `Display Text`
- `Description`

Internal metadata names may differ. The toolbar/component id for this user-facing concept is `DisplayText`. Each handler must map `DisplayText` to the correct backing metadata/API field, for example Dataverse metadata `DisplayName`, option set `Label`, web resource display text, or sitemap title XML.

For type 18:

- `DisplayText` maps to option metadata `Label`.
- `Description` maps to option metadata `Description`.
- Parent option set description rows are editable only for `Description`.

## Empty Cell Convention

Editable empty cells must be visibly discoverable with muted placeholder text. Readonly empty cells must use `-`.

For `Description`:

- Editable empty cell: `Add description`
- Readonly empty cell: `-`
- Clearing a description is valid and must be saved as an empty value.

For `Display Text`:

- Editable base-language empty cell: `Add display text (*)`
- Editable non-base-language empty cell: `Add display text`
- Readonly empty cell: `-`
- The `(*)` marks the base-language required constraint.

Placeholders are UI only. Never save placeholder text as a Dataverse label.

## Base Language Rule

`Display Text` in the base language is required.

Before any Dataverse write:

1. Detect whether the changed cell is `Display Text`.
2. Detect whether the changed language is the base language.
3. If the new base-language value is `null`, empty, or whitespace only, stop the save and show an error.
4. Include row context in the error so the user can find the bad cell. Prefer a path such as `<component schema/name> > <row schema/value>`.

Example:

```text
Display Text in the base language (English (en-us) (1033)) cannot be empty.
Row: pl_globalchoice > 222220000
```

`Display Text` in non-base languages may be cleared. The empty value must still be included in the save payload, for example `Label: ""`, so Dataverse receives the clear operation.

Do not use generic filtering such as `if (!value) continue` for fields where clearing is valid. Use component-aware and language-aware validation instead.

## Migrated Type Flow

Use this flow for a type after migration to the server-owned convention.

1. User selects solution/entity/type/component, then clicks `Load`.
2. The app validates required selections.
3. The app calls `XrmTranslator.CheckActiveOperationBeforeAction("Load")`.
4. The app restores language columns if needed and checks publish guards.
5. `XrmTranslator` selects the target handler and locks the UI.
6. `handler.Load()` calls `Helper.ExecuteTypedCustomAction(actionName, Helper.CustomActionTypes.Loading, input)`.
7. Server `Loading` reads Dataverse and returns `object`.
8. Client fills the grid from `Helper.GetCustomActionObject(result)`.
9. User edits cells.
10. `handler.Save()` validates changed cells before server writes.
11. `handler.Save()` calls `Saving`.
12. Server `Saving` writes Dataverse and returns changed component names/ids.
13. If no publish target is returned, client unlocks and stops.
14. Client locks with `Publishing ...` and calls `Publishing`.
15. Server `Publishing` performs publish or waits if there is no publish work.
16. Client locks with `Published` and calls `Published`.
17. Server `Published` performs post-publish wait/hook logic.
18. Client reloads by calling `Load` again.
19. Client enables Load/Save.

## Implementation Checklist

When applying this convention to another type:

1. Find the handler for the requested toolbar type.
2. Identify how the type represents `Display Text` and `Description` internally.
3. Identify which rows/cells are editable versus readonly.
4. Add placeholders only to editable empty cells.
5. Add `-` only to readonly empty cells.
6. Add base-language validation for `Display Text`.
7. Ensure non-base `Display Text` clear saves an empty value instead of being filtered out.
8. Ensure `Description` clear saves an empty value.
9. Include row context in validation errors.
10. Add an `ActionNames` constant for the type.
11. Add dispatcher mapping in `PostDataverseLabelTranslatorCustomActionSynchronous.cs`.
12. Create a server action class that implements all `ICustomAction` methods.
13. Create type-specific input DTOs inheriting `CustomActionInput`.
14. Keep CRUD/publish code on the server.
15. Update the JS handler to call `Helper.ExecuteTypedCustomAction`.
16. Keep type-specific grid parsing and rendering local unless reuse is proven.
17. Build the server project.
18. Run JS lint when JS changed.
19. Deploy server when server code changed.
20. Deploy web resources when mapped JS changed.

## Type 17 Notes

For `17. Web Resources`, use this document as the starting pattern.

Expected direction:

- Server action name should be singular and stable, for example `WebResource`.
- Move load/save/publish behavior from `WebResourceHandler.js` into a server action class.
- Keep grid rendering and changed-cell extraction in the JS handler unless another type needs the exact same logic.
- Use `Other` only for named extra web resource server operations that do not fit `Loading/Saving/Publishing/Published`.
- Preserve the same base-language `Display Text` validation and empty-cell convention already used by type 17.
