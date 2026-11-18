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
| `pc/` | .NET: the `DmxBox` library, the `dmxbox` command-line tool and end-to-end tests. |
| `schematic/` | The schematic and the breadboard plan (SVG, PNG, one PDF for printing), and the script that draws them. |
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
dotnet build pc/DmxBox.sln -c Release          # once; gives pc\DmxBox.Cli\bin\Release\net10.0\dmxbox.exe

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
`dmxbox --help` lists everything. Without building first: `dotnet run --project pc/DmxBox.Cli -- ping`.
In the first seconds after plugging in, `discover` can come back empty: try again. If it stays empty
although `ping` works, let the program through the Windows firewall.

### Library

Reference the project `pc/DmxBox/DmxBox.csproj`.

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
retransmits throws `DmxBoxTimeoutException`. `pc/DmxBox.Cli/Demos.cs` has a cue demo (box-side fades) and a
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

dotnet test pc/DmxBox.sln                      # 14 end-to-end tests against the simulator
```

`dmxbox_sim.exe --drop 0.3` loses 30 % of control packets in each direction. Use it to test the reliability
layer; the test suite does exactly this. On Linux or macOS build the simulator with `make -C sim`.

---

## 4. The DX protocol (UDP 7000)

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

## 5. Ideas for later

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
* **A real DMX light (2026-11-18):** one light with four RGB cells (12 channels from address 5) on the
  breadboard build showed everything correctly: each of the 12 channels alone and in the right order, full
  white, a fade to dark, and a fade from red to blue, without flicker.
* **PIO program:** unchanged from the first version. That light accepts its signal; the timing has not been
  looked at with an oscilloscope or a DMX tester.
* **Not done yet:** more than one light or a long cable, the LED states, a preset surviving a power cycle on
  the real Pico, and any other computer (Windows 10, macOS, Linux, tablets). Next steps:
  1. Check the LED, and that a preset is still there after unplugging.
  2. Move the circuit from the breadboard to a soldered board.
