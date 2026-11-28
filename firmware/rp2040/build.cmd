@echo off
rem Builds the RP2040 firmware of one box on Windows:  build.cmd BOX  (1, 3 or 4)
rem Result: firmware\rp2040\build\boxN\dmxbox.uf2, and a copy of it as
rem boxes\boxN\firmware\dmxboxN.uf2, next to that box's flash.cmd. The build.cmd
rem in boxes\boxN\firmware calls this one for its box.
rem
rem It expects the Pico SDK, the ARM compiler and Raspberry Pi's two helper
rem programs (picotool, pioasm) in the folder .pico-sdk in your user folder.
rem That is where the VS Code "Raspberry Pi Pico" extension puts them too.
rem CMake and Ninja are taken from the PATH, or else from Visual Studio.
setlocal
set "BOX=%~1"
if "%BOX%"=="" (
    echo Which box? build.cmd 1, 3 or 4
    exit /b 1
)
for %%d in ("%~dp0..\..\boxes\box%BOX%\firmware") do set "BOXDIR=%%~fd"
if not exist "%BOXDIR%\box.h" (
    echo There is no RP2040 box %BOX%: %BOXDIR%\box.h is missing.
    exit /b 1
)
rem What comes after the box number goes to CMake, for example -DCMAKE_BUILD_TYPE=Debug.
set "MORE="
:moreArgs
shift /1
if "%~1"=="" goto argsDone
set MORE=%MORE% %1
goto moreArgs
:argsDone
set "BUILD=%~dp0build\box%BOX%"
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
"%CMAKE%" -S "%~dp0." -B "%BUILD%" -G Ninja "-DCMAKE_MAKE_PROGRAM=%NINJA:\=/%" ^
    "-DPICO_SDK_PATH=%SDK:\=/%" "-DPICO_TOOLCHAIN_PATH=%TOOLCHAIN:\=/%" ^
    "-Dpicotool_DIR=%PICOTOOL:\=/%" "-Dpioasm_DIR=%PIOASM:\=/%" ^
    -DBOX=%BOX% -DCMAKE_BUILD_TYPE=Release %MORE% || exit /b 1
"%CMAKE%" --build "%BUILD%" || exit /b 1
rem A copy next to the box's flash.cmd, which puts it onto the box
copy /y "%BUILD%\dmxbox.uf2" "%BOXDIR%\dmxbox%BOX%.uf2" >nul || exit /b 1
echo.
echo Done: %BUILD%\dmxbox.uf2
echo A copy is %BOXDIR%\dmxbox%BOX%.uf2, next to flash.cmd, which puts it onto the box.
