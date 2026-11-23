// Minimal Art-Net 4 node: ArtDmx in, ArtPoll -> ArtPollReply out.
//
// A box with several outputs listens to as many universes, one after the
// other: output 1 to `universe`, output 2 to `universe` + 1, and so on. They
// must not cross into the next Sub-Net (the last 4 bits of `universe` plus the
// outputs stay below 16), because one ArtPollReply has only one Sub-Net.
#pragma once
#include <stdint.h>
#include "engine.h"

#define ARTNET_REPLY_LEN 239

typedef struct {
    engine_t *eng;
    uint8_t   ip[4];
    uint8_t   mac[6];
    uint16_t  universe;                  // 15-bit Port-Address of output 1
    uint8_t   last_seq[DMX_OUTPUTS];     // ArtDmx sequence tracking per output (0 = disabled)
    uint32_t  last_dmx_ms[DMX_OUTPUTS];
    uint32_t  dmx_packets;               // statistics
    uint32_t  poll_packets;
} artnet_node_t;

// Handle one received UDP datagram. If a reply must be sent back to the
// sender (ArtPollReply), it is written to `reply` and its length returned;
// otherwise returns 0. Takes plat_lock() internally while touching the engine.
int artnet_handle(artnet_node_t *n, const uint8_t *pkt, int len, uint32_t now,
                  uint8_t *reply, int reply_cap);
