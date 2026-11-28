// dmxbox 4, the full box: one DMX output with RDM, MIDI in and out, a USB hub
// for a chain of boxes. What sets it apart from the other boxes for the shared
// RP2040 firmware (firmware/rp2040, whose config.h includes this file). Numbers
// only, so the simulator (firmware/sim) and the tests (firmware/test) use it too.
#pragma once

#define BOX_MODEL  4            // PING tells the desk which box it is
#define BOX_NAME   "dmxbox 4"   // Art-Net's short name and the USB product name
#define BOX_ABOUT  "dmxbox 4 - RP2040 USB Art-Net node with RDM and MIDI"   // Art-Net's long name

#define BOX_MIDI   1            // MIDI IN and OUT sockets, and a USB MIDI port
#define BOX_CHAIN  1            // a USB hub with an OUT socket for the next box
#define BOX_DMX_IN 1            // a DMX input (J8): a console's levels for the desk

// The box sits behind the hub chip on its own board. A hub port may give a
// device 100 mA when the hub takes its own power from the cable; asking for
// more, the computer would refuse the box. What the board really draws (hub,
// RP2040, DMX side: about 200 mA) is the hub's business, see BOM.md.
#define USB_MAX_POWER_MA 100

// ---------------------------------------------------------------- pins
// GPIO numbers of the RP2040; scripts/circuit.py has the same list.
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
#define PIN_DMX_IN    16        // PIO: DMX IN, from its optocoupler U12 (high = the line rests)
#define PIN_BOX_N1    18        // SW1 switch 1 to 8: GPIO 18 to 25, ON = low
#define PIN_CC1       26        // ADC0: CC1 of the OUT socket
#define PIN_CC2       27        // ADC1: CC2 of the OUT socket
#define PIN_VSENSE    28        // ADC2: half the supply voltage
#define VSENSE_DIVIDER 2        // R14 and R15 halve it
