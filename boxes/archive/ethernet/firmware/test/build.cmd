@echo off
rem Builds and runs the PC test of the Ethernet driver and network stack (Visual Studio C compiler).
setlocal
set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
if not exist "%VSWHERE%" (
    echo Visual Studio was not found. Install it with the "Desktop development with C++" workload.
    exit /b 1
)
for /f "usebackq delims=" %%i in (`"%VSWHERE%" -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath`) do set "VSDIR=%%i"
if not defined VSDIR (
    echo The Visual Studio C++ tools are not installed. Add the "Desktop development with C++" workload.
    exit /b 1
)
call "%VSDIR%\VC\Auxiliary\Build\vcvars64.bat" >nul 2>&1 || exit /b 1

cd /d "%~dp0"
if not exist obj mkdir obj
set "SRC=..\src"
cl /nologo /std:c11 /O1 /W4 /D_CRT_SECURE_NO_WARNINGS /I"%SRC%" /I. /Fo"obj\\" /Fe"test_net.exe" ^
   test_net.c enc_model.c "%SRC%\enc28j60.c" "%SRC%\net.c" "%SRC%\engine.c" "%SRC%\artnet.c" "%SRC%\ctrl.c" "%SRC%\presets.c" || exit /b 1
"%~dp0test_net.exe"
