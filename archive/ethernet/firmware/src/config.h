// dmxbox - build-time configuration.
// Everything you are likely to change lives here.
#pragma once

#define FW_VERSION_MAJOR 1
#define FW_VERSION_MINOR 0

// ---------------------------------------------------------------- network
// Static IPv4 address. The PC must be in the same subnet.
// (No gateway setting: the box only ever answers, and it answers straight
// back to the Ethernet address a request came from.)
#define NET_IP       {192, 168, 1, 50}
#define NET_NETMASK  {255, 255, 255, 0}
// MAC is derived from the Pico's unique flash ID (locally administered 02:xx:..).

#define ARTNET_PORT    6454   // fixed by the Art-Net spec
#define CTRL_PORT      7000   // our own reliable command protocol
#define ARTNET_UNIVERSE   0   // 15-bit Port-Address this box listens to
#define NODE_SHORT_NAME "dmxbox"
#define NODE_LONG_NAME  "dmxbox - Pico + ENC28J60 Art-Net node"

// In MERGE_ARTNET_OVERRIDE mode, Art-Net is "live" for this long after the
// last packet. After that the box falls back to the cue layer.
#define ARTNET_LIVE_MS 3000

// ---------------------------------------------------------------- DMX
#define DMX_CHANNELS 512       // slots sent per frame (24..512). Fewer = faster refresh.
// Break (176 us) and mark-after-break (12 us) are set in dmx_tx.pio.

// ---------------------------------------------------------------- presets
#define PRESET_SLOTS 16        // 16 x 512 bytes, stored in the last 12 KB of flash

// ---------------------------------------------------------------- pins (plain Pico)
// DMX output. GP0 pulls the cathode of the 6N137 optocoupler LED low (anode to
// 3V3 through 180 ohm), so the pin sinks about 9 mA; dmx_out.c sets its drive
// strength to 12 mA. LED on = line low, which keeps the polarity as it is.
#define PIN_DMX_TX     0
// Driver-enable pin of an RS-485 transceiver, or -1 if DE is wired high as on
// the through-hole board (MAX485: DE and /RE tied to +5V_ISO, transmit only).
#define PIN_DMX_DE     (-1)

// ENC28J60 Ethernet module on SPI0 (module pin name -> Pico)
#define PIN_ENC_MISO   16      // SO    -> GP16 (SPI0 RX)
#define PIN_ENC_CS     17      // CS    -> GP17
#define PIN_ENC_SCK    18      // SCK   -> GP18 (SPI0 SCK)
#define PIN_ENC_MOSI   19      // SI    -> GP19 (SPI0 TX)
#define PIN_ENC_RST    20      // RESET -> GP20
#define PIN_ENC_INT    21      // INT   -> GP21 (wired, not used: the firmware polls)
#define ENC_SPI_HZ (10 * 1000 * 1000)    // the chip allows 20 MHz; 10 MHz is plenty and kind to the ribbon cable

#define PIN_BTN_BLACKOUT 14    // button to GND, toggles blackout
#define PIN_BTN_PRESET   15    // button to GND, fades to preset slot 0
#define PIN_LED          25    // on-board LED

#define BUTTON_PRESET_SLOT 0
#define BUTTON_PRESET_FADE_MS 2000
