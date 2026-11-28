// dmxbox 3: one DMX output with RDM and the switch for its number, one USB-C
// socket: box 4 without the hub, the OUT and POWER sockets and MIDI. What sets
// it apart from the other boxes for the shared RP2040 firmware (firmware/rp2040,
// whose config.h includes this file). Numbers only.
#pragma once

#define BOX_MODEL  3            // PING tells the desk which box it is
#define BOX_NAME   "dmxbox 3"   // Art-Net's short name and the USB product name
#define BOX_ABOUT  "dmxbox 3 - RP2040 USB Art-Net node with RDM"   // Art-Net's long name

#define BOX_MIDI   0            // no MIDI sockets and no USB MIDI port
#define BOX_CHAIN  0            // no hub and no OUT socket
#define BOX_DMX_IN 0            // no DMX input

// The box is straight on the computer's port. It draws about 150 to 200 mA (the
// RP2040, the isolated DMX side through its DC-DC module, the LEDs).
#define USB_MAX_POWER_MA 250

// ---------------------------------------------------------------- pins
// GPIO numbers of the RP2040; scripts/circuit.py has the same list (box 4's,
// without OUT, MIDI and the CC pins of OUT).
#define PIN_LED_USB   1         // green: a computer uses the box
#define PIN_LED_DMX   2         // yellow: DMX goes out, flickers with lighting data
#define PIN_LED_MIDI  3         // red: RDM traffic
#define PIN_LOG_TX    12        // UART0 TX: the log, 250000 baud, on the service pins J7 (pin 6)
#define PIN_DMX_RX    13        // UART0 RX: RDM answers, 250000 baud
#define PIN_DMX_TX    14        // PIO: DMX and RDM out. Low = optocoupler LED on = line low.
#define PIN_DMX_DE    15        // low = listen (RDM), high = send; the line sends while the RP2040 is off
#define PIN_BOX_N1    18        // SW1 switch 1 to 8: GPIO 18 to 25, ON = low
#define PIN_VSENSE    28        // ADC2: half the supply voltage
#define VSENSE_DIVIDER 2        // R14 and R15 halve it
