#include "artnet.h"
#include <string.h>
#include "platform.h"

#define OP_POLL      0x2000
#define OP_POLLREPLY 0x2100
#define OP_DMX       0x5000

static const uint8_t ARTNET_ID[8] = {'A', 'r', 't', '-', 'N', 'e', 't', 0};

static int build_poll_reply(const artnet_node_t *n, uint8_t *r, int cap) {
    if (cap < ARTNET_REPLY_LEN) return 0;
    memset(r, 0, ARTNET_REPLY_LEN);
    memcpy(r, ARTNET_ID, 8);
    wr_le16(r + 8, OP_POLLREPLY);
    memcpy(r + 10, n->ip, 4);
    wr_le16(r + 14, ARTNET_PORT);
    r[16] = FW_VERSION_MAJOR;               // VersInfoH
    r[17] = FW_VERSION_MINOR;               // VersInfoL
    r[18] = (uint8_t)((n->universe >> 8) & 0x7F);   // NetSwitch
    r[19] = (uint8_t)((n->universe >> 4) & 0x0F);   // SubSwitch
    r[20] = 0x00; r[21] = 0xFF;             // OEM: unknown
    r[23] = 0xD0;                           // Status1: indicators normal, front-panel addressing
    r[24] = 0xF0; r[25] = 0x7F;             // ESTA manufacturer 0x7FF0 = prototype range
    strncpy((char *)r + 26, NODE_SHORT_NAME, 17);
    strncpy((char *)r + 44, NODE_LONG_NAME, 63);
    strncpy((char *)r + 108, "#0001 [0000] dmxbox ok", 63);  // NodeReport
    r[173] = 1;                             // NumPortsLo
    r[174] = 0x80;                          // PortTypes[0]: outputs DMX512 from Art-Net
    r[182] = 0x80;                          // GoodOutput[0]: data is being transmitted
    r[190] = (uint8_t)(n->universe & 0x0F); // SwOut[0]
    r[200] = 0x00;                          // Style: StNode
    memcpy(r + 201, n->mac, 6);
    memcpy(r + 207, n->ip, 4);              // BindIp
    r[211] = 1;                             // BindIndex
    r[212] = 0x08;                          // Status2: supports 15-bit Port-Address
    return ARTNET_REPLY_LEN;
}

int artnet_handle(artnet_node_t *n, const uint8_t *pkt, int len, uint32_t now,
                  uint8_t *reply, int reply_cap) {
    if (len < 12 || memcmp(pkt, ARTNET_ID, 8) != 0) return 0;
    uint16_t op = rd_le16(pkt + 8);

    if (op == OP_POLL) {
        n->poll_packets++;
        return build_poll_reply(n, reply, reply_cap);
    }

    if (op == OP_DMX && len >= 18) {
        if (rd_le16(pkt + 14) != n->universe) return 0;
        uint8_t seq = pkt[12];
        // Drop packets that arrive out of order (seq 0 = sender doesn't use it).
        // A gap of >1 s means a new/restarted controller: accept any seq.
        if (seq != 0 && n->last_seq != 0 && (uint32_t)(now - n->last_dmx_ms) < 1000) {
            int8_t d = (int8_t)(uint8_t)(seq - n->last_seq);
            if (d < 0 && d > -64) return 0;
        }
        n->last_seq = seq;
        n->last_dmx_ms = now;
        uint16_t count = rd_be16(pkt + 16);
        if (count > len - 18) count = (uint16_t)(len - 18);
        plat_lock();
        engine_artnet(n->eng, now, count, pkt + 18);
        plat_unlock();
        n->dmx_packets++;
    }
    return 0;
}
