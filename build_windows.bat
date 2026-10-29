@echo off
setlocal
cd /d "%~dp0"
set "DOTNET_CLI_TELEMETRY_OPTOUT=1"
set "DOTNET_NOLOGO=1"
set "DOTNET=%ProgramFiles%\dotnet\dotnet.exe"
if not exist "%DOTNET%" set "DOTNET=dotnet"

"%DOTNET%" run --project tests\WeightTracker.Tests.csproj -c Release
if errorlevel 1 exit /b 1

if exist dist\WeightTracker rmdir /s /q dist\WeightTracker
"%DOTNET%" publish WeightTracker.csproj -c Release -r win-x64 --self-contained true ^
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true ^
    -p:DebugType=None -p:DebugSymbols=false -o dist\WeightTracker
if errorlevel 1 exit /b 1

copy /Y portable.flag dist\WeightTracker\portable.flag >nul
copy /Y README.md dist\WeightTracker\README.txt >nul
copy /Y DATA_LOCATION.txt dist\WeightTracker\DATA_LOCATION.txt >nul
copy /Y DEPENDENCIES.md dist\WeightTracker\DEPENDENCIES.md >nul
echo Build complete: dist\WeightTracker\WeightTracker.exe
endlocal