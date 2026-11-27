@echo off
rem Puts new firmware onto a dmxbox 3: copies dmxbox.uf2 onto its RP2040.
rem
rem dmxbox.uf2 has to be in the same folder as this file; build.cmd puts a copy here.
rem The RP2040 takes new firmware only in its USB boot loader, where Windows shows it
rem as a drive named RPI-RP2. A new box (empty flash) starts there by itself. A box
rem that runs the firmware is asked over USB to restart into it: this script finds
rem the boxes plugged into this computer by their network adapters (10.1.n.2 is the
rem computer's end of box n). If nothing helps: join BOOT and GND of the service pins
rem J7 while plugging the box in.
rem The script copies the file onto the drive, waits until the box has restarted with
rem the new firmware, and checks that it answers.
setlocal EnableExtensions
title dmxbox firmware
set "FIRMWARE=%~dp0dmxbox.uf2"
set "BOX="

echo.
echo dmxbox 3 firmware
echo.
if exist "%FIRMWARE%" goto haveFirmware
echo dmxbox.uf2 is missing. It has to be in the same folder as this file:
echo %~dp0
echo build.cmd makes it.
goto failed

:haveFirmware
for %%f in ("%FIRMWARE%") do (
    set /a "KB=(%%~zf + 1023) / 1024"
    set "WHEN=%%~tf"
)
echo File: dmxbox.uf2, %KB% KB, from %WHEN%
echo.

rem ---- 1. the box in its boot loader
<nul set /p "=[1/4] Looking for the boot loader ... "
call :findDrive
if %COUNT% gtr 0 goto found
call :findBoxes
if "%BOXES%"=="" goto askToPlug
for %%n in (%BOXES%) do set /a BOXCOUNT+=1
if %BOXCOUNT% equ 1 (
    set "BOX=%BOXES: =%"
    goto restartBox
)
echo not there yet.
echo.
echo       Boxes on this computer: %BOXES%
set /p "BOX=      Which box gets the firmware? Type its number and press Enter: "
echo.
<nul set /p "=[1/4] Looking for the boot loader ... "

:restartBox
echo asking box %BOX% to restart into it.
powershell -NoProfile -Command "$u = New-Object Net.Sockets.UdpClient; $b = [byte[]](0x44,0x58,1,0x60,0,0,0,0,0,0,0,0); [void]$u.Send($b, $b.Length, '10.1.%BOX%.1', 7000); $u.Close()" >nul 2>&1
<nul set /p "=      Waiting for its drive "
set "TRIES=0"
:waitAfterAsking
<nul set /p "=."
call :sleep
call :findDrive
if %COUNT% gtr 0 goto foundAfterWaiting
set /a TRIES+=1
if %TRIES% lss 20 goto waitAfterAsking
echo.
echo.
echo.
echo       Box %BOX% did not restart into its boot loader.
set "BOX="
goto plugInstructions

:askToPlug
echo not there yet.
:plugInstructions
echo.
echo       Plug the box into this computer. A new box starts its boot loader by itself.
echo       Otherwise: unplug it, join BOOT and GND of its service pins J7 with a jumper
echo       wire, plug it in again, then take the wire out.
echo       To give up, close this window.
echo.
<nul set /p "=Waiting for the boot loader "
:waitForDrive
<nul set /p "=."
call :sleep
call :findDrive
if %COUNT% equ 0 goto waitForDrive
:foundAfterWaiting
echo  there.
echo.
<nul set /p "=[1/4] Looking for the boot loader ... "

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
rem The RP2040 may restart before Windows has finished with the file. Then the copy
rem reports an error, although it got all of it: it restarts only then.
if not exist "%DRIVE%\INFO_UF2.TXT" goto written
echo failed.
echo.
echo Windows could not write onto %DRIVE%. Unplug the box and start again.
goto failed
:written
echo done

rem ---- 3. the box restarts with the new firmware; its drive goes away
<nul set /p "=[3/4] Waiting for the box to restart "
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
if defined BOX goto pingBox
rem A new box: its number shows once its network adapter is up.
call :findBoxes
for %%n in (%BOXES%) do ping -n 1 -w 1000 10.1.%%n.1 | find "TTL=" >nul && set "BOX=%%n"
if defined BOX goto answered
goto notYet
:pingBox
ping -n 1 -w 1000 10.1.%BOX%.1 | find "TTL=" >nul && goto answered
:notYet
set /a TRIES+=1
if %TRIES% gtr 45 goto noAnswer
call :sleep
goto waitForAnswer
:answered
echo  done
echo.
echo Successful: the new firmware runs on box %BOX% (10.1.%BOX%.1).
echo.
echo Press any key to close this window.
pause >nul
exit /b 0

:noRestart
echo.
echo.
echo The box took the file but did not restart. Is dmxbox.uf2 built for dmxbox 3 (build.cmd)?
goto failed

:noAnswer
echo.
echo.
echo The box took the firmware and restarted, but it does not answer.
echo The first time on a computer Windows may still be setting up its network adapter:
echo wait a minute and look at the box in Lumenet. See "Check that it works" in DEV.md.
goto failed

:failed
echo.
echo Press any key to close this window.
pause >nul
exit /b 1


rem ---- helpers

:findDrive
rem The drives of RP2040s in their boot loader: named RPI-RP2, with the file
rem INFO_UF2.TXT on them. Sets COUNT, and DRIVE and LABEL of the last one found, LIST
rem of all of them and LETTERS, their drive letters in one word. PowerShell looks only
rem at drives that are ready, so a card reader without a card brings up no "No disk"
rem window.
set "COUNT=0"
set "LIST="
set "LETTERS="
for /f "tokens=1,2" %%a in ('powershell -NoProfile -Command "[IO.DriveInfo]::GetDrives() | Where-Object { $_.IsReady -and $_.VolumeLabel -eq 'RPI-RP2' -and (Test-Path -LiteralPath ($_.Name + 'INFO_UF2.TXT')) } | ForEach-Object { $_.Name.Substring(0, 1) + ' ' + $_.VolumeLabel }" 2^>nul') do (
    set /a COUNT+=1
    set "DRIVE=%%a:"
    set "LABEL=%%b"
    call set "LIST=%%LIST%% %%a: (%%b)"
    call set "LETTERS=%%LETTERS%%%%a"
)
exit /b

:findBoxes
rem The numbers of the boxes plugged in: this computer has the address 10.1.n.2 on the
rem network adapter of box n. Sets BOXES ("3 7") and BOXCOUNT to 0.
set "BOXES="
set "BOXCOUNT=0"
for /f %%n in ('powershell -NoProfile -Command "Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue | Where-Object { $_.IPAddress -like '10.1.*.2' } | ForEach-Object { $_.IPAddress.Split('.')[2] }" 2^>nul') do call set "BOXES=%%BOXES%% %%n"
if defined BOXES set "BOXES=%BOXES:~1%"
exit /b

:sleep
rem About one second. (timeout does not work when the input of this script is redirected.)
ping -n 2 127.0.0.1 >nul
exit /b
