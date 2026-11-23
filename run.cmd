@echo off
rem Lumenet - starts the backend (.NET) and the frontend (Angular) in THIS console.
rem
rem   run.cmd                          the dmxbox on its USB cable (192.168.7.1)
rem   run.cmd --Box:Host=127.0.0.1     another address, for example the simulator in firmware\sim\
rem
rem Then open http://localhost:4200. Ctrl+C stops both.
rem The desk also works without the box: it shows "No box" until one answers.
rem For a phone or tablet in the same network, press LAN in the page: it shows the address and the password.

setlocal
cd /d "%~dp0"

where node >nul 2>nul
if errorlevel 1 (
  echo Node.js was not found in PATH. Install Node.js 22 or newer from https://nodejs.org
  exit /b 1
)

where dotnet >nul 2>nul
if errorlevel 1 (
  echo The .NET SDK was not found in PATH. Install the .NET 10 SDK from https://dot.net
  exit /b 1
)

node "%~dp0run.mjs" %*
exit /b %errorlevel%
