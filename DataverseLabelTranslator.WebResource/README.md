# DataverseLabelTranslator.WebResource

DynamicsCrm.DevKit WebResource project for the Dataverse Label Translator dashboard.

This folder contains the deployable Dataverse web resource files:

```text
html/  HTML web resources
js/    JavaScript web resources loaded directly by App.html
css/   CSS web resources
img/   Image web resources
tests/ Vitest unit tests
```

There is no JavaScript build step. Files under `html`, `js`, `css`, and `img` deploy directly to `pl_/DataverseLabelTranslator/`.

## Commands

```powershell
npm install
npm run lint
npm test
npm run test:coverage
```

`deploy.debug.bat` is commit-safe. Its first line loads DevKit connection settings from the repository root `.env` file through `DataverseLabelTranslator.Scripts\load-devkit-env.ps1`; its second line runs `devkit`. Values in `.env` override inherited `DEVKIT_*` environment variables for that batch run.
