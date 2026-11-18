#include "engine.h"
#include <string.h>

void engine_init(engine_t *e) {
    memset(e, 0, sizeof *e);
    e->merge_mode = MERGE_HTP;
    e->master = 255;
}

static uint16_t cue_value16(const engine_t *e, uint16_t ch, uint32_t now) {
    uint32_t elapsed = now - e->t0[ch];          // wrap-safe
    uint32_t dur = e->dur[ch];
    if (dur == 0 || elapsed >= dur) return e->to[ch];
    int32_t diff = (int32_t)e->to[ch] - (int32_t)e->from[ch];
    int64_t step = (int64_t)diff * (int64_t)elapsed / (int64_t)dur;
    return (uint16_t)((int32_t)e->from[ch] + (int32_t)step);
}

static uint8_t to8(uint16_t v16) {
    uint32_t v = ((uint32_t)v16 + 0x80u) >> 8;
    return v > 255 ? 255 : (uint8_t)v;
}

void engine_fade(engine_t *e, uint32_t now, uint16_t start, uint16_t count,
                 const uint8_t *values, uint32_t dur_ms) {
    if (start >= DMX_CHANNELS) return;
    if (count > DMX_CHANNELS - start) count = (uint16_t)(DMX_CHANNELS - start);
    for (uint16_t i = 0; i < count; i++) {
        uint16_t ch = (uint16_t)(start + i);
        e->from[ch] = cue_value16(e, ch, now);
        e->to[ch]   = (uint16_t)(values[i] << 8);
        e->t0[ch]   = now;
        e->dur[ch]  = dur_ms;
    }
}

void engine_artnet(engine_t *e, uint32_t now, uint16_t count, const uint8_t *values) {
    if (count > DMX_CHANNELS) count = DMX_CHANNELS;
    memcpy(e->artnet, values, count);
    e->artnet_has_data = true;
    e->artnet_last_ms = now;
}

void engine_release_artnet(engine_t *e) {
    memset(e->artnet, 0, sizeof e->artnet);
    e->artnet_has_data = false;
}

bool engine_artnet_live(const engine_t *e, uint32_t now) {
    return e->artnet_has_data && (uint32_t)(now - e->artnet_last_ms) < ARTNET_LIVE_MS;
}

void engine_render(const engine_t *e, uint32_t now, uint8_t *out, bool with_master) {
    // HTP: Art-Net layer holds its last look until released (survives the
    // laptop going to sleep). OVERRIDE: Art-Net wins only while packets flow.
    bool override = e->merge_mode == MERGE_ARTNET_OVERRIDE && engine_artnet_live(e, now);
    bool htp_art  = e->merge_mode == MERGE_HTP && e->artnet_has_data;

    for (uint16_t ch = 0; ch < DMX_CHANNELS; ch++) {
        uint8_t v = to8(cue_value16(e, ch, now));
        if (override) v = e->artnet[ch];
        else if (htp_art && e->artnet[ch] > v) v = e->artnet[ch];

        if (with_master) {
            if (e->blackout) v = 0;
            else if (e->master != 255) v = (uint8_t)(((uint32_t)v * e->master + 127) / 255);
        }
        out[ch] = v;
    }
}
