@echo off
setlocal

set OUTPUT_DIR=publish
set PROJECT=src\Cmux\Cmux.csproj
set RUNTIME=win-x64

echo [1/3] Cleaning previous build...
if exist %OUTPUT_DIR% rmdir /s /q %OUTPUT_DIR%

echo [2/3] Publishing %RUNTIME%...
dotnet publish %PROJECT% -c Release -r %RUNTIME% --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none -o %OUTPUT_DIR%

if %ERRORLEVEL% neq 0 (
    echo Build failed!
    exit /b 1
)

del /q %OUTPUT_DIR%\*.xml 2>nul
del /q %OUTPUT_DIR%\*.pdb 2>nul

echo [3/3] Done!
for %%A in (%OUTPUT_DIR%\cmuxw.exe) do echo Output: %OUTPUT_DIR%\cmuxw.exe (%%~zA bytes)
echo.
echo Run with: %OUTPUT_DIR%\cmuxw.exe
