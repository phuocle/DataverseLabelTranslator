# Test All In One

## Instructions for AI

This file is the consolidated test history for Dataverse Label Translator. It tracks both manual testing performed by aP (Anh Phước) and automated/unit testing implemented or executed with AI assistance.

When aP points to this file and provides a test update in chat:

- Understand the meaning of aP's message; do not copy the raw chat text verbatim.
- Rewrite it as a precise, complete test record that another developer can understand without reading the chat.
- Classify the test as `Manual` or `Unit`.
- Create a stable component key in PascalCase without spaces or punctuation, for example `Dictionary`, `GlobalOptionSet`, or `AiTranslate`.
- Treat `Key + Type` as the unique identity of a test record. This allows manual and unit-test results for the same component to be tracked separately.
- Before adding a row, search the existing table for the same key and test type.
- If a matching row exists, update that row with the latest confirmed date, result, verifier, and details. Do not append a duplicate row.
- If no matching row exists, append a new row to the end of the table.
- Reuse an existing key when aP refers to the same feature with different wording, casing, spacing, singular/plural forms, or abbreviations. For example, `global option set`, `global optionset`, and `Global Option Sets` must all resolve to `GlobalOptionSet`.
- Record the component or feature tested, the confirmed result, who performed or verified it, and a concise description of what was covered.
- Use `Passed`, `Failed`, `Partial`, or `Blocked` for the result. Include an explicitly reported coverage percentage when applicable.
- Record only facts supplied or confirmed by aP or produced by an actual test run. Do not invent test steps, results, dates, or coverage.
- If important information is missing, use `Not provided` rather than guessing.
- If aP corrects or retests an existing item, update its matching row so the table represents the latest confirmed status.
- Use the current date in `YYYY-MM-DD` format whenever creating or updating a confirmed test record.

### Checkpoint Rules

- A checkpoint is a grouped milestone that summarizes one or more completed test-history rows.
- When aP says a group of items is complete, asks to checkpoint the current test state, or confirms an AI-tested batch as ready for manual retest, create a new checkpoint section under `## Test History` and above the test-history table.
- Name checkpoint sections with the next sequential number, for example `## Checkpoint 2 - <short milestone name>`.
- Do not edit the meaning of older checkpoint sections unless aP explicitly asks to correct that checkpoint.
- Each checkpoint must include `Status`, `Date`, `Scope`, and `Summary`.
- The checkpoint scope must list the exact component names or keys covered by that milestone.
- The checkpoint summary must record only facts that were supplied by aP or produced by actual test/build output, such as JavaScript coverage, C# coverage, UI test pass status, or pending manual retest.
- If the checkpoint is created for a new item after Checkpoint 1, keep Checkpoint 1 unchanged and create Checkpoint 2 instead of appending the new item to Checkpoint 1.

### Interpretation Examples

- Input: `manually test dictionary load/edit ok`
  - Use key `Dictionary` and type `Manual`.
  - Create the row if it does not exist; otherwise update the existing `Dictionary + Manual` row.
  - Record as: Manual testing confirmed that Dictionary loading and editing both work successfully.
- Input: `Update Unit test global option set 100% by AI done`
  - Use key `GlobalOptionSet` and type `Unit`.
  - Create the row if it does not exist; otherwise update the existing `GlobalOptionSet + Unit` row.
  - Record as: AI completed unit-test coverage for Global Option Sets at 100%, and aP executed or reviewed the tests and confirmed the result.
- Later input: `unit test for global optionset done`
  - Resolve the wording to the existing key `GlobalOptionSet` and type `Unit`.
  - Find and update the existing row instead of adding another Global Option Set unit-test row.

## Test History

## Checkpoint 1 - Core translation UI automation baseline

Status: Passed

Date: 2026-07-13

Scope:

- Global Option Sets
- Web Resources
- Views

Summary:

- Checkpoint 1 confirms the first 3 Dataverse Label Translator component test items are complete.
- JavaScript and C# automated coverage for the scoped code is confirmed at 100%.
- Browser UI automation passed all cases for these 3 items.
- aP will manually retest this checkpoint as the final human verification pass.

## Checkpoint 2 - Chart translation UI automation

Status: Passed

Date: 2026-07-14

Scope:

- Charts

Summary:

- Checkpoint 2 confirms the Chart Dataverse Label Translator component test item is complete.
- JavaScript Chart-specific dashboard coverage is confirmed for the Chart unified type registration, entity-dependent type visibility, Display Text-only component behavior, and toolbar registration statements.
- C# focused verification passed all 24 `ChartAdapterTests`, with `ChartAdapter.cs` achieving 100% line, branch, and method coverage.
- The all-in-one script first built `DataverseLabelTranslator.slnx` successfully, then passed JavaScript, C# server, and browser UI automation suites.
- Browser UI automation passed for Chart Display Text only. The Chart flow edited a child chart row in English, Japanese, and Vietnamese, saved through the visible toolbar, reloaded the grid, and verified the persisted values.
- Chart has no Description UI test because Chart only supports Display Text.

| Key | Date | Type | Component / Feature | Result | Performed / Verified By | Details |
| --- | --- | --- | --- | --- | --- | --- |
| GlobalOptionSet | 2026-07-13 | Unit | Global Option Sets | Passed | AI / aP | Focused JavaScript verification passed all 31 tests and covered all 5 Global Option Set-specific statements, achieving 100% scoped coverage in the unified dashboard file. Focused C# verification passed all 15 tests, with `GlobalOptionSetAdapter` achieving 100% line, branch, and method coverage. The complete JavaScript regression suite also passed all 792 tests. Browser UI automation also passed for Global Option Set Display Text and Description: Display Text edited the child row in English, Japanese, and Vietnamese; Description edited both parent and child rows in all three languages. Both flows selected the controls through the rendered DOM, saved through the visible toolbar, reloaded the grid, and verified the persisted values. |
| WebResource | 2026-07-13 | Unit | Web Resources | Passed | AI / aP | Focused JavaScript verification passed all 793 tests and covered all 5 Web Resources-specific statements, achieving 100% scoped coverage in the unified dashboard file. Focused C# verification passed all 43 `WebResourceAdapterTests`, with `WebResourceAdapter.cs` achieving 100% line, branch, and method coverage. Browser UI automation also passed for Web Resources Display Text and Description: Display Text edited the child row in English, Japanese, and Vietnamese; Description edited the flat parent row in all three languages because Web Resources descriptions do not have child rows. Both flows selected the controls through the rendered DOM, saved through the visible toolbar, reloaded the grid, and verified the persisted values. |
| View | 2026-07-13 | Unit | Views | Passed | AI / aP | Focused JavaScript verification added View-specific dashboard coverage for unified type registration, Description support, Load payload, Save changedRows, and publish payload behavior. The complete JavaScript regression suite passed all 796 tests, and coverage audit confirmed the View-specific statements in `DataverseLabelTranslator.js` were covered. Focused C# verification passed all 12 `ViewAdapterTests`, with `ViewAdapter.cs` achieving 100% line, branch, and method coverage. The complete C# server test project also passed all 463 tests. Browser UI automation also passed for Views Display Text and Description: both flows edited child rows in English, Japanese, and Vietnamese, saved through the visible toolbar, reloaded the grid, and verified the persisted values. |
| Chart | 2026-07-14 | Unit | Charts | Passed | AI / aP | Focused JavaScript verification added Chart-specific dashboard coverage for unified type registration, entity-dependent type visibility, Display Text-only component behavior, and toolbar registration. The complete JavaScript regression suite passed all 797 tests, and coverage audit confirmed the Chart-specific statements in `DataverseLabelTranslator.js` were covered. Focused C# verification passed all 24 `ChartAdapterTests`, with `ChartAdapter.cs` achieving 100% line, branch, and method coverage. The all-in-one script built `DataverseLabelTranslator.slnx` successfully, then passed the JavaScript, C# server, and UI automation suites. Browser UI automation also passed for Charts Display Text: the test edited a child chart row in English, Japanese, and Vietnamese, saved through the visible toolbar, reloaded the grid, and verified the persisted values. Chart Description was intentionally not tested because Charts only support Display Text. |
