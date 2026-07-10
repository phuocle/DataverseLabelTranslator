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
