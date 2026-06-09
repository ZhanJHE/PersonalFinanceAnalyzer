; 个人收支趋势分析器 - NSIS 安装脚本
; 使用 NSIS 3.x + 简体中文

Unicode true

; -- 版本信息 --
!define PRODUCT_NAME "个人收支趋势分析器"
!define PRODUCT_VERSION "1.0.0"
!define PRODUCT_PUBLISHER "PersonalFinanceAnalyzer"
!define PRODUCT_WEB_SITE "https://localhost:5001"
!define PRODUCT_DIR_REGKEY "Software\Microsoft\Windows\CurrentVersion\App Paths\PersonalFinanceAnalyzer.exe"
!define PRODUCT_UNINST_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\${PRODUCT_NAME}"
!define PRODUCT_UNINST_ROOT_KEY "HKLM"

; -- 引入 MUI2 --
!include "MUI2.nsh"
!include "FileFunc.nsh"

; -- 安装页面 --
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_LICENSE "LICENSE"
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

; -- 卸载页面 --
!insertmacro MUI_UNPAGE_INSTFILES

; -- 语言设置 --
!insertmacro MUI_LANGUAGE "SimpChinese"

; -- 安装程序属性 --
Name "${PRODUCT_NAME} ${PRODUCT_VERSION}"
OutFile "PersonalFinanceAnalyzer_Setup.exe"
InstallDir "$PROGRAMFILES64\${PRODUCT_NAME}"
InstallDirRegKey HKLM "${PRODUCT_DIR_REGKEY}" ""
ShowInstDetails show
ShowUnInstDetails show
RequestExecutionLevel admin
Icon "app.ico"
UninstallIcon "app.ico"

Section "MainSection" SEC01
    ; 自动关闭正在运行的程序
    ExecWait 'taskkill /F /IM PersonalFinanceAnalyzer.exe 2>nul'
    Sleep 500
    
    SetOutPath "$INSTDIR"
    SetOverwrite ifnewer
    
    ; 复制所有发布文件
    File /r "publish\*.*"
    
    ; 创建开始菜单快捷方式
    CreateDirectory "$SMPROGRAMS\${PRODUCT_NAME}"
    CreateShortCut "$SMPROGRAMS\${PRODUCT_NAME}\${PRODUCT_NAME}.lnk" "$INSTDIR\PersonalFinanceAnalyzer.exe"
    CreateShortCut "$SMPROGRAMS\${PRODUCT_NAME}\卸载.lnk" "$INSTDIR\uninst.exe"
    
    ; 创建桌面快捷方式
    CreateShortCut "$DESKTOP\${PRODUCT_NAME}.lnk" "$INSTDIR\PersonalFinanceAnalyzer.exe"
    
    ; 写卸载信息到注册表
    WriteUninstaller "$INSTDIR\uninst.exe"
    WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "DisplayName" "$(^Name)"
    WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "UninstallString" "$INSTDIR\uninst.exe"
    WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "DisplayVersion" "${PRODUCT_VERSION}"
    WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "Publisher" "${PRODUCT_PUBLISHER}"
    WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "URLInfoAbout" "${PRODUCT_WEB_SITE}"
    WriteRegDword ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "NoModify" 1
    WriteRegDword ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "NoRepair" 1
SectionEnd

; -- 卸载段 --
Section Uninstall
    ; 自动关闭正在运行的程序
    ExecWait 'taskkill /F /IM PersonalFinanceAnalyzer.exe 2>nul'
    Sleep 500
    
    Delete "$SMPROGRAMS\${PRODUCT_NAME}\${PRODUCT_NAME}.lnk"
    Delete "$SMPROGRAMS\${PRODUCT_NAME}\卸载.lnk"
    RMDir "$SMPROGRAMS\${PRODUCT_NAME}"
    
    Delete "$DESKTOP\${PRODUCT_NAME}.lnk"
    
    RMDir /r "$INSTDIR\*.*"
    RMDir "$INSTDIR"
    
    ; 询问是否删除用户数据
    MessageBox MB_YESNO|MB_ICONQUESTION \
        "是否删除用户数据（数据库和登录信息）？$\r$\n如果不删除，重装后数据仍在。" \
        IDNO skip_data
    RMDir /r "$LOCALAPPDATA\PersonalFinanceAnalyzer"
    skip_data:
    
    DeleteRegKey ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}"
    DeleteRegKey HKLM "${PRODUCT_DIR_REGKEY}"
    
    SetAutoClose true
SectionEnd
