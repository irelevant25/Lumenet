// The lighting engine: two layers merged into one DMX universe.
//
//   cue layer    - driven by the ctrl protocol / buttons; every channel can
//                  fade independently, computed here at frame rate
//   Art-Net layer - raw values from the last ArtDmx packet
//
//   output = merge(cue, artnet) * master, or 0 when blacked out
//
// Pure C, no SDK calls: the same file runs on the Pico and in the simulator.
// The engine itself does no locking; callers wrap calls in plat_lock().
#pragma once
#include <stdbool.h>
#include <stdint.h>
#include "config.h"

typedef enum {
    MERGE_HTP = 0,              // highest value of cue and Art-Net wins
    MERGE_ARTNET_OVERRIDE = 1,  // Art-Net replaces the cue layer while live
} merge_mode_t;

typedef struct {
    // cue layer: value(t) = lerp(from, to, (t - t0) / dur); 16-bit (value << 8)
    uint16_t from[DMX_CHANNELS];
    uint16_t to[DMX_CHANNELS];
    uint32_t t0[DMX_CHANNELS];
    uint32_t dur[DMX_CHANNELS];

    uint8_t  artnet[DMX_CHANNELS];
    bool     artnet_has_data;
    uint32_t artnet_last_ms;

    uint8_t  merge_mode;
    uint8_t  master;    // 0..255, scales the final output
    bool     blackout;
} engine_t;

void engine_init(engine_t *e);

// Fade channels [start, start+count) (0-based) to values over dur_ms (0 = snap).
// A fade always starts from the value the channel has right now, so
// interrupting a running fade is smooth.
void engine_fade(engine_t *e, uint32_t now, uint16_t start, uint16_t count,
                 const uint8_t *values, uint32_t dur_ms);

void engine_artnet(engine_t *e, uint32_t now, uint16_t count, const uint8_t *values);
void engine_release_artnet(engine_t *e);
bool engine_artnet_live(const engine_t *e, uint32_t now);

// Render DMX_CHANNELS bytes. with_master=false gives the merged look before
// master/blackout (used for storing presets).
void engine_render(const engine_t *e, uint32_t now, uint8_t *out, bool with_master);
