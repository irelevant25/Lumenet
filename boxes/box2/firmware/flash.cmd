@echo off
rem Puts dmxbox2.hex (build.cmd makes it) onto a dmxbox 2 through J2, with
rem firmware\pic32\flash.cmd: "flash.cmd" for an MPLAB Snap, "flash.cmd PK4" or
rem "flash.cmd PK5" for a PICkit 4 or 5.
call "%~dp0..\..\..\firmware\pic32\flash.cmd" %1 "%~dp0dmxbox2.hex"
