# Archive

Earlier designs of the dmxbox, kept in case one of them is wanted again. Nothing in here is needed to build
dmxbox 3, the box in `hardware/` (its firmware is in `firmware/`, the old firmware in `firmware/archive/`).

## pico/

The box with a Raspberry Pi Pico in sockets (2026-11-12 to 2026-11-26): one DMX output (transmit only), the Pico's
own micro-USB, two buttons (blackout, preset). Built by the user on a breadboard and on a 70 × 90 mm perfboard, and
it worked with a real light. Schematic, breadboard and perfboard plans, EAGLE files, 3D model and BOM. Its firmware:
`firmware/archive/pico`.

## pic32/

dmxbox 2 (2026-11-23): a PIC32MX270F256B in DIP-28, one or two DMX outputs drawn ready for RDM, a USB-C breakout,
the piano DIP switch SW1 for the box number, two buttons. Drawn for a 90 × 150 mm perfboard and as EAGLE files;
never built. `PRICES.md` there (made by the user) priced it on a JLCPCB board; it is the example for pricing later
boxes. Its firmware: `firmware/archive/pic32` (compiled with XC32, never run).

`eagle.py` writes the EAGLE files of both.

## ethernet/

The box had an Ethernet (RJ45) port until 2026-11-12. Three designs were drawn, none was built:

| Design | Model | What it was |
|---|---|---|
| First | `3d/dmxbox.blend` | Pico, W5500 module, ISO1410, 5-pin XLR. `dmxbox_schematic.*`, `dmxbox.zip` (firmware source with the W5500 driver) and `dmxbox.uf2` (its build) belong to it. |
| Through-hole | `3d/dmxbox_tht.blend` | Pico, ENC28J60 module on a ribbon cable, 6N137, MAX485, 3-pin XLR, DC jack. |
| W5500-EVB-Pico | `3d/dmxbox_w5500evb.blend` | One WIZnet W5500-EVB-Pico instead of Pico and Ethernet module. |

`README.md` and `BOM.md` in that folder are the project's documents as they were on the day of the change:
they describe the through-hole design and, as a variant, the W5500-EVB-Pico one. `firmware/` holds the
ENC28J60 driver and its PC test.
