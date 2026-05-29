# Ribbon Save Plan

## Summary

Implement Save for `15. Ribbons` using the existing browser-only architecture: edit the loaded helper solution ZIP/XML in memory, import it back to Dataverse, call `PublishAllXmlAsync`, store the returned publish job id in browser `localStorage`, and drive the user through an import/publish status banner. No server-side C#, Custom API, Custom Action, or MCP ribbon mutation will be added.

## Key Changes

- Replace `RibbonHandler.Save()` read-only alert with a real save flow.
- Add a global Save pre-check in `XrmTranslator` before any handler `Save()` runs:
  - read the tool-owned publish job id from `localStorage`
  - query Dataverse for that async operation id and inspect `statecode` / `statuscode`
  - if the job is still non-terminal, block Save and show a publish-running error
  - if the job is terminal or no longer exists, clear `localStorage` and continue the requested Save
  - if the browser guard gets stale, clear it after 10 blocked checks so users are not stuck forever
- Keep using loaded `ribbonState.customizationsXml` and `ribbonState.zip`; do not re-export on Save.
- Collect pending changes from ribbon child rows only, using `locLabelId`, `property`, `controlId`, and language LCID fields.
- For each changed cell:
  - `null` or `undefined` becomes `""`; blank edits are saved as blank descriptions.
  - Ensure the target ribbon control has `Property="$LocLabels:<locLabelId>"` when the label did not exist before.
  - Upsert only `LocLabels/LocLabel[@Id]/Titles/Title[@languagecode]/@description`.
  - Preserve unrelated XML, other languages, commands, rules, and LocLabels.
- Replace only `customizations.xml` inside the existing JSZip object, generate base64, call `ImportSolution`, then call `PublishAllXmlAsync`.
- Capture the async publish job id returned by `PublishAllXmlAsync` and persist it to `localStorage`.
- Before modifying the ZIP, create a backup from the original loaded export:
  - original unmanaged solution ZIP as base64
  - original `customizations.xml`
  - target entity logical name
  - timestamp
  - changed row summary
- Prepare the backup ZIP file in memory before showing the Save confirmation.
- When Save is for ribbons, show a large confirmation dialog before importing:
  - `Download backup and save`: download the prepared backup immediately, then continue Save
  - `Save without backup`: continue without downloading and clearly mark this as at the user's own risk
  - `Cancel`: cancel Save and leave the grid unchanged
- If the user chooses backup download and the download/preparation fails, stop before `ImportSolution`.
- During `ImportSolution`, show an importing banner and temporarily disable Save/Load. Do not lock normal cell editing globally.
- After import completes, call `PublishAllXmlAsync`, store the async job id, and change the banner to the publish-running state.
- During publish, Save and Load stay disabled until the server job finishes. A 30-second poll re-checks the async job, decrements the recovery counter, turns the banner green when complete, then auto-hides and re-enables Save/Load.

## Public Interfaces

- `RibbonHandler.Save()` becomes functional.
- `XrmTranslator` gets global Save/Load guards that run before type-specific handlers, not only before ribbon Save.
- Add a namespaced localStorage key for publish jobs, for example `DataverseLabelTranslator.PublishXmlJob`.
- Add internal helper functions in `RibbonHandler.js` only:
  - collect changed ribbon label cells
  - find ribbon control node by `controlId`
  - ensure `$LocLabels:` attribute
  - upsert `LocLabel/Title`
  - create a downloadable backup artifact before import
  - show ribbon-specific backup confirmation before import
  - serialize, repack, import, trigger async publish, and save the publish job id
- No new web resources, no changes to command definitions, no changes to `.codex/mapping.xml`.

## Import And Publish Guard

- Every click on the global Save or Load button must check the stored publish job before it calls the current handler.
- Store only jobs created by this tool. Do not inspect or block on unrelated Dataverse system jobs.
- Store JSON under a namespaced key:

```json
{
  "jobId": "<async-operation-guid>",
  "source": "DataverseLabelTranslator",
  "operation": "PublishAllXmlAsync",
  "type": "ribbons",
  "entityLogicalName": "<entity>",
  "createdOn": "<iso-timestamp>"
}
```

- If the key exists, call Dataverse to retrieve the async operation by `jobId`.
- If Dataverse returns a non-terminal job record, show/update the publish-running banner and stop Save/Load before any handler writes data.
- If Dataverse returns a terminal job record or not found, clear the localStorage key, show a green completion banner, and continue normal interaction.
- Each blocked Save/Load check decrements the recovery counter. At 10 blocked checks, clear the local guard so a stale browser state cannot permanently disable the app.
- Ribbon Save must call `PublishAllXmlAsync` after `ImportSolution` succeeds. If the response does not contain a usable job id, show an error and do not mark the ribbon save as complete because the next Save cannot be guarded safely.
- The publish banner text must say: `Publish XML is running. Save and Load are temporarily disabled until this server job finishes. The page will unlock automatically.`
- The UI must not remain globally readonly after import or publish status changes. Save/Load are the only blocked commands during tool-owned import/publish work.

## Backup And Recovery

- Save must generate a backup before any XML mutation is imported.
- Because the app runs as a Dataverse web resource and cannot safely write to the user's filesystem, the backup will be offered as a browser download named like `ribbon-backup-<entity>-<yyyyMMdd-HHmmss>.zip`.
- The backup ZIP contains the original exported helper solution ZIP content plus a small `backup-info.json` with entity, timestamp, changed row count, and changed `locLabelId` values.
- Save prepares the backup ZIP first, then asks the user to confirm.
- If the user chooses `Download backup and save`, the app downloads the prepared backup immediately and only then continues to `ImportSolution`.
- If the user chooses `Save without backup`, the app continues without a local rollback file and the dialog must clearly say this is at the user's own risk.
- If the user chooses `Cancel`, Save is cancelled and no Dataverse write happens.
- If the browser blocks the download or backup generation throws after the backup option was selected, Save stops before `ImportSolution`.
- Recovery path is manual for MVP: import the backup ZIP back into Dataverse, then run publish all. A future version can add an in-app restore button.

## Test Plan

- Static: `node --check js/RibbonHandler.js` and `node --check js/XrmTranslator.js`.
- Manual happy path: edit Japanese/Vietnamese ribbon Text/Title/Description, Save, confirm the backup dialog shows the exact filename, choose backup download, confirm the backup downloads, confirm the importing banner appears, confirm it changes to publish-running with a job id, wait for the green completion banner, then reload and confirm values persist.
- No-backup path: edit a ribbon label, Save, choose `Save without backup`, confirm no backup download and the import/publish flow still runs.
- Async publish path: after ribbon Save, confirm `PublishAllXmlAsync` returns a job id, the app stores it in `localStorage`, and the 30-second poll clears it after Dataverse completes the job.
- Guard block path: with a stored job id that still exists on the server, click Save or Load for any type and confirm the command is blocked before the handler writes or reloads anything.
- Guard stale path: with a stored job id that no longer exists on the server, click Save/Load and confirm the app clears localStorage and re-enables Save/Load.
- Cancel path: edit a ribbon label, click Save, click `Cancel` in the backup dialog, confirm no backup download, no import, and pending grid changes remain.
- Missing description path: edit `Description` for label-only buttons, Save, reload, confirm new `ToolTipDescription` LocLabel exists and persists.
- Blank path: clear an existing translated value, Save, reload, confirm it remains blank.
- Preservation path: confirm other LCIDs on the same LocLabel are not removed.
- Backup path: confirm a backup ZIP downloads before import; simulate backup failure and confirm Save stops without import.
- Regression: confirm search box stays clean and Expand/Collapse still works after Save reload.

## Assumptions

- User accepts stale XML risk from using the loaded ZIP; no concurrent ribbon customization edits are expected between Load and Save.
- Save waits for `ImportSolution` to finish, then calls `PublishAllXmlAsync` and stores the returned job id. The browser then polls for completion instead of forcing the user to click manually.
- `PublishAllXmlAsync` is available in the Web API metadata. Implementation must read the async operation GUID from the actual action response shape.
- The publish job existence check uses the stored async operation GUID against Dataverse system jobs/`asyncoperation`.
- User must wait a few minutes for Dataverse Publish XML to complete before saving/loading again. Any Save or Load while the stored publish job still exists on the server is blocked.
- MVP recovery is manual import of the downloaded backup ZIP; no in-app restore flow in the first Save implementation.
- Only classic `RibbonDiffXml` labels are in scope; modern command designer / Power Fx command metadata is out of scope.
- OOB labels not represented as editable `$LocLabels:` inside the exported `RibbonDiffXml` remain out of scope.
