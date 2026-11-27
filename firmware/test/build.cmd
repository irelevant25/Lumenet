@echo off
rem Builds and runs the PC tests of the dmxbox 3 firmware (Visual Studio C compiler):
rem   test_net.exe  the network stack, USB and the whole firmware (the real main.c) on
rem                 pretend hardware: fake_pico.c plays the board, the USB library and
rem                 the computer at the other end of the cable
rem   test_rdm.exe  RDM (search, requests, over the DX protocol and Art-Net) and MIDI bytes
rem Both use the lights with RDM of the simulator (..\sim\rdm_lights.c).
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
for %%d in (net rdm) do if not exist obj\%%d mkdir obj\%%d
set "SRC=..\src"
set "COMMON=..\common"
set "SIM=..\sim"
rem fake_pico comes first on the include path: its headers stand in for the Pico SDK and TinyUSB.
cl /nologo /std:c11 /O1 /W4 /D_CRT_SECURE_NO_WARNINGS /I"fake_pico" /I"%SRC%" /I"%COMMON%" /I"%SIM%" /I. ^
   /Fo"obj\net\\" /Fe"test_net.exe" test_net.c fake_pico.c firmware_main.c ^
   "%COMMON%\usb_net.c" "%COMMON%\usb_descriptors.c" "%COMMON%\net.c" "%COMMON%\dhcp_server.c" ^
   "%COMMON%\engine.c" "%COMMON%\artnet.c" "%COMMON%\ctrl.c" "%COMMON%\presets.c" "%COMMON%\rdm.c" ^
   "%SIM%\rdm_lights.c" || exit /b 1
cl /nologo /std:c11 /O1 /W4 /D_CRT_SECURE_NO_WARNINGS /I"%SRC%" /I"%COMMON%" /I"%SIM%" ^
   /Fo"obj\rdm\\" /Fe"test_rdm.exe" test_rdm.c "%COMMON%\rdm.c" "%COMMON%\ctrl.c" "%COMMON%\artnet.c" ^
   "%COMMON%\engine.c" "%COMMON%\presets.c" "%COMMON%\midi_parse.c" "%SIM%\rdm_lights.c" || exit /b 1
echo.
echo test_net:
"%~dp0test_net.exe" || exit /b 1
echo.
echo test_rdm:
"%~dp0test_rdm.exe" || exit /b 1
