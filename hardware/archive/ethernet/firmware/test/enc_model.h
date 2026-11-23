// A behavioural model of the ENC28J60 at SPI level, written from the datasheet
// (register banks, buffer memory with its receive ring, PHY access, transmit).
// It lets the real driver run on a PC: tests push frames "onto the wire" and
// look at what the driver transmits.
#pragma once
#include <stdbool.h>
#include <stdint.h>
#include "enc28j60.h"

extern const enc_hal_t MODEL_HAL;

void model_power_on(bool chip_present);
void model_set_link(bool up);

// A frame arrives from the network. Returns false if the chip filtered it out
// or had no room for it.
bool model_wire_in(const uint8_t *frame, int len);

int            model_sent_count(void);
const uint8_t *model_sent(int index, int *len);   // frame as given to the chip (before padding / CRC)
void           model_clear_sent(void);

uint8_t  model_reg(int bank, int addr);
uint16_t model_phy(int addr);
int      model_rule_violations(void);             // e.g. an even value written to ERXRDPT
const char *model_last_violation(void);
