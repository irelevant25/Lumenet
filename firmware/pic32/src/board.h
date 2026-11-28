// The board apart from the DMX line: the clock, the box number switch, the
// three LEDs, the log and the time. The pins are in the box's box.h.
#pragma once
#include <stdbool.h>
#include <stdint.h>

#define PBCLK_HZ           40000000u   // the peripherals' clock: the UARTs, timer 1
#define BOARD_TICKS_PER_US 20u         // the core timer counts at half the 40 MHz of the CPU

// Pins, the UARTs' pins, the millisecond timer, the log, the interrupts.
void board_init(void);

// The box number from SW1, 0 to 255 (switch 1 counts 1 ... switch 8 counts
// 128; ON counts). Read once at power-up: change the switch with the box unplugged.
uint8_t board_box_number(void);

uint32_t board_millis(void);    // milliseconds since power-up
uint32_t board_ticks(void);     // the core timer: BOARD_TICKS_PER_US for every microsecond

void board_leds(bool usb, bool dmx, bool rdm);

// The log: printf-style text out of U2TX (pin 4 of J2), 250000 baud. It waits
// while the UART is busy (40 us a character): for rare lines only.
void board_log(const char *fmt, ...) __attribute__((format(printf, 1, 2)));
