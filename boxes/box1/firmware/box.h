// dmxbox 1: a Raspberry Pi Pico with one DMX output with RDM and the switch for
// its number; the Pico's micro-USB is the box's USB socket. What sets it apart
// from the other boxes for the shared RP2040 firmware (firmware/rp2040, whose
// config.h includes this file). Numbers only.
#pragma once

#define BOX_MODEL  1            // PING tells the desk which box it is
#define BOX_NAME   "dmxbox 1"   // Art-Net's short name and the USB product name
#define BOX_ABOUT  "dmxbox 1 - Raspberry Pi Pico USB Art-Net node with RDM"   // Art-Net's long name

#define BOX_MIDI   0            // no MIDI sockets and no USB MIDI port
#define BOX_CHAIN  0            // no hub and no OUT socket
#define BOX_DMX_IN 0            // no DMX input

// The box is straight on the computer's port. It draws about 150 mA (the Pico,
// the isolated DMX side through its DC-DC module, the LEDs).
#define USB_MAX_POWER_MA 250

// ---------------------------------------------------------------- pins
// GPIO numbers of the Pico; scripts/circuit.py has the same list.
#define PIN_LED_USB   1         // green: a computer uses the box
#define PIN_LED_DMX   2         // yellow: DMX goes out, flickers with lighting data
#define PIN_LED_MIDI  3         // red: RDM traffic
#define PIN_BOX_N1    4         // SW1 switch 1 to 8: GPIO 4 to 11, ON = low
#define PIN_LOG_TX    12        // UART0 TX: the log, 250000 baud, on the service pins J7 (pin 3)
#define PIN_DMX_RX    13        // UART0 RX: RDM answers, 250000 baud
#define PIN_DMX_TX    14        // PIO: DMX and RDM out. Low = optocoupler LED on = line low.
#define PIN_DMX_DE    15        // low = listen (RDM), high = send; the line sends while the Pico is off
#define PIN_VSENSE    29        // ADC3: the Pico's own VSYS / 3
#define VSENSE_DIVIDER 3        // the Pico's R5 and R6 divide VSYS by 3
