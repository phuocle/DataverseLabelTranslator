@for /f "usebackq delims=" %%L in (`powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0..\DataverseLabelTranslator.Scripts\load-devkit-env.ps1" "%~dp0..\.env"`) do @%%L
@cd /d "%~dp0" && devkit webresource --json "%~dp0..\DynamicsCrm.DevKit.Cli.json" --profile "DEBUG"
