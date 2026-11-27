// The board apart from the DMX line and MIDI: the box number switch, the three
// LEDs, the supply voltage and the 5 V for the next box in a chain.
#pragma once
#include <stdbool.h>
#include <stdint.h>

// Pins and the ADC. The OUT socket stays without 5 V until a box is seen there.
void board_init(void);

// The box number from SW1, 0 to 255 (switch 1 counts 1 ... switch 8 counts
// 128; ON counts). Read once at power-up: change the switch with the box unplugged.
uint8_t board_box_number(void);

// The supply voltage in millivolts (IN, or POWER when something is plugged in there).
uint16_t board_supply_mv(void);

// Call it from the main loop. It watches the CC pins of the OUT socket: a
// USB device plugged in there pulls one of them to about 0.4 V (its 5.1k to
// ground against the box's 33k to 3.3 V). Then the box switches the 5 V on,
// and off again when it is gone. A computer or a charger plugged into OUT by
// mistake pulls nothing down and gets no 5 V.
void board_poll(uint32_t now_ms);

// True while the OUT socket has 5 V.
bool board_out_powered(void);

void board_leds(bool usb, bool dmx, bool midi);
