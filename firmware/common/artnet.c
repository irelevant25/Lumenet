#include "artnet.h"
#include <string.h>
#include "platform.h"

#define OP_POLL      0x2000
#define OP_POLLREPLY 0x2100
#define OP_DMX       0x5000
#define OP_TODREQUEST 0x8000
#define OP_TODDATA   0x8100
#define OP_TODCONTROL 0x8200
#define OP_RDM       0x8300
#define TOD_DATA_LEN(count) (28 + 6 * (count))

// ArtPollReply has room for four ports.
#define REPLY_PORTS (DMX_OUTPUTS < 4 ? DMX_OUTPUTS : 4)

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
    r[23] = (uint8_t)(0xD0 | (n->rdm ? 0x02 : 0));  // Status1: indicators normal, front-panel addressing, RDM
    r[24] = 0xF0; r[25] = 0x7F;             // ESTA manufacturer 0x7FF0 = prototype range
    strncpy((char *)r + 26, NODE_SHORT_NAME, 17);
    strncpy((char *)r + 44, NODE_LONG_NAME, 63);
    strncpy((char *)r + 108, "#0001 [0000] dmxbox ok", 63);  // NodeReport
    r[173] = REPLY_PORTS;                   // NumPortsLo
    for (int port = 0; port < REPLY_PORTS; port++) {
        r[174 + port] = 0x80;               // PortTypes: outputs DMX512 from Art-Net
        r[182 + port] = 0x80;               // GoodOutput: data is being transmitted
        r[190 + port] = (uint8_t)((n->universe + port) & 0x0F);   // SwOut
    }
    r[200] = 0x00;                          // Style: StNode
    memcpy(r + 201, n->mac, 6);
    memcpy(r + 207, n->ip, 4);              // BindIp
    r[211] = 1;                             // BindIndex
    r[212] = 0x08;                          // Status2: supports 15-bit Port-Address
    return ARTNET_REPLY_LEN;
}

// Is this RDM packet for our port? Net in byte 21, the low byte of the Port-Address in 23.
static bool for_our_port(const artnet_node_t *n, uint8_t net, uint8_t address) {
    return net == ((n->universe >> 8) & 0x7F) && address == (n->universe & 0xFF);
}

static void header(uint8_t *r, uint16_t op) {
    memcpy(r, ARTNET_ID, 8);
    wr_le16(r + 8, op);
    r[10] = 0;
    r[11] = 14;                                  // protocol version 14
}

static int build_tod_data(const artnet_node_t *n, uint8_t *r, int cap) {
    int count = n->rdm->tod_count;
    if (cap < TOD_DATA_LEN(count)) return 0;
    memset(r, 0, 28);
    header(r, OP_TODDATA);
    r[12] = 1;                                   // RDM version 1.0
    r[13] = 1;                                   // physical port 1
    r[20] = 1;                                   // BindIndex
    r[21] = (uint8_t)((n->universe >> 8) & 0x7F);    // Net
    r[22] = 0x00;                                // TodFull: the whole table
    r[23] = (uint8_t)(n->universe & 0xFF);       // Address
    wr_be16(r + 24, (uint16_t)count);            // UidTotal
    r[26] = 0;                                   // BlockCount
    r[27] = (uint8_t)count;                      // UidCount
    memcpy(r + 28, n->rdm->tod, (size_t)count * 6);
    return TOD_DATA_LEN(count);
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
        uint16_t port = (uint16_t)(rd_le16(pkt + 14) - n->universe);   // 0 = output 1
        if (port >= DMX_OUTPUTS) return 0;
        uint8_t seq = pkt[12];
        // Drop packets that arrive out of order (seq 0 = sender doesn't use it).
        // A gap of >1 s means a new/restarted controller: accept any seq.
        if (seq != 0 && n->last_seq[port] != 0 && (uint32_t)(now - n->last_dmx_ms[port]) < 1000) {
            int8_t d = (int8_t)(uint8_t)(seq - n->last_seq[port]);
            if (d < 0 && d > -64) return 0;
        }
        n->last_seq[port] = seq;
        n->last_dmx_ms[port] = now;
        uint16_t count = rd_be16(pkt + 16);
        if (count > len - 18) count = (uint16_t)(len - 18);
        if (count > DMX_OUTPUT_CHANNELS) count = DMX_OUTPUT_CHANNELS;
        plat_lock();
        engine_artnet(n->eng, now, (uint16_t)(port * DMX_OUTPUT_CHANNELS), count, pkt + 18);
        plat_unlock();
        n->dmx_packets++;
        return 0;
    }

    if (!n->rdm || len < 24) return 0;
    if (op == OP_TODREQUEST) {                   // Net in 21, AdCount in 23, the addresses after it
        if (pkt[21] != ((n->universe >> 8) & 0x7F)) return 0;
        int count = pkt[23];
        for (int i = 0; i < count && 24 + i < len; i++)
            if (pkt[24 + i] == (n->universe & 0xFF)) return build_tod_data(n, reply, reply_cap);
        return 0;
    }
    if (op == OP_TODCONTROL) {                   // Net 21, Command 22 (1 = flush), Address 23
        if (!for_our_port(n, pkt[21], pkt[23])) return 0;
        if (pkt[22] == 0x01) {
            if (!n->rdm->discovering) rdm_start_discovery(n->rdm);
            n->tod_waiting = 1;
            n->deferred_new = 1;
            return 0;
        }
        return build_tod_data(n, reply, reply_cap);
    }
    if (op == OP_RDM && len >= 24 + 24) {        // Net 21, Command 22 (0 = process), Address 23, packet from 24
        if (!for_our_port(n, pkt[21], pkt[23]) || pkt[22] != 0x00) return 0;
        if (rdm_submit_raw(n->rdm, RDM_OWNER_ARTNET, pkt + 24, len - 24)) {
            n->rdm_waiting = 1;
            n->deferred_new = 1;
        }
        return 0;
    }
    return 0;
}

int artnet_poll(artnet_node_t *n, uint8_t *reply, int reply_cap) {
    if (!n->rdm) return 0;
    if (n->rdm_waiting) {
        uint8_t status;
        const uint8_t *raw;
        int raw_len;
        if (rdm_take(n->rdm, RDM_OWNER_ARTNET, &status, NULL, &raw, &raw_len)) {
            n->rdm_waiting = 0;
            if (status != RDM_DONE_OK || raw_len < 2 || 24 + raw_len - 1 > reply_cap) return 0;
            memset(reply, 0, 24);
            header(reply, OP_RDM);
            reply[12] = 1;                           // RDM version 1.0
            reply[21] = (uint8_t)((n->universe >> 8) & 0x7F);
            reply[22] = 0x00;                        // ArProcess
            reply[23] = (uint8_t)(n->universe & 0xFF);
            memcpy(reply + 24, raw + 1, (size_t)(raw_len - 1));   // without the start code
            return 24 + raw_len - 1;
        }
    }
    if (n->tod_waiting && !n->rdm->discovering) {
        n->tod_waiting = 0;
        return build_tod_data(n, reply, reply_cap);
    }
    return 0;
}
