# Translation Package Audit - translate_ribbon

Generated: 2026-05-30

## Source

- Environment: `https://contoso-pl-xrm-quick-edit-dev.crm5.dynamics.com`
- Solution: `translate_ribbon`
- Exported package: `%TEMP%\dlt-translation-audit\translate_ribbon\translate_ribbon.translation.zip`
- Parsed XML: `%TEMP%\dlt-translation-audit\translate_ribbon\translate_ribbon\CrmTranslations.xml`
- Helper script: `scripts/audit-translation-package.ps1`

## XML Shape

| Worksheet | Rows | Notes |
|---|---:|---|
| `Information` | 5 | Export metadata only. Not a translation surface. |
| `Display Strings` | 1 | Header row only. No actual Entity Message rows in this export. |
| `Localized Labels` | 101 | 1 header row + 100 localizable label rows. |

## Refined Coverage Summary

| Area in XML | Rows | Current app coverage | Decision note |
|---|---:|---|---|
| Table singular/plural labels | 2 | Covered by `6. Entity Metadata` | No action. |
| Attribute/form display labels | 42 | Covered by `1. Attributes` and `3. Forms` | No action. |
| Attribute descriptions | 26 | Covered by `1. Attributes` when component is `Description` | No action. |
| Form names | 3 | Covered by `5. Form Metadata` | No action for names. |
| Form descriptions | 2 | Covered by `5. Form Metadata` | Use toolbar component `Description`; saves `systemform.description`. |
| View names | 7 | Covered by `4. Views` | No action for names. |
| View descriptions | 1 | Covered by `4. Views` | `4. Views` includes both `Name` and `Description` rows and saves `savedquery.description`. |
| Chart name | 1 | Covered by `8. Charts` | Row object is `savedqueryvisualization`. |
| Ribbon labels/tooltips | 9 | Covered by `11. Ribbons` | No action. |
| Business rule step labels | 3 | Covered by `10. Business Rules` | No action for step labels. |
| Entity Messages / Display Strings | 0 data rows | No concrete missing row in this export | Worksheet exists but contains only header. |

## Remaining Gaps

| Priority | Rows | XML area | Current gap | Example/base text |
|---|---:|---|---|---|
| P2 | 1 | `workflow.name` for business rule | `10. Business Rules` edits XAML step labels only, not workflow record name. | `New business rule` |
| P2 | 1 | `workflow.description` for business rule | `10. Business Rules` edits XAML step labels only, not workflow record description. | `Click to add description` |
| P3 | 1 | `solution.friendlyname` | No handler for solution metadata labels. | `translate-ribbon` |
| P3 | 1 | `publisher.friendlyname` | No handler for publisher metadata labels. | `PL Data` |

## Implemented P1 Coverage

The P1 gaps from the original audit are implemented in the runtime handlers:

1. `5. Form Metadata` follows the toolbar component selector. `DisplayName` retrieves and saves `systemform.name`; `Description` retrieves and saves `systemform.description`. `3. Forms` remains scoped to labels inside the form XML and does not include form metadata descriptions.
2. `4. Views` follows the toolbar component selector. `DisplayName` retrieves and saves `savedquery.name`; `Description` retrieves and saves `savedquery.description`. Description rows are rendered even when the current description label collection is empty so users can add a new translation.

## Recommendation

Keep P2/P3 out unless there is a real customer need:

- Business rule record `name`/`description` are maker/admin-facing and are less important than the step messages already covered.
- Solution and publisher friendly names are package metadata, not normal app runtime UI.

Type `13. Entity Messages` does not show a missing data issue from this particular package because `Display Strings` has no data rows. To validate type 13, export an OOB table solution where Power Apps Messages have actual customized display strings.
