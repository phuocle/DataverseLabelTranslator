# Type Convention From Global Option Sets

Use this document when the user asks to apply the convention proven on `18. Global Option Sets` to another toolbar type, for example: "read this file and apply the same convention to type 1".

## Scope Rule

Change only the requested toolbar type unless the user explicitly approves a shared app-wide change.

If a shared helper is useful, gate behavior by the target type/component so other types do not change accidentally. Prefer type-handler changes first, then small shared helpers only when the type needs rendering or save-flow support that cannot live locally.

The app reads existing Dataverse solution contents. Do not auto-create solution membership and do not automatically add components to a solution. If a missing component must be included, the user will add it.

## Component Names

Use these user-facing component names:

- `Display Text`
- `Description`

Internal metadata names may differ. Preserve existing internal ids unless every caller, grid selector, and save payload mapper is updated. For type `18. Global Option Sets`, the internal component id remains `DisplayName`, and the handler maps it to Dataverse `Label`.

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

## Type Flow

Use this flow for a type from load through reload.

1. User selects solution/entity/type/component, then clicks `Load`.
2. Load validates required selections.
3. Load calls `XrmTranslator.CheckActiveOperationBeforeAction("Load")`.
4. Load restores language columns if needed and checks any stored publish guard.
5. Load selects the target handler and calls `XrmTranslator.LockGrid("Loading ...")`.
6. `XrmTranslator.LockGrid` must lock both the grid and toolbar.
7. `handler.Load()` fetches Dataverse data, builds records, sets editable/readonly state, applies empty-cell placeholders, and fills the grid.
8. After data is filled, the handler unlocks through `XrmTranslator.UnlockGrid()` so both the grid and toolbar become usable again.
9. User edits cells. Empty cells that are editable must show the placeholder convention above. Empty cells that are readonly must show `-`.
10. User clicks `Save`.
11. Save normalizes grid changes, refreshes changed rows, disables Save, checks active operation state, and checks pending publish state.
12. If there are no pending changes, show `No changes to save.` with the same success styling family as `Published`, then re-enable Load/Save.
13. If there are changes, the type handler should use `XrmTranslator.RunTypeSaveFlow`.
14. `saveAction` validates changes first. Base-language `Display Text` empty errors must stop before any Dataverse write.
15. `saveAction` builds payloads. Include empty strings for user-cleared fields when clearing is valid.
16. Execute Web API save requests.
17. If no update payloads were built, `shouldPublish` returns false and the app does not publish or reload.
18. If updates were saved, show `Publishing ...` with the same success styling family as `Published`, but use a distinct publishing icon.
19. Keep the `Publishing ...` banner visible for at least 5 seconds, even if the publish action is fast or is only a no-op placeholder for that type.
20. Run the targeted publish action for the type. If the type has no real publish action, use a resolved/no-op publish action but keep the same publishing UX.
21. Show `Published ...` with the `OK` icon.
22. Keep the `Published ...` banner visible for at least 5 seconds before reload starts.
23. Do not let `Published` inherit the `Publishing` icon; set or preserve the `OK` icon explicitly.
24. Reload the type so the grid reflects Dataverse after publish.
25. Clear operation status and leave Load/Save enabled.

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
10. Verify load, edit, save, publishing, published, and reload behavior.
11. Deploy changed mapped web resources when the user needs to test in Dataverse.
