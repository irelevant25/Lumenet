// The DMX line of box 2: DMX frames out, and RDM both ways between them.
//
// UART1 sends the bytes and hears the RDM answers; its interrupt feeds it and
// collects them. The main loop (dmx_line_poll) decides what goes out next: a
// DMX frame, then the RDM transactions rdm.c has handed over (plat_rdm_send in
// platform.h), then the next frame. The RP2040 boxes do the same on their
// second core (rp2040/src/dmx_line.c).
#pragma once
#include <stdbool.h>
#include <stdint.h>

// The pins and UART1: the driver sends, the line rests at mark.
void dmx_line_init(void);

// Call it from the main loop as often as it comes round. `frame` is the next
// frame when one is ready (its start code and DMX_CHANNELS levels), else NULL.
// Returns true when it has started sending that frame: it must then stay
// unchanged until this returns true again for another one.
bool dmx_line_poll(const uint8_t *frame);

// UART1's interrupt (board.c calls it): feeds the bytes, collects the answers.
void dmx_line_interrupt(void);
