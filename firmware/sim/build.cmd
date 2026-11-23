@echo off
rem Builds the two simulators on Windows with the Visual Studio C compiler:
rem   dmxbox_sim.exe   the Pico box: one output, 512 channels
rem   dmxbox_sim2.exe  the PIC32 box: two outputs, 1024 channels
rem Needs Visual Studio (any edition) with the "Desktop development with C++" workload.
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
rem The code both boxes share; config.h comes from the box the simulator plays.
set "COMMON=..\common"
set "CORE=sim.c %COMMON%\engine.c %COMMON%\artnet.c %COMMON%\ctrl.c %COMMON%\presets.c ws2_32.lib"
if not exist obj\pico mkdir obj\pico
if not exist obj\pic32 mkdir obj\pic32
cl /nologo /std:c11 /O2 /W3 /D_CRT_SECURE_NO_WARNINGS /I"..\pico\src" /I"%COMMON%" /Fo"obj\pico\\" /Fe"dmxbox_sim.exe" %CORE% || exit /b 1
cl /nologo /std:c11 /O2 /W3 /D_CRT_SECURE_NO_WARNINGS /I"..\pic32\src" /I"%COMMON%" /Fo"obj\pic32\\" /Fe"dmxbox_sim2.exe" %CORE% || exit /b 1
echo built %~dp0dmxbox_sim.exe and %~dp0dmxbox_sim2.exe
