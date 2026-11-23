@echo off
rem Puts new firmware onto the dmxbox: copies dmxbox.uf2 onto its Pico.
rem
rem dmxbox.uf2 has to be in the same folder as this file; build.cmd puts a copy here.
rem The Pico takes new firmware only in its boot loader: unplug the box, hold the
rem BOOTSEL button on the Pico and plug the USB cable in again. Windows then shows the
rem Pico as a drive named RPI-RP2 (a Pico 2: RP2350). This script waits for that drive
rem and finds it by itself; if there are several, it asks which one. It copies the file
rem onto it, waits until the Pico has restarted with the new firmware, and checks that
rem the box answers.
setlocal EnableExtensions
title dmxbox firmware
set "FIRMWARE=%~dp0dmxbox.uf2"
rem The address of the box: NET_IP in firmware\pico\src\config.h
set "BOX=192.168.7.1"

echo.
echo dmxbox firmware
echo.
if exist "%FIRMWARE%" goto haveFirmware
echo dmxbox.uf2 is missing. It has to be in the same folder as this file:
echo %~dp0
goto failed

:haveFirmware
for %%f in ("%FIRMWARE%") do (
    set /a "KB=(%%~zf + 1023) / 1024"
    set "WHEN=%%~tf"
)
echo File: dmxbox.uf2, %KB% KB, from %WHEN%
echo.

rem ---- 1. the Pico in its boot loader
<nul set /p "=[1/4] Looking for the Pico ......... "
call :findPico
if %COUNT% gtr 0 goto found
echo not there yet.
echo.
echo       Unplug the dmxbox from USB. Hold the white BOOTSEL button on the Pico,
echo       plug the cable in again, then let go of the button.
echo       To give up, close this window.
echo.
<nul set /p "=Waiting for the Pico "
:waitForPico
<nul set /p "=."
call :sleep
call :findPico
if %COUNT% equ 0 goto waitForPico
echo  there.
echo.
<nul set /p "=[1/4] Looking for the Pico ......... "

:found
if %COUNT% gtr 1 goto choose
echo %DRIVE% (%LABEL%)
goto write
:choose
echo %COUNT% found:%LIST%
choice /c %LETTERS% /n /m "Which one gets the firmware? Press its letter: "
set /a INDEX=%errorlevel% - 1
call set "DRIVE=%%LETTERS:~%INDEX%,1%%:"

rem ---- 2. the file onto it
:write
<nul set /p "=[2/4] Writing dmxbox.uf2 onto %DRIVE% ... "
copy /b /y "%FIRMWARE%" "%DRIVE%\dmxbox.uf2" >nul 2>&1
if not errorlevel 1 goto written
rem The Pico may restart before Windows has finished with the file. Then the copy
rem reports an error, although the Pico got all of it: it restarts only then.
if not exist "%DRIVE%\INFO_UF2.TXT" goto written
echo failed.
echo.
echo Windows could not write onto %DRIVE%. Unplug the box and start again.
goto failed
:written
echo done

rem ---- 3. the Pico restarts with the new firmware; its drive goes away
<nul set /p "=[3/4] Waiting for the Pico to restart "
set "TRIES=0"
:waitForRestart
<nul set /p "=."
if not exist "%DRIVE%\INFO_UF2.TXT" goto restarted
set /a TRIES+=1
if %TRIES% gtr 20 goto noRestart
call :sleep
goto waitForRestart
:restarted
echo  done

rem ---- 4. the box answers in the network over USB
<nul set /p "=[4/4] Waiting for the box to answer "
set "TRIES=0"
:waitForAnswer
<nul set /p "=."
ping -n 1 -w 1000 %BOX% | find "TTL=" >nul && goto answered
set /a TRIES+=1
if %TRIES% gtr 30 goto noAnswer
call :sleep
goto waitForAnswer
:answered
echo  done
echo.
echo Successful: the new firmware runs on the dmxbox.
echo.
echo Press any key to close this window.
pause >nul
exit /b 0

:noRestart
echo.
echo.
echo The Pico took the file but did not restart. Is dmxbox.uf2 built for this board?
echo A Pico (drive RPI-RP2) needs firmware built for the Pico, a Pico 2 (drive RP2350)
echo firmware built with -DPICO_BOARD=pico2.
goto failed

:noAnswer
echo.
echo.
echo The Pico took the firmware and restarted, but the box does not answer at %BOX%.
echo The first time on a computer Windows may still be setting up its network adapter:
echo wait a minute and try "ping %BOX%". If it stays silent, see "Check that it works"
echo in DEV.md of the dmxbox.
goto failed

:failed
echo.
echo Press any key to close this window.
pause >nul
exit /b 1


rem ---- helpers

:findPico
rem The drives of Picos in their boot loader: named RPI-RP2 or RP2350, with the file
rem INFO_UF2.TXT on them. Sets COUNT, and DRIVE and LABEL of the last one found, LIST
rem of all of them and LETTERS, their drive letters in one word. PowerShell looks only
rem at drives that are ready, so a card reader without a card brings up no "No disk"
rem window.
set "COUNT=0"
set "LIST="
set "LETTERS="
for /f "tokens=1,2" %%a in ('powershell -NoProfile -Command "[IO.DriveInfo]::GetDrives() | Where-Object { $_.IsReady -and $_.VolumeLabel -in 'RPI-RP2', 'RP2350' -and (Test-Path -LiteralPath ($_.Name + 'INFO_UF2.TXT')) } | ForEach-Object { $_.Name.Substring(0, 1) + ' ' + $_.VolumeLabel }" 2^>nul') do (
    set /a COUNT+=1
    set "DRIVE=%%a:"
    set "LABEL=%%b"
    call set "LIST=%%LIST%% %%a: (%%b)"
    call set "LETTERS=%%LETTERS%%%%a"
)
exit /b

:sleep
rem About one second. (timeout does not work when the input of this script is redirected.)
ping -n 2 127.0.0.1 >nul
exit /b
