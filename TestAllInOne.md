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

| Key | Date | Type | Component / Feature | Result | Performed / Verified By | Details |
| --- | --- | --- | --- | --- | --- | --- |
| GlobalOptionSet | 2026-07-13 | Unit | Global Option Sets | Passed | AI / aP | Focused JavaScript verification passed all 31 tests and covered all 5 Global Option Set-specific statements, achieving 100% scoped coverage in the unified dashboard file. Focused C# verification passed all 15 tests, with `GlobalOptionSetAdapter` achieving 100% line, branch, and method coverage. The complete JavaScript regression suite also passed all 792 tests. Browser UI automation also passed for Global Option Set Display Text and Description: Display Text edited the child row in English, Japanese, and Vietnamese; Description edited both parent and child rows in all three languages. Both flows selected the controls through the rendered DOM, saved through the visible toolbar, reloaded the grid, and verified the persisted values. |
| WebResource | 2026-07-13 | Unit | Web Resources | Passed | AI / aP | Focused JavaScript verification passed all 793 tests and covered all 5 Web Resources-specific statements, achieving 100% scoped coverage in the unified dashboard file. Focused C# verification passed all 43 `WebResourceAdapterTests`, with `WebResourceAdapter.cs` achieving 100% line, branch, and method coverage. Browser UI automation also passed for Web Resources Display Text and Description: Display Text edited the child row in English, Japanese, and Vietnamese; Description edited the flat parent row in all three languages because Web Resources descriptions do not have child rows. Both flows selected the controls through the rendered DOM, saved through the visible toolbar, reloaded the grid, and verified the persisted values. |
| View | 2026-07-13 | Unit | Views | Passed | AI / aP | Focused JavaScript verification added View-specific dashboard coverage for unified type registration, Description support, Load payload, Save changedRows, and publish payload behavior. The complete JavaScript regression suite passed all 796 tests, and coverage audit confirmed the View-specific statements in `DataverseLabelTranslator.js` were covered. Focused C# verification passed all 12 `ViewAdapterTests`, with `ViewAdapter.cs` achieving 100% line, branch, and method coverage. The complete C# server test project also passed all 463 tests. Browser UI automation also passed for Views Display Text and Description: both flows edited child rows in English, Japanese, and Vietnamese, saved through the visible toolbar, reloaded the grid, and verified the persisted values. |
