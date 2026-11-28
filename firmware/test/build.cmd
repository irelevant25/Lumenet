@echo off
rem Builds and runs the PC tests of the boxes' firmware (Visual Studio C compiler):
rem   test_net.exe  the network stack, USB and the whole firmware (the real main.c) on
rem                 pretend hardware: fake_pico.c plays the board, the USB library and
rem                 the computer at the other end of the cable; as dmxbox 4
rem   test_net3.exe the same as dmxbox 3 (no MIDI, no chain)
rem   test_net1.exe the same as dmxbox 1 (a Pico, no MIDI, no chain)
rem   test_rdm.exe  RDM (search, requests, over the DX protocol and Art-Net) and MIDI bytes
rem   test_line.exe the DMX line of dmxbox 2 (the PIC32's dmx_line.c) on a pretend UART:
rem                 frames, and the RDM search and requests through it
rem All of them use the lights with RDM of the simulator (..\sim\rdm_lights.c).
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
for %%d in (net net3 net1 rdm line) do if not exist obj\%%d mkdir obj\%%d
set "SRC=..\rp2040\src"
set "BOX=..\..\boxes\box4\firmware"
set "BOX3=..\..\boxes\box3\firmware"
set "BOX1=..\..\boxes\box1\firmware"
set "BOX2=..\..\boxes\box2\firmware"
set "PIC=..\pic32\src"
set "COMMON=..\common"
set "SIM=..\sim"
rem fake_pico comes first on the include path: its headers stand in for the Pico SDK and TinyUSB.
cl /nologo /std:c11 /O1 /W4 /D_CRT_SECURE_NO_WARNINGS /I"fake_pico" /I"%BOX%" /I"%SRC%" /I"%COMMON%" /I"%SIM%" /I. ^
   /Fo"obj\net\\" /Fe"test_net.exe" test_net.c fake_pico.c firmware_main.c ^
   "%COMMON%\usb_net.c" "%COMMON%\usb_descriptors.c" "%COMMON%\net.c" "%COMMON%\dhcp_server.c" ^
   "%COMMON%\engine.c" "%COMMON%\artnet.c" "%COMMON%\ctrl.c" "%COMMON%\presets.c" "%COMMON%\rdm.c" "%COMMON%\dmx_in.c" ^
   "%SIM%\rdm_lights.c" || exit /b 1
cl /nologo /std:c11 /O1 /W4 /D_CRT_SECURE_NO_WARNINGS /I"fake_pico" /I"%BOX3%" /I"%SRC%" /I"%COMMON%" /I"%SIM%" /I. ^
   /Fo"obj\net3\\" /Fe"test_net3.exe" test_net.c fake_pico.c firmware_main.c ^
   "%COMMON%\usb_net.c" "%COMMON%\usb_descriptors.c" "%COMMON%\net.c" "%COMMON%\dhcp_server.c" ^
   "%COMMON%\engine.c" "%COMMON%\artnet.c" "%COMMON%\ctrl.c" "%COMMON%\presets.c" "%COMMON%\rdm.c" "%COMMON%\dmx_in.c" ^
   "%SIM%\rdm_lights.c" || exit /b 1
cl /nologo /std:c11 /O1 /W4 /D_CRT_SECURE_NO_WARNINGS /I"fake_pico" /I"%BOX1%" /I"%SRC%" /I"%COMMON%" /I"%SIM%" /I. ^
   /Fo"obj\net1\\" /Fe"test_net1.exe" test_net.c fake_pico.c firmware_main.c ^
   "%COMMON%\usb_net.c" "%COMMON%\usb_descriptors.c" "%COMMON%\net.c" "%COMMON%\dhcp_server.c" ^
   "%COMMON%\engine.c" "%COMMON%\artnet.c" "%COMMON%\ctrl.c" "%COMMON%\presets.c" "%COMMON%\rdm.c" "%COMMON%\dmx_in.c" ^
   "%SIM%\rdm_lights.c" || exit /b 1
cl /nologo /std:c11 /O1 /W4 /D_CRT_SECURE_NO_WARNINGS /I"%BOX%" /I"%SRC%" /I"%COMMON%" /I"%SIM%" ^
   /Fo"obj\rdm\\" /Fe"test_rdm.exe" test_rdm.c "%COMMON%\rdm.c" "%COMMON%\ctrl.c" "%COMMON%\artnet.c" "%COMMON%\dmx_in.c" ^
   "%COMMON%\engine.c" "%COMMON%\presets.c" "%COMMON%\midi_parse.c" "%SIM%\rdm_lights.c" || exit /b 1
rem fake_pic32 comes first on the include path: its uart1.h stands in for the PIC's registers.
cl /nologo /std:c11 /O1 /W4 /D_CRT_SECURE_NO_WARNINGS /I"fake_pic32" /I"%PIC%" /I"%BOX2%" /I"%COMMON%" /I"%SIM%" ^
   /Fo"obj\line\\" /Fe"test_line.exe" test_line.c "%PIC%\dmx_line.c" "%COMMON%\rdm.c" "%SIM%\rdm_lights.c" ^
   || exit /b 1
echo.
echo test_net:
"%~dp0test_net.exe" || exit /b 1
echo.
echo test_net3 (dmxbox 3):
"%~dp0test_net3.exe" || exit /b 1
echo.
echo test_net1 (dmxbox 1):
"%~dp0test_net1.exe" || exit /b 1
echo.
echo test_rdm:
"%~dp0test_rdm.exe" || exit /b 1
echo.
echo test_line (dmxbox 2):
"%~dp0test_line.exe" || exit /b 1
