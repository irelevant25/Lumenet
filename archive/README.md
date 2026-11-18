# Archive

Earlier designs of the dmxbox, kept in case one of them is wanted again. Nothing in here is needed to build
the current USB box.

## ethernet/

The box had an Ethernet (RJ45) port until 2026-11-12. Three designs were drawn, none was built:

| Design | Model | What it was |
|---|---|---|
| First | `3d/dmxbox.blend` | Pico, W5500 module, ISO1410, 5-pin XLR. `dmxbox_schematic.*`, `dmxbox.zip` (firmware source with the W5500 driver) and `dmxbox.uf2` (its build) belong to it. |
| Through-hole | `3d/dmxbox_tht.blend` | Pico, ENC28J60 module on a ribbon cable, 6N137, MAX485, 3-pin XLR, DC jack. |
| W5500-EVB-Pico | `3d/dmxbox_w5500evb.blend` | One WIZnet W5500-EVB-Pico instead of Pico and Ethernet module. |

`README.md` and `BOM.md` in that folder are the project's documents as they were on the day of the change:
they describe the through-hole design and, as a variant, the W5500-EVB-Pico one. `firmware/` holds the
ENC28J60 driver and its PC test, which the USB firmware no longer uses.

The isolated DMX side (6N137, DC-DC converter, RS-485 transceiver, protection diodes, XLR) is the same in the
current box.
