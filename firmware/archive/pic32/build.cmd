@echo off
rem Builds the firmware of the PIC32 box (dmxbox 2) on Windows.
rem Result: firmware\pic32\build\dmxbox2.hex, and a copy of it in firmware\pic32\, which
rem MPLAB IPE puts onto the chip with a PICkit or an MPLAB Snap (see DEV.md).
rem "build.cmd 1" builds the firmware of the box with one output: dmxbox2-1-universe.hex.
rem
rem It needs:
rem   MPLAB XC32 (free; v6.00 tried) in C:\Program Files\Microchip\xc32, or XC32 set to the
rem     folder of one version, for example C:\Program Files\Microchip\xc32\v6.00
rem   the PIC32MX device pack (PIC32MX_DFP) in %USERPROFILE%\.mchp_packs\Microchip, where MPLAB
rem     puts it too, or DFP set to the folder of one version of it
rem   TinyUSB: the copy in the Pico SDK (%USERPROFILE%\.pico-sdk\sdk\2.3.1\lib\tinyusb), the
rem     one the Pico box uses, or TINYUSB set to another copy
setlocal enabledelayedexpansion
cd /d "%~dp0"

if not defined XC32 for /d %%v in ("%ProgramFiles%\Microchip\xc32\v*") do set "XC32=%%v"
if not defined DFP for /d %%v in ("%USERPROFILE%\.mchp_packs\Microchip\PIC32MX_DFP\*") do set "DFP=%%v"
if not defined TINYUSB set "TINYUSB=%USERPROFILE%\.pico-sdk\sdk\2.3.1\lib\tinyusb"
for %%d in ("%XC32%\bin\xc32-gcc.exe" "%DFP%\xc32\32MX270F256B" "%TINYUSB%\src\tusb.c") do if not exist "%%~d" (
    echo Missing: %%~d
    echo See "The PIC32 box: building the firmware" in DEV.md.
    exit /b 1
)
rem XC32 wants forward slashes in the pack's folder.
set "DFP=%DFP:\=/%"
set "TU=%TINYUSB%\src"

set "CHIP=-mprocessor=32MX270F256B "-mdfp=%DFP%""
set "FLAGS=%CHIP% -std=gnu11 -O1 -ffunction-sections -fdata-sections -Wall -Wextra -Wno-unused-parameter"
set "FLAGS=%FLAGS% -DCFG_TUSB_MCU=OPT_MCU_PIC32MX -Isrc -I..\common -I"%TU%""
set "NAME=dmxbox2"
if "%~1"=="1" (
    set "FLAGS=%FLAGS% -DDMX_OUTPUTS=1"
    set "NAME=dmxbox2-1-universe"
)

if not exist build\obj mkdir build\obj
del /q build\obj\*.o 2>nul
set "OBJECTS="
for %%f in (
    src\main.c src\board.c src\dmx_out.c src\storage_flash.c
    ..\common\engine.c ..\common\artnet.c ..\common\ctrl.c ..\common\presets.c
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
copy /y build\%NAME%.hex %NAME%.hex >nul || exit /b 1
echo.
echo Done: %~dp0build\%NAME%.hex
echo A copy is next to this file: put it onto the chip with MPLAB IPE (see DEV.md).
