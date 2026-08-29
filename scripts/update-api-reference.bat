@echo off
setlocal EnableExtensions

rem Rebuild FluentKit and regenerate or verify the canonical API reference.
set "CONFIGURATION=%~1"
if not defined CONFIGURATION set "CONFIGURATION=Release"
set "MODE=%~2"
if /I "%~1"=="--verify" (
  set "CONFIGURATION=Release"
  set "MODE=--verify"
)
for %%I in ("%~dp0..") do set "ROOT_DIR=%%~fI"
pushd "%ROOT_DIR%"

dotnet build src\FluentKit\FluentKit.csproj -c "%CONFIGURATION%"
if errorlevel 1 goto :fail

dotnet run --project tools\FluentKit.ApiReferenceGenerator -c "%CONFIGURATION%" -- ^
  --assembly "src\FluentKit\bin\%CONFIGURATION%\net10.0\FluentKit.dll" ^
  --xml "src\FluentKit\bin\%CONFIGURATION%\net10.0\FluentKit.xml" ^
  --manifest docs\integration\manifest.json ^
  --json docs\reference\api.json ^
  --markdown docs\reference\api.md ^
  --summary-baseline docs\reference\summary-baseline.json ^
  --check-summaries %MODE%
if errorlevel 1 goto :fail

for /f "delims=" %%V in ('dotnet msbuild src\FluentKit\FluentKit.csproj -getProperty:Version -nologo') do set "PACKAGE_VERSION=%%V"

echo API reference is current for FluentKit %PACKAGE_VERSION%.
popd
exit /b 0

:fail
set "EXIT_CODE=%ERRORLEVEL%"
if "%EXIT_CODE%"=="0" set "EXIT_CODE=1"
popd
exit /b %EXIT_CODE%
