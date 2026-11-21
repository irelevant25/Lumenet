# dmxbox: USB to DMX512 with a Raspberry Pi Pico

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

| Folder / file | Contents |
|---|---|
| `firmware/` | Pico SDK project (C). Builds `dmxbox.uf2`; `flash.cmd` puts it onto the Pico. `firmware/test/` tests the network code on a PC. |
| `sim/` | The **same** engine and protocol C code as the firmware, running on a PC over normal sockets. Use it to develop software before the hardware exists. |
| `backend/` | .NET: the `DmxBox` library, the `dmxbox` command-line tool, the server of the Lumenet desk, and the tests. |
| `frontend/` | Angular: the page of the Lumenet fader desk. |
| `run.cmd` | Starts the Lumenet desk: backend and frontend together. |
| `examples/` | An example project for the desk, to load and look at, and an example show file of a theatre console, to import. |
| `schematic/` | The schematic, the breadboard plan and the perfboard plan (SVG, PNG, PDF for printing), and the scripts that draw them. |
| `3d/` | Blender model, renders, and a drawing of the board's tracks. |
| [BOM.md](BOM.md) | Parts list for ordering. |
| `archive/` | Earlier designs with an Ethernet port. Not needed for this box. |

> **State on 2026-11-18.** The box changed from Ethernet to USB on 2026-11-12. The firmware is compiled for
> the Pico and runs on one: on a Windows 11 computer the box shows up as a network adapter and answers ping,
> Art-Net and the control commands. The circuit is built on a breadboard: a multimeter at the XLR jack shows
> the DMX signal with the right polarity, both buttons work, and **a real DMX light follows the box correctly**.

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
   to end. Tracks of different colours must not conduct to each other. **Nothing left of the gap may conduct
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

> **Master and blackout scale every channel.** That is right for dimmers and RGB PARs. If you add
> moving heads, a blackout would also send pan/tilt to 0. In that case, add a channel mask in
> `engine_render()`.

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

## 4. Lumenet, the fader desk

A lighting desk in the browser, in the old style with faders. It needs the .NET 10 SDK and Node.js 22.

```powershell
run.cmd            # starts the backend and the frontend; then open http://localhost:4200
```

Ctrl+C stops both. The desk also works without the box: it shows "No box" and sends the whole state as
soon as a box answers.

**What is on the page**

The top bar has seven links: **Main**, **Memories**, **Sequences**, **Stack**, **Stage**, **Settings** and
**Keyboard**. Main is the desk itself. Memories only shows what is stored. Sequences and Stack are for the two
things that run by themselves (see below), Stage shows the lights of the rig in their colours, Settings is
for how this browser shows the desk (see [Settings](#settings)), Keyboard for its shortcuts. Beside the name
stand **LAN**, **Undo** and **Redo**, at the right the keys of the project. At the right of Main stand three
columns: the **Master** section (Black out and the master fader with its level) over **Record**; the
**General** section (Clear, Solo up, Solo down, Rate and the rate fader with the rate on top) over the **Stack**; and at
the right edge, over the whole height, the **Independents** (unless Settings shows none of their keys). On a
phone the master section, the general section and the stack lie across the top, one line each.

* **Channels:** two rows of faders (or one, as Settings says). The keys at the right choose which of the 512
  channels they show. When they do not fit into one column, they get flatter, and then they go on in another
  column beside it.
* **Memories:** one row of faders (or two), with page keys for the 128 memories. A memory holds a look:
  the levels the channel faders were set to at the moment it was saved. Its fader plays that look at the
  fader's level. For every channel the highest value wins, whether it comes from the channel fader or from
  a memory.
* **A memory fader moves the channel faders it uses.** A channel fader shows what goes out for its channel
  (before the master): its own level, or what the memories give it when that is higher. The slot is lit green
  up to the fader's own level and red above it, where a memory holds the channel. The own level is kept:
  bring the memory down, and the fader goes back to it. While a memory holds a channel up, its fader can
  still be moved; the handle stays where the memory holds it, and the green part shows what you set.
  Save mem stores the faders' own levels, not the part a memory adds.
* **Keys under the faders:** a press puts the fader to full, and back to zero when it is at full already (a
  fader somewhere between goes to full). It works the fader's own level: a channel that a memory holds at
  full still goes to full by its key. While a record key waits for a memory, the memory keys are for that
  (see Record); while a sequence is recorded, the channel keys put in steps. A channel key is lit when the
  channel is above zero. A memory key is half lit when something is stored in it and fully lit when its
  fader is up. A page key is fully lit for the page that is shown and half lit for another page with
  something on it.
* **Notes:** under each key are two lines to type into: which light or which look it is (11 characters per
  line). Each line stays where it was typed; the second can be used without the first.
* **Record** (under the master section) has Save mem, Edit mem, Rec seq, Copy and Del: press one, and the
  keys that can be used blink between their normal light and full.
  * **Save mem:** press a memory key, and the channel faders are stored there as a look.
  * **Edit mem:** press a memory that holds a look, and it is loaded into the channel faders. Change it, press
    Edit mem again, and it is stored back. A memory that holds a sequence does not blink: it is changed in
    the Sequences view.
  * **Rec seq** records a sequence key by key. Press it, then the memory for the sequence: an empty one, or
    one that holds a sequence already, which is then continued behind its last step. From then on every
    channel key, and every key of a memory that holds a look, puts in one more step: that channel or memory
    at full, with 1 second up, 1 second wait and 1 second down. A key shows the numbers of the steps it is in,
    small and yellow in its top left corner (the same channel can be used several times: 1, 5, 14). Press
    Rec seq again when the sequence is complete. A new sequence is played in a loop.
  * **Copy:** press the memory to copy from, then the memory to copy to. A sequence is copied like a look.
  * **Del:** press the memory to empty, whatever it holds.
  * Pressing the lit function again cancels it.
* **Clear** cancels an unfinished function and brings every channel fader and memory fader to zero.
* **Black out** sends zero to every address but leaves the desk as it is; the key blinks while it is active.
  The blackout button on the box does the same, and the desk shows it.
* **Master** dims the whole output.
* **Solo up** and **Solo down** (white, in General under Clear) change what a key under a fader or of the
  independents does while it is held; so does its shortcut. Lighting one darkens the other.
  * **Solo up:** the key puts its fader to full and every other light goes dark: only that channel (or that
    memory's look or sequence, at full) shines. Let go, the fader is back where it was and every light as
    before.
  * **Solo down:** the key takes only its own fader down, to zero; every other light stays. A channel goes
    dark even where a memory holds it up. Let go, it is back.
  * A solo changes what goes out, like Black out: the other faders do not move. The master still dims what a
    solo lets out, and Black out still darkens it. Several keys can be held at once; a key held on a phone
    counts for the whole desk, and the desk lets it go if that phone loses its connection. While a record
    key waits for a memory, the memory keys are for that, and while a sequence is recorded the channel keys
    put in steps, as always. Both dark again: the keys switch on and off as before.
* **Rate** (in General, under Solo down) is a speed master for the sequences. Dark, every sequence plays at its own
  speed; lit, at its own speed times the rate of the fader under the key, which stands on top of the fader
  (×1.00). The fader goes from ×0.1 at the bottom to ×10 at the top, with ×1 in the middle (a line marks it),
  as the speed fader of a sequence does: the lower half from ×0.1 to ×1, the upper half from ×1 to ×10, in
  steps of 0.05; the arrow keys go 0.05 below ×1 and 0.5 above. A double click puts it back to ×1.00. While
  Rate is dark the fader is dim; it can still be set, for the next time. A sequence goes on from where it is
  when the rate changes. Like the master fader, the rate is not saved with the project, and the other pages
  see it at once.
* **Undo** and **Redo** (top left) take back the last change of what the project holds, and make it again:
  storing, copying and emptying memories, sequences and their speed, the steps of the stack, notes and the
  channels and memories of the independent keys. Their tooltip says what they would do, for example "Undo
  storing memory 5", and the top bar says what they did. The faders, Clear, the master and the settings are
  no steps; changes of one kind in quick succession (the moves of a speed fader) are one.
  Up to 100 steps are kept, for the whole desk, whichever device made a change. Loading a project, a new
  one and an import start afresh, and while the stack is live they are locked. Ctrl + Z and Ctrl + Y are
  their shortcuts until the Keyboard view gives them others.
* **Stack** (under the general section) is the stack at hand on Main: Stack takes it live, Prev, Play, Pause
  and Next go through it, as on the Stack view. Under the keys stands a wheel of the stack's steps: the one
  that is on in the middle (green, with an arrow beside it), the step before it above (blue), the one Play
  goes to below (yellow), the others grey, their numbers about half as high as a key is wide (15 to 22
  pixels, times the lettering of Settings). It shows 3 steps, or 1, 5, 7 or 9 as Settings says, and rolls
  on when another step comes on. The stack goes round, and so does the wheel, but a step stands in it once
  only. While no step is on, the middle is empty and the step Play goes to stands under it. On a low window
  the wheel shows fewer steps (3 at least) and the keys of the column get flatter.
* **Independents:** keys in a column at the right edge, over the whole height: green ones for channels and
  red ones for memories, ten of each unless Settings says otherwise (0 to 32 of each; with none of either
  the column is left out). A green key switches
  its channel between zero and full, and that channel's fader follows; a red key does the same with the fader
  of its memory. The keys start on channels 1, 2, 3 ... and memories 1, 2, 3 ... Press Edit there, type
  other numbers onto the keys, and press Edit again. A green key is lit when its channel's own fader is up (a
  memory that holds the channel does not light it); a red key is lit like the key under its memory's fader,
  and runs from red to yellow for a sequence. Keys that do not fit into the column go on in another one.
* **The Memories view** is for looking only; nothing there changes the desk. The memory keys along the top
  choose a memory, each with the two lines of its note under it, and the table under them shows the level
  that memory holds for each of the 512 channels, with each channel's note under its level, in rows of 32
  (16 in a narrower window). All cells have the same size: when channels have notes, every cell keeps room
  for two lines of one. The page keys at the right choose which memories the keys show. This view and
  the Sequences view go by the window only: what Settings says about Main does not change them.
* **The Stage view** is a plan of the stage seen from above, for programming without the rig: a grid of
  squares (16 by 10 at first), the audience below it, and the lights of the rig on it, each in the colour
  its channels give it at this moment, as it goes out: memories, sequences and the stack included, after
  the master, and dark during blackout. A light glows around itself, and where lights meet their light adds
  up. Every window shows the same desk, so the plan can stay open in a second window, on a second screen or
  on a tablet over LAN while you program on Main.
  * **The panel** at the right (under the plan on a phone) is always there, so the plan never changes its
    place or size. **A click on a light** shows it there: its name, its kind and its channels, and a fader
    for each of its channels (R red, D dimmer, ... under it, then the channel's number), coloured as its
    channel is. They are the channel faders of Main: moving one there moves it here, and a memory that
    holds a channel up holds its fader up here too. A press beside the lights, Close or Esc lets the light
    go, and the panel says again what a click does. Its fields stand one in a row, the label at the left.
  * **A double click on a light** puts its faders to full, and the next one back to zero: those of its
    dimmer and its colours, not a strobe or a mode (of a device that gives no light: all its channels).
    **Clear** (top right) puts the faders of every light on the plan to zero; a memory, a sequence or the
    stack that holds a light up still holds it, as on Main.
  * **Edit** (top right) places the lights: **Add light** puts one on the plan, on the channel after the
    last light, named after the note of that channel. Drag a light by its middle to move it (in half
    squares; the arrow keys move it too, with Shift by two squares; Delete takes it away), and by an edge
    or a corner to size it, in whole squares, with the edge across from it staying where it is; the cursor
    shows what a press does there. Click a light to change it in the panel, over its faders: its name, its
    address (first channel), its kind, its shape and size, and how it is turned (45° a step). While no light
    is chosen the panel sets the size of the stage and the picture under it. Lights can also hang beside
    the stage, behind it and over the audience.
  * **Shape and size:** a light is round (a circle, or an ellipse when it is wider than deep) or square (a
    square, or an oblong over several squares of the grid), 1 to 32 whole squares wide and deep. A square
    light lies with its edges on the lines of the grid (unless it is turned askew) and moves by whole squares.
    The size is not the cells: a bar four squares wide with one RGB cell turns all red with its red channel;
    with four cells, each quarter has its own colour. Cells lie along the longer side.
  * **Picture:** a photo or a drawing of the stage under the grid and the lights (Edit, with no light chosen:
    Choose a picture). The page makes it smaller first (at most 1600 pixels on its longer side, as a JPEG),
    and the stage takes its proportions: its depth changes so that the picture fits. It lies under the stage
    or under the whole plan, shows through from 10 to 100 % (60 % at first), and Fit the stage to it and
    Remove the picture do what they say. It is saved with the project.
  * **Kinds of lights:** a dimmer of one colour (you pick its colour, like a gel; warm white until then),
    RGB, RGBW, RGBA, RGBWA, RGBW + UV and RGBWA + UV, each with or without a dimmer channel (first or last)
    and in 1 to 16 cells (a bar of four RGB cells is 12 channels from its address; each cell shows its own
    colour), a device that gives no light (a hazer, a mirror ball: it shows the level of its channel), or
    the channels typed as letters for any other fixture mode: D dimmer, R red, G green, B blue, W white,
    A amber, U UV, and any other letter for a channel that gives no light, such as S for a strobe. A D dims
    the cell it stands in; one D in a light of several cells dims all of them.
  * The plan belongs to the project: it is saved with it, and a project that is loaded brings its own. A
    new project, one from before plans and an imported show have an empty stage (no lights, no picture,
    16 by 10 squares). Changes of the plan are steps of Undo, and like everything stored the plan cannot be
    changed while the stack is live.
* **Project:** New, Save, Load and Import at the top right. A project is one JSON file in
  `Documents\Lumenet`. It holds the memories (looks and sequences), the stack, the notes and what the
  independent keys are bound to, not the fader positions, so loading a project never switches a light on by
  itself. New empties the desk; it asks first. A project keeps 32 channel keys and 32 memory keys of the
  independents, however many the page shows. An older project keeps the keys it has (10 of each, or 16
  channel keys from before there were memory keys); the others are where they are on a new desk. The
  project also keeps the layout of Main and the shortcuts of the PC (see [Settings](#settings)).
* **Autosave:** what is not saved yet is kept every five seconds in a file of its own on this PC
  (`%LOCALAPPDATA%\Lumenet\autosave.json`, the setting `Autosave:File`), and that file goes once the
  project is saved. If the desk program stops before that (a crash, Windows restarting, the window
  closed), it brings the work back when it starts: the project under its name and file, marked "not saved",
  and the top bar says so. Saving keeps it; loading the project again drops it. The faders are not brought
  back: a desk program that starts switches no light on by itself. Settings switches the autosave off and
  on and sets how often it keeps the work (see [Settings](#settings)).
* **Example:** `examples/Example show.json` is a small show to look at, for 17 channels: two red lights in a
  pot, a front wash, four blue back lights, a spot, an RGBW LED bar, a hazer, a mirror ball and the house
  light. It has notes on every light and memory, ten looks (memory 10 is a copy of 1, made dimmer), eleven
  sequences (every template, some made by hand and some recorded with Rec seq; in a loop, once and at random;
  at speeds from 0.5× to 2×), all twenty independent keys bound, and a stack of 14 steps with steps put in
  between (3.5, 4.1), waits that go on by themselves and a channel step for the hazer. Its stage plan has
  the 17 channels as 14 lights (the LED bar is one RGBW light, a bar four squares wide; the hazer a square)
  over a drawing of the stage: a wooden floor, the back curtain, masking at the sides, a riser, the fire pot
  between its two red lights and tape marks. Copy it into `Documents\Lumenet` and load "Example show".
* **Import** opens the show of a theatre console as a project: a file in the USITT ASCII format (`.asc`, up
  to 8 MB), the plain text in which consoles such as ETC Eos, Ion and Express, the ETC (AVAB) SmartFade or the
  Strand consoles hand a show on (on an Eos: File, Export, USITT ASCII; a SmartFade saves its shows in this
  format). Press Import, choose the file, and press Import in the
  window; the window then says what came across and what was left out. Like Load, it replaces the open
  project, and the window warns when that has changes that are not saved. The imported project is called
  after the file and is not saved until Save.
  * **Cues:** every cue of the first cue list becomes a memory with its look, and a step of the stack that
    brings that memory up: with the cue's number, its up time, and as down the down time of the cue after it
    (the stack takes the old step down while the new one comes up). A follow time becomes the step's wait,
    which counts from the moment the step is up. A cue in parts becomes one fade, as long as its slowest
    part; a delay is added to the fade time. A cue without a time fades in 5 seconds. A last cue that links
    to the first goes round, as the stack does; other links are left out.
  * **Submasters and groups** become memories too, after the cues, as long as memories are left. There are 128
    in all: what does not fit is left out, and the window says so.
  * **Patch:** the levels go through the file's patch onto the DMX addresses 1 to 512, which are Lumenet's
    channels; a file without a patch has channel 1 on address 1. The note under a channel names the console
    channel on that address ("Ch 7"; under it the share, if the patch gives less than 100 %).
  * **Notes under the memories:** the cue, submaster or group and the start of its label, like "Cue 4.5" over
    "Spot on the".
  * **Not taken over:** lines that only one maker's consoles understand (they begin with `$`: moving lights,
    effects, macros), cue lists after the first, and cue numbers the stack cannot have (more than one digit
    after the dot, or above 999.9): those steps get the next free number, and the note keeps the cue's own.
    Of cues, groups, submasters, sequence steps and labels the import keeps at most 10,000 of each kind, far
    more than a show has; a file with more is still read in well under a second.
  * **One difference in the fades:** a console fades every light straight from its old level to its new one.
    The stack fades the old memory down while the new one comes up, and the higher of the two wins, so a light
    that stays on from one cue to the next dips to about half in the middle of the fade.
  * **ETC SmartFade:** its files have a dialect of their own, and its show keeps its shape. Its memories become
    memories, in the order of their numbers, with a note like "Mem 25" over the memory's label. The steps of
    its stack bring those memories up, so a memory that several steps play is there once; only a step with
    levels of its own (a "state", a single channel) gets a memory of its own, and a memory that a step plays
    but the file does not hold stays empty, as on the console. Its sequences become memories that hold
    sequences: Loop and One Shot as on the console, Manual in a loop. A wait counts from the moment a step is
    up, as on the console, and a wait of 0 waits for Play. Its patch has the DMX address first; the labels of
    its channels become the notes under them, and its independents IND 1 and IND 2 get independent keys 1
    and 2. A percentage becomes a DMX value rounded up, as the console has it (17 % is 44).
  * `examples/Example theatre show.asc` is a short play for the lights of the example project, written by
    hand to show all of this: a patch, a blackout, a cue in two parts with a delay, a storm whose cues follow
    on by themselves, a fade of 1:30, submasters and groups. Import it from the `examples` folder.
* **LAN** (top left, beside the name) lets a phone or a tablet in the same network use the desk; see below.

**Steps.** A sequence and the stack are both made of steps. A step stands for a channel or a memory, has the
value it brings that to (0 to 255), and three times in seconds: **up** (from zero to the value), **wait**
(how long it stays) and **down** (back to zero when the next step takes over). The old step goes down while
the new one goes up.

In both tables the **number** of a step is chosen from a list. Each line of the list reads like
`5: Front left`: the number, then the note that stands under that fader in the main view (its two lines with
a space between them); a fader without a note shows its number alone. For a channel the list has all 512
channels; for a memory only the memories that hold something. The **Notes** column beside the number shows
the note of the channel or memory the step uses. It is read there, not written: a note is changed under its
fader.

**The Sequences view.** A memory can hold a sequence in place of a look; its key is then red running into
yellow. A sequence runs while the fader of its memory is up: it starts at its first step when the fader
leaves zero, and the fader dims what it gives, like a look. It moves no fader; the channel faders show what
it gives in red.

* Choose a memory with the keys along the top; the two lines of its note stand under each. Add step puts
  in a step like the last one, on the next channel or memory. Every change goes to the desk at once. Rec
  seq in the main view is the other way to put in steps: by pressing keys.
* **Once**, **Loop** or **Random:** played once, the last step stays on; in a loop the first step follows
  the last. Random loops too, but plays all steps on every round in a new order, so that a few steps never
  look like a loop (the same step never comes twice in a row between two rounds). A new sequence is played
  in a loop until you choose another.
* **Speed:** a lying fader above the table, with 1× in the middle: to the left down to 0.1× (ten times
  slower), to the right up to 10× (ten times faster), in steps of 0.05. It divides every time of the
  sequence, so 2× plays it twice as fast. A running sequence goes on from where it is; it does not start
  again. The 1× key puts it back. The arrow keys move it by 0.05 left of the middle and by 0.5 right of it.
  The speed is kept with the sequence in the project.
* **Template** fills the sequence from a pattern, for the lights you name (channels like `1, 2` or `1-4`,
  or memories). The steps it makes replace the old ones and can be changed in the table like any others.
  * **Fire:** every light flickers on its own, always between Low and High (now and then near Low, where a
    flame ducks), played at random. Low and High both at 255 give a steady full light.
    It looks best over a glow: set the faders of those lights to about 80.
  * **Chase:** one light after the other. **Wave:** each light swells and fades in turn; a single light
    breathes. **Strobe:** short flashes, light after light. For several lights at once, name a memory
    that holds them.
  * **Storm:** strikes up to High and faint flickers from Low, with dark pauses between, at random. The
    pauses are steps with the value 0: they give no light at all.
* A wait of 0 means the next step starts as soon as this one is up.
* A step that stands for a memory plays that memory's look. A memory that holds a sequence itself gives
  nothing there: sequences are not played inside sequences. So the list for a memory step offers only the
  memories that hold a look.
* A memory that holds a look is not turned into a sequence: empty it first with Del. Save mem over a
  sequence replaces it by a look; Copy and Del work on sequences as on looks; Edit mem does not.

**The Stack view.** The project has one stack: a list of steps for a show. The stack moves the real faders:
going to a step moves the fader of its channel or memory up to the step's value, leaving it moves that fader
back to zero. A memory that holds a sequence starts running, because its fader goes up.

* **Stack** is the switch of the stack. Pressed, it is fully lit and the stack is **live**: every channel
  fader and memory fader goes to zero, and the first step starts. Pressed again, the stack stops and the
  faders stay where they are. While the stack is live, the link to the Stack view in the top bar carries a
  red **Live** tag, in every view.
* **While the stack is live, nothing that is stored can be changed:** not the steps of the stack, not a
  memory (Save mem, Edit mem, Rec seq, Copy and Del are locked), not a sequence, not a note, not the channels
  of the independent keys, and New and Load are locked too. The faders, the independent keys themselves,
  Clear, Black out, the master and saving the project work as always.
* While the stack is not live it does not run: Play, Pause, Next and Prev are dark. That is the time to
  change the steps.
* **Play** goes to the next step, or goes on after Pause. **Pause** holds a fade or a wait where it is.
  **Next** and **Prev** go to the next or the previous step at once, with the steps' own times.
* **The stack goes round:** after the last step comes the first, for Play, for Next and when the stack goes
  on by itself. Prev from the first step goes to the last.
* A wait of 0 means the stack stays on the step until Play. With a wait above 0 it goes on by itself that
  many seconds after the step is up.
* The view shows all steps, the **current** one in green, the **next** one with an amber edge and the
  **previous** one (the step before the current one, where Prev goes) in blue, and for the current one
  how far it is. Prev, Play, Pause and Next are bigger than the other keys of the desk, Play the biggest.
* A fader the stack is moving and that you move by hand is yours from then on. Clear brings every fader down
  and takes the stack off its step; it stays live, and Play starts with the first step again.
* **Step numbers** look like 1.0, 2.0, 2.1. A new step gets the next whole number. Drag a step by its grip
  (or use the arrows at the end of its row): it gets the number of the step it now follows with the digit
  after the dot one higher, and the others keep theirs. Moved between 2.0 and 3.0, a step becomes 2.1.
  Type a number to change it: if another step has it, that one gives way (3.0 becomes 4.0, 3.1 becomes 4.1).

**Window width:** a window at least 1850 pixels wide shows 32 faders in a row, so 64 channels and 32 memories
at a time. A narrower window shows 16 in a row (32 channels and 16 memories) and twice as many page keys.
Settings can fix another number, and the rows, for Main. The page keys stand in a column at the right; when
they are too many for the height, they get flatter and then go on in another column.

**Small screens:** in a window narrower than 900 pixels or lower than 600 pixels (a phone, a tablet held
upright) the sections stand under each other and the page scrolls. The page keys lie under the section titles,
in as many lines as they need. Narrower than 720 pixels, a row of 16 faders is shown as two lines of 8 and the
top bar as three lines. The numbering does not change: a page is still 32 channels or 16 memories. Narrower
than 1300 pixels the top bar takes two lines, the links to the views under the rest. The limits are in
`frontend/src/app/desk/desk-types.ts`, and only there.

**A phone or a tablet (LAN):** the desk can be used from another device in the same network as the PC.

* Press **LAN** in the page on the PC. It is lit, and under the box light a line appears with the address and
  the password, for example `LAN 192.168.1.20:5080 Password K7M2Q`.
* On the phone, open that address in the browser (`http://192.168.1.20:5080`). The page asks for the
  password once; the phone remembers it. Small letters and spaces do not matter.
* Press LAN again and it is off: every other device loses the desk at once, its connections are refused, and
  no password is shown. LAN is off whenever the desk starts. Only the PC can switch it; the page on a phone
  has no LAN key.
* The password has 5 letters and digits (no 0, O, 1, I or L, which are easily mixed up). It is made once and
  kept in `%LOCALAPPDATA%\Lumenet\lan-password.txt`, so a phone that knows it gets in again next time.
  Settings can type another one (5 to 20 letters and digits) or make a new one; delete that file for a new
  one too. Every phone then has to give the new password, and the connected ones lose the desk.
* When the PC is in more than one network (a cable and Wi-Fi), Settings can choose the address that phones
  use: the desk then lets devices in at that address only. Without a choice it lets them in at every address
  and shows the first. A chosen address that the PC no longer has (the router gave it another one) counts as
  no choice until it is back. The choice is kept in the same file.
* A phone is remembered by a cookie that holds a long random key, not the password, so the cookie cannot be
  guessed. Guessing the password is slowed down instead: after 5 wrong passwords within a minute a device
  has to wait, and after 20 from all devices together every device has to wait. So someone in the network
  who keeps sending wrong passwords can stop a new phone from getting in; a phone that got in before is not
  asked again, and the PC never needs the password.
* The first time the desk program starts after this change, Windows asks whether `DmxBox.Server` may use the
  network. Allow it for **private** networks only. If that window was closed with "Cancel", Windows blocks the
  program; then allow it under Windows Security, Firewall, "Allow an app through firewall".
* The phone gets a built copy of the page from the desk program (port 5080). `run.cmd` builds that copy when
  the page's files have changed since the last time; while the desk runs, `npm run build` in `frontend`
  brings it up to date. The PC itself keeps using http://localhost:4200, which shows every change at once.

**Moving a fader:** drag the handle, or press in the slot to bring the handle there. The mouse wheel moves it
in steps of 5 (with Shift: 1). With the keyboard: the arrows, Page Up, Page Down, Home and End.
With a finger a fader moves only by its handle: a touch in the slot does nothing, so a thumb that scrolls the
page cannot move a light. In the small-screen layout the mouse wheel scrolls the page and leaves the faders alone.

### Settings

On the PC the desk program runs on, the layout of Main (all but the lettering below) and the colours of the
keys belong to the project, as the shortcuts of the Keyboard view do: they are saved with the project, a
project that is loaded brings its own, and a new project, an imported one or one from before this starts with
the ones in use. The lettering belongs to the browser on this device. A phone or a tablet keeps all of its
settings to itself, for its screen differs. The autosave and LAN belong to the desk program.

* **Channels on a page** (Main): Auto (a row of 32 or 16 by the window, as above), or 8, 16, 32, 64 or 128.
* **Rows of channel faders** and **rows of memory faders** (Main): 1 or 2 of each, at first 2 and 1. A
  channel size is spread over the rows of channels, and a row of memories is as long as a row of channels:
  with 8 in two rows, rows of 4, and 4 memories on a page, so 64 channel pages and 32 memory pages. The two
  sections share the height in the proportion of their rows. The faders keep the width the window gives
  them; when the rows are then wider than the window, they scroll sideways, the channels and the memories
  each on their own, with a big scrollbar. The Memories and Sequences views keep the rows of the window.
* **Steps the stack wheel shows** (Main): 1, 3, 5, 7 or 9; at first 3.
* **Keys of the independents:** how many green keys and how many red keys the column shows, 0 to 32 of each;
  with 0 of both, Main has no independents.
* **Lettering:** the minus and plus keys make all lettering of the page smaller or bigger, from ×0.7 to ×1.5
  in steps of 0.1, numbers on keys too. The keys keep their size, so words on them grow by 15 % at most; a
  word too long for its key goes on in a second line, and so does a page range, after its dash (flat page
  keys then go on in another column sooner, so that the two lines fit).
* **Autosave:** On or Off, and how often: every 5 seconds (at first), 30 seconds, 1 minute or 5 minutes. Off,
  the desk program keeps nothing and takes away the file it kept. The desk program keeps the choice in
  `autosave-options.json` beside the autosave file. On the PC only.
* **Network (LAN):** while LAN is on, the address phones use and the password; see above. On the PC only.
* **Colours of the keys:** a colour for each kind (channels, memories, and the colour a memory with a
  sequence runs into), which is that of all its keys, page keys and faders, and one for each other key, group
  by group: Black out; Clear, Solo up, Solo down and Rate (with its fader); Stack, Prev, Play, Pause, Next and Add step (Play also colours the
  step that is on, Next the step Play goes to); the keys of Record, of the top bar, Edit of the independents,
  the keys of the Sequences view and of the Stage view. While a colour is picked, the desk shows it at once. Each has a key back
  to its usual colour, and one key takes all of them back. At first Clear, Solo and Pause are white, Black out
  blue, Play green, Stack red and the others amber.

### Keyboard

The Keyboard view gives a shortcut to any key of the desk: the view links, New, Save, Load, Import, LAN,
Clear, Solo up, Solo down, Black out, Rate, the keys of the stack and of Record, those of the Sequences view and of the Stage
view, Edit and every key of the independents, every page key, the key under every channel and memory fader,
Undo and Redo. On the
PC the shortcuts belong to the project, like the layout; a phone keeps its own. Undo and Redo have Ctrl + Z
and Ctrl + Y until they get others (or none: then they stay without).

* The kinds of keys are tabs side by side (Views, Project and LAN, Master, General, Stack, Record, Sequences
  view, Stage view, Independents, Channel pages, Memory pages, Channel keys, Memory keys), each with the number of its keys; the
  arrow keys go from tab to tab. The search field searches all of them: then each tab shows how many of its
  keys it finds beside the number of its keys (1/4), and when the tab that is open finds none, the first tab
  that finds some opens.
* The list under the tabs has the keys at the left and their shortcuts at the right. **Set**, then press the
  keys of the shortcut (with Ctrl, Alt or Shift as wanted; Esc stops, Tab and Esc cannot be shortcuts). A
  shortcut that another key had moves over, and the page says so.
* **Listen** shows the desk; the next key you click there is not pressed but picked, and the Keyboard view
  comes back with its tab open and the key chosen in the list and scrolled into sight (Esc stops listening).
* A shortcut is the physical key, whatever letter the keyboard's language puts on it. It presses its key on
  the page as a click would, and a locked key stays locked; when the key is not on the view that is shown (a
  page of faders that is not shown, another view), it does what that key does, except the keys of the
  Sequences view and of the Stage view and Edit of the independents, which work only where they are. Shortcuts rest while a note
  or another field is typed into and while a window such as Load is open.

**How it is built**

* `backend/DmxBox.Server` is the desk itself. It holds the state (levels, memories, sequences, the stack,
  notes, the project), works out the output and sends it to the box through the `DmxBox` library. A small
  clock in it lets time pass about 30 times a second, for the sequences and the stack. Its settings are in
  `appsettings.json`: the address of the box (`Box:Host`), the projects folder (`Projects:Folder`), the
  built page it hands out (`Ui:Folder`), the file with the LAN password (`Lan:PasswordFile`) and the
  autosave file (`Autosave:File`; the autosave's own settings go beside it).
* `frontend/` is an Angular app. It only shows the desk and passes on what you do, over SignalR (`/hub/desk`).
  Because the state lives in the backend, reloading the page loses nothing.
* The backend listens on every network card (`Urls` is `http://*:5080`), but serves another device only while
  LAN is on and only with the password; while LAN is off it closes such a connection before reading it. Two
  more rules keep other websites that are open in a browser from operating the desk: the backend answers only
  to the names of this PC (`AllowedHosts`, plus this PC's own addresses and computer name), and the hub only
  to the pages listed in `Server:AllowedOrigins`, plus the page the backend hands out itself. With `Urls` set
  to `http://localhost:5080` the desk stays on this PC for good.

**Tests:** `dotnet test backend/DmxBox.sln` (440 tests: the client, the desk, solo, sequences and the stack with a
clock the tests set by hand, the rate, undo and the autosave, the stage plan and its picture, project files
and the example project, the import of show files, the link to the box, the hub, LAN with a pretend PC and a
pretend phone), and in `frontend/`: `npm test -- --watch=false` (140 tests of the page).

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
  the parts list, not from measuring the parts. **Nothing has been soldered after this plan yet.**
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
* **Solo, Rate, the colours of the keys, the autosave setting and tabs in the Keyboard view
  (2026-11-20):** the backend passes 412 tests and the page 122. On a test copy without a box, with its own
  autosave files, in Chrome at 1920 × 1080: the master section from top to bottom Clear, Solo, Black out,
  the level, the fader (178 pixels), Rate, the wheel and ×1.00, every key 33 pixels high and nothing
  scrolling; Solo and Pause white on Main and in the Stack view. Rate dark: the mouse wheel did not turn the
  wheel. Rate lit: one notch gave ×1.06, a drag of 60 pixels to the right ×1.88 with the dot turned 165°, the
  desk program had Rate on at 1.88, and a double click gave ×1.00. Solo held a channel at 255 and let it back
  to 0. The stack wheel's numbers 19 pixels high (13 before), three steps in sight. The Keyboard view: eleven
  tabs in one line; "black" gave Master 1/4 and 0 in the ten others. Autosave every minute, then every 5
  seconds with the file written within them, then off with the file gone. Pause made pink went into the
  project and onto Main, and All the usual colours took it back. At 1366 × 768 every key 20 pixels or more,
  the fader 50 and the rate wheel in sight; phones of 390 and 360 pixels with Rate and its wheel in a line
  of their own and nothing scrolling sideways.
* **The Stage view (2026-11-20):** the backend passes 430 tests and the page 132. On a test copy without a
  box, in Chrome: the example show got its plan of 14 lights through the hub and was saved by the desk
  itself. With Full stage up and a few channels set by hand, the back lights glowed blue, the front wash
  warm white, the two pot lights red at two levels and the hazer showed its level; with the rainbow and the
  fire running, the LED bar and the pot changed colour as they played. In Edit a light was chosen (the
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
  the real Pico, and any other computer (Windows 10, macOS, Linux, tablets). Next steps:
  1. Check the LED, and that a preset is still there after unplugging.
  2. Move the circuit from the breadboard to a soldered board, after the
     [perfboard plan](#building-it-on-a-perfboard).
