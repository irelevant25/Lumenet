// Minimal Art-Net 4 node: ArtDmx in, ArtPoll -> ArtPollReply out.
#pragma once
#include <stdint.h>
#include "engine.h"

#define ARTNET_REPLY_LEN 239

typedef struct {
    engine_t *eng;
    uint8_t   ip[4];
    uint8_t   mac[6];
    uint16_t  universe;      // 15-bit Port-Address
    uint8_t   last_seq;      // ArtDmx sequence tracking (0 = disabled)
    uint32_t  last_dmx_ms;
    uint32_t  dmx_packets;   // statistics
    uint32_t  poll_packets;
} artnet_node_t;

// Handle one received UDP datagram. If a reply must be sent back to the
// sender (ArtPollReply), it is written to `reply` and its length returned;
// otherwise returns 0. Takes plat_lock() internally while touching the engine.
int artnet_handle(artnet_node_t *n, const uint8_t *pkt, int len, uint32_t now,
                  uint8_t *reply, int reply_cap);
