// The two DMX512 outputs of the PIC32 box, on its two UARTs.
#pragma once
#include <stdbool.h>
#include <stdint.h>

// Sets up both UARTs. The lines rest (mark) until the first frame.
void dmx_out_init(void);

// True when no output is sending: the next frame can start.
bool dmx_out_idle(void);

// Starts a frame on both outputs at once: output 1 sends levels[0..511],
// output 2 levels[512..1023]. The levels must stay unchanged until
// dmx_out_idle() is true again (about 23 ms).
void dmx_out_start(const uint8_t *levels);
