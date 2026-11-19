# dmxbox: parts list

Everything needed to build one box: a Raspberry Pi Pico on the carrier board, powered and controlled over USB.
Designators (U1, R3, ...) match the board silkscreen in [3d/dmxbox_usb.blend](3d/dmxbox_usb.blend)
and the drawing [3d/dmxbox_usb_routing.svg](3d/dmxbox_usb_routing.svg).

## Modules and ICs

| Qty | Ref | Part | What to look for |
|---|---|---|---|
| 1 | U1 | Raspberry Pi Pico (RP2040) | The plain Pico. A "Pico H" is the same board with the pins already soldered on. The firmware is set up for this board. A Pico W or Pico 2 fits the sockets too, but needs a different build setting. |
| 1 | U3 | 6N137 optocoupler | DIP-8, any maker |
| 1 | U4 | Recom RKK-0505S/H isolated DC-DC converter | 5 V in, 5 V out, 1 W, 4 kV isolation. A 19.6 mm long part with seven pin positions, of which four have a pin: 1 = input plus, 2 = input minus, 5 = output minus, 7 = output plus. 1.83 € at TME on 2026-11-12. The board is drawn for exactly this part. The shorter 4-pin converters do not fit it: their pins sit elsewhere and their input pins are the other way round. |
| 1 | U5 | RS-485 transceiver: SN75176BP or MAX485 | DIP-8. Both have the same pins and fit the same socket; nothing else on the board changes. The **SN75176BP** (Texas Instruments) is the cheap one: about $0.7 to $2 for one piece in November 2026, against about $4.5 to $8 for a MAX485CPA+ / MAX485EPA+. It draws about 40 mA more, which the DC-DC converter and the USB port cover. |

## Sockets and headers

| Qty | For | Part | What to look for |
|---|---|---|---|
| 2 | U3, U5 | IC socket DIP-8 | 7.62 mm row spacing |
| 2 | U1 | Female pin header 1 × 20 | 2.54 mm pitch, about 8.5 mm high. The two strips are soldered to the carrier board 17.78 mm apart. |
| 2 | U1 | Male pin header 1 × 20 | 2.54 mm pitch, standard length: about 6 mm plug-in pin and 2.4 to 3 mm solder end. For example Amphenol 68000-220HLF (10.8 mm overall; in stock at TME on 2026-11-12, 1.13 €) or Connfly DS1021-1*20SF11-B (TME symbol ZL201-20G). Only if your Pico has no pins soldered on. |
| 1 | U4 | Female pin header 1 × 7 | 2.54 mm pitch, the standard kind (can be cut from a longer strip). The converter's pins are 0.64 mm square like ordinary header pins, and 4.1 mm long. Positions 3, 4 and 6 stay empty when the converter is plugged in. If you like, pull the contacts at positions 3 and 4 out of the strip before soldering: that keeps the isolation gap free of metal. |

## Resistors, capacitors, diodes

| Qty | Ref | Value | What to look for |
|---|---|---|---|
| 1 | R3 | 180 Ω | Axial, 0.25 W. Sets the optocoupler LED current (about 9 mA). |
| 1 | R4 | 1 kΩ | Axial, 0.25 W. Pull-up on the optocoupler output. |
| 1 | R2 | 270 Ω | Axial, 0.25 W. Base load for the DC-DC converter: it keeps the output voltage from rising when little current is drawn. |
| 2 | C1, C2 | 100 nF | Ceramic, 50 V, 5 mm lead spacing |
| 1 | C3 | 10 µF | Electrolytic, 16 V or more, 5 mm diameter, 2 mm lead spacing |
| 1 | C4 | 4.7 µF | Electrolytic, 16 V or more, 5 mm diameter, 2 mm lead spacing |
| 2 | D1, D3 | P6KE6.8CA | Protection diode (TVS), DO-15. The "CA" matters: it means it works in both directions. |

## Connectors and switches

| Qty | Ref | Part | What to look for |
|---|---|---|---|
| 1 | J1 | XLR 3-pin **female** chassis jack | D-size flange with solder cups (e.g. Neutrik NC3FD-L-1) |
| 1 | J1 (on the board) | Screw terminal block, 3 poles | 5.08 mm pitch |
| 2 | SW1, SW2 | Tactile push button 12 × 12 mm | Through-hole, 4 pins; add 2 push-on caps if you like |

There is no power jack: the box takes its 5 V from the USB cable.

## Board, box and small parts

| Qty | Part | Notes |
|---|---|---|
| 1 | Carrier board | **Not something you can order yet.** The model shows a routed board (100 × 66 mm) with every track on the solder side, but there are no manufacturing files. Until then: build it on a perfboard after the [perfboard plan](README.md#building-it-on-a-perfboard) (a 70 × 90 mm board with 26 × 31 holes, every hole with a pad of its own; plus some bare wire for the tracks and a short piece of insulated wire), or make a single-sided board from the [track drawing](3d/dmxbox_usb_routing.svg). |
| 1 | Enclosure | The model uses a 108 × 75 × 38 mm box (inside: 103 × 70 × 33 mm). Openings: 12 × 8 mm for the USB plug in the left end, and a 24 mm hole plus two screw holes for the XLR jack in the right end. The board's left edge sits 1.5 mm from the inside of the left wall, so that the Pico's USB socket ends right at the wall. The opening is that large so the plug's plastic grip fits into the wall; a thicker wall than 2.5 mm needs it even more. |
| 4 | Screw M3 × 6 mm | For the carrier board. One of the four holes is under the Pico: put that screw in before you plug the Pico in. |
| 4 | Spacer M3 × 6 mm | Only if your box has no moulded posts |
| 2 | Screw M3 × 10 mm with nut | For the XLR jack |
| 3 | Hook-up wire, about 10 cm each | Three colours, for XLR jack to screw terminal |
| 1 | Micro-USB cable | It powers the box and carries the data, so it must be a **data cable**, not a charge-only one. USB allows up to 5 m. The box draws about 0.2 A. |
| 1 | USB charger or power bank (optional) | Only for running the box without a computer, with its buttons and stored presets |
| 1 | DMX terminator (optional) | XLR 3-pin male plug with a 120 Ω resistor between pins 2 and 3, for the last fixture in the chain |

## Count check

On the carrier board: 4 socketed parts (U1, U3, U4, U5), 3 resistors, 4 capacitors, 2 diodes, 2 buttons,
1 screw terminal. Off the board: the XLR jack on three wires.

## No longer needed

These were on the list while the box had an Ethernet port. Take them out of the order if they are still in it:

* WIZnet W5500-EVB-Pico (or the ENC28J60 Ethernet module with its 2 × 5 pin header and ribbon cable)
* DC power jack J2 and diode D2 (1N5819, a Schottky diode)
* 5 V power supply and Ethernet patch cable

The parts lists of the Ethernet versions are kept in [archive/ethernet/BOM.md](archive/ethernet/BOM.md).
