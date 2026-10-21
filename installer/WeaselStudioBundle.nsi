Unicode true
ManifestDPIAware true

!include "MUI2.nsh"
!include "LogicLib.nsh"
!include "x64.nsh"

!define APP_NAME "小狼毫一体化安装包"
!define APP_VERSION "0.6.1"
!define APP_PUBLISHER "baipiao5555"
!define APP_EXE "WeaselStudio.exe"
!define APP_REG_KEY "Software\WeaselStudio"
!define APP_UNINSTALL_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\WeaselStudioBundle"

Name "${APP_NAME}"
OutFile "..\publish\WeaselStudio-AllInOne-${APP_VERSION}.exe"
InstallDir "$PROGRAMFILES64\WeaselStudio"
InstallDirRegKey HKLM "${APP_REG_KEY}" "InstallDir"
RequestExecutionLevel admin
SetCompressor /SOLID lzma
SetCompressorDictSize 64
CRCCheck on
XPStyle on

VIProductVersion "${APP_VERSION}.0"
VIAddVersionKey "ProductName" "${APP_NAME}"
VIAddVersionKey "ProductVersion" "${APP_VERSION}"
VIAddVersionKey "CompanyName" "${APP_PUBLISHER}"
VIAddVersionKey "FileDescription" "${APP_NAME}"
VIAddVersionKey "FileVersion" "${APP_VERSION}.0"
VIAddVersionKey "LegalCopyright" "Copyright (C) 2026 ${APP_PUBLISHER}"

!define MUI_ABORTWARNING
!define MUI_ICON "..\Assets\app.ico"
!define MUI_UNICON "..\Assets\app.ico"
!define MUI_FINISHPAGE_RUN "$INSTDIR\${APP_EXE}"
!define MUI_FINISHPAGE_RUN_TEXT "启动 Weasel Studio"

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_COMPONENTS
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

!insertmacro MUI_LANGUAGE "SimpChinese"
!insertmacro MUI_LANGUAGE "English"

Function .onInit
  SetShellVarContext all
  ${IfNot} ${RunningX64}
    MessageBox MB_ICONSTOP "此安装包需要 64 位 Windows。"
    Abort
  ${EndIf}
  InitPluginsDir
FunctionEnd

Function un.onInit
  SetShellVarContext all
FunctionEnd

Section "小狼毫输入法（含内置剪贴板）" SecWeasel
  SectionIn RO
  SetOutPath "$PLUGINSDIR"
  File "weasel-installer.exe"
  ExecWait '"$PLUGINSDIR\weasel-installer.exe" /S' $0
  ${If} $0 != 0
    MessageBox MB_ICONSTOP "小狼毫安装失败，退出码 $0。安装已中止。"
    SetErrorLevel $0
    Quit
  ${EndIf}
  Delete "$PLUGINSDIR\weasel-installer.exe"
SectionEnd

Section "Weasel Studio 设置中心" SecStudio
  SectionIn RO
  SetOutPath "$INSTDIR"
  File "WeaselStudio.exe"
  WriteUninstaller "$INSTDIR\Uninstall.exe"

  CreateDirectory "$SMPROGRAMS\小狼毫设置中心"
  CreateShortcut "$SMPROGRAMS\小狼毫设置中心\小狼毫设置中心.lnk" "$INSTDIR\${APP_EXE}" "" "$INSTDIR\${APP_EXE}" 0
  CreateShortcut "$SMPROGRAMS\小狼毫设置中心\卸载小狼毫设置中心.lnk" "$INSTDIR\Uninstall.exe"

  WriteRegStr HKLM "${APP_REG_KEY}" "InstallDir" "$INSTDIR"
  WriteRegStr HKLM "${APP_UNINSTALL_KEY}" "DisplayName" "小狼毫设置中心"
  WriteRegStr HKLM "${APP_UNINSTALL_KEY}" "DisplayVersion" "${APP_VERSION}"
  WriteRegStr HKLM "${APP_UNINSTALL_KEY}" "Publisher" "${APP_PUBLISHER}"
  WriteRegStr HKLM "${APP_UNINSTALL_KEY}" "DisplayIcon" "$INSTDIR\${APP_EXE}"
  WriteRegStr HKLM "${APP_UNINSTALL_KEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKLM "${APP_UNINSTALL_KEY}" "UninstallString" '"$INSTDIR\Uninstall.exe"'
  WriteRegStr HKLM "${APP_UNINSTALL_KEY}" "QuietUninstallString" '"$INSTDIR\Uninstall.exe" /S'
  WriteRegDWORD HKLM "${APP_UNINSTALL_KEY}" "NoModify" 1
  WriteRegDWORD HKLM "${APP_UNINSTALL_KEY}" "NoRepair" 1
SectionEnd

Section "创建桌面快捷方式" SecDesktop
  CreateShortcut "$DESKTOP\小狼毫设置中心.lnk" "$INSTDIR\${APP_EXE}" "" "$INSTDIR\${APP_EXE}" 0
SectionEnd

!insertmacro MUI_FUNCTION_DESCRIPTION_BEGIN
  !insertmacro MUI_DESCRIPTION_TEXT ${SecWeasel} "安装改版小狼毫输入法，包含内置剪贴板面板和默认词库。"
  !insertmacro MUI_DESCRIPTION_TEXT ${SecStudio} "安装小狼毫设置中心，用于方案、外观、词典和剪贴板配置。"
  !insertmacro MUI_DESCRIPTION_TEXT ${SecDesktop} "在桌面创建设置中心快捷方式（可选）。"
!insertmacro MUI_FUNCTION_DESCRIPTION_END

Section "Uninstall"
  Delete "$DESKTOP\小狼毫设置中心.lnk"
  Delete "$SMPROGRAMS\小狼毫设置中心\小狼毫设置中心.lnk"
  Delete "$SMPROGRAMS\小狼毫设置中心\卸载小狼毫设置中心.lnk"
  RMDir "$SMPROGRAMS\小狼毫设置中心"

  ReadRegStr $0 HKLM "Software\Rime\Weasel" "InstallDir"
  ${If} $0 != ""
    ${If} ${FileExists} "$0\uninstall.exe"
      MessageBox MB_YESNO|MB_ICONQUESTION "是否同时卸载小狼毫输入法？$\r$\n如果不卸载，输入法仍可继续使用。" IDNO skip_weasel
      ExecWait '"$0\uninstall.exe" /S'
      skip_weasel:
    ${EndIf}
  ${EndIf}

  Delete "$INSTDIR\${APP_EXE}"
  Delete "$INSTDIR\Uninstall.exe"
  RMDir "$INSTDIR"
  DeleteRegKey HKLM "${APP_UNINSTALL_KEY}"
  DeleteRegKey HKLM "${APP_REG_KEY}"
SectionEnd
