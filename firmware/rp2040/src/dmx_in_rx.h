// DMX IN of the box (BOX_DMX_IN in box.h): a PIO state machine reads the
// console's line on PIN_DMX_IN (dmx_rx.pio), and an interrupt of core 0 gives
// its bytes to dmx_in.c, which keeps the frames for the desk.
#pragma once
#include "dmx_in.h"

// Starts the receiver; from then on `in` gets every frame. Core 0.
void dmx_in_rx_start(dmx_in_t *in);
