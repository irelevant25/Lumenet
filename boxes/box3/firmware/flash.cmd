@echo off
rem Puts dmxbox3.uf2 (build.cmd makes it) onto a dmxbox 3, with the steps of
rem firmware\rp2040\flash.cmd, which all RP2040 boxes share.
call "%~dp0..\..\..\firmware\rp2040\flash.cmd" "%~dp0dmxbox3.uf2"
