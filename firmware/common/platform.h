// Hooks the portable core (engine/artnet/ctrl) needs from the platform.
// Implemented by the main.c of each box (firmware/pico, firmware/pic32) and by
// firmware/sim/sim.c on a PC.
#pragma once
#include <stdbool.h>
#include <stdint.h>
#include "presets.h"

// Mutual exclusion between the network core and the DMX core.
void plat_lock(void);
void plat_unlock(void);

// Persist the preset bank. Called WITHOUT the lock held. May block ~150 ms.
bool plat_save_presets(const preset_bank_t *bank);

// ---- small big/little endian helpers shared by the protocol code
static inline uint16_t rd_be16(const uint8_t *p) { return (uint16_t)((p[0] << 8) | p[1]); }
static inline uint32_t rd_be32(const uint8_t *p) {
    return ((uint32_t)p[0] << 24) | ((uint32_t)p[1] << 16) | ((uint32_t)p[2] << 8) | p[3];
}
static inline uint16_t rd_le16(const uint8_t *p) { return (uint16_t)(p[0] | (p[1] << 8)); }
static inline void wr_be16(uint8_t *p, uint16_t v) { p[0] = (uint8_t)(v >> 8); p[1] = (uint8_t)v; }
static inline void wr_be32(uint8_t *p, uint32_t v) {
    p[0] = (uint8_t)(v >> 24); p[1] = (uint8_t)(v >> 16); p[2] = (uint8_t)(v >> 8); p[3] = (uint8_t)v;
}
static inline void wr_le16(uint8_t *p, uint16_t v) { p[0] = (uint8_t)v; p[1] = (uint8_t)(v >> 8); }
