// Preset bank: PRESET_SLOTS full-universe snapshots, persisted as one blob.
#pragma once
#include <stdbool.h>
#include <stdint.h>
#include "config.h"

#define PRESET_MAGIC   0x44585042u  // 'DXPB'
#define PRESET_VERSION 1u

typedef struct {
    uint32_t magic;
    uint32_t version;
    uint32_t valid;   // bit n set = slot n holds data
    uint32_t crc;     // crc32 over data[]
    uint8_t  data[PRESET_SLOTS][DMX_CHANNELS];
} preset_bank_t;

void     presets_clear(preset_bank_t *b);
// Validate a bank read from storage; clears it if magic/version/crc are wrong.
bool     presets_validate(preset_bank_t *b);
void     presets_seal(preset_bank_t *b);  // recompute crc before saving
uint32_t crc32_calc(const uint8_t *data, uint32_t len);
