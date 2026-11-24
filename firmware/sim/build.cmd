@echo off
rem Builds the four simulators on Windows with the Visual Studio C compiler:
rem   dmxbox_sim.exe        the Pico box: one output, 512 channels
rem   dmxbox_sim_pico2.exe  a Pico box with two outputs (no such hardware exists)
rem   dmxbox_sim2.exe       the PIC32 box, dmxbox 2: two outputs, 1024 channels
rem   dmxbox_sim2_1.exe     dmxbox 2 with one output
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
set "CL_FLAGS=/nologo /std:c11 /O2 /W3 /D_CRT_SECURE_NO_WARNINGS"
for %%d in (pico pico2 pic32 pic32-1) do if not exist obj\%%d mkdir obj\%%d
cl %CL_FLAGS% /I"..\pico\src" /I"%COMMON%" /Fo"obj\pico\\" /Fe"dmxbox_sim.exe" %CORE% || exit /b 1
cl %CL_FLAGS% /DDMX_OUTPUTS=2 /I"..\pico\src" /I"%COMMON%" /Fo"obj\pico2\\" /Fe"dmxbox_sim_pico2.exe" %CORE% || exit /b 1
cl %CL_FLAGS% /I"..\pic32\src" /I"%COMMON%" /Fo"obj\pic32\\" /Fe"dmxbox_sim2.exe" %CORE% || exit /b 1
cl %CL_FLAGS% /DDMX_OUTPUTS=1 /I"..\pic32\src" /I"%COMMON%" /Fo"obj\pic32-1\\" /Fe"dmxbox_sim2_1.exe" %CORE% || exit /b 1
echo built dmxbox_sim.exe, dmxbox_sim_pico2.exe, dmxbox_sim2.exe and dmxbox_sim2_1.exe in %~dp0
