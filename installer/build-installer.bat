@echo off
setlocal
cd /d "%~dp0"

if not defined NSIS_EXE set NSIS_EXE=makensis.exe
if not exist "..\publish\WeaselStudio.exe" (
  echo publish\WeaselStudio.exe not found. Run the publish step first.
  exit /b 1
)

"%NSIS_EXE%" /V3 WeaselStudio.nsi
if errorlevel 1 (
  echo.
  echo Installer build failed.
  pause
  exit /b 1
)

echo.
echo Installer output: %~dp0..\publish\WeaselStudio-Setup-0.4.0.exe
pause
