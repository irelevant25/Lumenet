// The PIC32 box's hardware apart from the DMX outputs: the clock, the pins of
// the switch, the buttons and the LED, and a millisecond clock. The pin
// numbers are listed in config.h.
#pragma once
#include <stdbool.h>
#include <stdint.h>

// Pins, the millisecond timer, the interrupt priorities; interrupts on.
void board_init(void);

// The box number from switches 1 to 6, 0 to 63 (switch 1 counts 1 ... switch
// 6 counts 32). Read once at power-up: change the switch with the box unplugged.
uint8_t board_box_number(void);

// Milliseconds since power-up.
uint32_t board_millis(void);

// True while the button is held down.
bool board_button_blackout(void);
bool board_button_preset(void);

void board_led(bool on);
