@echo off
rem Lumenet - makes publish\Lumenet.exe: the desk program with the page and the examples inside it,
rem one file for Windows 10 and 11 (64-bit) that needs nothing installed. It sits in the notification
rem area of the taskbar: its icon opens the page, its menu shows the log and exits.
rem
rem   publish.cmd                     version 0.1.<number of commits>
rem   publish.cmd --version 0.1.7     another version
rem
rem GitHub makes the same file after every push to main and puts it on the Releases page.

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

node "%~dp0publish.mjs" %*
exit /b %errorlevel%
