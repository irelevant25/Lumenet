// MIDI: the DIN sockets MIDI IN and MIDI OUT on UART1 (31250 baud), and the
// box's USB MIDI port. What comes in at MIDI IN goes to the computer, what the
// computer sends goes out of MIDI OUT. The computer sees the box as a MIDI
// interface named "dmxbox 3", like any other.
#pragma once
#include <stdint.h>

void midi_init(void);

// Call it from the main loop (after usb_net_poll, which runs TinyUSB).
void midi_poll(void);

// When a MIDI byte last went in or out, in milliseconds since power-up (for the LED).
uint32_t midi_last_ms(void);
