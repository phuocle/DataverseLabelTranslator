@if exist "%~dp0..\.env" for /f "usebackq eol=# tokens=1,* delims==" %%A in ("%~dp0..\.env") do @if not "%%~A"=="" set "%%~A=%%~B"
@cd /d "%~dp0" && devkit modelbuilder --json "%~dp0..\DynamicsCrm.DevKit.Cli.json" --profile "ALL"
