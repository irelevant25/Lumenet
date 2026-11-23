// NOT the Pico SDK: stand-in for pico/unique_id.h, see fake_types.h.
#pragma once
#include "fake_types.h"

#define PICO_UNIQUE_BOARD_ID_SIZE_BYTES 8

typedef struct {
    uint8_t id[PICO_UNIQUE_BOARD_ID_SIZE_BYTES];
} pico_unique_board_id_t;

void pico_get_unique_board_id(pico_unique_board_id_t *id_out);
void pico_get_unique_board_id_string(char *id_out, uint len);
