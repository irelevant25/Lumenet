#pragma once
#include <stdbool.h>
#include "presets.h"

void storage_load(preset_bank_t *b);        // always yields a valid (maybe empty) bank
bool storage_save(const preset_bank_t *b);
