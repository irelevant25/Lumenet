@echo off
rem Builds the firmware of dmxbox 2: dmxbox2.hex, next to this file. The firmware is
rem firmware\pic32, with box.h from here.
call "%~dp0..\..\..\firmware\pic32\build.cmd" %*
