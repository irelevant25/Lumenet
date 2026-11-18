// DMX512 output: PIO generates break/MAB and the 250 kbaud 8N2 bytes,
// DMA feeds it. The CPU only kicks off each frame.
#pragma once
#include <stdint.h>
#include "hardware/pio.h"

// pin_de: driver-enable pin of the RS-485 transceiver, or -1 if DE is wired high.
void dmx_out_init(PIO pio, uint pin_tx, int pin_de);

// Send one packet: frame[0] is the start code (0x00), followed by `len - 1`
// channel bytes. Blocks until the last stop bit has left the pin
// (~23 ms for a full 513-byte packet).
void dmx_out_send(const uint8_t *frame, uint len);
