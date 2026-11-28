@echo off
rem Builds the firmware of dmxbox 2 (PIC32MX270F256B) on Windows. Result:
rem firmware\pic32\build\dmxbox2.hex, and a copy of it in boxes\box2\firmware\, which
rem flash.cmd puts onto the chip through J2 with a programmer (see boxes\box2\BOM.md).
rem
rem It needs:
rem   MPLAB XC32 (free; v6.00 tried) in C:\Program Files\Microchip\xc32, or XC32 set to the
rem     folder of one version, for example C:\Program Files\Microchip\xc32\v6.00
rem   the PIC32MX device pack (PIC32MX_DFP) in %USERPROFILE%\.mchp_packs\Microchip, where MPLAB
rem     puts it too, or DFP set to the folder of one version of it
rem   TinyUSB: the copy in the Pico SDK (%USERPROFILE%\.pico-sdk\sdk\2.3.1\lib\tinyusb), the
rem     one the RP2040 boxes use, or TINYUSB set to another copy
setlocal enabledelayedexpansion
cd /d "%~dp0"

if not defined XC32 for /d %%v in ("%ProgramFiles%\Microchip\xc32\v*") do set "XC32=%%v"
if not defined DFP for /d %%v in ("%USERPROFILE%\.mchp_packs\Microchip\PIC32MX_DFP\*") do set "DFP=%%v"
if not defined TINYUSB set "TINYUSB=%USERPROFILE%\.pico-sdk\sdk\2.3.1\lib\tinyusb"
for %%d in ("%XC32%\bin\xc32-gcc.exe" "%DFP%\xc32\32MX270F256B" "%TINYUSB%\src\tusb.c") do if not exist "%%~d" (
    echo Missing: %%~d
    echo See "dmxbox 2: the PIC32 firmware" in DEV.md.
    exit /b 1
)
rem XC32 wants forward slashes in the pack's folder.
set "DFP=%DFP:\=/%"
set "TU=%TINYUSB%\src"
set "BOX=..\..\boxes\box2\firmware"
set "NAME=dmxbox2"

set "CHIP=-mprocessor=32MX270F256B "-mdfp=%DFP%""
set "FLAGS=%CHIP% -std=gnu11 -O1 -ffunction-sections -fdata-sections -Wall -Wextra -Wno-unused-parameter"
set "FLAGS=%FLAGS% -DCFG_TUSB_MCU=OPT_MCU_PIC32MX -Isrc -I"%BOX%" -I..\common -I"%TU%""

if not exist build\obj mkdir build\obj
del /q build\obj\*.o 2>nul
set "OBJECTS="
for %%f in (
    src\main.c src\board.c src\dmx_line.c src\storage_flash.c
    ..\common\engine.c ..\common\artnet.c ..\common\ctrl.c ..\common\rdm.c ..\common\dmx_in.c ..\common\presets.c
    ..\common\net.c ..\common\dhcp_server.c ..\common\usb_net.c ..\common\usb_descriptors.c
) do (
    echo %%~nxf
    "%XC32%\bin\xc32-gcc.exe" %FLAGS% -c "%%~f" -o "build\obj\%%~nf.o" || exit /b 1
    set "OBJECTS=!OBJECTS! build\obj\%%~nf.o"
)
rem TinyUSB's own files. Its PIC driver casts "volatile" away in many places and writes
rem "volatile static", which the compiler warns about; that is TinyUSB's business, so these
rem two warnings are off for its files.
for %%f in (
    "%TU%\tusb.c" "%TU%\common\tusb_fifo.c" "%TU%\device\usbd.c" "%TU%\device\usbd_control.c"
    "%TU%\class\net\ncm_device.c" "%TU%\portable\microchip\pic\dcd_pic.c"
) do (
    echo %%~nxf
    "%XC32%\bin\xc32-gcc.exe" %FLAGS% -Wno-discarded-qualifiers -Wno-old-style-declaration -c "%%~f" -o "build\obj\%%~nf.o" || exit /b 1
    set "OBJECTS=!OBJECTS! build\obj\%%~nf.o"
)

"%XC32%\bin\xc32-gcc.exe" %CHIP% -Wl,--gc-sections,--defsym=_min_heap_size=0,-Map=build\%NAME%.map ^
    -o build\%NAME%.elf %OBJECTS% || exit /b 1
"%XC32%\bin\xc32-bin2hex.exe" build\%NAME%.elf || exit /b 1
"%XC32%\bin\xc32-size.exe" build\%NAME%.elf
copy /y build\%NAME%.hex "%BOX%\%NAME%.hex" >nul || exit /b 1
echo.
echo Done: %~dp0build\%NAME%.hex
echo A copy is in boxes\box2\firmware: flash.cmd there puts it onto the chip.
