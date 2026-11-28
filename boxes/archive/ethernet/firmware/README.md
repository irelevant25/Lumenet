# Ethernet firmware parts (archived on 2026-11-12)

The firmware in `firmware/` now reaches the computer over USB. These are the files that were only
needed for the ENC28J60 Ethernet chip, as they were before that change. They are kept in case the
box gets a real network port again. Nothing here is built or tested any more.

| File | What it is |
|---|---|
| `src/enc28j60.c`, `src/enc28j60.h` | Driver for the ENC28J60 chip on SPI: whole Ethernet frames in and out. Moved here. |
| `test/enc_model.c`, `test/enc_model.h` | A model of the chip for the PC test, written from the datasheet. Moved here. |
| `test/test_net.c` | The old PC test (driver, network stack and protocols against the chip model, 221 checks). Moved here. |
| `src/main.c`, `src/config.h`, `CMakeLists.txt`, `test/build.cmd` | Copies of the files that were rewritten for USB, for reference. |

The driver was tested on a PC only. It was never compiled for the Pico and never ran on a real chip.

## Putting it back

The network stack (`firmware/src/net.c`) did not change: it still takes and gives whole Ethernet
frames and does not care where they come from. To use the chip again:

1. Copy `src/enc28j60.c` and `src/enc28j60.h` back into `firmware/src/` and add `src/enc28j60.c` and
   `hardware_spi` to `firmware/CMakeLists.txt`.
2. In `main.c`, give `net_init()` the function `enc_send` instead of `usb_net_send`, and feed
   `net_input()` from `enc_receive()`. The archived `src/main.c` shows how (`net_start`, `net_service`).
3. Put the SPI pins back into `config.h` (GP16 to GP21 in the archived copy). Note that the USB
   firmware uses GP16 and GP17 for the log and GP18 and GP19 for the buttons.
4. Decide what to do about addresses: the archived version used a fixed address (192.168.1.50), the
   USB version hands the computer an address itself (`dhcp_server.c`), which is wrong on a real network.
