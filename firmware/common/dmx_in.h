// DMX IN: what a console sends to the box's input (dmxbox 4), kept as frames of
// up to 512 levels for the desk.
//
// The receiver (the RP2040's PIO, rp2040/src/dmx_in_rx.c) hands over every
// byte with a flag for a framing error (its stop bit was low). A 0 with a
// framing error is a break: it begins a frame. The first byte after it is the
// start code: 0 for levels; a frame with another one (RDM, text) is passed
// over. A frame is complete at the next break, or after its 512th level. A
// framing error that is no break spoils the frame being received, and so do
// bytes the receiver lost: it is dropped, and the next break begins anew.
//
// The desk asks for the last complete frame with the number of the one it saw
// last (CT_GET_INPUT, ctrl.c). The receiver stores frames in an interrupt, the
// network code reads them in the main loop: dmx_in_read takes a copy that no
// new frame changed while it was taken.
#pragma once
#include <stdbool.h>
#include <stdint.h>

#define DMX_IN_SLOTS   512
#define DMX_IN_LIVE_MS 1000     // a frame within this long: a console is sending

typedef struct {
    int16_t  at;                 // the frame coming in: -1 waits for a break, 0 for the start code, n > 0 has n - 1 levels
    uint8_t  rx[DMX_IN_SLOTS];
    uint8_t  levels[DMX_IN_SLOTS];   // the last complete frame
    uint16_t count;
    volatile uint32_t frames;    // complete frames so far: the last one's number, 0 before the first
    volatile uint32_t changing;  // odd while a frame is being stored (the reader tries again)
    volatile uint32_t last_ms;   // when the last frame was complete
    uint32_t spoiled;            // frames dropped: framing errors, lost bytes
} dmx_in_t;

void dmx_in_init(dmx_in_t *d);

// The receiver: one byte, and whether its stop bit was low.
void dmx_in_byte(dmx_in_t *d, uint8_t b, bool framing_error, uint32_t now_ms);

// The receiver lost bytes (its buffer ran over): the frame coming in is spoiled.
void dmx_in_lost(dmx_in_t *d);

// A whole frame at once, levels[count] with count 1 to 512: the simulator's console.
void dmx_in_frame(dmx_in_t *d, const uint8_t *levels, int count, uint32_t now_ms);

// The last complete frame: copies its levels and their count, and when it came;
// returns its number (0: none yet, and nothing is copied).
uint32_t dmx_in_read(const dmx_in_t *d, uint8_t *levels, uint16_t *count, uint32_t *last_ms);
