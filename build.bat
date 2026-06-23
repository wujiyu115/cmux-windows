@echo off
setlocal

set OUTPUT_DIR=publish
set GUI_PROJECT=src\Cmux\Cmux.csproj
set CLI_PROJECT=src\Cmux.Cli\Cmux.Cli.csproj
set DAEMON_PROJECT=src\Cmux.Daemon\Cmux.Daemon.csproj
set RUNTIME=win-x64

echo [1/5] Cleaning previous build...
if exist %OUTPUT_DIR% rmdir /s /q %OUTPUT_DIR%

echo [2/5] Publishing GUI (%RUNTIME%)...
dotnet publish %GUI_PROJECT% -c Release -r %RUNTIME% --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none -o %OUTPUT_DIR%

if %ERRORLEVEL% neq 0 (
    echo GUI build failed!
    exit /b 1
)

echo [3/5] Publishing CLI (%RUNTIME%)...
dotnet publish %CLI_PROJECT% -c Release -r %RUNTIME% --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none -o %OUTPUT_DIR%

if %ERRORLEVEL% neq 0 (
    echo CLI build failed!
    exit /b 1
)

echo [4/5] Publishing Daemon (%RUNTIME%)...
dotnet publish %DAEMON_PROJECT% -c Release -r %RUNTIME% --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none -o %OUTPUT_DIR%

if %ERRORLEVEL% neq 0 (
    echo Daemon build failed!
    exit /b 1
)

del /q %OUTPUT_DIR%\*.xml 2>nul
del /q %OUTPUT_DIR%\*.pdb 2>nul

echo [5/5] Done!
for %%A in (%OUTPUT_DIR%\cmuxw.exe) do echo GUI: %OUTPUT_DIR%\cmuxw.exe (%%~zA bytes)
for %%A in (%OUTPUT_DIR%\cmux.exe) do echo CLI: %OUTPUT_DIR%\cmux.exe (%%~zA bytes)
for %%A in (%OUTPUT_DIR%\cmux-daemon.exe) do echo Daemon: %OUTPUT_DIR%\cmux-daemon.exe (%%~zA bytes)
echo.
echo Run GUI: %OUTPUT_DIR%\cmuxw.exe
echo Run CLI: %OUTPUT_DIR%\cmux.exe
