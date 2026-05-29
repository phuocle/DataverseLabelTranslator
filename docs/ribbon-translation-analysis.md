# Ribbon translation analysis

## Executive summary

Ribbon and command bar text is in scope for translation, but it is not handled like the current label types in Dataverse Label Translator.

Most current handlers update normal Dataverse label metadata through `RetrieveLocLabels`, `SetLocLabels`, metadata `MergeLabels`, or specific table records. Classic ribbon text is different: editable ribbon labels live inside `RibbonDiffXml` under `<LocLabels>`, and the updated XML must be imported back through an unmanaged solution and then published.

Recommended direction:

1. Treat Ribbon/Command Bar as a separate "big translate" component, not as a small extension to `ChartHandler`, `ViewHandler`, or metadata handlers.
2. For the first implementation, support classic RibbonDiffXml labels for custom command definitions. This covers custom buttons, split buttons, flyouts, tooltip titles, tooltip descriptions, and alt text.
3. Do not claim full support for modern Power Fx commanding from the same handler. Microsoft documents the pages below as "classic commands"; modern command designer has a different model.
4. Do not use `SetLocLabels` for ribbon translations. The reliable write path is solution export, edit `customizations.xml` / `RibbonDiffXml`, import solution, publish all.
5. Use a dedicated helper solution `translate_ribbon`, equivalent to the DevKit MCP `devkit_ribbon` solution, and put it under the existing Dataverse Label Translator data publisher when available.
6. Build a local parser/updater around `RibbonDiffXml/LocLabels`, modeled after the DevKit MCP ribbon implementation.

## Official Dataverse model

Microsoft's classic ribbon documentation gives three important rules.

First, localized ribbon text is represented by `LocLabels` inside `RibbonDiffXml`. A `LocLabel` contains `Title` nodes, and each `Title` has a `languagecode` and `description`.

```xml
<RibbonDiffXml>
  <CustomActions>
    <CustomAction Id="pl.account.Sample.Form.CustomAction"
                  Location="Mscrm.Form.account.MainTab.Save.Controls._children"
                  Sequence="85">
      <CommandUIDefinition>
        <Button Id="pl.account.Sample.Form.Button"
                Command="pl.account.Sample.Form.Command"
                LabelText="$LocLabels:pl.account.Sample.Form.Button.LabelText"
                ToolTipTitle="$LocLabels:pl.account.Sample.Form.Button.ToolTipTitle"
                ToolTipDescription="$LocLabels:pl.account.Sample.Form.Button.ToolTipDescription"
                Sequence="85"
                TemplateAlias="isv" />
      </CommandUIDefinition>
    </CustomAction>
  </CustomActions>
  <LocLabels>
    <LocLabel Id="pl.account.Sample.Form.Button.LabelText">
      <Titles>
        <Title languagecode="1033" description="Sample" />
        <Title languagecode="1066" description="Mau" />
      </Titles>
    </LocLabel>
  </LocLabels>
</RibbonDiffXml>
```

Second, visible ribbon attributes reference localized labels through the `$LocLabels:` directive. The important attributes for translation are:

- `LabelText`
- `Alt`
- `ToolTipTitle`
- `ToolTipDescription`

Third, editing the ribbon is an export-edit-import-publish workflow. Microsoft documents the manual process as exporting a solution, editing `customizations.xml`, importing the ZIP, then publishing customizations.

Useful official references:

- [Use localized labels with ribbons](https://learn.microsoft.com/en-us/power-apps/developer/model-driven-apps/use-localized-labels-ribbons)
- [Export ribbon definitions](https://learn.microsoft.com/en-us/power-apps/developer/model-driven-apps/export-ribbon-definitions)
- [Export, prepare to edit, and import the ribbon](https://learn.microsoft.com/en-us/power-apps/developer/model-driven-apps/export-prepare-edit-import-ribbon)
- [RetrieveEntityRibbon Web API function](https://learn.microsoft.com/en-us/power-apps/developer/data-platform/webapi/reference/retrieveentityribbon?view=dataverse-latest)
- [ExportSolutionResponse Web API type](https://learn.microsoft.com/en-us/power-apps/developer/data-platform/webapi/reference/exportsolutionresponse?view=dataverse-latest)
- [ImportSolution Web API action](https://learn.microsoft.com/en-us/power-apps/developer/data-platform/webapi/reference/importsolution?view=dataverse-latest)

## What is actually translatable

Ribbon translation has two read models and one write model.

### Read model 1: merged runtime ribbon

`RetrieveEntityRibbon` returns compressed `RibbonXml.xml` for a table and location filter. This is useful to inventory what users see on:

- form command bars
- homepage grid command bars
- subgrid command bars

The returned XML is the merged runtime ribbon. It can include Microsoft OOB commands, managed solution commands, and unmanaged/custom commands. This is useful for discovery, but it is not the safest object to write back.

Important nuance: OOB labels may resolve through `$Resources:` or other platform resource references, not project-owned `LocLabels`. Those labels are visible in the merged ribbon but usually are not owned by our solution.

### Read model 2: editable RibbonDiffXml

The editable customization is `RibbonDiffXml` stored inside an unmanaged solution export. For entity ribbon customizations, the relevant XML is in:

```text
customizations.xml
  ImportExportXml
    Entities
      Entity
        Name = account
        RibbonDiffXml
```

This XML contains the custom deltas:

- `CustomAction`
- `HideCustomAction`
- `CommandDefinition`
- `RuleDefinitions`
- `LocLabels`

For translation, the important section is `LocLabels`.

### Write model: update solution XML and import

The safe write path is:

1. Ensure an unmanaged helper solution contains the target entity, without required subcomponents unless needed.
2. Export that solution.
3. Open the solution ZIP.
4. Parse `customizations.xml`.
5. Locate the target entity's `RibbonDiffXml`.
6. Upsert only the relevant `LocLabel/Title` nodes.
7. Repack the ZIP.
8. Call `ImportSolution`.
9. Publish all customizations.

This is exactly why Ribbon should not be implemented as another `SetLocLabels` handler. The label is not a simple attribute on a Dataverse row. It is part of XML customization payload.

## DevKit MCP implementation findings

I reviewed the DevKit MCP implementation under:

```text
D:\github\Dynamics-Crm-DevKit\v5\DynamicsCrm.DevKit.Cli\Mcp\Tools
```

Relevant files:

```text
ManageRibbonTool.cs
Ribbon\RibbonXmlHelpers.cs
Ribbon\RibbonButtonOperations.cs
Ribbon\RibbonFlyoutOperations.cs
Ribbon\RibbonSolutionFetcher.cs
Ribbon\RibbonValidation.cs
Models\RibbonButtonInfo.cs
Models\RibbonSurfaceButtons.cs
Models\ManageRibbonResult.cs
```

The MCP implementation is command customization, not translation UI, but it proves the correct mechanics.

### Solution strategy

`RibbonSolutionFetcher` uses a helper solution named `devkit_ribbon`.

It does this:

1. Finds the helper solution.
2. Removes all existing solution components from that helper solution.
3. Adds the target entity as a solution component with `DoNotIncludeSubcomponents = true`.
4. Exports the helper solution unmanaged.
5. Reads `customizations.xml` from the exported ZIP.
6. Extracts the target entity's `RibbonDiffXml`.

This avoids exporting a huge solution and keeps the update focused on one entity.

### Button inventory strategy

`ManageRibbonTool` uses `RetrieveEntityRibbonRequest` for button listing. It reads the compressed entity ribbon XML and unzips `RibbonXml.xml`.

It handles three surfaces:

```text
form      -> RibbonLocationFilters.Form
main_grid -> RibbonLocationFilters.HomepageGrid
sub_grid  -> RibbonLocationFilters.SubGrid
```

It then finds expected groups:

```text
Mscrm.Form.{entity}.MainTab.Save
Mscrm.HomepageGrid.{entity}.MainTab.Actions
Mscrm.SubGrid.{entity}.MainTab.Actions
```

For labels, it resolves:

- `$LocLabels:<id>` from the helper solution's `RibbonDiffXml/LocLabels`
- `$Resources:<id>` by falling back to the last key segment
- `{!EntityDisplayName:<entity>}` by extracting the entity name
- literal text as-is

This is useful for discovery, but for a translator we need a stronger rule: editable rows should come from project-owned `LocLabel` entries, not from fallback display strings that cannot be safely written.

### Label write strategy

`RibbonXmlHelpers.UpsertLocLabel` currently removes the whole matching `<LocLabel>` and recreates it with only one `Title` for the base LCID:

```csharp
internal static void UpsertLocLabel(XElement root, int lcid, string locLabelId, string description)
{
    var locLabelsEl = GetOrCreateElement(root, "LocLabels");

    var existing = locLabelsEl.Elements("LocLabel")
        .Where(e => string.Equals(e.Attribute("Id")?.Value, locLabelId, StringComparison.OrdinalIgnoreCase))
        .ToList();
    foreach (var e in existing) e.Remove();

    locLabelsEl.Add(new XElement("LocLabel",
        new XAttribute("Id", locLabelId),
        new XElement("Titles",
            new XElement("Title",
                new XAttribute("description", description),
                new XAttribute("languagecode", lcid)))));
}
```

That behavior is fine for "create/update a command label in the base language", but it is not enough for translation. A translation handler must preserve all existing `Title` language nodes and upsert only the edited language. Removing the whole `LocLabel` would destroy translations for other LCIDs.

The translator version should behave like:

```text
UpsertRibbonLocLabelTitle(root, locLabelId, languageCode, description)
  find or create LocLabels
  find or create LocLabel by Id
  find or create Titles
  find Title by languagecode
  if description is empty and product policy says empty means no change, skip
  otherwise set Title/@description
  preserve other Title nodes
```

### Import and publish strategy

`ManageRibbonTool` builds a solution ZIP from an embedded template, replaces the entity name and `RibbonDiffXml`, imports the solution, and starts `PublishAllXmlAsyncRequest`.

Two operational lessons are important for Dataverse Label Translator:

1. Ribbon publish is treated as `PublishAll`, not entity-scoped publish.
2. Readback immediately after import is unsafe. The MCP tool blocks readback until the async publish job reaches a terminal status.

The app currently uses `PublishXml` for some components and `PublishAllXml` for sitemap/all-in-one. Ribbon should use `PublishAllXml` and show a long-running status, but it should remain an entity-level component only.

## Current Dataverse Label Translator state

Current handlers are loaded from `html/App.html`:

```text
AttributeHandler.js
OptionSetHandler.js
GlobalOptionSetHandler.js
TranslationDictionaryService.js
TranslationHandler.js
FormHandler.js
ViewHandler.js
FormMetaHandler.js
EntityHandler.js
ChartHandler.js
ContentSnippetHandler.js
WebResourceHandler.js
BpfHandler.js
RelationshipHandler.js
SiteMapHandler.js
AllInOneHandler.js
XrmTranslator.js
```

There is no `RibbonHandler.js` today.

`AllInOneHandler.js` currently includes:

```text
attributes
options
views
formMeta
entityMeta
relationships
charts
bpf
forms handled separately
```

No ribbon entry exists in all-in-one, and the implementation plan should keep it that way. Ribbon save is solution import plus publish-all, so it should not be part of any bulk all-in-one save flow.

`XrmTranslator.js` currently exposes entity-dependent types:

```text
allInOne
attributes
options
forms
views
formMeta
entityMeta
relationships
charts
content
bpf
```

Global types:

```text
webresources
dashboards
sitemap
globalOptionSets
```

No ribbon type exists in the toolbar.

The bundled `WebApiClient.js` already has request prototypes for:

```text
RetrieveEntityRibbon
RetrieveApplicationRibbon
ExportSolution
ImportSolution
PublishAllXml
PublishXml
```

That means the missing pieces are not the Web API names. The missing pieces are:

- ZIP read/write support in browser
- robust XML parsing/update logic
- helper-solution lifecycle
- safe import/publish UX
- tests around RibbonDiffXml variants

There is no current JSZip or equivalent ZIP library in `js/` or `html/`. Without a ZIP library, browser-side solution export/import mutation is not practical.

JSZip is a third-party JavaScript library for creating, reading, and editing ZIP files in the browser. The [JSZip documentation](https://stuk.github.io/jszip/) says the manual browser install is to include either `dist/jszip.js` or the minified `dist/jszip.min.js`. In this project, `jszip.min.js` would let the web resource open the Dataverse solution ZIP returned by `ExportSolution`, read and replace `customizations.xml`, and generate the updated ZIP payload for `ImportSolution`. It is not Dataverse-specific and does not translate anything by itself; it only handles the ZIP container.

## Proposed product scope

### MVP scope

Add a new entity-dependent component:

```text
15. Ribbons / Command Bar
```

Recommended visible label:

```text
Ribbons
```

The term "Ribbon" is still the correct technical term for the XML model. The app can show a tooltip or internal comments saying it covers classic command bar/ribbon definitions.

This component is entity-level only. It requires a selected entity and must not be added to All-in-One.

MVP should support:

- entity form command bar labels
- entity main grid command bar labels
- entity subgrid command bar labels
- `Button`
- `SplitButton`
- `FlyoutAnchor`
- menu item `Button`
- `LabelText`
- `Alt`
- `ToolTipTitle`
- `ToolTipDescription`
- existing `LocLabel` entries in editable `RibbonDiffXml`
- creating missing `Title` nodes for installed languages

MVP should not support:

- modern Power Fx command designer metadata
- changing command actions, rules, sequences, icons, locations
- translating Microsoft OOB `$Resources:` labels
- global application ribbon labels
- creating brand new command buttons
- changing hidden/visible behavior
- overwriting complete `RibbonDiffXml`
- All-in-One execution

### Why not translate OOB labels first

OOB buttons often appear in the merged ribbon returned by `RetrieveEntityRibbon`, but their labels are not necessarily editable `LocLabel` entries owned by the app's solution. Many resolve through Microsoft resource keys.

Trying to turn every visible OOB button into an editable translated label would risk introducing custom overrides for platform-owned commands. That is larger and more dangerous than translating custom buttons already present in `RibbonDiffXml`.

The safe first rule:

```text
If the visible text is backed by a LocLabel inside the exported editable RibbonDiffXml, show it as editable.
If it is only visible in merged RibbonXml but not backed by editable RibbonDiffXml, show it as read-only or exclude it from MVP.
```

## Proposed technical design

### Files to add

```text
js/RibbonHandler.js
js/RibbonXmlService.js
js/ZipService.js
```

If keeping files minimal:

```text
js/RibbonHandler.js
js/jszip.min.js
```

The cleaner split is:

- `RibbonHandler.js`: UI handler contract, grid load/save, integration with `XrmTranslator`.
- `RibbonXmlService.js`: parse/update `RibbonDiffXml`, map controls to labels, preserve XML.
- `ZipService.js`: solution ZIP read/write wrapper over JSZip.

### Files to update

```text
html/App.html
js/XrmTranslator.js
.codex/mapping.xml
```

`App.html` needs script order like:

```html
<script type="text/javascript" src="../js/jszip.min.js"></script>
<script type="text/javascript" src="../js/RibbonXmlService.js"></script>
<script type="text/javascript" src="../js/ZipService.js"></script>
<script type="text/javascript" src="../js/RibbonHandler.js"></script>
```

`XrmTranslator.js` needs:

- add `type:ribbons` to `ENTITY_DEPENDENT_TYPE_ITEMS`
- add toolbar menu item as `15. Ribbons`
- add branch in `SetHandler()` to select `RibbonHandler`
- decide whether `typesWithDescription` includes ribbons

`AllInOneHandler.js` should not be updated for ribbon. Ribbon is explicitly entity-level only because save imports a solution ZIP and publishes all customizations.

### Helper solution

Use a dedicated unmanaged helper solution, equivalent to how the DevKit MCP ribbon tool uses its own `devkit_ribbon` solution.

Recommended names for this project:

```text
Display name: translate-ribbon
Unique name:  translate_ribbon
```

Use an underscore in the Dataverse unique name. The MCP tool's description says `devkit-ribbon`, but its actual code uses `devkit_ribbon` as `SOLUTION_NAME`.

The helper solution is runtime/customer-owned working storage, like the dictionary data solution. Do not reuse the main AppSource solution `DataverseLabelTranslator` for the export/edit/import cycle. The helper solution should contain only the entity being edited. This reduces ZIP size and reduces the chance of changing unrelated components.

### Helper solution publisher

Use the existing Dataverse Label Translator data publisher when it already exists.

The current dictionary bootstrap in `TranslationDictionaryService.js` does this:

```text
Base solution:     DataverseLabelTranslator
Data publisher:   <base publisher unique name>Data
Data solution:    DataverseLabelTranslatorData
Dictionary file:  pl_/DataverseLabelTranslator/data/TranslationDictionary.xml
```

Therefore Ribbon should resolve the publisher in this order:

1. Find the base solution `DataverseLabelTranslator`.
2. Read its publisher.
3. Find the dictionary/data publisher using the same convention as dictionary:
   - unique name: `<basePublisher.uniquename>Data`
   - friendly name: `<basePublisher.friendlyname> Data`
4. If that publisher exists because the user already used dictionary, reuse it.
5. If it does not exist, create it with the same prefix candidate logic used by dictionary.
6. Create or reuse helper solution `translate_ribbon` with that publisher.

This keeps all runtime/customer-owned helper artifacts under the same publisher instead of creating another publisher just for Ribbon.

Required helper solution operations:

1. Resolve or create the data publisher as above.
2. Find or create helper solution `translate_ribbon`.
3. Remove existing helper solution components.
4. Add target entity component with no required subcomponents.
5. Export unmanaged.

The MCP code uses SDK messages:

```text
AddSolutionComponentRequest
RemoveSolutionComponentRequest
ExportSolutionRequest
ImportSolutionRequest
```

The browser app already has `AddSolutionComponentRequest` in `WebApiClient`, and dictionary storage already uses it for web resources. Ribbon should use the same Web API action pattern, but with entity component type `1`.

Expected add payload:

```js
{
    ComponentId: entityMetadataId,
    ComponentType: 1,
    SolutionUniqueName: "translate_ribbon",
    AddRequiredComponents: false,
    IncludedComponentSettingsValues: null,
    DoNotIncludeSubcomponents: true
}
```

Expected remove payload:

```js
{
    ComponentId: componentId,
    ComponentType: componentType,
    SolutionUniqueName: "translate_ribbon"
}
```

The remove step should enumerate existing `solutioncomponent` rows for the helper solution, then call `RemoveSolutionComponent` for each row before adding the selected entity.

## Implementation plan split

### Part 1: Read

The read side has two possible data sources, and they are not equivalent.

| Read need | API/source | Is it enough for translation? | Notes |
| --- | --- | --- | --- |
| Show visible command inventory | `RetrieveEntityRibbon` / `RetrieveApplicationRibbon` | No | This returns the merged runtime ribbon. It is enough to list visible OOB/custom commands and surfaces, but it does not give the editable solution XML that should be saved. |
| Read editable translated labels | Helper solution export, then `customizations.xml` / `RibbonDiffXml` / `LocLabels` | Yes | This is required for real translation because the editable `Title languagecode="..." description="..."` nodes live in `RibbonDiffXml`. |

MCP confirms this split:

- `manage_ribbon(action="buttons")` uses `RetrieveEntityRibbonRequest` to read merged buttons for form, main grid, and subgrid.
- Before parsing those buttons, MCP also calls `LoadDevKitRibbonData`, which exports the helper `devkit_ribbon` solution once to resolve custom `LocLabels` and hidden actions.
- `manage_ribbon(action="detail")` does not rely on `RetrieveEntityRibbon`; it calls `RibbonSolutionFetcher.FetchExistingRibbonDiffXml`, which resets helper solution components, exports the helper solution, reads `customizations.xml`, and extracts `RibbonDiffXml`.

Decision for Dataverse Label Translator MVP:

```text
Read by exporting helper solution translate_ribbon.
Do not require RetrieveEntityRibbon for MVP.
```

Use `RetrieveEntityRibbon` only if the UI later needs runtime context, such as visible command ordering, OOB/custom flags, or merged OOB command inventory. Editable rows must come from `translate_ribbon` export and only from `RibbonDiffXml/LocLabels`.

Proposed `RibbonHandler.Load()` tasks:

1. Get selected entity from `XrmTranslator.GetEntity()`.
2. Ensure installed languages and base/user language are available from `XrmTranslator`.
3. Resolve the selected entity metadata id. This id is the `ComponentId` used when adding the table to the helper solution.
4. Ensure helper solution `translate_ribbon` exists under the data publisher.
5. Reset helper solution components:
   - query existing `solutioncomponent` rows for `translate_ribbon`
   - call `RemoveSolutionComponent` for each
   - add selected entity with `ComponentType = 1`, `AddRequiredComponents = false`, `DoNotIncludeSubcomponents = true`
6. Execute `ExportSolution` for `translate_ribbon`, unmanaged.
7. Decode the returned `ExportSolutionFile` from base64/binary. In the browser this should stay in memory; it does not need to be saved as a physical ZIP file.
8. Open the in-memory ZIP with JSZip.
9. Read `customizations.xml`.
10. Parse XML with `DOMParser`.
11. Locate target entity `RibbonDiffXml`.
12. Build a map of `LocLabel` id to `Title` nodes by language.
13. Build a map of control references in editable `RibbonDiffXml`:
    - control id
    - command id
    - control type
    - surface/location if available
    - attribute name
    - loc label id
14. Do not call `RetrieveEntityRibbon` in the MVP read path. Add it later only for read-only runtime inventory/enrichment. Do not make runtime-only `$Resources:` labels editable.
15. Create one grid row per editable loc label attribute.
16. Add summary row through `XrmTranslator.AddSummary(records)`.

The user's high-level read checklist is correct with these corrections:

- "export ra file zip" means export a ZIP payload from Dataverse. In the web resource implementation, keep it as in-memory bytes/base64 and open it with JSZip.
- Do not use command id alone as the grid key. Ribbon labels are attached to control attributes (`Button`, `SplitButton`, `FlyoutAnchor`, menu item `Button`) through `$LocLabels:<id>`. A command can be shared or can have multiple visible label-bearing controls. The safest record key is `entity + surface + controlId + property + locLabelId`.
- Keep `commandId` as a grid/display/debug field, not as the unique translation key.

Read scope assessment:

- This is medium-to-big scope.
- It does not import or publish, so it is lower risk than save.
- It still requires helper solution lifecycle, JSZip, XML parsing, and careful mapping between controls and `LocLabels`.
- Skipping runtime enrichment with `RetrieveEntityRibbon` is the recommended MVP path and is enough for translation.

Example row shape:

```js
{
    recid: "account|form|pl.account.Sample.Form.Button|LabelText|pl.account.Sample.Form.Button.LabelText",
    schemaName: "Form / Button / LabelText",
    component: "Button",
    controlId: "pl.account.Sample.Form.Button",
    commandId: "pl.account.Sample.Form.Command",
    property: "LabelText",
    locLabelId: "pl.account.Sample.Form.Button.LabelText",
    "1033": "Sample",
    "1066": "Mau"
}
```

The handler should store the exported ZIP, parsed XML, and loc label map in `XrmTranslator.metadata` or a handler-private state object. Do not re-export on save unless the user reloads.

### Part 2: Save after user edit

Implemented `RibbonHandler.SaveOnly()`:

1. Read grid changes.
2. Use the loaded helper solution ZIP/XML from the current Load session. User must reload if they want to pick up external ribbon changes.
3. Decode/open the loaded ZIP with JSZip.
4. Parse `customizations.xml` and locate target entity `RibbonDiffXml`.
5. For each changed record, find `locLabelId` and changed LCID.
6. Upsert only the `Title` node for that LCID.
7. Preserve every unrelated `LocLabel`, `Title`, `CustomAction`, command, rule, and XML attribute.
8. Serialize `customizations.xml`.
9. Replace only `customizations.xml` in the loaded solution ZIP.
10. Generate the updated solution ZIP payload.
11. Execute `ImportSolution` with:
   - `CustomizationFile`
   - `ImportJobId`
   - `OverwriteUnmanagedCustomizations = true`
   - `PublishWorkflows = true`
12. Execute `PublishAllXmlAsync`.
13. Store the returned async job id in `localStorage`, show the publish banner, and poll until Dataverse finishes the job.

The implemented flow follows the async publish pattern because ribbon publish can be slow. Save and Load are temporarily disabled while the tool-owned import/publish state is active, then the page unlocks automatically after the async job completes.

Save scope assessment:

- This is big scope and higher risk than read.
- It imports a solution and publishes all customizations.
- It needs strong error handling and should not run from All-in-One.
- It should keep a pre-save export in memory as a backup candidate. If rollback is required, the app would need an explicit "restore previous RibbonDiffXml" flow, which is another feature.
- The XML write should be surgical: only changed `Title/@description` values should change.

### Upsert algorithm

Do not use DevKit MCP's current `UpsertLocLabel` unchanged for translation. It recreates the whole `LocLabel`, which would drop other language titles.

Translation-safe algorithm:

```text
function upsertTitle(ribbonDiffXml, locLabelId, languageCode, description) {
  locLabels = getOrCreateChild(ribbonDiffXml, "LocLabels")
  locLabel = find child LocLabel where Id equals locLabelId, or create it
  titles = getOrCreateChild(locLabel, "Titles")
  title = find child Title where languagecode equals languageCode, or create it
  set title @languagecode = languageCode
  set title @description = description
}
```

Recommended empty-value policy:

- If the grid cell was untouched, do nothing.
- If the user explicitly clears a translated value, preserve an empty `description=""` only if existing app behavior allows clearing labels. Otherwise skip empty values and document that clearing a ribbon translation is not supported in MVP.

### LocLabel extraction algorithm

Parse all controls under:

```text
CustomActions/CustomAction/CommandUIDefinition/*
```

For each element where local name is:

```text
Button
SplitButton
FlyoutAnchor
MenuSection
```

Read attributes:

```text
LabelText
Alt
ToolTipTitle
ToolTipDescription
```

If the value starts with `$LocLabels:`, strip the prefix and link to that `LocLabel`.

Also scan nested menu controls:

```text
SplitButton/Menu/MenuSection/Controls/Button
FlyoutAnchor/Menu/MenuSection/Controls/Button
```

For each referenced `LocLabel`, load all installed language values:

```text
LocLabels/LocLabel[@Id='<id>']/Titles/Title[@languagecode='<lcid>']/@description
```

### Grid grouping

Use hierarchical rows to make this usable:

```text
Form
  Button: Sample
    LabelText
    ToolTipTitle
    ToolTipDescription
Homepage Grid
  Button: Export
    LabelText
Subgrid
  Flyout: Actions
    Button: Recalculate
      LabelText
      ToolTipTitle
```

If implementation needs to be simpler, flat rows are acceptable for MVP, but include these columns:

- `schemaName`
- `surface`
- `controlType`
- `controlId`
- `property`
- `locLabelId`
- one editable column per installed language

## Risks and design decisions

### Browser ZIP mutation

The current app has no build step and deploys JS directly as Dataverse web resources. Adding JSZip means either:

- commit a minified browser file under `js/`, or
- implement a tiny ZIP wrapper manually, which is not recommended.

Recommendation: vendor `jszip.min.js` as a web resource. This is consistent with the current no-build architecture.

### Import permissions

Users must have solution import/export/customization privileges. The app already restricts by role names:

```text
System Administrator
System Customizer
```

Ribbon import/export should stay behind the same role check. It may still fail if a custom security model removes import/export privileges from those roles.

### Managed layer interaction

Importing unmanaged RibbonDiffXml creates or updates unmanaged customizations above managed layers. This is expected for a translator tool, but it must be explicit in release notes and UI help.

### OOB command labels

OOB labels are visible but usually not app-owned. Treat them as read-only unless backed by editable `LocLabels` in exported `RibbonDiffXml`.

### Modern commanding

Microsoft's classic ribbon docs repeatedly note that modern commanding is a newer model. This analysis covers classic ribbon XML only. If the project needs modern Power Fx command designer support later, research it separately and add a second handler or mode.

### Publish behavior

The MCP code uses `PublishAllXmlAsyncRequest`, not entity-scoped publish. The app also uses async Publish XML for ribbon save and polls the returned async job id until Dataverse completes it.

### Concurrency

Because the workflow exports, mutates, imports, and publishes a solution, concurrent edits can overwrite each other. Mitigation:

- export on load
- warn if the page is stale for a long time before save
- require the user to reload before save if another admin changed ribbon XML after the current load
- preserve all unrelated XML nodes

### XML preservation

The XML updater must be surgical. It should only change `Title/@description` for targeted `LocLabel` IDs and language codes. It must preserve:

- element order where possible
- unknown attributes
- namespace behavior if introduced
- unrelated loc labels
- command definitions
- rule definitions
- custom actions
- hide custom actions

## Recommended implementation phases

### Phase 1: Prototype parser and document samples

Build `RibbonXmlService.js` with pure functions and sample XML fixtures.

Functions:

```text
parseRibbonLabels(ribbonDiffXml, installedLanguages)
upsertRibbonLabel(ribbonDiffXml, locLabelId, languageCode, description)
serializeRibbonDiffXml(document)
```

Test with:

- simple button
- split button with nested menu button
- flyout anchor with nested buttons
- missing `LocLabels`
- missing `Titles`
- existing multiple `Title` languages
- direct literal `LabelText`
- `$Resources:` label

### Phase 2: Read-only handler

Add `RibbonHandler.Load()` and show editable-looking rows, but disable save until the parser has been verified against real exported solutions.

This validates:

- helper solution export
- ZIP decode
- `customizations.xml` parsing
- grid UX
- row count and label quality

### Phase 3: Save custom RibbonDiffXml labels

Enable save for rows backed by `RibbonDiffXml/LocLabels`.

Write path:

```text
ExportSolution on Load -> update customizations.xml -> ImportSolution -> PublishAllXmlAsync -> poll job -> Reload when needed
```

Keep OOB/merged-only rows excluded or read-only.

### Phase 4: Entity-level hardening

Keep ribbons outside `AllInOneHandler`. Harden the standalone entity-level handler after save has been stable.

Reasons:

- solution import is slower than normal label update
- publish all can be slow
- error handling is more complex
- accidental bulk save should never import a solution unexpectedly

### Phase 5: Optional OOB override research

Research whether there is a supported pattern to override OOB command label resources by adding custom `LocLabel` references without replacing the whole command. Do not include this in MVP.

## Acceptance criteria for a future RibbonHandler

Load:

- User can select an entity and type `Ribbons`.
- App creates or reuses helper solution `translate_ribbon` under the existing data publisher when available.
- Helper solution contains only the selected entity before export.
- App lists labels from editable `RibbonDiffXml/LocLabels`.
- Rows show one column per installed language.
- Existing translations are shown for all `Title/@languagecode` nodes.
- OOB merged labels not backed by editable `LocLabels` are not silently editable.

Save:

- Only changed `Title/@description` values are modified.
- Other languages on the same `LocLabel` are preserved.
- Other ribbon XML nodes are preserved.
- The helper solution ZIP is imported successfully.
- The app publishes all customizations.
- The app reloads and shows the saved values.

Failure behavior:

- If export fails, no XML is mutated.
- If ZIP parsing fails, no import is attempted.
- If XML parsing fails, no import is attempted.
- If import fails, show the Dataverse import error and do not claim success.
- If publish fails, show that import succeeded but publish failed.

## Final recommendation

Ribbon is a valid translation surface, but it should be implemented carefully as a solution/XML translator, not as another `SetLocLabels` component.

The safest first release is:

```text
Translate existing custom classic ribbon labels that are already represented by RibbonDiffXml LocLabels.
```

Do not attempt full OOB command label override or modern commanding translation in the first version. The DevKit MCP implementation provides a strong reference for export/import/publish mechanics, but its current LocLabel upsert logic must be adapted to preserve all language `Title` nodes before it is reused for translation.
