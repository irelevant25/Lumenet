@echo off
rem Builds the simulators of dmxbox 4 on Windows with the Visual Studio C compiler:
rem   dmxbox_sim.exe   dmxbox 4: one output, 512 channels, lights with RDM on its line
rem   dmxbox_sim2.exe  the same with two outputs, 1024 channels (no such box: for the tests)
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
rem The code the box runs (..\common, config.h too) and box 4's box.h
rem (boxes\box4\firmware), and the lights of the line.
set "COMMON=..\common"
set "CORE=sim.c rdm_lights.c %COMMON%\engine.c %COMMON%\artnet.c %COMMON%\ctrl.c %COMMON%\presets.c %COMMON%\rdm.c %COMMON%\dmx_in.c ws2_32.lib"
set "CL_FLAGS=/nologo /std:c11 /O2 /W3 /D_CRT_SECURE_NO_WARNINGS /I"..\..\boxes\box4\firmware" /I"%COMMON%""
for %%d in (one two) do if not exist obj\%%d mkdir obj\%%d
cl %CL_FLAGS% /Fo"obj\one\\" /Fe"dmxbox_sim.exe" %CORE% || exit /b 1
cl %CL_FLAGS% /DDMX_OUTPUTS=2 /Fo"obj\two\\" /Fe"dmxbox_sim2.exe" %CORE% || exit /b 1
echo built dmxbox_sim.exe and dmxbox_sim2.exe in %~dp0
