@echo off
rem Puts new firmware onto a dmxbox 2: writes its .hex into the PIC32 through J2, with a
rem programmer and ipecmd, the command-line tool of MPLAB IPE (installed with MPLAB X IDE).
rem   flash.cmd [TOOL] [FILE]
rem TOOL: SNAP (MPLAB Snap, the default), PK4 (PICkit 4) or PK5 (PICkit 5)
rem FILE: the .hex; without it dmxbox2.hex in boxes\box2\firmware, which build.cmd makes
rem
rem Plug the box into USB first: the PIC gets its 3.3 V from the box, not from the
rem programmer. The programmer's pin 1 (its arrow, MCLR) goes to pin 1 of J2. Programming
rem stops the box; it starts again with the new firmware when the programmer lets go.
rem If ipecmd does not take these options, "ipecmd -?" lists those of the version installed;
rem MPLAB IPE's window does the same: device PIC32MX270F256B, the tool, the file, Program.
setlocal
set "TOOL=%~1"
if "%TOOL%"=="" set "TOOL=SNAP"
set "HEX=%~2"
if "%HEX%"=="" set "HEX=%~dp0..\..\boxes\box2\firmware\dmxbox2.hex"
if not exist "%HEX%" (
    echo Missing: %HEX%
    echo boxes\box2\firmware\build.cmd makes it.
    exit /b 1
)
if not defined IPECMD for /d %%v in ("%ProgramFiles%\Microchip\MPLABX\v*") do (
    if exist "%%~v\mplab_platform\mplab_ipe\ipecmd.exe" set "IPECMD=%%~v\mplab_platform\mplab_ipe\ipecmd.exe"
    if exist "%%~v\mplab_platform\bin\ipecmd.exe" set "IPECMD=%%~v\mplab_platform\bin\ipecmd.exe"
)
if not defined IPECMD (
    echo MPLAB IPE was not found. Install MPLAB X IDE with IPE from microchip.com, or set
    echo IPECMD to the full name of ipecmd.exe.
    exit /b 1
)
echo "%IPECMD%" -TP%TOOL% -P32MX270F256B -M -F"%HEX%"
"%IPECMD%" -TP%TOOL% -P32MX270F256B -M -F"%HEX%"
if errorlevel 1 (
    echo The programmer did not write the firmware: see what ipecmd said above.
    exit /b 1
)
echo Done.
