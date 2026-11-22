# Building and changing Lumenet

For people who build the dmxbox or change the code: the box's hardware and firmware, the PC side, how the
desk is built and tested, the DX protocol, and what has been tried so far. The [README](README.md) says what
Lumenet is and how to get it; the [manual](MANUAL.md) describes the desk.

## The dmxbox in short

A small box with a **USB socket** on one end and a **3-pin XLR DMX output** on the other.
One USB cable powers it and carries the data.

When you plug it in, the computer sees a new network adapter with exactly one device behind it: the box,
at **192.168.7.1**. So the box is controlled over UDP like a network device, but there is no network to set up:

* **Art-Net** (UDP 6454) is supported, so QLC+ and other lighting software work with it.
* The **DX protocol** (UDP 7000) is a custom, reliable command protocol. It uses sequence numbers, ACKs,
  retransmits and duplicate detection. Fades run on the Pico, so they stay smooth even if the PC hiccups.

```
 PC ──USB──► Pico core 0: USB network link, protocols, buttons, flash
             Pico core 1: fade engine ──► PIO + DMA ──► 6N137 ──► SN75176 ──► XLR-3
```

## What is where

| Folder / file | Contents |
|---|---|
| `firmware/` | Pico SDK project (C). Builds `dmxbox.uf2`; `flash.cmd` puts it onto the Pico. `firmware/test/` tests the network code on a PC. |
| `sim/` | The **same** engine and protocol C code as the firmware, running on a PC over normal sockets. Use it to develop software before the hardware exists. |
| `backend/` | .NET: the `DmxBox` library, the `dmxbox` command-line tool, the server of the Lumenet desk, `Lumenet.exe` (`backend/Lumenet`), and the tests. |
| `frontend/` | Angular: the page of the Lumenet fader desk. |
| `run.cmd` | Starts the Lumenet desk: backend and frontend together. |
| `publish.cmd` | Makes `Lumenet.exe`, the whole desk in one file. |
| `.github/` | The workflow that makes `Lumenet.exe` after every push to `main` and puts it on the Releases page. |
| `examples/` | The example projects of the desk, which its Load window lists, and an example show file of a theatre console, to import. |
| `docs/` | The screenshots of the README. |
| `schematic/` | The schematic, the breadboard plan and the perfboard plan (SVG, PNG, PDF for printing), and the scripts that draw them. |
| `3d/` | Blender model, renders, and a drawing of the board's tracks. |
| [BOM.md](BOM.md) | Parts list for ordering. |
| `archive/` | Earlier designs with an Ethernet port. Not needed for this box. |

Where things stand: [Verification status](#verification-status), at the end.

---

## 1. Hardware

Parts: see [BOM.md](BOM.md). Pictures: [top](3d/dmxbox_usb_top.png), [solder side](3d/dmxbox_usb_bottom.png),
[USB end](3d/dmxbox_usb_usb_side.png), [DMX end](3d/dmxbox_usb_dmx_side.png), [closed box](3d/dmxbox_usb_closed.png).

Schematic: [drawing](schematic/dmxbox_schematic.png). To try the circuit on a breadboard first:
[breadboard plan](schematic/dmxbox_breadboard.png). Both are in [one PDF](schematic/dmxbox_drawings.pdf) for printing.
To solder it on a perfboard: [perfboard plan](#building-it-on-a-perfboard).

Tracks: the board is 100 × 66 mm and has every track on the solder side
([drawing](3d/dmxbox_usb_routing.png), [SVG](3d/dmxbox_usb_routing.svg)).

Everything with many pins sits in a socket (Pico, 6N137, RS-485 transceiver, DC-DC converter), so it can be
replaced. U5 can be an SN75176BP or a MAX485: same pins, same socket (see [BOM.md](BOM.md)).

The Pico sits at the left end of the board, with its micro-USB socket at the wall of the box. The cable plugs
straight into the Pico through an opening in the wall.

### Pin map

| Pico pin | GPIO | Connects to |
|---|---|---|
| 1 | GP0 | 6N137 pin 3 (LED cathode) |
| 24 | GP18 | Button SW1 "blackout" to GND |
| 25 | GP19 | Button SW2 "preset 1" to GND |
| 36 | 3V3(OUT) | R3 (180 Ω) to 6N137 pin 2 (LED anode) |
| 39 | VSYS | DC-DC converter U4 pin 1, and capacitor C4 |
| 23, 28, 33 | GND | Ground. The other five ground pins have no track: the Pico joins all eight itself. |
| 21, 22 | GP16, GP17 | Serial log (TX, RX, 115200 baud). No track; connect a serial adapter only if you want to read the log. |
| 40 | VBUS | Not connected |

All pins can be changed in `firmware/src/config.h`.

### Schematic (signal level)

![Schematic of the USB version](schematic/dmxbox_schematic.png)

The same in text:

```
              LOGIC SIDE (Pico GND)                   ║           ISOLATED SIDE (GND2)
                                                      ║
 USB 5 V ──►|── VSYS (Pico 39) ──┬── U4 pin 1 (+in)   ║   U4 pin 7 ── +5V_ISO ──┬── U3 pin 8 and pin 7
     (diode on the Pico)      C4 ┴ 4.7µ               ║   (RKK-0505S/H)         ├── R4 1k ── U3 pin 6 ──► U5 pin 4 (DI)
 GND (Pico 23, 28, 33) ──────────┴── U4 pin 2 (−in)   ║                         ├── U5 pin 8, pin 3 (DE), pin 2 (/RE)
                                                      ║                         ├── R2 270R ── GND2
 Pico 3V3 ── R3 180R ── U3 pin 2 (LED anode)          ║                         └── C1, C2 100n and C3 10µ ── GND2
 Pico GP0 ───────────── U3 pin 3 (LED cathode)        ║   U4 pin 5 ── GND2 ── U3 pin 5, U5 pin 5, XLR pin 1
 Pico GP18 ── SW1 ── GND                              ║   U5 pin 6 (A) ── XLR pin 3 (Data+)   and D1 to GND2
 Pico GP19 ── SW2 ── GND                              ║   U5 pin 7 (B) ── XLR pin 2 (Data−)   and D3 to GND2

 U3 = 6N137, U4 = Recom RKK-0505S/H, U5 = SN75176BP or MAX485
 D1, D3 = P6KE6.8CA, a TVS protection diode (both the same part, either way round)
```

U4 has seven pin positions, of which four have a pin: 1 and 2 on the logic side, 5 and 7 on the isolated side.

How the data path works: GP0 low lights the optocoupler LED, the 6N137 output goes low, the transceiver
drives the line low. An unlit LED means "line idle", so the line stays quiet while the Pico boots.
DE is tied high: the box only transmits.

Rules that matter:

* **A = Data+ = XLR pin 3, B = Data− = XLR pin 2**, pin 1 is the common. Swapping A and B is the most common DMX build mistake.
* **Never connect GND2 to the Pico's GND.** That would defeat the isolation. The Pico's ground is the
  computer's ground now, so the isolation is also what protects the computer from faults on the DMX line.
* **No 120 Ω terminator in the box.** The box is the start of the line. Terminate at the last fixture.
* Keep the logic and the isolated side apart on the board. Only the 6N137 and the DC-DC converter bridge the gap.
* DMX512 officially uses 5-pin XLR. Many fixtures have 3-pin; the wiring of pins 1 to 3 is the same.

### Building it on a breadboard

![Breadboard plan](schematic/dmxbox_breadboard.png)

The picture lists every part and every wire with its holes. It is drawn for one full-size breadboard
(830 holes, rows 1 to 63). The circuit does not fit on a half-size board (400 holes).

* Lay the board with row 1 at the left and the letters a to e at the bottom. If the letters on your board
  run the other way, go by the picture.
* **The two power lines at the top edge are the isolated side:** red (+) is +5V_ISO, blue (−) is GND2.
  Nothing from the Pico goes into them. The two power lines at the bottom edge stay empty.
* The plan only uses the right half of the power lines, so it also works on boards whose lines have a break
  in the middle.
* The chips sit across the middle gap with the notch towards the Pico. U4 sits with its two close pins
  towards the Pico. The buttons only work across the middle gap.
* On the breadboard the Pico's ground comes from pins 38 and 18. Any ground pin will do: the Pico joins them.
* A breadboard keeps the two sides only one hole apart. That is fine for a test on the desk, but it is not
  the isolation the real board will have.

A safe order:

1. Put in the parts, but leave U3 and U5 out. Put in wires 1 to 20.
2. Plug in the USB cable. Between the red and the blue line there should be about 5 V (4.6 to 5.5 V).
   If not, unplug and check U4, C3 and wires 1, 2, 9 and 10.
3. Unplug. Put in U3 and U5. Solder three wires to the XLR jack and plug them in (wires 21 to 23).
4. Plug in again and measure with a multimeter (DC volts) from XLR pin 3 (red probe) to pin 2 (black probe).
   What to expect depends on the Pico:
   * **Without firmware** the line rests: pin 3 is 2 V or more above pin 2 (4.28 V on the first build).
   * **With the firmware** the Pico sends DMX all the time, and the meter shows the average of it. With all
     channels at 0 that is about −2.5 V (pin 3 *below* pin 2). Now set all channels to full, in
     PowerShell: `dmxbox set 1 (,255 * 512)`. The reading turns positive, about +3.5 V.
     `dmxbox set 1 (,0 * 512)` brings it back. If the reading follows like this, the whole path from the Pico
     to the jack works. If the signs are the other way round, Data + and Data − are swapped.
   * **A steady positive reading that never changes** although the firmware runs means the data does not
     reach U5. First press the Pico firmly into the board: one pin without contact is enough.
5. If the firmware is not on the Pico yet, [flash](#flash) it. Then go on with [Check that it works](#check-that-it-works).

The plan is checked by the script that draws it (`schematic/draw.py`): every leg and wire end has a hole of
its own, and the holes joined by the board and the wires give exactly the connections of the schematic.

### Building it on a perfboard

![Perfboard plan, parts side](schematic/dmxbox_perfboard.png)

The soldered version, on a 70 × 90 mm perfboard with 26 × 31 holes (lettered A to Z, numbered 1 to 31), the
kind where every hole has a solder pad of its own. If the pads of your board are joined in strips or in
groups, this plan does not fit it.

* [The solder side](schematic/dmxbox_perfboard_solder.png) shows the tracks, as you see them with the board
  turned over.
* [The lists](schematic/dmxbox_perfboard.md) give the holes of every part and every track from hole to hole.
* [One PDF](schematic/dmxbox_perfboard.pdf) has both drawings for printing.

The 50 × 70 mm board (18 × 24 holes) is not used: the Pico alone takes 8 of its 18 columns and 20 of its 24
rows, which leaves too little room for the isolated side behind a gap.

How to read the plan:

* A hole is written with a dash: N-3 is column N, row 3. (C1, R3 or U4 without a dash are parts.)
* **Columns O and P stay empty from top to bottom.** That is the isolation gap. Everything left of it belongs
  to the Pico and the computer, everything right of it to the DMX line. Only U3 and U4 reach across.
* The Pico stands at the left edge with its USB socket at row 1. Everything with many pins sits in a socket.
* A track is a line of neighbouring pads joined on the solder side: lay a bare wire along them, or run solder
  from pad to pad. A pad in a track with no leg in it is just a place the track passes.
* One connection is an insulated wire (W1, from B-1 to M-4), because it has to get past the Pico's socket
  strip. Run it on whichever side you like.
* Each button has four legs. Its signal goes to one corner and ground to the opposite corner; the other two
  legs are soldered for a firm seat but join nothing. That works whichever way the button is joined inside.
* The screw terminal J1 has, from the top: XLR pin 2 (Data −), pin 3 (Data +), pin 1 (common).
* U4's socket strip has seven contacts, the converter only four pins. Pull contacts 3 and 4 out of the strip
  before soldering it in: they would stand in the gap (O-8 and P-8).

A safe order:

1. Solder the socket strips and sockets, then the resistors and diodes, then the capacitors, the buttons
   and J1. Mind the notch of U3 and U5 (towards row 1) and the plus leg of C3 and C4.
2. Join the pads track by track after the solder-side drawing, and tick each track off in the list.
3. Before anything is plugged in, test with a multimeter set to continuity. Every track must conduct from end
   to end. Tracks of different colors must not conduct to each other. **Nothing left of the gap may conduct
   to anything right of it.**
4. Plug in the Pico and U4, leave U3 and U5 out, and connect the USB cable. Between S-8 (plus) and Q-8 (minus)
   there should be about 5 V (4.6 to 5.5 V).
5. Unplug, put in U3 and U5 (pin 1 at the notch), wire J1 to the XLR jack, and go on with step 4 of the
   breadboard list above: the measurement between XLR pin 3 and pin 2.

How the plan is made: the places of the parts are chosen by hand, and `schematic/perfboard.py` finds the
tracks. A separate check in the same script then confirms that the joined pads give exactly the connections
of the schematic (whichever way the buttons are joined inside), that every leg has a hole of its own, that
nothing reaches across the gap, and that no two parts stand in each other's way. Run with `--selftest`, the
script plants 19 mistakes (a missing piece of track in each connection, Data + and Data − swapped at J1, U5
turned round, a solder bridge, a track across the gap, and others) and confirms that the check notices each.

### Power

* **From the USB cable.** The box draws about 0.2 A (estimate: about 30 mA for the Pico and 100 to 150 mA
  into the DC-DC converter). A normal USB port gives 0.5 A. The box tells the computer it may take up to 0.3 A,
  so a port that only allows 0.1 A (an unpowered hub, some phones) refuses it.
* **While the computer sleeps** the box keeps drawing its normal current and keeps the DMX line going. Strictly,
  USB asks a sleeping device to draw almost nothing; computers do not enforce that.
* **Without a computer:** any USB charger or power bank. The buttons and the stored presets keep working.
* **Why U4 hangs on VSYS and not on VBUS.** VSYS is the USB voltage behind the Pico's own diode, about 4.7 V.
  U4 is not regulated: its output follows its input and rises a few percent when little current is drawn.
  Fed from VSYS, the isolated side ends up close to 5 V.
* **3.3 V:** the Pico's regulator only feeds the optocoupler LED (about 9 mA).
* **Isolated side:** R2 is a base load for U4. It keeps the output voltage from rising when the line is idle.

### Box

* The model's box is 108 × 75 × 38 mm outside (103 × 70 × 33 mm inside).
* The opening for the USB plug is 12 × 8 mm, so that the plug's plastic grip fits into the wall. The Pico's
  socket ends 0.2 mm short of the wall's inner face when the board's left edge is 1.5 mm from it.
* One of the four board screws is under the Pico, between its pin rows. Put it in before you plug the Pico in.
* The two buttons are on the board, inside the box. The lid in the model has no openings for them.

---

## 2. Firmware

### Build

You need the Pico SDK (2.1.0 or newer, with its TinyUSB submodule), an ARM toolchain (`gcc-arm-none-eabi`),
CMake, and Ninja or Make. It is built and tried with Pico SDK 2.3.1 and Arm GNU Toolchain 14.2.Rel1.

**Windows:** `firmware\build.cmd` does it in one step. It expects the tools in the folder `.pico-sdk` in your
user folder, laid out the way the VS Code "Raspberry Pi Pico" extension lays them out:

| Folder in `.pico-sdk` | What it holds |
|---|---|
| `sdk\2.3.1` | The Pico SDK from GitHub, with its submodule `lib/tinyusb` |
| `toolchain\14_2_Rel1` | Arm GNU Toolchain for Windows, from arm.com |
| `picotool\2.3.1\picotool` | `picotool`, ready-made in Raspberry Pi's `pico-sdk-tools` releases on GitHub |
| `tools\2.3.1\pioasm` | `pioasm`, from the same place |

CMake and Ninja are taken from the PATH, or from Visual Studio if it is installed.

**Linux and macOS** (this line has not been run yet):

```bash
cd firmware
mkdir build && cd build
PICO_SDK_PATH=/path/to/pico-sdk cmake ..
make -j
```

Both produce `build/dmxbox.uf2`; `build.cmd` also puts a copy into `firmware/`, next to `flash.cmd`. For a
Pico W or Pico 2 add `-DPICO_BOARD=pico_w` or `-DPICO_BOARD=pico2` after `build.cmd` or to the `cmake` line,
and start with an empty `build` folder. On a Pico W the LED stays dark.

### Flash

Flashing replaces whatever was on the Pico before, for example MicroPython and the scripts stored with it.

**Windows:** `firmware\flash.cmd` does it. It takes the `dmxbox.uf2` in its own folder, so the two files can
also be copied together to another computer. Start it, then unplug the box, hold **BOOTSEL** on the Pico and
plug the USB cable in again. It finds the Pico's drive by itself (if there are several, it asks which one),
writes the file, waits until the Pico has restarted and then until the box answers at 192.168.7.1, showing
each of the four steps as it goes. At the end it says whether it was successful and waits for a key.

**By hand:** hold **BOOTSEL** while plugging the Pico into USB. It mounts as a drive named RPI-RP2; copy
`dmxbox.uf2` onto it. The Pico then restarts by itself and the drive disappears. If the drive stays, wait a
few seconds and copy the file again.

### Configure

Edit `firmware/src/config.h`:

* The address of the box (default **192.168.7.1**) and the address it gives the computer (192.168.7.2)
* Art-Net universe (default 0)
* Number of channels sent per frame (default 512)
* Pins, and the button preset and fade time

### Check that it works

* **Plug it in.** A new network adapter appears on the computer and gets the address 192.168.7.2 from the
  box within a few seconds.
* **Ping:** `ping 192.168.7.1` is answered by the box.
* **Control:** `dmxbox ping` shows the firmware version and the state of the box (see [Command line](#command-line)).
* **Windows shows "This device cannot start (code 10)":** the firmware was built without the correction
  described under `usb_net.c` below. Build it from the current sources.
* **On-board LED:**

| LED | Meaning |
|---|---|
| slow blink (1 Hz) | Powered, but no computer attached (for example on a charger) |
| on | Connected to a computer, idle |
| flicker | Art-Net or control packets arriving |

There is no USB serial port any more. The log goes to the serial pins GP16 / GP17 instead.

### How it works

* **`dmx_tx.pio`:** a 9-instruction PIO program. It generates the break (176 µs), the mark-after-break (12 µs)
  and 250 kbaud 8N2 bytes, with exact timing and no CPU involvement. DMA feeds it the 513-byte packet.
  A full universe refreshes at **43 Hz**. Lower `DMX_CHANNELS` for a faster refresh.
* **`usb_net.c`:** makes the Pico a USB network adapter, using the USB library that comes with the Pico SDK
  (TinyUSB, class CDC-NCM). It only moves raw Ethernet frames between the USB cable and `net.c`.
  One question of the computer it answers itself instead of leaving it to TinyUSB: how the blocks of frames
  are laid out. TinyUSB up to 0.18 (the version in Pico SDK 2.3.1) gives numbers there that Windows has
  refused since an update of September 2026.
* **`dhcp_server.c`:** hands the computer its address, so nothing has to be configured by hand.
* **`net.c`:** the network stack, about 200 lines: it answers ARP and ping, and receives and sends UDP.
  The box never starts a conversation, so every packet goes straight back to where the request came from.
  No ARP cache, no gateway setting.
* **`engine.c`:** two layers, merged:
  * The **cue layer** is driven by the DX protocol and the buttons. Every channel has its own fade,
    computed in 16-bit at frame rate.
  * The **Art-Net layer** holds the last ArtDmx data.
  * `output = merge(cue, artnet) × master`, or 0 during blackout.
* **Merge modes:**
  * **HTP** (default): the highest value wins. The Art-Net look is *held* even if the PC goes to sleep,
    until you call `release-artnet`.
  * **Override:** Art-Net replaces the cue layer while packets keep arriving. After 3 s of silence, the
    cue layer takes over again.
* **Presets:** 16 snapshots of the current look, stored in the last 12 KB of flash with a CRC.
  They survive power cycles. While flash is being written, core 1 is paused safely (`flash_safe_execute`),
  and the DMX line just idles for about 150 ms, which the DMX standard allows.
* **Losing the computer:** as long as the box has power, it keeps outputting the last state. The buttons work
  without a computer: GP18 toggles blackout, and GP19 fades to preset slot 0 in 2 s.

> **The box's own master and blackout act on every channel.** That is right for dimmers and RGB PARs; with
> moving heads a blackout of the box also sends pan and tilt to 0. Lumenet therefore does its master and
> blackout in the levels it sends, on the light only (see the Fixtures view), and keeps the box's own
> master at full; the box's blackout is only that of its button.

### Testing the network code on a PC

`firmware\test\build.cmd` (Visual Studio C compiler) builds and runs a test program. It runs the real
network stack, the address server, the USB glue and the firmware's main program on pretend hardware, and
checks ARP, ping, UDP, DHCP, Art-Net and the control protocol, including damaged packets (198 checks). On Linux or macOS use the `cc` line at the top of `test_net.c`.

---

## 3. PC side (.NET)

Needs the .NET 10 SDK.

### Connection

Plug the box in. There is nothing to set up: the computer gets 192.168.7.2 from the box, and the box is at
192.168.7.1.

Windows 11, macOS and Linux have the driver for this kind of USB network adapter (CDC-NCM) built in.
So far the box has been tried on one Windows 11 computer only.
Windows 10 has the driver too, but usually does not pick it by itself. The firmware sends a hint that names
the driver. On Windows 11 that hint is what selects the driver; on Windows 10 it is untested. If the device shows as unknown, choose the driver by hand in Device Manager:
update the driver of the unknown device, pick from the list, then Network adapters, Microsoft,
"UsbNcm Host Device". Tablets: iPads and many Android tablets accept USB network adapters, but this box has
not been tried on one.

### Command line

```powershell
dotnet build backend/DmxBox.sln -c Release     # once; gives backend\DmxBox.Cli\bin\Release\net10.0\dmxbox.exe

dmxbox discover                  # ArtPoll: lists every Art-Net node
dmxbox ping
dmxbox set 1 255 0 0             # ch1=255, ch2=0, ch3=0
dmxbox fade 1 0 80 255 -t 3      # 3-second fade, computed on the box
dmxbox preset save 0
dmxbox preset go 0 -t 2
dmxbox master 128
dmxbox blackout on
dmxbox get 1 12                  # what is on the DMX line right now
dmxbox merge override            # or: htp
dmxbox artnet 1 255 255          # one raw Art-Net frame
dmxbox release-artnet
dmxbox demo cues                 # demos for 4 RGB PARs at channels 1-12 (also: demo stream)
```

The tool talks to 192.168.7.1 unless you name another address with `-H <address>` or in the environment
variable `DMXBOX_HOST` (for example `-H 127.0.0.1` for the simulator).
`dmxbox --help` lists everything. Without building first: `dotnet run --project backend/DmxBox.Cli -- ping`.
In the first seconds after plugging in, `discover` can come back empty: try again. If it stays empty
although `ping` works, let the program through the Windows firewall.

### Library

Reference the project `backend/DmxBox/DmxBox.csproj`.

```csharp
using DmxBox;

using var box = new DmxBoxClient("192.168.7.1");
box.Fade(1, [255, 120, 20, 255, 120, 20], TimeSpan.FromSeconds(2.5));   // two RGB PARs at channels 1-6
box.PresetSave(3);
Console.WriteLine(box.Ping());

using var art = new ArtNetSender("192.168.7.1", universe: 0);           // for effects computed on the PC
art.Send(frame);                                                         // up to 512 bytes, about 40 times per second
```

A rejected request throws `DmxBoxException` (with the box's status, e.g. `BAD_ARG`); no answer after all
retransmits throws `DmxBoxTimeoutException`. `backend/DmxBox.Cli/Demos.cs` has a cue demo (box-side fades) and a
streaming rainbow demo (Art-Net).

### QLC+ (or any Art-Net software)

Add an Art-Net output on the network adapter with the address 192.168.7.2 (that is the box's USB link), for
universe 0 and IP 192.168.7.1. `discover` (or QLC+'s own scan) should find a node called `dmxbox`.

To control the lights from a tablet or phone over Wi-Fi, run the lighting program on the computer the box is
plugged into and open that program's remote page on the tablet (QLC+ has a web interface for this). The box
itself cannot be connected to a router.

### Simulator and tests

```powershell
sim\build.cmd                                  # builds sim\dmxbox_sim.exe (Visual Studio C compiler)
sim\dmxbox_sim.exe --show 12                   # prints channel values as they change
# another terminal:
dmxbox -H 127.0.0.1 fade 1 255 128 0 -t 2

dotnet test backend/DmxBox.sln                 # the client against the simulator, and the desk server
```

`dmxbox_sim.exe --drop 0.3` loses 30 % of control packets in each direction. Use it to test the reliability
layer; the test suite does exactly this. On Linux or macOS build the simulator with `make -C sim`.

---

## 4. The desk: how it is built

From the sources the desk needs the .NET 10 SDK and Node.js 22:

```powershell
run.cmd            # starts the backend and the frontend; then open http://localhost:4200
```

Ctrl+C stops both. The desk also works without the box: it shows "No box" and sends the whole state as
soon as a box answers.

* `backend/DmxBox.Server` is the desk itself. It holds the state (levels, memories, sequences, the stack,
  notes, the project), works out the output and sends it to the box through the `DmxBox` library. A small
  clock in it lets time pass about 30 times a second, for the sequences and the stack. Its settings are in
  `appsettings.json`: the address of the box (`Box:Host`), the projects folder (`Projects:Folder`), the
  folder of the examples (`Projects:Examples`, at first `examples` beside the program), the built page it
  hands out (`Ui:Folder`), the file with the LAN password (`Lan:PasswordFile`) and the autosave file
  (`Autosave:File`; the autosave's own settings go beside it).
* `frontend/` is an Angular app. It only shows the desk and passes on what you do, over SignalR (`/hub/desk`).
  Because the state lives in the backend, reloading the page loses nothing.
* The backend listens on every network card (`Urls` is `http://*:5080`), but serves another device only while
  LAN is on and only with the password; while LAN is off it closes such a connection before reading it. Two
  more rules keep other websites that are open in a browser from operating the desk: the backend answers only
  to the names of this PC (`AllowedHosts`, plus this PC's own addresses and computer name), and the hub only
  to the pages listed in `Server:AllowedOrigins`, plus the page the backend hands out itself. With `Urls` set
  to `http://localhost:5080` the desk stays on this PC for good.

### Lumenet.exe and its releases

`publish.cmd` (with `publish.mjs`) builds the page and then publishes `backend/Lumenet` as one self-contained
file, `publish\Lumenet.exe` (about 60 MB, .NET included):

```powershell
publish.cmd        # builds the page, then publish\Lumenet.exe
```

* `backend/Lumenet` holds the tray, the log window, and the sources of `DmxBox.Server` compiled in as well (a
  program cannot be put into another one's single file). The page and the examples are resources of the exe;
  `make-icon.ps1` there draws its icon (three faders with knobs in the colors of the keys), which is the
  page's icon too.
* The version is 0.1 and the number of commits (`backend/Version.props` has the 0.1); the page shows it in
  the top bar.
* `.github/workflows/release.yml` runs after every push to `main`: the tests of the page, the box simulator
  built with `sim\build.cmd` (GitHub's Windows machines have Visual Studio's C compiler) and the tests of the
  desk program, then `publish.mjs`, then a release on the [Releases](../../releases) page with `Lumenet.exe` as its
  only file, named `v0.1.<number of commits>`, whose notes are the messages of the commits since the release
  before (`.github/release-notes.mjs`; the first release, those since the workflow came). When a test fails,
  there is no release.

### Tests

`dotnet test backend/DmxBox.sln` (475 tests: the client, the desk, solo, sequences and the stack with a
clock the tests set by hand, the rate, undo and the autosave, the stage plan and its picture, fixtures, their
attributes and their steps, the notes, project files with Export and Import,
the examples, the import of show files, the link to the box, the hub, LAN with a pretend PC and a
pretend phone), and in `frontend/`: `npm test -- --watch=false` (198 tests of the page).

---

## 5. The DX protocol (UDP 7000)

All integers are **big-endian**. Every message has a 12-byte header:

| Offset | Size | Field |
|---|---|---|
| 0 | 2 | magic `"DX"` |
| 2 | 1 | version = 1 |
| 3 | 1 | type |
| 4 | 4 | session ID: random per client; 0 = no duplicate detection |
| 8 | 2 | seq: +1 per new request |
| 10 | 2 | payload length |
| 12 | n | payload |

Every request gets an **ACK** (type `0x80`) back to the sender's IP and port. The ACK echoes the
request's session and seq, and its payload is `status u8` followed by data.

| Type | Name | Request payload | ACK data |
|---|---|---|---|
| 0x01 | PING | none | fw major, fw minor, flags, master, uptime_ms u32, preset mask u16, channels u16 |
| 0x10 | SET | start u16 (1-based), count u16, values[count] | none |
| 0x11 | FADE | start u16, count u16, duration_ms u32, values[count] | none |
| 0x20 | PRESET_SAVE | slot u8 | none |
| 0x21 | PRESET_GO | slot u8, duration_ms u32 | none |
| 0x22 | PRESET_CLEAR | slot u8 | none |
| 0x30 | MASTER | level u8 | none |
| 0x31 | BLACKOUT | 0/1 | none |
| 0x32 | RELEASE_ARTNET | none | none |
| 0x33 | MERGE_MODE | 0 = HTP, 1 = override | none |
| 0x40 | GET_OUTPUT | start u16, count u16 | values[count] |
| 0x41 | GET_PRESET | slot u8 | values[512] |

The PING flags byte: bit 0 = Art-Net live, bit 1 = blackout, bit 2 = override mode, bit 3 = Art-Net layer holding data.

Status codes: 0 OK, 1 BAD_LENGTH, 2 BAD_ARG, 3 UNKNOWN_TYPE, 4 STORAGE_ERROR, 5 EMPTY_SLOT.

### Reliability (TCP-ish, over UDP)

* **Client:** stop-and-wait. It sends a request and waits for the ACK with the same session and seq.
  If none arrives, it retransmits the identical packet with exponential back-off.
* **Box:** for each of the 8 most recent sessions, it remembers the last seq and the ACK it sent.
  * **Same seq again:** the request is a retransmit (the original or its ACK got lost). The box replays the
    cached ACK and does **not** execute the request again. Without this, a retransmitted FADE would restart
    the fade.
  * **Older seq:** the packet is stale and has been wandering the network. It is dropped silently.
  * **Newer seq:** the request is executed, and the ACK is cached.
* **Multiple threads or programs:** give each its own `DmxBoxClient` instance (its own session).

---

## 6. Ideas for later

* **Wi-Fi:** a Pico W has the same size and pins and has Wi-Fi on board. With a Wi-Fi part in the firmware the
  box could join a wireless network itself, and USB would only supply power.
* **sACN / E1.31** (UDP 5568, multicast) alongside Art-Net.
* **A second universe:** a second PIO state machine plus a second transceiver; the code is ready for more SMs.
* **Pushed state:** a "subscribe" message, so the box sends live output to your UI.
* **Fabrication files for the carrier board**, so it can be ordered instead of hand-wired.
* **A sturdier USB socket:** a USB-B socket in the panel with a short cable to the Pico inside the box.

## Verification status

* **Pin orders:** checked against the pinout of the Pico and the datasheets of the 6N137, the SN75176BP
  (same pins as the MAX485) and the Recom RKK-0505S/H (Rev. 5-2026).
* **Board routing:** 12 nets on 34 tracks, all on the bottom, pass a script check for clearance (0.25 mm),
  connectivity and the isolation gap. Supply tracks are 0.6 mm wide over their whole length. Three of the
  Pico's eight ground pins have a track. It is a drawing, not a manufacturing dataset, and no board has been built.
* **USB socket and box:** the socket's position comes from KiCad's 3D model of the Pico. The fit of a real
  plug in a real box has not been tried.
* **Network stack, address server and main program:** pass 198 checks on a PC (`firmware/test`), built with
  the PC compiler against stand-ins for the Pico and the USB library.
* **Engine, Art-Net and DX protocol:** pass 14 end-to-end tests of the .NET client against the simulator,
  including a run with 30 % packet loss in both directions.
* **Firmware on a real Pico (2026-11-18):** built on Windows with Pico SDK 2.3.1 and Arm GNU Toolchain
  14.2.Rel1, without warnings, and flashed onto a Pico with nothing connected to its pins. On one Windows 11
  computer the box appears as a network adapter, hands out 192.168.7.2 and answers ping. These commands of
  the `dmxbox` tool work against it: `ping`, `discover`, `set`, `get`, `fade`, `master`, `blackout`, `artnet`,
  `release-artnet`, and saving, showing and clearing a preset. Windows first refused the adapter (code 10);
  the correction in `usb_net.c` cured that.
* **Circuit on a breadboard (2026-11-18):** built after the [breadboard plan](#building-it-on-a-breadboard).
  The isolated supply reads 5 V. A multimeter at the XLR jack (pin 3 against pin 2) shows what was calculated
  for a DMX signal with the right polarity: 4.28 V on the resting line, about −2.5 V with all channels at
  zero, and a positive reading with all channels at full. Both buttons work: SW1 switches blackout on and
  off, SW2 fades to preset 0 in 2 s.
* **Lumenet desk (2026-11-18):** the backend passes 212 tests and the page 36. The page was operated in Chrome
  against the real backend: levels, memories, the four functions, notes, the independent keys, and saving,
  loading and starting a project. The owner then used it with the box and a real light, and it worked.
* **Lumenet, second day (2026-11-19):** the two views, the master section, the channel faders that follow a
  memory fader and the two note lines were checked in Chrome on a test copy without a box, at five window
  sizes. Not yet used by the owner, and not with a box.
* **Sequences and the stack (2026-11-19):** the backend passes 274 tests and the page 43. On a test copy
  without a box a sequence of four steps was built and run (the channel faders followed it), and a stack of
  four steps was built, reordered by dragging and by typing a number, played, paused, and saved and loaded
  with a project. Not yet used by the owner; no light has been driven by a sequence or the stack.
* **Rec seq and the live stack (2026-11-19):** the backend passes 287 tests and the page 52. On a test copy
  without a box a sequence of eight steps was recorded with Rec seq (the keys showed their step numbers) and
  ran in a loop; the stack was taken live, went round from its last step to its first by itself, and the
  lock was tried in every view. The layout was looked at in Chrome at five window sizes, from 390 × 844 to
  1920 × 1080. The list of numbers itself, when open, is drawn by the browser and was not looked at in a
  picture. Not yet used by the owner, and not with a box.
* **A real DMX light (2026-11-18):** one light with four RGB cells (12 channels from address 5) on the
  breadboard build showed everything correctly: each of the 12 channels alone and in the right order, full
  white, a fade to dark, and a fade from red to blue, without flicker.
* **Perfboard plan (2026-11-19):** 89 legs, one insulated wire and tracks for all 12 connections pass the
  script's check, and the check notices all 19 planted mistakes. The sizes of the parts in the plan come from
  the parts list, not from measuring the parts.
* **Perfboard (2026-11-19, evening):** soldered by the owner after this plan, with the second Pico in it.
  The light stayed dark at first: two tracks were open, found with a multimeter (the data track from U3 pin 6
  to R4 and U5 pin 4, and the 5 V run that feeds U3 and R4). With both mended, their light at address 5
  follows the box.
* **Lumenet on small screens (2026-11-19):** the layout was looked at in Chrome at seven window sizes, from
  320 × 568 to 1920 × 1080, with an emulated finger and with the mouse.
* **Example project (2026-11-19):** made on a test copy without a box, through the hub and the template
  window of the page, and saved by the desk itself. The backend passes 319 tests, among them four that load
  the example, save it again unchanged and check that no step or key points at an empty memory; the page
  passes 70. The notes were checked in Chrome at 1920 × 1080: none is cut off under its fader.
* **Import of show files (2026-11-19):** the backend passes 367 tests, 48 of them for the import: the reader
  of the format, with the examples of Strand's "Guide to ASCII Cues" and ETC's labels, the making of the
  project, the hub, and a file of 3 MB. The page passes 75. On a test copy without a box the example show file
  was imported in Chrome at 1920 × 1080 and 1366 × 768: the report, the memories with their notes and the 14
  steps of the stack came out as planned, and a file that is no show was refused with a message.
* **Settings, keyboard and the stack on Main (2026-11-20):** the backend passes 384 tests (32 + 32 keys of
  the independents in project format 4, projects of versions 1 to 3 still read; the LAN address and password),
  the page 95. On a test copy without a box, in Chrome at 1920 × 1080: the stack section on Main and the
  previous step on the Stack view, Play 93 × 67 pixels beside Prev, Pause and Next at 58 × 50; 8 channels on
  a page (64 page keys in three columns, no scrollbar), 128 (the rows scroll sideways, the memories with the
  channels, the page itself does not); 32 + 32 independent keys in two columns; the lettering from ×0.7 to
  ×1.5 with every key's lettering inside its key; a shortcut recorded and used (Ctrl+B for Black out);
  listening picked a key without pressing it, a locked Play included. Phones of 390 and 360 pixels: the five
  view links fit, and no view scrolls sideways (before, the four links were 3 pixels too wide at 360).
* **The stack wheel, keys under the faders and the Keyboard view (2026-11-20):** the page passes 109 tests
  (the backend did not change). On a test copy without a box, in Chrome: at 1920 × 1080 the wheel of 3 and of
  9 steps with every key of the side column at full height and the master fader 260 pixels high, the wheel
  rolling to the next step; at 1366 × 768 a wheel of 3 (also when 9 are asked for) with all keys 28 pixels
  high (before, the stack keys were about 12). A channel key to full and back to zero; one row of channels
  over two of memories; 128 channels on a page with scrollbars 18 pixels high, the channels scrolled without
  the memories; the Memories and Sequences views unchanged by it, the notes in the cells of the Memories
  view; no independents with 0 + 0 keys; Listen picked a key low in the list of the Keyboard view, which
  scrolled to it while the page and its top bar stayed in place; at ×1.5 with 64 channel pages every key
  holds its lettering. Phones of 390 and 360 pixels: the six view links fit, nothing scrolls sideways.
* **Autosave, undo, Flash, Tap and settings in the project (2026-11-20):** the backend passes 411 tests and
  the page 116. On a test copy without a box, with its own autosave file, in Chrome: a change kept within
  five seconds, the test copy's desk program stopped hard and started again, and the work was back under its
  name and file, unsaved, with the message in the top bar; once saved, the file went. Undo took a note back
  and Redo made it again, also with Ctrl + Y. Flash (now Solo) held a channel at 255 while the mouse button
  was down and let it back to 0. Three taps 0.4 seconds apart set a running sequence (137 a minute, with the
  delay of the clicks; Tap became Rate the same day). Channel size 16 went into a saved project, stayed with
  a new one and came back with a load. At 1920 × 1080 every key of the side column at full height and the
  master fader 278 pixels high; at 1366 × 768 all keys 26 pixels or more and the fader 60. The top bar fits
  at 1920, 1366, 1280, 1000 and 800 pixels and on phones of 390 and 360.
* **Solo, Rate, the colors of the keys, the autosave setting and tabs in the Keyboard view
  (2026-11-20):** the backend passes 412 tests and the page 122. On a test copy without a box, with its own
  autosave files, in Chrome at 1920 × 1080: the master section from top to bottom Clear, Solo, Black out,
  the level, the fader (178 pixels), Rate, the wheel and ×1.00, every key 33 pixels high and nothing
  scrolling; Solo and Pause white on Main and in the Stack view. Rate dark: the mouse wheel did not turn the
  wheel. Rate lit: one notch gave ×1.06, a drag of 60 pixels to the right ×1.88 with the dot turned 165°, the
  desk program had Rate on at 1.88, and a double click gave ×1.00. Solo held a channel at 255 and let it back
  to 0. The stack wheel's numbers 19 pixels high (13 before), three steps in sight. The Keyboard view: eleven
  tabs in one line; "black" gave Master 1/4 and 0 in the ten others. Autosave every minute, then every 5
  seconds with the file written within them, then off with the file gone. Pause made pink went into the
  project and onto Main, and All the usual colors took it back. At 1366 × 768 every key 20 pixels or more,
  the fader 50 and the rate wheel in sight; phones of 390 and 360 pixels with Rate and its wheel in a line
  of their own and nothing scrolling sideways.
* **The Stage view (2026-11-20):** the backend passes 430 tests and the page 132. On a test copy without a
  box, in Chrome: the example show got its plan of 14 lights through the hub and was saved by the desk
  itself. With Full stage up and a few channels set by hand, the back lights glowed blue, the front wash
  warm white, the two pot lights red at two levels and the hazer showed its level; with the rainbow and the
  fire running, the LED bar and the pot changed color as they played. In Edit a light was chosen (the
  panel showed it) and a drag of one square moved the mirror ball by exactly one square. The plan fits at
  1920 × 1080 and 1366 × 768 and on phones of 390 and 360 pixels, and the seven view links fit at 1920,
  1366, 1280, 1000 and 800 pixels and on those phones (from 1300 to about 1330 pixels a project name like
  "Example show" loses its last letters, as names are meant to). Not yet compared with a real rig.
* **General, the rate fader, the lights' faders, shapes and a picture (2026-11-21):** the backend passes 435
  tests and the page 136. On a test copy without a box, in Chrome: RATE was said not to work; on the desk
  program of that day a looping sequence of the example changed its step 12 times in 4 seconds with RATE
  dark and 47 times lit at ×3.98 (with the wheel), then 48 times at ×4.00 with the new fader (six arrow
  keys up); the fader dragged to the bottom gave ×0.10 and a double click ×1.00. Main at 1920 × 1080:
  MASTER over RECORD, GENERAL over STACK, then IND, every key 32 pixels high, the rate fader 527 and the
  master fader 687 pixels, nothing scrolling; at 1366 × 768, 1600 × 900 and 1280 × 1024 every key 38 pixels
  or more and both faders 120 or more; phones of 390 and 360 pixels without sideways scrolling. On the
  Stage view a click on the LED bar showed its four faders; its red fader to full made it red and channel 11
  of the desk 255. Made square and four squares wide, it lay on the lines of the grid and turned red all
  over with its one cell. A picture of 2400 × 1500 pixels chosen as a file came out as a JPEG of 68 KB, the
  stage fitted to it, under the stage and then under the whole plan, and went again with Remove.
* **Solo up, Solo down, and the stage of the project (2026-11-21):** the backend passes 440 tests and the
  page 137. On a test copy without a box, in Chrome, with the example show and Full stage up: with Solo up
  lit, Channel key 3 held on Main, the desk program had that key as held and a second page's stage plan
  showed Front left lit and every other light dark; let go, all came back. With Solo down lit, memory key 4
  (Full stage) held: its fader went to 0 and its lights dark, and back to full when let go. New gave an
  empty stage; loading the example brought its 14 lights back. General holds its four keys and the rate
  fader at 1920 × 1080, at 1366 × 768 (keys 41 pixels, fader 76) and on a phone of 360 pixels.
* **The Stage panel always there, sizing on the plan, double click and Clear (2026-11-21):** the backend
  passes 440 tests (unchanged) and the page 140. On a test copy without a box, in Chrome at 1920 × 1080: the
  plan kept its place and size (1155 × 924 pixels) with no light chosen, with one chosen and in Edit. In
  Edit the cursor over the LED bar showed ew-resize at its left and right edges, ns-resize at the back and
  the front, nwse-resize and nesw-resize at the corners and move in its middle; its right edge dragged two
  squares out made it 6 squares wide with its left edge where it was, and its front right corner one square
  down made it 6 by 2. A double click on it put its four channels to 255 and the next one to 0, in Edit too
  (where it stayed put); Clear put the lights of the plan to 0. Made RGB in 10 cells, all eight fields stood
  beside their labels inside the panel. On a phone of 390 pixels the panel lies under the plan, with nothing
  scrolling sideways.
* **Fixtures (2026-11-21):** the backend passes 450 tests and the page 159. In the tests: of two memories that
  hold a moving head at two places, the one raised last gives the place, as stored (a memory fader at half
  does not move the head half way), and the one before gives it again when that one goes down; a fader
  moved later takes its own channel only; a memory that does not hold the head leaves it alone; a sequence
  of two memory steps gives the head what its step gives; the master and blackout leave pan and tilt where
  they are; limits of 60 and 50 per cent gave 153, 128 and an orange 128, 64, 0. On a test copy without a
  box, in Chrome at 1920 × 1080: a new light of the example was made an American DJ Inno Pocket Spot from
  the library on GitHub (it offered the 9- and 11-channel modes; 11 channels, PFTFXXSDXXX). On the
  Fixtures view its key brought its dimmer to full and opened its shutter (8), a press on the pad three
  quarters across and a quarter down gave pan 191/255 and tilt 64/0, and Red of its color wheel turned the
  wheel to 8. A group of the head and the LED bar was made, and its fader took both down. The eight view
  links fit in one line from 1920 down to 800 pixels, and in two rows of four on phones of 390 and 360
  pixels, where the panel lies under the strips.
* **Steps of fixtures and groups (2026-11-21):** the backend passes 455 tests and the page 163. In the tests:
  a sequence of a fixture in red and a group in white gave 255, 0, 0 and then both pars at 128 (the RGBW one
  in its white channel); a step of a moving head gave its dimmer, opened its shutter (8) and left its pan; in
  the stack a fixture step moved the par's faders up over 2 s and the next step opened the head's shutter at
  once while the par went down; the project file names fixtures and groups by their place in its lists (a
  light that is gone as 0) and keeps the color. On a test copy without a box, in Chrome: a sequence of the
  LED bar in red and a group of the four back lights was put together in the table (Fixture, Group and the
  color box), and played the LED bar in red only; Rec seq with the keys of the Fixtures view put in a fixture
  step and a group step, and the key showed its step number; a Chase template over the group in blue made
  four fixture steps; a stack step of the group put the faders of its four lights to 200.
* **The order of the views, the palette of the key colors and red ✕ keys (2026-11-21):** the backend passes
  456 tests and the page 168. In the tests: views left out of the top bar and put in another order with ▲
  and ▼, in Settings and in the top bar alike, with the keyboard focus going with the arrow; a desk without
  Main opened on the first view of the new order; ↺ brought every view back in the usual order; a key color
  taken from the palette and one from Other; a project file and settings that still spell color the British
  way kept their colors, and are written the new way. On a test copy without a box, in Chrome at 1600 × 1000: Enter on ▲
  of Keyboard moved it twice to the left in the top bar with the focus staying on the arrow, ▲ of Main at the
  top went dark and the focus went to ▼, and ↺ gave the usual eight links; the palette of Pause offered the
  six colors with White ringed, and Red took it; the ✕ keys of the Sequences, Stack and Keyboard views were
  red, with a red edge under the pointer. The two drawings whose notes spelled color the British way were
  made again by their scripts; everything else they draw came out the same.
* **The palette of a key color closes on a press outside (2026-11-21):** the page passes 168 tests; in them a
  press elsewhere on the page closed the palette, a press on Other in it did not, and Esc closed it with the
  keyboard focus back on the color. On a test copy without a box, in Chrome at 1600 × 1000: a click on the
  hint, on the title of the views and on an empty corner each closed it; a click on another color opened that
  one instead; Esc, and a color taken with Enter, put the focus back on the color with its ring, and a color
  taken with the mouse put it there without one.
* **Notes, Export and Import, settings per device and six examples (2026-11-21):** the backend passes 474
  tests and the page 184. In the tests: the notes of a project in its file, in the history (typing that goes
  on is one step), locked while the stack is live and gone with a new project; a file of an earlier version
  with the settings of the PC read without them; a project exported and imported back in place of the open
  one, a stage imported in place of the project's and undone; every example opens without a file, with
  notes, in the current format, without settings, and with every memory, light and group its steps and keys
  use; the notes shown with only their marks (a script, a picture and their handlers taken out); Export and
  Import with nothing checked at first, .ASC taken alone, and nothing imported when a checked part is
  missing. The example files were made by a script and written by the desk program's own code; their notes
  come through the page's cleaner unchanged. On a test copy without a box, in Chrome at 1600 × 1000: Load
  listed the six examples and opened "3 Fixtures and the stage" without a file and with nothing to save;
  Notes showed its notes, Edit went from faint to full and put the keyboard in the text; a word typed,
  chosen, made bold, red and large lit B and ringed red, and went to the desk program (the page loaded
  again showed it); Export with Project, Settings and Keyboard saved one file with those three parts; a new
  project took the stage of that file with Import (10 lights, and Undo offered "importing a stage"). At
  390 × 844 the tools of Notes went into two lines with Edit at the right, and nothing scrolled sideways.
* **The library of QLC+ beside the Open Fixture Library (2026-11-22):** the backend passes 475 tests and the
  page 187. In the tests: both lists in one, each light with its library, kept for a week each; one library
  out of reach leaves the other's lights; ADJ finds American DJ and "flatpro" finds Flat Pro; QLC+'s newer
  files (a preset per channel) and older ones (groups, a byte for the fine part, Colour and Color) made into
  letters, fine channels, ranges with their colors (none for a slot of two colors) and an open shutter; a
  name with a sign such as "+" written for its address, and a name that leads out of the library refused;
  the desk program takes QLC+'s names of files as where a kind of light is. With the real libraries: of 84
  lights of QLC+ (the user's Dotz Bar 1.4, Inno Color Beam Quad 7 and CLP Flat Pro series, and every
  21st light of the list) all were read, and all 247 of their modes passed the desk program's own check of
  a kind of light; the Dotz Bar 1.4 in 12 channels is four cells of red, green and blue. On a test copy
  without a box, in Chrome: the window counted 666 lights of the Open Fixture Library (7 of them have
  capitals in their file names, which it had left out before) and 1,781 of QLC+; "adj dotz bar" found the
  Dotz Bar 1.4 of QLC+, and its 12-channel mode made the light a bar of four cells.
* **What the channels of a fixture do (2026-11-22):** the page passes 189 tests. In the tests: the list of a
  strobe with the values of each choice; the line under the faders before a fader moved and after (a fader
  moved to 32 said "S (4) at 32: Open (32 to 63)", and a screen reader hears "32, Open"); the pointer on a
  letter with all its values; a strobe of the Open Fixture Library named with its speed and at random. The
  moving head of the examples "4 Moving heads" and "6 Concert" was read again from the library for these
  names. On a test copy without a box, in Chrome: the Ultra Hex Bar 12 of ADJ from the Open Fixture
  Library in 12 channels showed its strobe as Closed (0 to 31), Open (32 to 63), Strobe, slow to fast (64 to
  95), Open (96 to 127), Pulse, slow to fast (128 to 159), Open (160 to 191), Random strobe (192 to 223) and
  Open (224 to 255); a color opened it at 32 ("32, Open"), and the strobe moved to 64 said "S (8) at 64:
  Strobe, slow to fast (64 to 95)" under the faders.
* **A strobe on the Stage view (2026-11-22):** the page passes 194 tests. In the tests: no strobe with the
  shutter open or closed, or in a range that does something else; the kind (strobe, pulse, ramp, at
  random) and the speed from the range (Hz at the place of the value, "fast to slow" turned round); a light
  of letters strobing above 9; a strobe speed channel without ranges ignored beside a shutter with ranges;
  the flashes on the screen (1.5 a second as they are, 11 as 2.5, 20 and more as 3); on the plan a bar at 20
  Hz flashing every 0.333 s with "⚡ 20 Hz" over it and in its title, and nothing while it gives no light.
  On a test copy without a box, in Chrome, with the example "4 Moving heads": the left head, its dimmer up
  and its shutter at 96 ("Strobe, 0.3Hz to 19Hz"), flashed on the plan every 0.381 s with "⚡ 13.3 Hz"
  over it; with less motion asked for it stopped flashing and kept the mark.
* **The color window of a step (2026-11-22):** the page passes 194 tests. In the tests: a press on Blue, then
  Red, left the window open, said "Chosen: #ff0000" and sent nothing; Use gave the step red and closed it;
  the window opened again on the step's color, White chose no color, and Cancel sent nothing. On a test copy
  without a box, in Chrome, with the example "3 Fixtures and the stage": the window of step 2.0 opened on
  its blue, Red and a drag across the field changed the color chosen while the window stayed open (also with
  the drag ending outside it), and Use closed it.
* **Left and right Ctrl and Alt in shortcuts (2026-11-22):** the page passes 198 tests. In the tests: the side
  of Ctrl and Alt in a shortcut as their own presses said it, no side when both are down or the side was not
  seen, AltGr as Right Alt without its Ctrl (and with a right Ctrl held with it), and the names "Left Ctrl +
  Z" and "Right Alt + 1"; on the desk, Set with the left Ctrl and B gave Black out "Left Ctrl + B", the right
  Ctrl and B then did not press it, and Undo's Ctrl + Z, which has no side, worked with the right Ctrl;
  AltGr and B pressed a Black out set earlier as Ctrl + Alt + B, and Set with them gave "Right Alt + B",
  which they then pressed. On a test copy without a box, in Chrome at 1600 × 1000: Set gave "Right Ctrl +
  B" for Black out and "Left Alt + C" for Clear; on Main the left Ctrl and B left blackout off, and the
  right Ctrl and B switched it on and, pressed again, off. The keys came from the test script, not from a
  keyboard, and a real AltGr key was not tried in Chrome.
* **Lumenet.exe, its tray and its releases (2026-11-22):** `publish.cmd` made `publish\Lumenet.exe` alone,
  61 MB, version 0.1.25. Started as a test copy on port 5081 (folders of its own, the box at 127.0.0.1, no
  page opened), it handed out the page from inside itself (also under /stage), the new icon, the scripts with
  their types and /api/health with 0.1.25; in Chrome the desk connected, the top bar said v0.1.25, Load listed
  the six examples and opened "6 Concert" with its 15 lights, and the page had no error. Started on 5080 while
  the desk of `run.cmd` had it, it said "Lumenet could not start. Another program uses the port of
  http://*:5080: Lumenet itself, or the desk of run.cmd. Only one desk can run on this PC at a time." and
  ended with 1 without starting its desk. A check program drove the real tray and log window: the menu Open
  Lumenet (bold), Show the log, Exit; the log window in Cascadia Mono, dark with dark scroll bars, an error in
  red with its stack under it; five lines that came while the top was read left the top in sight, three that
  came at the bottom were in sight; Esc and the close box hid the window without ending anything, and a line
  that came meanwhile was there when it was shown again; Exit ended the program. The release notes, tried in
  a copy of the repository: with a release three commits back, those three, their lines joined again and
  without Co-Authored-By. The workflow's version and test steps ran in Git's bash (198 and 475 tests). Its
  first two runs on GitHub passed the 198 tests of the page and 458 of the desk program, and failed the 17
  that drive the box simulator, which is built on a PC and is no part of the repository: the workflow now
  builds it first with `sim\build.cmd` (here those 19 tests passed with it), and a first release lists the
  commits since the workflow came. Lumenet started a second time (it then only opens the page) was not tried.
* **Show files of a real console (2026-11-19):** 30 show files that ETC SmartFades wrote (software 3.0.1 and
  3.0.2) went through the import. The first version read them wrongly: their "FOLLOWON 0" (no wait) as a
  follow at once, their patch the wrong way round, and it lost their labels. With the SmartFade's dialect
  (see Import) all 29 files that hold a show import without a line that could not be read; the 30th holds
  none and is refused with a message. Checked by hand in the results: the memories with their labels, the
  stack steps on the memories they play, waits of 50 s and 1:30 and a run of point cues, sequences with the
  same memory twice, labels with "Č", the independents. Each of the 29 also went into a desk and came back
  whole from a project file. The backend passes 377 tests, the page 75.
* **Templates, random order and speed (2026-11-19):** the backend passes 315 tests and the page 69. On a
  test copy without a box a Fire template on channels 1 and 2 was made and run: the two channels flickered
  each on its own (about 20 different levels each in 2 seconds), the running step was marked in the table
  in random order too, and the speed fader dragged with the mouse gave 5.80× and 0.50× without restarting
  the sequence. Not yet used by the owner, and not with a light.
* **Independents with memory keys (2026-11-19):** the backend passes 306 tests and the page 59. On a test copy
  without a box the new main layout (master and record in one column, the twenty independents at the right
  edge) and the notes under the memory keys of the Memories and Sequences views were looked at in Chrome at
  five window sizes, from 390 × 844 to 1920 × 1080: every key in its section, nothing out of the window.
  Not yet used by the owner.
* **LAN (2026-11-19):** the backend passes 303 tests and the page 55. The tests play a phone over this PC's
  own connection: refused while LAN is off, asked for the password, let in with the cookie (and not with the
  password as a cookie), cut off when LAN goes off, stopped after 5 wrong passwords, and all devices stopped
  after 20. On a test copy that listened on this PC only, the LAN key, the
  line with the address and the password, and the built page handed out by the backend (also under /stack,
  at phone size) were looked at in Chrome. **Not yet tried with a real phone**, and the Windows firewall
  question for `DmxBox.Server` has not been answered yet.
* **PIO program:** unchanged from the first version. That light accepts its signal; the timing has not been
  looked at with an oscilloscope or a DMX tester.
* **Not done yet:** more than one light or a long cable, the LED states, a preset surviving a power cycle on
  the real Pico, the two buttons on the perfboard, and any other computer (Windows 10, macOS, Linux, tablets).
  Next: check the LED, the buttons, and that a preset is still there after unplugging.
