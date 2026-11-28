# dmxbox: Ethernet to DMX512 with a Raspberry Pi Pico

A small box with an **RJ45 Ethernet port** on one side and a **3-pin XLR DMX output** on the other.
It is controlled from your own software over UDP:

* **Art-Net** (UDP 6454) is supported, so QLC+ and other lighting software work with it immediately.
* The **DX protocol** (UDP 7000) is a custom, reliable command protocol. It uses sequence numbers, ACKs,
  retransmits and duplicate detection. Fades run on the Pico, so they stay smooth even if the PC hiccups.

```
 PC ──Ethernet──► ENC28J60 ──SPI──► Pico core 0: network, protocols, buttons, flash
                                    Pico core 1: fade engine ──► PIO + DMA ──► 6N137 ──► MAX485 ──► XLR-3
```

| Folder / file | Contents |
|---|---|
| `firmware/` | Pico SDK project (C). Builds `dmxbox.uf2`. `firmware/test/` tests the Ethernet code on a PC. |
| `sim/` | The **same** engine and protocol C code as the firmware, running on a PC over normal sockets. Use it to develop software before the hardware exists. |
| `pc/` | .NET: the `DmxBox` library, the `dmxbox` command-line tool and end-to-end tests. |
| `3d/` | Blender models, renders, and drawings of the board's tracks (a bottom-only and a two-layer version). |
| [BOM.md](BOM.md) | Parts list for ordering. |

> **State on 2026-11-11.** The hardware changed to a through-hole build: ENC28J60 module instead of the
> W5500, 6N137 + MAX485 instead of the ISO1410, 3-pin instead of 5-pin XLR. The firmware was ported and its
> Ethernet code is tested on a PC, but it has **not been compiled for the Pico or run on hardware yet**.
> `dmxbox.uf2` in this folder is still the old W5500 build: do not flash it for this board.
> `dmxbox_schematic.svg/.pdf` and `3d/dmxbox.blend` also show the first design.
>
> **State on 2026-11-12.** A second hardware variant is drawn and is the current choice: one WIZnet
> W5500-EVB-Pico replaces the Pico and the ENC28J60 module. See "Variant: W5500-EVB-Pico" in section 1.
> The rest of this README still describes the ENC28J60 variant, and the firmware in this repo still contains
> only the ENC28J60 driver.

---

## 1. Hardware

Parts: see [BOM.md](BOM.md). Pictures: [top](3d/dmxbox_tht_top.png), [solder side](3d/dmxbox_tht_bottom.png),
[closed box](3d/dmxbox_tht_closed.png).

Tracks: the 3D model has every track on the solder side
([drawing](3d/dmxbox_tht_routing_single_layer.png), [SVG](3d/dmxbox_tht_routing_single_layer.svg)).
A two-layer version, meant for a board ordered from a board maker, is in
[3d/dmxbox_tht_routing.svg](3d/dmxbox_tht_routing.svg). Both use the same parts in the same places.

Everything with many pins sits in a socket (Pico, 6N137, MAX485, DC-DC converter), so it can be replaced.
U5 can be a MAX485 or the much cheaper SN75176BP: same pins, same socket (see [BOM.md](BOM.md)).
The Ethernet module is a separate board next to the carrier, connected by a 10-way ribbon cable.

### Variant: W5500-EVB-Pico (current choice)

One WIZnet W5500-EVB-Pico (RP2040, W5500 and the RJ45 jack on one 75 × 21 mm board) replaces the Pico, the
ENC28J60 module, header J3 and the ribbon cable. The DMX side is unchanged.

Model: `3d/dmxbox_w5500evb.blend`. Pictures: [top](3d/dmxbox_w5500evb_top.png),
[solder side](3d/dmxbox_w5500evb_bottom.png), [Ethernet side](3d/dmxbox_w5500evb_ethernet_side.png),
[closed box](3d/dmxbox_w5500evb_closed.png). Tracks, all on the solder side:
[drawing](3d/dmxbox_w5500evb_routing.png), [SVG](3d/dmxbox_w5500evb_routing.svg).

What differs from the description below:

* **Sockets:** the board plugs into the same two 1 × 20 sockets, turned so that its RJ45 jack goes through
  the left panel. Its USB port points into the box.
* **Pins:** GP16 to GP21 are wired to the W5500 on the board itself, so they get no tracks on the carrier.
  GP0 (DMX), GP14 and GP15 (buttons), 3V3, VSYS and GND are used as in the pin map below.
* **Buttons:** SW1 "blackout" is the front button, SW2 "preset 1" the back one.
* **DC jack and D2:** on a tab of the carrier at the left panel, behind the RJ45 jack.
* **3.3 V:** the board's own regulator feeds the W5500. Pin 36 only feeds R3, so the 3.3 V budget below does
  not apply. VSYS must be at least 4.3 V (WIZnet); 5 V through D2 gives about 4.6 V.
* **DC-DC converter:** U4 is a Recom RKK-0505S/H in a 1 × 7 socket strip, not the 4-pin B0505S-1W of the
  schematic below. Its pins: 1 = VSYS (5 V in), 2 = GND, 5 = GND2, 7 = +5V_ISO; positions 3, 4 and 6 have no
  pin. Capacitor C3 moved next to the DMX terminal to make room for the longer part.
* **Box:** 5 mm taller (142 × 75 × 43 mm), because the jack sits on top of the socketed board.
* **Firmware:** this board needs the W5500 driver, which is not in `firmware/` at the moment. It is in
  `dmxbox.zip`, and `dmxbox.uf2` is the old build for a W5500 on these same pins. That build has not been
  run on hardware and lacks the later drive-strength fix for the optocoupler.

### Pin map (plain Pico)

| Pico pin | GPIO | Connects to |
|---|---|---|
| 21 | GP16 (SPI0 RX) | ENC28J60 **SO** |
| 22 | GP17 | ENC28J60 **CS** |
| 24 | GP18 (SPI0 SCK) | ENC28J60 **SCK** |
| 25 | GP19 (SPI0 TX) | ENC28J60 **SI** |
| 26 | GP20 | ENC28J60 **RESET** |
| 27 | GP21 | ENC28J60 **INT** (wired, not used yet: the firmware polls) |
| 1 | GP0 | 6N137 pin 3 (LED cathode) |
| 19 | GP14 | Button SW1 "blackout" to GND |
| 20 | GP15 | Button SW2 "preset 1" to GND |
| 36 | 3V3(OUT) | ENC28J60 VCC, and R3 (180 Ω) to 6N137 pin 2 (LED anode) |
| 39 | VSYS | 5 V from the DC jack through D2; also feeds the DC-DC converter |
| 3, 8, 13, 18, 23, 28, 33, 38 | GND | Ground |

All pins can be changed in `firmware/src/config.h`.

### Ethernet module header and J3

The module is the common 56 × 34 mm ENC28J60 board with a 2 × 5 pin header. As printed on the board
(RJ45 jack on the right, the left name is the pin next to the board edge):

| Pin | Name | Pin | Name |
|---|---|---|---|
| 1 | CLKOUT (not used) | 2 | INT |
| 3 | WOL (not used) | 4 | SO |
| 5 | SI | 6 | SCK |
| 7 | CS | 8 | RESET |
| 9 | VCC (3.3 V) | 10 | GND |

Header J3 on the carrier board repeats this pin for pin, so a straight 10-way IDC ribbon cable connects them.
**Put the red stripe on pin 1 (CLKOUT) at both ends.** The smaller 12-pin "mini" module has a different pin
order: wire that one by signal name.

### Schematic (signal level)

```
              LOGIC SIDE (Pico GND)                   ║           ISOLATED SIDE (GND2)
                                                      ║
 DC jack + ──►|── VSYS (Pico 39) ──┬── U4 pin 2       ║   U4 pin 4 ── +5V_ISO ──┬── U3 pin 8 and pin 7
             D2 1N5819          C4 ┴ 4.7µ             ║   (B0505S-1W)           ├── R4 1k ── U3 pin 6 ──► U5 pin 4 (DI)
 DC jack − ──── GND ───────────────┴── U4 pin 1       ║                         ├── U5 pin 8, pin 3 (DE), pin 2 (/RE)
                                                      ║                         ├── R2 270R ── GND2
 Pico 3V3 ── R3 180R ── U3 pin 2 (LED anode)          ║                         └── C1, C2 100n and C3 10µ ── GND2
 Pico GP0 ───────────── U3 pin 3 (LED cathode)        ║   U4 pin 3 ── GND2 ── U3 pin 5, U5 pin 5, XLR pin 1
                                                      ║   U5 pin 6 (A) ── XLR pin 3 (Data+)   and D1 to GND2
 U3 = 6N137, U4 = B0505S-1W, U5 = MAX485              ║   U5 pin 7 (B) ── XLR pin 2 (Data−)   and D3 to GND2
```

How the data path works: GP0 low lights the optocoupler LED, the 6N137 output goes low, the MAX485 drives
the line low. An unlit LED means "line idle", so the line stays quiet while the Pico boots.
DE is tied high: the box only transmits.

Rules that matter:

* **A = Data+ = XLR pin 3, B = Data− = XLR pin 2**, pin 1 is the common. Swapping A and B is the most common DMX build mistake.
* **Never connect GND2 to the Pico's GND.** That would defeat the isolation.
* **No 120 Ω terminator in the box.** The box is the start of the line. Terminate at the last fixture.
* Keep the logic and the isolated side apart on the board. Only the 6N137 and the DC-DC converter bridge the gap.
* DMX512 officially uses 5-pin XLR. Many fixtures have 3-pin; the wiring of pins 1 to 3 is the same.

### Power

* **In the box:** 5 V DC jack → Schottky diode D2 → VSYS. Any 5 V supply with 1 A is plenty (the box draws about 0.3 A).
* **On the bench:** USB into the Pico also powers everything, and gives log output over the same cable.
  The enclosure has no USB opening: open the cover or lift the Pico out of its socket for that.
* **3.3 V budget.** The Pico's regulator feeds the Ethernet module too. The Pico datasheet recommends keeping
  the load on the 3V3 pin below 300 mA.

  | Consumer | Typical | Worst case |
  |---|---|---|
  | ENC28J60 (datasheet: 120 mA active, 160 mA typ. / 180 mA max. while transmitting) | 120 mA | 180 mA |
  | LEDs on the module (estimate) | 5 mA | 10 mA |
  | 6N137 LED through R3 | 5 mA | 12 mA |
  | Pico itself (estimate) | 30 mA | 40 mA |
  | **Total** | **about 160 mA** | **about 240 mA** |

  That fits. If you want more margin, give the Ethernet module its own 3.3 V regulator from the 5 V rail
  (for example an LF33CV with its two capacitors) and leave pin 36 for the optocoupler only.
* **Isolated side:** the B0505S needs at least 20 mA load (datasheet); R2 plus the two ICs provide that.

---

## 2. Firmware

### Build

You need the Pico SDK 2.x and an ARM toolchain (`gcc-arm-none-eabi`). The VS Code "Raspberry Pi Pico"
extension installs both.

```bash
cd firmware
mkdir build && cd build
PICO_SDK_PATH=/path/to/pico-sdk cmake ..
make -j
```

This produces `build/dmxbox.uf2`.

### Flash

Hold **BOOTSEL** while plugging the Pico into USB. It mounts as a drive; copy the new `dmxbox.uf2` onto it.

### Configure

Edit `firmware/src/config.h`:

* IP address (default **192.168.1.50/24**)
* Art-Net universe (default 0)
* Number of channels sent per frame (default 512)
* Pins, and the button preset and fade time

### Check that it works

* **Serial log:** open the Pico's USB serial port (any terminal, 115200). It prints the IP, the Art-Net universe and whether the ENC28J60 was found.
* **Ping:** `ping 192.168.1.50` from the PC is answered by the box.
* **On-board LED:**

| LED | Meaning |
|---|---|
| fast blink (5 Hz) | ENC28J60 not answering. Check the ribbon cable (pin 1!) and 3.3 V. |
| slow blink (1 Hz) | No Ethernet link |
| on | Link up, idle |
| flicker | Packets arriving |

The link is 10 Mbit/s half duplex. That is all the ENC28J60 can do, and it is far more than DMX needs.

### How it works

* **`dmx_tx.pio`:** a 9-instruction PIO program. It generates the break (176 µs), the mark-after-break (12 µs)
  and 250 kbaud 8N2 bytes, with exact timing and no CPU involvement. DMA feeds it the 513-byte packet.
  A full universe refreshes at **43 Hz**. Lower `DMX_CHANNELS` for a faster refresh.
* **`enc28j60.c`:** driver for the Ethernet chip. It only moves raw Ethernet frames in and out.
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
* **Losing the network:** the box keeps outputting the last state. The buttons work without a PC:
  GP14 toggles blackout, and GP15 fades to preset slot 0 in 2 s.

> **Master and blackout scale every channel.** That is right for dimmers and RGB PARs. If you add
> moving heads, a blackout would also send pan/tilt to 0. In that case, add a channel mask in
> `engine_render()`.

### Testing the Ethernet code on a PC

`firmware\test\build.cmd` (Visual Studio C compiler) builds and runs a test program. It runs the real
driver and network stack against a simulated ENC28J60 and checks ARP, ping, UDP, Art-Net and the control
protocol, including damaged packets. On Linux or macOS use the `cc` line at the top of `test_net.c`.

---

## 3. PC side (.NET)

Needs the .NET 10 SDK.

### Network setup

Give the PC an address in the same subnet, e.g. **192.168.1.10/24**. A direct cable works; modern
network cards handle crossover automatically. A switch also works.

### Command line

```powershell
dotnet build pc/DmxBox.sln -c Release          # once; gives pc\DmxBox.Cli\bin\Release\net10.0\dmxbox.exe
$env:DMXBOX_HOST = "192.168.1.50"              # or pass -H <address> each time

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

`dmxbox --help` lists everything. Without building first: `dotnet run --project pc/DmxBox.Cli -- ping`.
If `discover` finds nothing although `ping` works, let the program through the Windows firewall.

### Library

Reference the project `pc/DmxBox/DmxBox.csproj`.

```csharp
using DmxBox;

using var box = new DmxBoxClient("192.168.1.50");
box.Fade(1, [255, 120, 20, 255, 120, 20], TimeSpan.FromSeconds(2.5));   // two RGB PARs at channels 1-6
box.PresetSave(3);
Console.WriteLine(box.Ping());

using var art = new ArtNetSender("192.168.1.50", universe: 0);          // for effects computed on the PC
art.Send(frame);                                                         // up to 512 bytes, about 40 times per second
```

A rejected request throws `DmxBoxException` (with the box's status, e.g. `BAD_ARG`); no answer after all
retransmits throws `DmxBoxTimeoutException`. `pc/DmxBox.Cli/Demos.cs` has a cue demo (box-side fades) and a
streaming rainbow demo (Art-Net).

### QLC+ (or any Art-Net software)

Add an Art-Net output on the network adapter in the box's subnet, for universe 0 and IP 192.168.1.50.
`discover` (or QLC+'s own scan) should find a node called `dmxbox`.

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
| 0x01 | PING | – | fw major, fw minor, flags, master, uptime_ms u32, preset mask u16, channels u16 |
| 0x10 | SET | start u16 (1-based), count u16, values[count] | – |
| 0x11 | FADE | start u16, count u16, duration_ms u32, values[count] | – |
| 0x20 | PRESET_SAVE | slot u8 | – |
| 0x21 | PRESET_GO | slot u8, duration_ms u32 | – |
| 0x22 | PRESET_CLEAR | slot u8 | – |
| 0x30 | MASTER | level u8 | – |
| 0x31 | BLACKOUT | 0/1 | – |
| 0x32 | RELEASE_ARTNET | – | – |
| 0x33 | MERGE_MODE | 0 = HTP, 1 = override | – |
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

* **A DHCP client** (UDP 67/68) instead of a static IP. `net.c` is the place to add it.
* **sACN / E1.31** (UDP 5568, multicast) alongside Art-Net. Needs the multicast filter of the ENC28J60.
* **A second universe:** a second PIO state machine plus a second transceiver; the code is ready for more SMs.
* **Pushed state:** a "subscribe" message, so the box sends live output to your UI.
* **Fabrication files for the carrier board**, so it can be ordered instead of hand-wired.

## Verification status

* **Pin orders, W5500-EVB-Pico variant:** the board's pins follow WIZnet's drawing, and the Recom RKK-0505S/H
  follows its datasheet (Rev. 5-2026). The SN75176BP has the same pins as the MAX485 (TI datasheet).
* **Pin orders:** checked against the datasheets of the ENC28J60, 6N137, MAX485, B0505S-1W and the Pico. The
  header order of the Ethernet module comes from photos of the real board (the module has no datasheet).
* **Board routing:** two versions, one with every track on the bottom (shown in the 3D model) and one on
  two layers. Each connects all 19 nets and passes a script check for clearance, connectivity and the
  isolation gap. The bottom-only version leaves four of the Pico's eight ground pins without a track (the
  Pico joins them itself) and narrows two supply tracks to 0.3 mm where they pass between pins. Both are
  drawings, not manufacturing datasets, and no board has been built.
* **Board routing, W5500-EVB-Pico variant:** 13 nets, all on the bottom, pass the same script check. One
  supply track narrows to 0.3 mm for 2.6 mm where it passes between two socket pins, and three of the
  board's eight ground pins have no track. The board model is WIZnet's own STEP file; its position follows
  WIZnet's dimension drawing. No two-layer version of this variant exists yet.
* **Ethernet driver and network stack:** pass 221 checks on a PC against a simulated ENC28J60
  (`firmware/test`). The chip model is written from the datasheet; a real chip has not been driven yet.
* **Engine, Art-Net and DX protocol:** pass 14 end-to-end tests of the .NET client against the simulator,
  including a run with 30 % packet loss in both directions.
* **PIO program:** unchanged from the first version.
* **Not done yet:** compiling the firmware with the Pico SDK, and everything on real hardware. Bring it up
  step by step:
  1. Check the LED and serial log.
  2. Run `ping 192.168.1.50`, then `dmxbox ping`.
  3. Measure DMX with one fixture.
