// dmxbox 2: a PIC32MX270F256B with one DMX output with RDM and the switch for
// its number, every part through-hole. What sets it apart from the other boxes
// in the settings they share (firmware/common/config.h includes this file);
// its firmware is firmware/pic32. Numbers only.
#pragma once

#define BOX_MODEL  2            // PING tells the desk which box it is
#define BOX_NAME   "dmxbox 2"   // Art-Net's short name and the USB product name
#define BOX_ABOUT  "dmxbox 2 - PIC32 USB Art-Net node with RDM"   // Art-Net's long name

#define BOX_MIDI   0            // no MIDI sockets and no USB MIDI port
#define BOX_CHAIN  0            // no hub and no OUT socket
#define BOX_DMX_IN 0            // no DMX input

// The box is straight on the computer's port. It draws about 150 mA (the PIC,
// the isolated DMX side through its DC-DC module, the LEDs).
#define USB_MAX_POWER_MA 250

// ---------------------------------------------------------------- pins
// Bits of the PIC's ports: PA(n) is RAn, PB(n) is RBn; then the pin of its
// 28-pin DIP. scripts/circuit.py has the same list. The UARTs reach their pins
// through the PIC's peripheral pin select, which firmware/pic32 sets for
// exactly these pins.
#define PA(n) (n)
#define PB(n) (16 + (n))
#define PIN_LED_USB   PB(8)     // pin 17: green, a computer uses the box
#define PIN_LED_DMX   PB(9)     // pin 18: yellow, DMX goes out, flickers with lighting data
#define PIN_LED_RDM   PB(1)     // pin 5 (PGEC1, the programmer's too): red, RDM traffic
#define PIN_LOG_TX    PB(0)     // pin 4 (PGED1, the programmer's too): U2TX, the log, 250000 baud, J2 pin 4
#define PIN_DMX_RX    PB(13)    // pin 24: U1RX, RDM answers, 250000 baud
#define PIN_DMX_DE    PB(14)    // pin 25: low = listen (RDM), high = send; the line sends while the PIC is in reset
#define PIN_DMX_TX    PB(15)    // pin 26: U1TX, DMX and RDM out. Low = optocoupler LED on = line low.
// SW1 switch 1 to 8 (they count 1, 2, 4 ... 128), ON = low: pins 2, 3, 6, 7, 11, 12, 14 and 16
#define PIN_BOX_N     {PA(0), PA(1), PB(2), PB(3), PB(4), PA(4), PB(5), PB(7)}
