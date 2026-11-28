// The firmware of the dmxboxes - build-time configuration, the same for the
// RP2040 firmware (firmware/rp2040: boxes 1, 3 and 4) and the PIC32 firmware
// (firmware/pic32: box 2). Everything you are likely to change lives here;
// what sets one box apart from the others (its name, MIDI, the chain, the
// pins) is in its own box.h, in boxes/boxN/firmware, which build.cmd puts on
// the include path. This file holds numbers only, so the simulator
// (firmware/sim) and the tests (firmware/test) use it too.
#pragma once

#include "box.h"

#define FW_VERSION_MAJOR 3
#define FW_VERSION_MINOR 2

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
#define NODE_SHORT_NAME BOX_NAME
#define NODE_LONG_NAME  BOX_ABOUT

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
#define USB_PRODUCT      BOX_NAME
// The current the box asks the computer for, USB_MAX_POWER_MA: in box.h.

// ---------------------------------------------------------------- DMX
#ifndef DMX_OUTPUTS
#define DMX_OUTPUTS  1          // one DMX socket. The simulator also builds a box with two (for tests).
#endif
#define DMX_CHANNELS (512 * DMX_OUTPUTS)   // 512 for each: Lumenet counts the outputs from it
// Break and mark after break: 193 and 17 us on the RP2040 (rp2040/src/dmx_tx.pio),
// 180 and 40 us on the PIC32 (pic32/src/dmx_line.c).

// ---------------------------------------------------------------- RDM
// The box's own RDM ID: manufacturer 7FF0 (E1.20 keeps 7FF0 to 7FFF for
// prototypes), then four bytes of the flash chip's serial number (RP2040),
// or 0002 and then the box number in two bytes (the PIC32 has no serial number).
#define RDM_MANUFACTURER 0x7FF0

// ---------------------------------------------------------------- presets
#define PRESET_SLOTS 16         // 16 x 512 bytes, in the chip's flash (its storage_flash.c)

// ---------------------------------------------------------------- pins
// In box.h. Every box has the DMX line (TX, RX, DE), the log, the switch SW1
// and its LEDs.

// The log: on the RP2040 it shares UART0 with the RDM answers, so it has their
// speed; the PIC32 sends it from UART2 at the same speed.
#define LOG_BAUD      250000
