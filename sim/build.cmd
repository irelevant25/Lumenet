@echo off
rem Builds dmxbox_sim.exe on Windows with the Visual Studio C compiler.
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
if not exist obj mkdir obj
set "SRC=..\firmware\src"
cl /nologo /std:c11 /O2 /W3 /D_CRT_SECURE_NO_WARNINGS /I"%SRC%" /Fo"obj\\" /Fe"dmxbox_sim.exe" ^
   sim.c "%SRC%\engine.c" "%SRC%\artnet.c" "%SRC%\ctrl.c" "%SRC%\presets.c" ws2_32.lib || exit /b 1
echo built %~dp0dmxbox_sim.exe
