@echo off
rem Builds and runs the PC test of the firmware (Visual Studio C compiler).
rem The firmware's own code is the real one; the Pico, the USB library and the
rem computer at the other end of the cable are played by fake_pico.c.
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
set "COMMON=..\..\common"
rem fake_pico comes first on the include path: its headers stand in for the Pico SDK and TinyUSB.
rem ..\src has the Pico's config.h; the code both boxes share is in ..\..\common.
cl /nologo /std:c11 /O1 /W4 /D_CRT_SECURE_NO_WARNINGS /I"fake_pico" /I"%SRC%" /I"%COMMON%" /I. /Fo"obj\\" /Fe"test_net.exe" ^
   test_net.c fake_pico.c firmware_main.c ^
   "%COMMON%\usb_net.c" "%COMMON%\usb_descriptors.c" "%COMMON%\net.c" "%COMMON%\dhcp_server.c" ^
   "%COMMON%\engine.c" "%COMMON%\artnet.c" "%COMMON%\ctrl.c" "%COMMON%\presets.c" || exit /b 1
"%~dp0test_net.exe"
