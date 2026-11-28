#include "dmx_in.h"
#include <string.h>

// Keeps the compiler (and the RP2040) from moving the copy of a frame past the
// counters around it. The PC's simulator and tests have no interrupt.
#if defined(__GNUC__)
#define BARRIER() __sync_synchronize()
#else
#define BARRIER() ((void)0)
#endif

void dmx_in_init(dmx_in_t *d) {
    memset(d, 0, sizeof *d);
    d->at = -1;
}

static void store(dmx_in_t *d, const uint8_t *levels, int count, uint32_t now_ms) {
    d->changing++;
    BARRIER();
    memcpy(d->levels, levels, (size_t)count);
    d->count = (uint16_t)count;
    d->last_ms = now_ms;
    d->frames++;
    BARRIER();
    d->changing++;
}

void dmx_in_byte(dmx_in_t *d, uint8_t b, bool framing_error, uint32_t now_ms) {
    if (framing_error) {
        if (b != 0) {                                    // a broken byte: the frame is lost
            if (d->at > 0) d->spoiled++;
            d->at = -1;
            return;
        }
        if (d->at > 1) store(d, d->rx, d->at - 1, now_ms);   // a break ends the frame before it
        d->at = 0;
        return;
    }
    if (d->at < 0) return;                               // waiting for a break
    if (d->at == 0) {                                    // the start code
        d->at = b == 0 ? 1 : -1;
        return;
    }
    d->rx[d->at - 1] = b;
    if (++d->at > DMX_IN_SLOTS) {                        // 512 levels: complete without waiting for the break
        store(d, d->rx, DMX_IN_SLOTS, now_ms);
        d->at = -1;
    }
}

void dmx_in_lost(dmx_in_t *d) {
    if (d->at > 0) d->spoiled++;
    d->at = -1;
}

void dmx_in_frame(dmx_in_t *d, const uint8_t *levels, int count, uint32_t now_ms) {
    if (count < 1) return;
    if (count > DMX_IN_SLOTS) count = DMX_IN_SLOTS;
    store(d, levels, count, now_ms);
}

uint32_t dmx_in_read(const dmx_in_t *d, uint8_t *levels, uint16_t *count, uint32_t *last_ms) {
    for (;;) {
        uint32_t before = d->changing;
        BARRIER();
        uint32_t frames = d->frames;
        if (frames == 0) {
            *count = 0;
            *last_ms = 0;
            return 0;
        }
        uint16_t n = d->count;
        memcpy(levels, d->levels, n);
        uint32_t when = d->last_ms;
        BARRIER();
        if (!(before & 1) && d->changing == before) {    // no frame was stored meanwhile
            *count = n;
            *last_ms = when;
            return frames;
        }
    }
}
