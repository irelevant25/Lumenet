@echo off
rem Builds the firmware of dmxbox 4: dmxbox4.uf2, next to this file. The firmware is
rem the one all RP2040 boxes share (firmware\rp2040); box.h here makes it box 4's.
call "%~dp0..\..\..\firmware\rp2040\build.cmd" 4 %*
