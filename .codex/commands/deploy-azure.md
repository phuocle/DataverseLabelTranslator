# Deploy Azure

Run this workflow when the user asks for `Deploy Azure`.

Use:

```powershell
powershell -ExecutionPolicy Bypass -File D:\github\DataverseLabelTranslator\scripts\deploy-azure.ps1
```

If the user mentions a version:

```powershell
powershell -ExecutionPolicy Bypass -File D:\github\DataverseLabelTranslator\scripts\deploy-azure.ps1 -SolutionVersion <version>
```

For validation without upload:

```powershell
powershell -ExecutionPolicy Bypass -File D:\github\DataverseLabelTranslator\scripts\deploy-azure.ps1 -DryRun
```

Rules:

- Expected Azure user is `sales@d365iconsandtooltips.com`.
- Storage account is `ple` in resource group `SHARED`.
- Container is `dataverselabeltranslator`; script creates it if missing.
- Upload only the final all-in-one ZIP from `release\<version>\appsource\zip`.
- Do not export solution.
- Do not run Release AppSource unless the user separately asks.
- Do not paste SAS URL in chat.
- Do not stage or commit.

After success, tell the user to open:

```text
D:\github\DataverseLabelTranslator\release\<version>\appsource\zip\release.md
```

and paste the SAS URL into:

```text
Partner Center -> Technical configuration -> CRM package -> URL of your package location
```
