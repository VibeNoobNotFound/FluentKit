@echo off
setlocal EnableExtensions

rem Rebuild FluentKit, regenerate the API reference, copy the skill JSON, and verify freshness.
set "CONFIGURATION=%~1"
if not defined CONFIGURATION set "CONFIGURATION=Release"
for %%I in ("%~dp0..") do set "ROOT_DIR=%%~fI"
pushd "%ROOT_DIR%"

dotnet build src\FluentKit\FluentKit.csproj -c "%CONFIGURATION%"
if errorlevel 1 goto :fail

dotnet run --project tools\FluentKit.ApiReferenceGenerator -c "%CONFIGURATION%" -- ^
  --assembly "src\FluentKit\bin\%CONFIGURATION%\net10.0\FluentKit.dll" ^
  --xml "src\FluentKit\bin\%CONFIGURATION%\net10.0\FluentKit.xml" ^
  --manifest docs\consumer\manifest.json ^
  --json docs\reference\api.json ^
  --markdown docs\reference\api.md
if errorlevel 1 goto :fail

copy /Y docs\reference\api.json fluentkit-consumer\references\api.json >nul
if errorlevel 1 goto :fail

dotnet run --project tools\FluentKit.ApiReferenceGenerator -c "%CONFIGURATION%" -- ^
  --assembly "src\FluentKit\bin\%CONFIGURATION%\net10.0\FluentKit.dll" ^
  --xml "src\FluentKit\bin\%CONFIGURATION%\net10.0\FluentKit.xml" ^
  --manifest docs\consumer\manifest.json ^
  --json docs\reference\api.json ^
  --markdown docs\reference\api.md ^
  --summary-baseline docs\reference\summary-baseline.json ^
  --check-summaries --verify
if errorlevel 1 goto :fail

fc /b docs\reference\api.json fluentkit-consumer\references\api.json >nul
if errorlevel 1 goto :fail

for /f "delims=" %%V in ('dotnet msbuild src\FluentKit\FluentKit.csproj -getProperty:Version -nologo') do set "PACKAGE_VERSION=%%V"
for /f "delims=" %%V in ('powershell -NoProfile -Command "(Get-Content -Raw ''fluentkit-consumer/metadata.json'' | ConvertFrom-Json).fluentkitVersion"') do set "SKILL_VERSION=%%V"
if /I not "%PACKAGE_VERSION%"=="%SKILL_VERSION%" (
  echo Version mismatch: package=%PACKAGE_VERSION% skill=%SKILL_VERSION%
  goto :fail
)

echo Consumer reference is current for FluentKit %PACKAGE_VERSION%.
popd
exit /b 0

:fail
set "EXIT_CODE=%ERRORLEVEL%"
if "%EXIT_CODE%"=="0" set "EXIT_CODE=1"
popd
exit /b %EXIT_CODE%
