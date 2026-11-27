@echo off
rem Builds the dmxbox 3 firmware on Windows. Result: firmware\build\dmxbox.uf2, and a copy
rem of it in firmware\ for flash.cmd.
rem
rem It expects the Pico SDK, the ARM compiler and Raspberry Pi's two helper
rem programs (picotool, pioasm) in the folder .pico-sdk in your user folder.
rem That is where the VS Code "Raspberry Pi Pico" extension puts them too.
rem CMake and Ninja are taken from the PATH, or else from Visual Studio.
setlocal
set "PICO=%USERPROFILE%\.pico-sdk"
set "SDK=%PICO%\sdk\2.3.1"
set "TOOLCHAIN=%PICO%\toolchain\14_2_Rel1"
set "PICOTOOL=%PICO%\picotool\2.3.1\picotool"
set "PIOASM=%PICO%\tools\2.3.1\pioasm"
for %%d in ("%SDK%" "%TOOLCHAIN%\bin" "%PICOTOOL%" "%PIOASM%") do if not exist "%%~d" (
    echo Missing: %%~d
    echo See "Build" in DEV.md.
    exit /b 1
)

rem CMake and Ninja: from the PATH, or the ones that come with Visual Studio
set "CMAKE=cmake"
set "NINJA="
for /f "delims=" %%i in ('where ninja 2^>nul') do if not defined NINJA set "NINJA=%%i"
where cmake >nul 2>&1 || set "NINJA="
if defined NINJA goto build
set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
if exist "%VSWHERE%" for /f "usebackq delims=" %%i in (`"%VSWHERE%" -latest -products * -property installationPath`) do set "VSDIR=%%i"
set "VSCMAKE=%VSDIR%\Common7\IDE\CommonExtensions\Microsoft\CMake"
if not exist "%VSCMAKE%\CMake\bin\cmake.exe" (
    echo CMake and Ninja were not found. Install both, or add "C++ CMake tools for Windows" to Visual Studio.
    exit /b 1
)
set "CMAKE=%VSCMAKE%\CMake\bin\cmake.exe"
set "NINJA=%VSCMAKE%\Ninja\ninja.exe"

:build
rem CMake wants forward slashes in the paths it is given.
"%CMAKE%" -S "%~dp0." -B "%~dp0build" -G Ninja "-DCMAKE_MAKE_PROGRAM=%NINJA:\=/%" ^
    "-DPICO_SDK_PATH=%SDK:\=/%" "-DPICO_TOOLCHAIN_PATH=%TOOLCHAIN:\=/%" ^
    "-Dpicotool_DIR=%PICOTOOL:\=/%" "-Dpioasm_DIR=%PIOASM:\=/%" ^
    -DCMAKE_BUILD_TYPE=Release %* || exit /b 1
"%CMAKE%" --build "%~dp0build" || exit /b 1
rem A copy next to flash.cmd, which puts it onto the box
copy /y "%~dp0build\dmxbox.uf2" "%~dp0dmxbox.uf2" >nul || exit /b 1
echo.
echo Done: %~dp0build\dmxbox.uf2
echo A copy is next to flash.cmd, which puts it onto the box.
