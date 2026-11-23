// dmxbox - build-time configuration.
// Everything you are likely to change lives here.
#pragma once

#define FW_VERSION_MAJOR 1
#define FW_VERSION_MINOR 0

// ---------------------------------------------------------------- network (inside the USB cable)
// The Pico shows up on the computer as a USB network adapter. That "network"
// has exactly two ends: this box and the computer it is plugged into.
//
// The box hands the computer its address by itself (a tiny DHCP server), so
// nothing has to be set up on the computer.
//
// 192.168.7.x is a range home routers rarely use. If your own network happens
// to use it, change the third number in all three lines below.
#define NET_IP          {192, 168, 7, 1}     // the box
#define NET_COMPUTER_IP {192, 168, 7, 2}     // given to the computer
#define NET_NETMASK     {255, 255, 255, 0}
#define NET_LEASE_S     3600                 // the computer asks again after at most this many seconds
// Both Ethernet addresses (box and computer side) are made from the Pico's
// unique serial number (locally administered, 02:xx:..).

#define ARTNET_PORT    6454   // fixed by the Art-Net spec
#define CTRL_PORT      7000   // our own reliable command protocol
#define ARTNET_UNIVERSE   0   // 15-bit Port-Address this box listens to
#define NODE_SHORT_NAME "dmxbox"
#define NODE_LONG_NAME  "dmxbox - Pico USB Art-Net node"

// In MERGE_ARTNET_OVERRIDE mode, Art-Net is "live" for this long after the
// last packet. After that the box falls back to the cue layer.
#define ARTNET_LIVE_MS 3000

// ---------------------------------------------------------------- USB identity
// What the computer reads when the box is plugged in.
// 0x1209 / 0x0001 is the "for testing only" number pair of pid.codes. It is
// fine for a private prototype. A box that is given away or sold needs its
// own pair.
#define USB_VID          0x1209
#define USB_PID          0x0001
#define USB_MANUFACTURER "dmxbox"
#define USB_PRODUCT      "dmxbox"
#define USB_MAX_POWER_MA 300    // what the box may draw from the USB port (it needs about 200 mA)

// ---------------------------------------------------------------- DMX
#define DMX_OUTPUTS  1         // one DMX socket
#define DMX_CHANNELS 512       // slots sent per frame (24..512). Fewer = faster refresh,
                               // but Lumenet counts the outputs from it: keep 512 for it.
// Break (176 us) and mark-after-break (12 us) are set in dmx_tx.pio.

// ---------------------------------------------------------------- presets
#define PRESET_SLOTS 16        // 16 x 512 bytes, stored in the last 12 KB of flash

// ---------------------------------------------------------------- pins
// DMX output. GP0 pulls the cathode of the 6N137 optocoupler LED low (anode to
// 3V3 through 180 ohm), so the pin sinks about 9 mA; dmx_out.c sets its drive
// strength to 12 mA. LED on = line low, which keeps the polarity as it is.
#define PIN_DMX_TX     0       // GP0, Pico pin 1
// Driver-enable pin of an RS-485 transceiver, or -1 if DE is wired high as on
// the through-hole board (DE and /RE tied to +5V_ISO, transmit only).
#define PIN_DMX_DE     (-1)

#define PIN_BTN_BLACKOUT 18    // GP18, Pico pin 24: button to GND, toggles blackout
#define PIN_BTN_PRESET   19    // GP19, Pico pin 25: button to GND, fades to preset slot 0

// The log text goes to a serial pin, because the USB port is the network
// adapter: UART0, TX = GP16 (Pico pin 21), RX = GP17 (pin 22), 115200 baud.
// These two pins are set in CMakeLists.txt. Nothing has to be connected there.
//
// The on-board LED needs no entry here: the Pico SDK knows its pin (GP25 on a
// Pico and a Pico 2).

#define BUTTON_PRESET_SLOT 0
#define BUTTON_PRESET_FADE_MS 2000
