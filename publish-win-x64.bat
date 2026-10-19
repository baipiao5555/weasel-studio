@echo off
setlocal
cd /d "%~dp0"

dotnet publish WeaselStudio.csproj -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -o publish

if errorlevel 1 (
  echo.
  echo Publish failed.
  pause
  exit /b 1
)

echo.
echo Output: %~dp0publish\WeaselStudio.exe
pause
