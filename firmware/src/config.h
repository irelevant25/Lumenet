// dmxbox 3 (RP2040: one DMX output with RDM, MIDI in and out, a USB hub for a
// chain of boxes) - build-time configuration. Everything you are likely to
// change lives here. This file holds numbers only, so the simulator
// (firmware/sim) and the tests (firmware/test) use it too.
#pragma once

#define FW_VERSION_MAJOR 3
#define FW_VERSION_MINOR 0

// ---------------------------------------------------------------- network (inside the USB cable)
// The box shows up on the computer as a USB network adapter and hands the
// computer its address by itself (a tiny DHCP server).
//
// The address comes from the switch SW1 on the board. Its eight switches give
// the box number n, 0 to 255 (switch 1 counts 1, switch 2 counts 2, switch 3
// counts 4 ... switch 8 counts 128; ON counts). The box is 10.1.n.1 and the
// computer gets 10.1.n.2, so every box is a small network of its own, and
// several boxes can be plugged into one computer, also one behind the other.
// Give every box its own number. Lumenet looks for 10.1.n.1 on every network
// adapter of the computer that has an address 10.1.n.x.
//
// If a network you use is in 10.1.x.x, change the first two numbers here.
#define NET_FIRST     10                     // the first two numbers of both addresses
#define NET_SECOND    1
#define NET_NETMASK   {255, 255, 255, 0}
#define NET_LEASE_S   3600                   // the computer asks again after at most this many seconds
// Both Ethernet addresses are made from the box number too: box
// 02:00:0A:01:nn:01, computer 02:00:0A:01:nn:02.

#define ARTNET_PORT    6454   // fixed by the Art-Net spec
#define CTRL_PORT      7000   // our own reliable command protocol
// Art-Net: box n listens to universe ARTNET_UNIVERSE + n.
#define ARTNET_UNIVERSE   0
#define NODE_SHORT_NAME "dmxbox 3"
#define NODE_LONG_NAME  "dmxbox 3 - RP2040 USB Art-Net node with RDM and MIDI"

// In MERGE_ARTNET_OVERRIDE mode, Art-Net is "live" for this long after the
// last packet. After that the box falls back to the cue layer.
#define ARTNET_LIVE_MS 3000

// ---------------------------------------------------------------- USB identity
// 0x1209 / 0x0001 is the "for testing only" number pair of pid.codes. It is
// fine for a private prototype; a box that is given away or sold needs its own
// pair. The USB serial number is "box-" and the box number ("box-007").
#define USB_VID          0x1209
#define USB_PID          0x0001
#define USB_MANUFACTURER "dmxbox"
#define USB_PRODUCT      "dmxbox 3"
// The box sits behind the hub chip on its own board. A hub port may give a
// device 100 mA when the hub takes its own power from the cable; asking for
// more, the computer would refuse the box. What the board really draws (hub,
// RP2040, DMX side: about 200 mA) is the hub's business, see BOM.md.
#define USB_MAX_POWER_MA 100

// ---------------------------------------------------------------- DMX
#ifndef DMX_OUTPUTS
#define DMX_OUTPUTS  1          // one DMX socket. The simulator also builds a box with two (for tests).
#endif
#define DMX_CHANNELS (512 * DMX_OUTPUTS)   // 512 for each: Lumenet counts the outputs from it
// Break 193 us, mark after break 17 us: see dmx_tx.pio.

// ---------------------------------------------------------------- RDM
// The box's own RDM ID: manufacturer 7FF0 (E1.20 keeps 7FF0 to 7FFF for
// prototypes), then four bytes of the flash chip's serial number.
#define RDM_MANUFACTURER 0x7FF0

// ---------------------------------------------------------------- presets
#define PRESET_SLOTS 16         // 16 x 512 bytes, in the last 12 KB of the flash

// ---------------------------------------------------------------- pins
// GPIO numbers of the RP2040; hardware/scripts/circuit.py has the same list.
#define PIN_OUT_EN    0         // high = 5 V on the OUT socket, for the next box
#define PIN_LED_USB   1         // green: a computer uses the box
#define PIN_LED_DMX   2         // yellow: DMX goes out, flickers with lighting data
#define PIN_LED_MIDI  3         // red: MIDI and RDM traffic
#define PIN_MIDI_TX   4         // UART1, 31250 baud
#define PIN_MIDI_RX   5
#define PIN_LOG_TX    12        // UART0 TX: the log, 250000 baud, on the service pads J7 (pin 6)
#define PIN_DMX_RX    13        // UART0 RX: RDM answers, 250000 baud
#define PIN_DMX_TX    14        // PIO: DMX and RDM out. Low = optocoupler LED on = line low.
#define PIN_DMX_DE    15        // low = listen (RDM), high = send; the line sends while the RP2040 is off
#define PIN_BOX_N1    18        // SW1 switch 1 to 8: GPIO 18 to 25, ON = low
#define PIN_CC1       26        // ADC0: CC1 of the OUT socket
#define PIN_CC2       27        // ADC1: CC2 of the OUT socket
#define PIN_VSENSE    28        // ADC2: half the supply voltage

#define LOG_BAUD      250000    // the log shares UART0 with the RDM answers, so it has their speed
