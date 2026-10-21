@echo off
setlocal
cd /d "%~dp0"

if not defined WEASEL_INSTALLER (
  echo Set WEASEL_INSTALLER to the modified Weasel installer path.
  exit /b 1
)
if not defined STUDIO_EXE set STUDIO_EXE=..\publish\WeaselStudio.exe
if not defined NSIS_EXE set NSIS_EXE=makensis.exe

if not exist "%WEASEL_INSTALLER%" (
  echo Weasel installer not found: %WEASEL_INSTALLER%
  exit /b 1
)
if not exist "%STUDIO_EXE%" (
  echo Weasel Studio executable not found: %STUDIO_EXE%
  exit /b 1
)

copy /y "%WEASEL_INSTALLER%" weasel-installer.exe >nul
copy /y "%STUDIO_EXE%" WeaselStudio.exe >nul

"%NSIS_EXE%" /INPUTCHARSET UTF8 WeaselStudioBundle.nsi
set rc=%errorlevel%

del /q weasel-installer.exe WeaselStudio.exe 2>nul
exit /b %rc%
