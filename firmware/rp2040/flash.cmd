@echo off
rem Puts new firmware onto an RP2040 dmxbox (box 1, 3 or 4): copies its .uf2 file onto the RP2040.
rem   flash.cmd FILE   that file, for example ..\..\boxes\box4\firmware\dmxbox4.uf2
rem   flash.cmd        the .uf2 file in this script's own folder: copy the two to another
rem                    computer and run it there
rem The flash.cmd in each box's firmware folder calls this one with its own file, which
rem build.cmd puts there.
rem
rem The RP2040 takes new firmware only in its USB boot loader, where Windows shows it
rem as a drive named RPI-RP2. A new box (empty flash) starts there by itself. A box
rem that runs the firmware is asked over USB to restart into it: this script finds
rem the boxes plugged into this computer by their network adapters (10.1.n.2 is the
rem computer's end of box n). If nothing helps: join BOOT and GND of the service pins
rem J7 while plugging the box in (box 1: hold the BOOTSEL button of its Pico).
rem The script copies the file onto the drive, waits until the box has restarted with
rem the new firmware, and checks that it answers.
setlocal EnableExtensions
title dmxbox firmware
set "FIRMWARE=%~1"
if not "%FIRMWARE%"=="" goto haveName
for %%f in ("%~dp0dmxbox*.uf2") do set "FIRMWARE=%%~ff"
if not "%FIRMWARE%"=="" goto haveName
echo.
echo There is no .uf2 file in the folder of this file:
echo %~dp0
echo build.cmd in the box's firmware folder makes it.
goto failed

:haveName
for %%f in ("%FIRMWARE%") do (
    set "FILE=%%~nxf"
    set "MODEL=%%~nf"
)
rem dmxbox4.uf2 is the firmware of box 4: the text of step 1 says how to start its boot loader.
set "MODEL=%MODEL:dmxbox=%"
set "BYHAND=unplug it, join BOOT and GND of its service pins J7 with a jumper"
set "BYHAND2=wire, plug it in again, then take the wire out"
if "%MODEL%"=="1" (
    set "BYHAND=unplug it, hold the BOOTSEL button of its Pico down, plug it in"
    set "BYHAND2=again, then let go of the button"
)
set "BOX="

echo.
echo dmxbox %MODEL% firmware
echo.
if exist "%FIRMWARE%" goto haveFirmware
echo %FILE% is missing:
echo %FIRMWARE%
echo build.cmd in the box's firmware folder makes it.
goto failed

:haveFirmware
for %%f in ("%FIRMWARE%") do (
    set /a "KB=(%%~zf + 1023) / 1024"
    set "WHEN=%%~tf"
)
echo File: %FILE%, %KB% KB, from %WHEN%
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
rem Which box it is (PING: firmware 3.1 and later say it): a box gets only its own firmware unless you say so.
set "BOXMODEL=0"
for /f %%m in ('powershell -NoProfile -Command "$u = New-Object Net.Sockets.UdpClient; $u.Client.ReceiveTimeout = 1500; $b = [byte[]](0x44,0x58,1,0x01,0,0,0,7,0,1,0,0); [void]$u.Send($b, $b.Length, '10.1.%BOX%.1', 7000); $ep = New-Object Net.IPEndPoint([Net.IPAddress]::Any, 0); try { $r = $u.Receive([ref]$ep); if ($r.Length -ge 30) { $r[29] } else { 0 } } catch { 0 }; $u.Close()" 2^>nul') do set "BOXMODEL=%%m"
if "%BOXMODEL%"=="0" goto askRestart
if "%BOXMODEL%"=="%MODEL%" goto askRestart
echo box %BOX% is a dmxbox %BOXMODEL%.
echo.
echo       %FILE% is the firmware of dmxbox %MODEL%, not of a dmxbox %BOXMODEL%.
choice /c yn /n /m "      Put it on all the same? Press y or n: "
if errorlevel 2 goto failed
echo.
<nul set /p "=[1/4] Looking for the boot loader ... "
:askRestart
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
echo       Otherwise: %BYHAND%
echo       %BYHAND2%.
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
<nul set /p "=[2/4] Writing %FILE% onto %DRIVE% ... "
copy /b /y "%FIRMWARE%" "%DRIVE%\%FILE%" >nul 2>&1
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
echo The box took the file but did not restart. Is %FILE% the firmware of this box?
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
