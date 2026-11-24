// dmxbox 2 (PIC32MX270F256B, two DMX outputs) - build-time configuration.
// Everything you are likely to change lives here. This file holds numbers
// only, so the simulator (firmware/sim) can use it too.
#pragma once

#define FW_VERSION_MAJOR 1
#define FW_VERSION_MINOR 0

// ---------------------------------------------------------------- network (inside the USB cable)
// The box shows up on the computer as a USB network adapter, like the Pico
// box, and hands the computer its address by itself (a tiny DHCP server).
//
// The address comes from the switch on the board. Switches 1 to 6 give the
// box number n, 0 to 63 (switch 1 counts 1, switch 2 counts 2, switch 3
// counts 4 ... switch 6 counts 32; ON counts). The box is 10.1.n.1 and the
// computer gets 10.1.n.2, so every box is a small network of its own, and
// several boxes can be plugged into one computer. Lumenet looks for 10.1.n.1
// on every network adapter of the computer that has an address 10.1.n.x.
//
// If a network you use is in 10.1.x.x, change the first two numbers here.
#define NET_FIRST     10                     // the first two numbers of both addresses
#define NET_SECOND    1
#define NET_NETMASK   {255, 255, 255, 0}
#define NET_LEASE_S   3600                   // the computer asks again after at most this many seconds
// Both Ethernet addresses are made from the box number too (the PIC32 has no
// serial number): box 02:00:0A:01:nn:01, computer 02:00:0A:01:nn:02.

#define ARTNET_PORT    6454   // fixed by the Art-Net spec
#define CTRL_PORT      7000   // our own reliable command protocol
// Art-Net: output 1 of box n listens to universe ARTNET_UNIVERSE + 2n, output
// 2 to the one after it. Box 0: universes 0 and 1; box 3: 6 and 7. A box with
// one output listens to ARTNET_UNIVERSE + n.
#define ARTNET_UNIVERSE   0
#define NODE_SHORT_NAME "dmxbox 2"
#if defined(DMX_OUTPUTS) && DMX_OUTPUTS == 1
#define NODE_LONG_NAME  "dmxbox 2 - PIC32 USB Art-Net node with one output"
#else
#define NODE_LONG_NAME  "dmxbox 2 - PIC32 USB Art-Net node with two outputs"
#endif

// In MERGE_ARTNET_OVERRIDE mode, Art-Net is "live" for this long after the
// last packet. After that the box falls back to the cue layer.
#define ARTNET_LIVE_MS 3000

// ---------------------------------------------------------------- USB identity
// 0x1209 / 0x0001 is the "for testing only" number pair of pid.codes, as on
// the Pico box. The USB serial number is "box-" and the box number ("box-07").
#define USB_VID          0x1209
#define USB_PID          0x0001
#define USB_MANUFACTURER "dmxbox"
#define USB_PRODUCT      "dmxbox 2"
#define USB_MAX_POWER_MA 400    // what the box may draw from the USB port (it needs about 250 mA)
// The USB port of the PIC32 cannot use one endpoint number in both directions.
#define NET_EP_NOTIFY    0x81
#define NET_EP_OUT       0x02
#define NET_EP_IN        0x83

// ---------------------------------------------------------------- DMX
#ifndef DMX_OUTPUTS
#define DMX_OUTPUTS  2          // two DMX sockets; "build.cmd 1" builds the box with one
#endif
#define DMX_CHANNELS (512 * DMX_OUTPUTS)   // 512 for each: Lumenet counts the outputs from it
// Break 180 us, mark after break 40 us: see dmx_out.c.

// ---------------------------------------------------------------- presets
#define PRESET_SLOTS 8          // 8 x 1024 bytes, in flash (storage_flash.c)

// ---------------------------------------------------------------- pins
// The pin numbers of the PIC32MX270F256B in its 28-pin DIP case. They are set
// in board.c; the drawings in hardware/pic32 show the same.
//
//   output 1: TX RB15 (pin 26), direction RB14 (pin 25), RX RB13 (pin 24)
//   output 2: TX RB9  (pin 18), direction RB7  (pin 16), RX RB8  (pin 17)
//   switches 1 to 6 (box number): RA0, RA1, RB2, RB3, RB4, RA4 (pins 2, 3, 6, 7, 11, 12)
//   buttons: blackout RB0 (pin 4), preset RB1 (pin 5); they share the
//            programming pins PGED1/PGEC1 (do not press them while programming)
//   LED: RB5 (pin 14)
//
// The direction pins are low while the box sends; RDM (later) sets them high
// to listen. RX is wired for RDM but not used yet.

#define BUTTON_PRESET_SLOT 0
#define BUTTON_PRESET_FADE_MS 2000
