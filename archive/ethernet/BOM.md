# dmxbox: parts list

Everything needed to build one box in the current variant: a WIZnet W5500-EVB-Pico on the carrier board.
Designators (U1, R3, ...) match the board silkscreen in [3d/dmxbox_w5500evb.blend](3d/dmxbox_w5500evb.blend)
and the drawing [3d/dmxbox_w5500evb_routing.svg](3d/dmxbox_w5500evb_routing.svg).

The older variant with a plain Pico and an ENC28J60 module needs a few other parts. They are listed at the
end and are not needed for the current variant.

## Modules and ICs

| Qty | Ref | Part | What to look for |
|---|---|---|---|
| 1 | U1 | WIZnet W5500-EVB-Pico | RP2040, W5500 and the RJ45 jack on one 75 × 21 mm board. Not the 80 × 21 mm "Pico2" version. TME order code `W5500-EVB-PICO`, 12.13 € without VAT on 2026-11-12. |
| 1 | U3 | 6N137 optocoupler | DIP-8, any maker |
| 1 | U4 | Recom RKK-0505S/H isolated DC-DC converter | 5 V in, 5 V out, 1 W, 4 kV isolation. A 19.6 mm long part with seven pin positions, of which four have a pin: 1 = input plus, 2 = input minus, 5 = output minus, 7 = output plus. 1.83 € at TME on 2026-11-12. The board is drawn for exactly this part. The shorter 4-pin converters do not fit it: their pins sit elsewhere and their input pins are the other way round. |
| 1 | U5 | RS-485 transceiver: SN75176BP or MAX485 | DIP-8. Both have the same pins and fit the same socket; nothing else on the board changes. The **SN75176BP** (Texas Instruments) is the cheap one: about $0.7 to $2 for one piece in November 2026, against about $4.5 to $8 for a MAX485CPA+ / MAX485EPA+. It draws about 40 mA more, which the DC-DC converter covers. |

## Sockets and headers

| Qty | For | Part | What to look for |
|---|---|---|---|
| 2 | U3, U5 | IC socket DIP-8 | 7.62 mm row spacing |
| 2 | U1 | Female pin header 1 × 20 | 2.54 mm pitch, about 8.5 mm high. The two strips are soldered to the carrier board 17.78 mm apart. |
| 2 | U1 | Male pin header 1 × 20 | 2.54 mm pitch, standard length: about 6 mm plug-in pin and 2.4 to 3 mm solder end. For example Amphenol 68000-220HLF (10.8 mm overall; in stock at TME on 2026-11-12, 1.13 €) or Connfly DS1021-1*20SF11-B (TME symbol ZL201-20G). Only if the W5500-EVB-Pico comes without pins soldered on. |
| 1 | U4 | Female pin header 1 × 7 | 2.54 mm pitch, the standard kind (can be cut from a longer strip). The converter's pins are 0.64 mm square like ordinary header pins, and 4.1 mm long. Positions 3, 4 and 6 stay empty when the converter is plugged in. If you like, pull the contacts at positions 3 and 4 out of the strip before soldering: that keeps the isolation gap free of metal. |

## Resistors, capacitors, diodes

| Qty | Ref | Value | What to look for |
|---|---|---|---|
| 1 | R3 | 180 Ω | Axial, 0.25 W. Sets the optocoupler LED current (about 9 mA). |
| 1 | R4 | 1 kΩ | Axial, 0.25 W. Pull-up on the optocoupler output. |
| 1 | R2 | 270 Ω | Axial, 0.25 W. Minimum load for the DC-DC converter. |
| 2 | C1, C2 | 100 nF | Ceramic, 50 V, 5 mm lead spacing |
| 1 | C3 | 10 µF | Electrolytic, 16 V or more, 5 mm diameter, 2 mm lead spacing |
| 1 | C4 | 4.7 µF | Electrolytic, 16 V or more, 5 mm diameter, 2 mm lead spacing |
| 1 | D2 | 1N5819 | Schottky diode, DO-41 |
| 2 | D1, D3 | P6KE6.8CA | Protection diode (TVS), DO-15. The "CA" matters: it means it works in both directions. |

## Connectors and switches

| Qty | Ref | Part | What to look for |
|---|---|---|---|
| 1 | J1 | XLR 3-pin **female** chassis jack | D-size flange with solder cups (e.g. Neutrik NC3FD-L-1) |
| 1 | J1 (on the board) | Screw terminal block, 3 poles | 5.08 mm pitch |
| 1 | J2 | DC power jack for PCB mounting | 5.5 mm / 2.1 mm barrel, three solder lugs (DC-005 type) |
| 2 | SW1, SW2 | Tactile push button 12 × 12 mm | Through-hole, 4 pins; add 2 push-on caps if you like |

## Board, box and small parts

| Qty | Part | Notes |
|---|---|---|
| 1 | Carrier board | **Not something you can order yet.** The model shows a routed board (134 × 66 mm) with every track on the solder side, but there are no manufacturing files. Until then: build it on perfboard following the schematic in the README, or make a single-sided board from the [track drawing](3d/dmxbox_w5500evb_routing.svg). |
| 1 | Enclosure | The model uses a 142 × 75 × 43 mm box (inside: 137 × 70 × 38 mm). Openings: RJ45 jack (high up, because the jack sits on top of the socketed board), DC jack, and a 24 mm hole plus two screw holes for the XLR jack. |
| 4 | Screw M3 × 6 mm | For the carrier board |
| 4 | Spacer M3 × 6 mm | Only if your box has no moulded posts |
| 2 | Screw M3 × 10 mm with nut | For the XLR jack |
| 3 | Hook-up wire, about 10 cm each | Three colors, for XLR jack to screw terminal |
| 1 | Power supply 5 V DC, 1 A or more | 5.5 / 2.1 mm plug, **centre positive**. The box draws about 0.3 A. |
| 1 | Ethernet patch cable | |
| 1 | Micro-USB cable | For loading the firmware onto the W5500-EVB-Pico |
| 1 | DMX terminator (optional) | XLR 3-pin male plug with a 120 Ω resistor between pins 2 and 3, for the last fixture in the chain |

## Count check

On the carrier board: 4 socketed parts (U1, U3, U4, U5), 3 resistors, 4 capacitors, 3 diodes, 2 buttons,
1 screw terminal, 1 DC jack. Off the board: the XLR jack on three wires.

## Only for the older ENC28J60 variant

Model: [3d/dmxbox_tht.blend](3d/dmxbox_tht.blend). In that variant a plain Pico and a separate Ethernet module,
joined by a ribbon cable, take the place of the W5500-EVB-Pico. Leave out the W5500-EVB-Pico and add:

| Qty | Ref | Part | What to look for |
|---|---|---|---|
| 1 | U1 | Raspberry Pi Pico (RP2040) | Plain Pico, no Wi-Fi needed. It must have its two rows of pins soldered on (the "Pico H" comes with them). |
| 1 | U2 | ENC28J60 Ethernet module | The 56 × 34 mm board with a HanRun HR911105A jack and a **2 × 5 pin header**, 3.3 V supply. Not the smaller "mini" module with 12 pins: its pin order is different. |
| 1 | J3 | Male pin header 2 × 5 | The plug on the carrier board for the ribbon cable. 2.54 mm pitch (a shrouded "box header" also fits and cannot be plugged in the wrong way round). |
| 1 | J3 to U2 | 10-way ribbon cable with a 2 × 5 IDC socket on both ends | 2.54 mm pitch, about 10 cm long, wired straight (pin 1 to pin 1) |
| 4 | Screw M3 × 6 mm and spacer M3 × 6 mm | For the Ethernet module |
| 1 | U4 | Isolated DC-DC converter, 5 V in, 5 V out, 1 W, SIP-4 | Instead of the Recom RKK: that board has four holes for the short 4-pin kind. Pin order: 1 = input minus, 2 = input plus, 3 = output minus, 4 = output plus. Any of these fits: Mornsun B0505S-1W (B0505S-1WR3, the one in the model); GAPTEC 1S4A3_0505S1.5UP (1.22 € at TME on 2026-11-12, checked against its datasheet); GAPTEC 2S4A1_0505S3UP (2 W, 4.73 €; it wants at least 40 mA load, which the SN75176BP provides and the MAX485 does not); XP Power IE0505S (5.19 €; only 1 kV isolation, no short-circuit protection). |
| 1 | U4 | Female pin header 1 × 4 | Instead of the 1 × 7 strip. These converters have thin pins about 4 mm long, so a turned-pin (machined) socket strip holds them better than a standard female header. |

The box for that variant is 5 mm lower: 142 × 75 × 38 mm (inside: 137 × 70 × 33 mm). Its track drawings are
[bottom-only](3d/dmxbox_tht_routing_single_layer.svg) and [two-layer](3d/dmxbox_tht_routing.svg).
