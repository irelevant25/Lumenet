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
| `firmware/` | Pico SDK project (C). Builds `dmxbox.uf2`. `firmware/test/` tests the network code on a PC. |
| `sim/` | The **same** engine and protocol C code as the firmware, running on a PC over normal sockets. Use it to develop software before the hardware exists. |
| `backend/` | .NET: the `DmxBox` library, the `dmxbox` command-line tool, the server of the Lumenet desk, and the tests. |
| `frontend/` | Angular: the page of the Lumenet fader desk. |
| `run.cmd` | Starts the Lumenet desk: backend and frontend together. |
| `examples/` | An example project for the desk, to load and look at. |
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

Both produce `build/dmxbox.uf2`. For a Pico W or Pico 2 add `-DPICO_BOARD=pico_w` or `-DPICO_BOARD=pico2`
after `build.cmd` or to the `cmake` line, and start with an empty `build` folder. On a Pico W the LED stays dark.

### Flash

Hold **BOOTSEL** while plugging the Pico into USB. It mounts as a drive named RPI-RP2; copy `dmxbox.uf2` onto
it. The Pico then restarts by itself and the drive disappears. If the drive stays, wait a few seconds and
copy the file again. Flashing replaces whatever was on the Pico before, for example MicroPython and the
scripts stored with it.

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

The top bar has four links: **Main**, **Memories**, **Sequences** and **Stack**. Main is the desk itself.
Memories only shows what is stored. Sequences and Stack are for the two things that run by themselves (see
below). At the right of Main stand the **Master** section (Clear, Black out and the master fader with its
level) with **Record** under it, and at the right edge, over the whole height, the **Independents**.

* **Channels:** two rows of faders. The keys at the right choose which of the 512 channels they show.
* **Memories:** one row of faders, with page keys for the 128 memories. A memory holds a look:
  the levels the channel faders were set to at the moment it was saved. Its fader plays that look at the
  fader's level. For every channel the highest value wins, whether it comes from the channel fader or from
  a memory.
* **A memory fader moves the channel faders it uses.** A channel fader shows what goes out for its channel
  (before the master): its own level, or what the memories give it when that is higher. The slot is lit green
  up to the fader's own level and red above it, where a memory holds the channel. The own level is kept:
  bring the memory down, and the fader goes back to it. While a memory holds a channel up, its fader can
  still be moved; the handle stays where the memory holds it, and the green part shows what you set.
  Save mem stores the faders' own levels, not the part a memory adds.
* **Keys under the faders:** a channel key is lit when the channel is above zero. A memory key is half lit
  when something is stored in it and fully lit when its fader is up. A page key is fully lit for the page
  that is shown and half lit for another page with something on it.
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
* **Independents:** twenty keys in a column at the right edge, over the whole height: ten green ones for
  channels and ten red ones for memories. A green key switches its channel between zero and full, and that
  channel's fader follows; a red key does the same with the fader of its memory. The keys start on channels
  1 to 10 and memories 1 to 10. Press Edit there, type other numbers onto the keys, and press Edit again. A
  green key is lit when its channel's own fader is up (a memory that holds the channel does not light it);
  a red key is lit like the key under its memory's fader, and runs from red to yellow for a sequence.
* **The Memories view** is for looking only; nothing there changes the desk. The memory keys along the top
  choose a memory, each with the two lines of its note under it, and the table under them shows the level
  that memory holds for each of the 512 channels, in rows as long as a row of faders (32 by 16, or 16 by 32
  in a narrower window). The page keys at the right choose which memories the keys show.
* **Project:** New, Save and Load at the top right. A project is one JSON file in
  `Documents\Lumenet`. It holds the memories (looks and sequences), the stack, the notes and what the
  independent keys are bound to, not the fader positions, so loading a project never switches a light on by
  itself. New empties the desk; it asks first. A project saved before the independents had memory keys
  (16 channel keys) keeps its first 10 channel keys; its memory keys start on memories 1 to 10.
* **Example:** `examples/Example show.json` is a small show to look at, for 17 channels: two red lights in a
  pot, a front wash, four blue back lights, a spot, an RGBW LED bar, a hazer, a mirror ball and the house
  light. It has notes on every light and memory, ten looks (memory 10 is a copy of 1, made dimmer), eleven
  sequences (every template, some made by hand and some recorded with Rec seq; in a loop, once and at random;
  at speeds from 0.5× to 2×), all twenty independent keys bound, and a stack of 14 steps with steps put in
  between (3.5, 4.1), waits that go on by themselves and a channel step for the hazer. Copy it into
  `Documents\Lumenet` and load "Example show".
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
* The view shows all steps, the **current** one in green and the **next** one with an amber edge, and for
  the current one how far it is.
* A fader the stack is moving and that you move by hand is yours from then on. Clear brings every fader down
  and takes the stack off its step; it stays live, and Play starts with the first step again.
* **Step numbers** look like 1.0, 2.0, 2.1. A new step gets the next whole number. Drag a step by its grip
  (or use the arrows at the end of its row): it gets the number of the step it now follows with the digit
  after the dot one higher, and the others keep theirs. Moved between 2.0 and 3.0, a step becomes 2.1.
  Type a number to change it: if another step has it, that one gives way (3.0 becomes 4.0, 3.1 becomes 4.1).

**Window width:** a window at least 1850 pixels wide shows 32 faders in a row, so 64 channels and 32 memories
at a time. A narrower window shows 16 in a row (32 channels and 16 memories) and twice as many page keys.
The page keys stand in one column at the right; when they are too many for the height, they get flatter and
then the column scrolls.

**Small screens:** in a window narrower than 900 pixels or lower than 600 pixels (a phone, a tablet held
upright) the sections stand under each other and the page scrolls. The page keys lie under the section titles,
in as many lines as they need. Narrower than 720 pixels, a row of 16 faders is shown as two lines of 8 and the
top bar as three lines. The numbering does not change: a page is still 32 channels or 16 memories. The three
limits are in `frontend/src/app/desk/desk-types.ts`, and only there.

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
  Delete that file for a new password; every phone then has to give the new one.
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

**How it is built**

* `backend/DmxBox.Server` is the desk itself. It holds the state (levels, memories, sequences, the stack,
  notes, the project), works out the output and sends it to the box through the `DmxBox` library. A small
  clock in it lets time pass about 30 times a second, for the sequences and the stack. Its settings are in
  `appsettings.json`: the address of the box (`Box:Host`), the projects folder (`Projects:Folder`), the
  built page it hands out (`Ui:Folder`) and the file with the LAN password (`Lan:PasswordFile`).
* `frontend/` is an Angular app. It only shows the desk and passes on what you do, over SignalR (`/hub/desk`).
  Because the state lives in the backend, reloading the page loses nothing.
* The backend listens on every network card (`Urls` is `http://*:5080`), but serves another device only while
  LAN is on and only with the password; while LAN is off it closes such a connection before reading it. Two
  more rules keep other websites that are open in a browser from operating the desk: the backend answers only
  to the names of this PC (`AllowedHosts`, plus this PC's own addresses and computer name), and the hub only
  to the pages listed in `Server:AllowedOrigins`, plus the page the backend hands out itself. With `Urls` set
  to `http://localhost:5080` the desk stays on this PC for good.

**Tests:** `dotnet test backend/DmxBox.sln` (319 tests: the client, the desk, sequences and the stack with a
clock the tests set by hand, project files and the example project, the link to the box, the hub, LAN with a
pretend PC and a pretend phone), and in `frontend/`: `npm test -- --watch=false` (70 tests of the page).

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
