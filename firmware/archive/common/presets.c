#include "presets.h"
#include <string.h>

uint32_t crc32_calc(const uint8_t *data, uint32_t len) {
    uint32_t crc = 0xFFFFFFFFu;
    for (uint32_t i = 0; i < len; i++) {
        crc ^= data[i];
        for (int k = 0; k < 8; k++)
            crc = (crc >> 1) ^ (0xEDB88320u & (uint32_t)-(int32_t)(crc & 1u));
    }
    return ~crc;
}

void presets_clear(preset_bank_t *b) {
    memset(b, 0, sizeof *b);
    b->magic = PRESET_MAGIC;
    b->version = PRESET_VERSION;
    presets_seal(b);
}

void presets_seal(preset_bank_t *b) {
    b->crc = crc32_calc(&b->data[0][0], sizeof b->data);
}

bool presets_validate(preset_bank_t *b) {
    if (b->magic == PRESET_MAGIC && b->version == PRESET_VERSION &&
        b->crc == crc32_calc(&b->data[0][0], sizeof b->data))
        return true;
    presets_clear(b);
    return false;
}
