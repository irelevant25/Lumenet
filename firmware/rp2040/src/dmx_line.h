// The DMX line of the box: DMX frames out, and RDM both ways between them.
//
// Core 1 owns the line. It sends a DMX frame, then the RDM transactions core 0
// has handed over (plat_rdm_send in platform.h), then the next frame. A
// transaction is the request, and for one that gets an answer: the line
// driver switched to listening, the answer read by UART0, the driver switched
// back. The gaps follow E1.20 (table 3-2).
//
// The PIO makes break, mark after break and the bytes (dmx_tx.pio), DMA feeds
// it, the CPU only starts each packet.
#pragma once
#include <stdint.h>

// Works out the next frame's channels (DMX_CHANNELS bytes). Runs on core 1.
typedef void (*dmx_render_fn)(uint8_t *channels);

// The pins: the driver sends, the line idles. Core 0, before core 1 starts.
void dmx_line_init(void);

// Core 1: frames and RDM forever. Never returns.
void dmx_line_run(dmx_render_fn render);

// DMX frames sent so far (any core may read it).
uint32_t dmx_line_frames(void);
