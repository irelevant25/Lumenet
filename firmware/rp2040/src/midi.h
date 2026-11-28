// MIDI: the DIN sockets MIDI IN and MIDI OUT on UART1 (31250 baud), and the
// box's USB MIDI port. What comes in at MIDI IN goes to the computer, what the
// computer sends goes out of MIDI OUT. The computer sees the box as a MIDI
// interface named after the box ("dmxbox 4"), like any other. A box without
// MIDI (BOX_MIDI 0 in its box.h) gets functions that do nothing.
#pragma once
#include <stdint.h>

void midi_init(void);

// Call it from the main loop (after usb_net_poll, which runs TinyUSB).
void midi_poll(void);

// When a MIDI byte last went in or out, in milliseconds since power-up (for the LED).
uint32_t midi_last_ms(void);
