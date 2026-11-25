@echo off
rem Starts a simulated DMX device or MIDI controller, as if it were plugged into this PC; the desk
rem finds it by itself.
rem   sim.cmd                     lists the devices and the options
rem   sim.cmd pico-1-universe     the dmxbox with the Pico (also pico-2-universes, pic32-1-universe,
rem                               pic32-2-universes)
rem   sim.cmd artnet              an Art-Net node
rem   sim.cmd sacn                an sACN receiver
rem   sim.cmd enttec              an Enttec DMX USB Pro
rem   sim.cmd apc-mini-mk2        an Akai APC mini mk2, with its page in the browser (also
rem                               nanokontrol2, x-touch-mini, x-touch-compact)
rem U pulls the device out and plugs it in again, Q ends it. Needs the .NET 10 SDK; the dmxboxes
rem are built from the firmware with Visual Studio's C compiler the first time. See DEV.md.
dotnet run --project "%~dp0backend\DmxBox.Sim" -v quiet -- %*
