// PC test of the dmxbox 3 firmware. No board and no USB cable are needed:
// fake_pico.c plays the hardware, the USB library (TinyUSB) and the computer at
// the other end of the cable.
//
//   1. the network stack (net.c) with the protocols on top, fed with frames directly
//   2. the address hand-out (dhcp_server.c)
//   3. the USB adapter code (usb_net.c) against a model of TinyUSB's network driver
//   4. the USB descriptor tables (usb_descriptors.c): network adapter and MIDI
//   5. the whole firmware: the real main.c running on the pretend hardware, with
//      lights that answer RDM on its DMX line
//
//   Windows:      build.cmd (also builds and runs test_rdm.c)
//   Linux/macOS:  cc -std=c11 -Wall -Wextra -Ifake_pico -I../src -I../common -I../sim -I. test_net.c fake_pico.c \
//                    firmware_main.c ../common/usb_net.c ../common/usb_descriptors.c ../common/net.c \
//                    ../common/dhcp_server.c ../common/engine.c ../common/artnet.c ../common/ctrl.c \
//                    ../common/presets.c ../common/rdm.c ../sim/rdm_lights.c -o test_net && ./test_net
#include <stdio.h>
#include <string.h>

#include "artnet.h"
#include "config.h"
#include "ctrl.h"
#include "dhcp_server.h"
#include "engine.h"
#include "fake_pico.h"
#include "net.h"
#include "platform.h"
#include "tusb.h"
#include "device/usbd_pvt.h"
#include "usb_descriptors.h"
#include "usb_net.h"

// The box being tested has the number 7 on its switch (fake_reset() sets it):
// it is 10.1.7.1, the computer gets 10.1.7.2, and both Ethernet addresses carry
// the number. use_box() switches to another number.
#define BOX_N 7
static uint8_t BOX_MAC[6], PC_MAC[6], BOX_IP[4], PC_IP[4];   // the computer's end of the link: PC_

static void use_box(uint8_t n) {
    const uint8_t box_mac[6] = {0x02, 0x00, NET_FIRST, NET_SECOND, n, 0x01};
    const uint8_t pc_mac[6]  = {0x02, 0x00, NET_FIRST, NET_SECOND, n, 0x02};
    const uint8_t box_ip[4]  = {NET_FIRST, NET_SECOND, n, 1};
    const uint8_t pc_ip[4]   = {NET_FIRST, NET_SECOND, n, 2};
    memcpy(BOX_MAC, box_mac, 6);
    memcpy(PC_MAC, pc_mac, 6);
    memcpy(BOX_IP, box_ip, 4);
    memcpy(PC_IP, pc_ip, 4);
}
static const uint8_t NO_IP[4]   = {0, 0, 0, 0};
static const uint8_t ALL_IP[4]  = {255, 255, 255, 255};
static const uint8_t BCAST[6]   = {0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF};

static int failures, checks;
static const char *current = "";

#define CHECK(cond) do { checks++; if (!(cond)) { failures++; \
    printf("  FAIL %s (line %d): %s\n", current, __LINE__, #cond); } } while (0)
#define TEST(name) do { current = name; printf("- %s\n", name); } while (0)

// ================================================================ building packets (independent of net.c)
// Checksum written differently from net.c on purpose: bytes at even and odd offsets are added separately.
static uint16_t checksum(const uint8_t *a, int alen, const uint8_t *b, int blen) {
    uint32_t hi = 0, lo = 0;
    for (int i = 0; i < alen; i++) { if (i & 1) lo += a[i]; else hi += a[i]; }
    for (int i = 0; i < blen; i++) { if (i & 1) lo += b[i]; else hi += b[i]; }
    uint32_t sum = hi * 256 + lo;
    sum = (sum & 0xFFFF) + (sum >> 16);
    sum = (sum & 0xFFFF) + (sum >> 16);
    return (uint16_t)(~sum & 0xFFFF);
}

static uint8_t pk[1700];                    // the packet being built
static const uint8_t *src_ip = PC_IP;       // sender address the builders put in

static int eth(const uint8_t *dst, const uint8_t *src, uint16_t type) {
    memcpy(pk, dst, 6);
    memcpy(pk + 6, src, 6);
    pk[12] = (uint8_t)(type >> 8);
    pk[13] = (uint8_t)type;
    return 14;
}

static int arp_request(const uint8_t *target_ip) {
    int n = eth(BCAST, PC_MAC, 0x0806);
    static const uint8_t fixed[8] = {0, 1, 8, 0, 6, 4, 0, 1};
    memcpy(pk + n, fixed, 8);
    memcpy(pk + n + 8, PC_MAC, 6);
    memcpy(pk + n + 14, PC_IP, 4);
    memset(pk + n + 18, 0, 6);
    memcpy(pk + n + 24, target_ip, 4);
    return n + 28;
}

// IPv4 packet from the computer; `payload` is the ICMP or UDP part, already complete.
static int ipv4(const uint8_t *dst_mac, const uint8_t *dst_ip, uint8_t proto, const uint8_t *payload, int plen,
                uint16_t frag_field) {
    int n = eth(dst_mac, PC_MAC, 0x0800);
    uint8_t *ip = pk + n;
    memset(ip, 0, 20);
    ip[0] = 0x45;
    ip[2] = (uint8_t)((20 + plen) >> 8);
    ip[3] = (uint8_t)(20 + plen);
    ip[4] = 0x12; ip[5] = 0x34;
    ip[6] = (uint8_t)(frag_field >> 8);
    ip[7] = (uint8_t)frag_field;
    ip[8] = 128;
    ip[9] = proto;
    memcpy(ip + 12, src_ip, 4);
    memcpy(ip + 16, dst_ip, 4);
    uint16_t c = checksum(ip, 20, NULL, 0);
    ip[10] = (uint8_t)(c >> 8);
    ip[11] = (uint8_t)c;
    memcpy(ip + 20, payload, (size_t)plen);
    return n + 20 + plen;
}

static int udp(const uint8_t *dst_mac, const uint8_t *dst_ip, uint16_t sport, uint16_t dport, const uint8_t *data,
               int dlen, bool with_checksum) {
    static uint8_t u[1500];
    int ulen = 8 + dlen;
    u[0] = (uint8_t)(sport >> 8); u[1] = (uint8_t)sport;
    u[2] = (uint8_t)(dport >> 8); u[3] = (uint8_t)dport;
    u[4] = (uint8_t)(ulen >> 8);  u[5] = (uint8_t)ulen;
    u[6] = u[7] = 0;
    memcpy(u + 8, data, (size_t)dlen);
    if (with_checksum) {
        uint8_t pseudo[12];
        memcpy(pseudo, src_ip, 4);
        memcpy(pseudo + 4, dst_ip, 4);
        pseudo[8] = 0; pseudo[9] = 17; pseudo[10] = u[4]; pseudo[11] = u[5];
        uint16_t c = checksum(pseudo, 12, u, ulen);
        if (c == 0) c = 0xFFFF;
        u[6] = (uint8_t)(c >> 8); u[7] = (uint8_t)c;
    }
    return ipv4(dst_mac, dst_ip, 17, u, ulen, 0);
}

static int ping_request(int size) {
    uint8_t icmp[80] = {8, 0, 0, 0, 0x00, 0x01, 0x00, 0x2A};
    for (int i = 8; i < 8 + size; i++) icmp[i] = (uint8_t)('a' + i % 23);
    uint16_t c = checksum(icmp, 8 + size, NULL, 0);
    icmp[2] = (uint8_t)(c >> 8); icmp[3] = (uint8_t)c;
    return ipv4(BOX_MAC, BOX_IP, 1, icmp, 8 + size, 0);
}

static int dx_request(uint8_t *out, uint8_t type, uint16_t seq, const uint8_t *payload, int plen) {
    static const uint8_t head[8] = {'D', 'X', 1, 0, 0xCA, 0xFE, 0xBA, 0xBE};
    memcpy(out, head, 8);
    out[3] = type;
    out[8] = (uint8_t)(seq >> 8); out[9] = (uint8_t)seq;
    out[10] = (uint8_t)(plen >> 8); out[11] = (uint8_t)plen;
    if (plen) memcpy(out + 12, payload, (size_t)plen);
    return 12 + plen;
}

// A control request to the box, as a complete frame in pk.
static int dx_frame(uint8_t type, uint16_t seq, const uint8_t *payload, int plen) {
    uint8_t req[600];
    int n = dx_request(req, type, seq, payload, plen);
    return udp(BOX_MAC, BOX_IP, 50000, CTRL_PORT, req, n, true);
}

// An ArtDmx packet with `count` channel values for a universe, as a complete frame in pk.
static int artdmx_frame_to(uint16_t universe, uint8_t seq, const uint8_t *values, int count) {
    uint8_t a[18 + 512] = {'A', 'r', 't', '-', 'N', 'e', 't', 0, 0x00, 0x50, 0, 14};
    a[12] = seq;
    a[14] = (uint8_t)universe; a[15] = (uint8_t)(universe >> 8);
    a[16] = (uint8_t)(count >> 8); a[17] = (uint8_t)count;
    memcpy(a + 18, values, (size_t)count);
    return udp(BOX_MAC, BOX_IP, 6454, ARTNET_PORT, a, 18 + count, true);
}

// The same for the box's own universe (box n listens to ARTNET_UNIVERSE + n).
static int artdmx_frame(uint8_t seq, const uint8_t *values, int count) {
    return artdmx_frame_to((uint16_t)(ARTNET_UNIVERSE + BOX_N), seq, values, count);
}

static const uint8_t ARTPOLL[14] = {'A', 'r', 't', '-', 'N', 'e', 't', 0, 0x00, 0x20, 0, 14, 0, 0};

// Is `f` a correct IPv4/UDP frame from the box to (dst_mac, dst_ip)? Returns the UDP payload (or NULL).
static const uint8_t *check_udp_reply(const uint8_t *f, int len, const uint8_t *dst_mac, const uint8_t *dst_ip,
                                      uint16_t box_port, uint16_t pc_port, int *dlen) {
    if (len < 42) return NULL;
    const uint8_t *ip = f + 14, *u = f + 34;
    int total = (ip[2] << 8) | ip[3], ulen = (u[4] << 8) | u[5];
    uint8_t pseudo[12];
    memcpy(pseudo, ip + 12, 8);
    pseudo[8] = 0; pseudo[9] = 17; pseudo[10] = u[4]; pseudo[11] = u[5];
    bool ok = memcmp(f, dst_mac, 6) == 0 && memcmp(f + 6, BOX_MAC, 6) == 0 && f[12] == 0x08 && f[13] == 0x00 &&
              ip[0] == 0x45 && ip[9] == 17 && total == len - 14 && ulen == total - 20 &&
              memcmp(ip + 12, BOX_IP, 4) == 0 && memcmp(ip + 16, dst_ip, 4) == 0 &&
              checksum(ip, 20, NULL, 0) == 0 && checksum(pseudo, 12, u, ulen) == 0 &&
              ((u[0] << 8) | u[1]) == box_port && ((u[2] << 8) | u[3]) == pc_port;
    if (!ok) return NULL;
    *dlen = ulen - 8;
    return u + 8;
}

static bool is_arp_reply(const uint8_t *f, int len) {
    return len == 42 && memcmp(f, PC_MAC, 6) == 0 && memcmp(f + 6, BOX_MAC, 6) == 0 && f[12] == 0x08 && f[13] == 0x06 &&
           f[20] == 0 && f[21] == 2 && memcmp(f + 22, BOX_MAC, 6) == 0 && memcmp(f + 28, BOX_IP, 4) == 0 &&
           memcmp(f + 32, PC_MAC, 6) == 0 && memcmp(f + 38, PC_IP, 4) == 0;
}

static bool is_ping_reply(const uint8_t *f, int len, int size) {
    return len == 14 + 20 + 8 + size && memcmp(f, PC_MAC, 6) == 0 && checksum(f + 14, 20, NULL, 0) == 0 && f[23] == 1 &&
           memcmp(f + 26, BOX_IP, 4) == 0 && memcmp(f + 30, PC_IP, 4) == 0 && f[34] == 0 && f[35] == 0 &&
           checksum(f + 34, 8 + size, NULL, 0) == 0 && f[38] == 0x00 && f[39] == 0x01 && f[41] == 0x2A;
}

// ---------------------------------------------------------------- DHCP messages
enum { D_DISCOVER = 1, D_OFFER = 2, D_REQUEST = 3, D_DECLINE = 4, D_ACK = 5, D_NAK = 6, D_RELEASE = 7, D_INFORM = 8 };

// A DHCP message as a computer sends it. `ciaddr`, `wanted` and `server` may be NULL.
static int dhcp_message(uint8_t *m, uint8_t type, uint32_t xid, bool broadcast_flag, const uint8_t *ciaddr,
                        const uint8_t *wanted, const uint8_t *server) {
    static const uint8_t cookie[4] = {0x63, 0x82, 0x53, 0x63};
    memset(m, 0, 300);
    m[0] = 1; m[1] = 1; m[2] = 6;
    m[4] = (uint8_t)(xid >> 24); m[5] = (uint8_t)(xid >> 16); m[6] = (uint8_t)(xid >> 8); m[7] = (uint8_t)xid;
    if (broadcast_flag) m[10] = 0x80;
    if (ciaddr) memcpy(m + 12, ciaddr, 4);
    memcpy(m + 28, PC_MAC, 6);
    memcpy(m + 236, cookie, 4);
    int o = 240;
    m[o++] = 0;                                             // a padding byte before the first option is allowed
    m[o++] = 53; m[o++] = 1; m[o++] = type;
    m[o++] = 61; m[o++] = 7; m[o++] = 1;                    // client identifier, as Windows sends it
    memcpy(m + o, PC_MAC, 6); o += 6;
    if (wanted) { m[o++] = 50; m[o++] = 4; memcpy(m + o, wanted, 4); o += 4; }
    if (server) { m[o++] = 54; m[o++] = 4; memcpy(m + o, server, 4); o += 4; }
    m[o++] = 12; m[o++] = 6; memcpy(m + o, "laptop", 6); o += 6;   // host name
    m[o++] = 55; m[o++] = 5; m[o++] = 1; m[o++] = 3; m[o++] = 6; m[o++] = 15; m[o++] = 121;   // "please send me ..."
    m[o++] = 255;
    return 300;
}

typedef struct {
    bool    ok;                                             // well-formed answer to our question
    uint8_t type;
    uint8_t ciaddr[4], yiaddr[4], server[4], mask[4];
    bool    has_server, has_mask, has_lease, has_router, has_dns;
    uint32_t lease;
    uint8_t flags_hi;
} dhcp_reply_t;

static dhcp_reply_t dhcp_parse(const uint8_t *d, int dlen, uint32_t xid) {
    static const uint8_t cookie[4] = {0x63, 0x82, 0x53, 0x63};
    dhcp_reply_t r;
    memset(&r, 0, sizeof r);
    if (!d || dlen != DHCP_REPLY_LEN) return r;
    uint32_t got_xid = ((uint32_t)d[4] << 24) | ((uint32_t)d[5] << 16) | ((uint32_t)d[6] << 8) | d[7];
    static const uint8_t zeros[10] = {0};
    bool head = d[0] == 2 && d[1] == 1 && d[2] == 6 && d[3] == 0 && got_xid == xid &&
                memcmp(d + 28, PC_MAC, 6) == 0 && memcmp(d + 34, zeros, 10) == 0 && memcmp(d + 236, cookie, 4) == 0;
    if (!head) return r;
    r.flags_hi = d[10];
    memcpy(r.ciaddr, d + 12, 4);
    memcpy(r.yiaddr, d + 16, 4);
    bool ended = false;
    int o = 240;
    while (o < dlen) {
        uint8_t code = d[o];
        if (code == 255) { ended = true; break; }
        if (code == 0) { o++; continue; }
        if (o + 1 >= dlen || o + 2 + d[o + 1] > dlen) return r;
        const uint8_t *v = d + o + 2;
        int l = d[o + 1];
        if (code == 53 && l == 1) r.type = v[0];
        if (code == 54 && l == 4) { r.has_server = true; memcpy(r.server, v, 4); }
        if (code == 1 && l == 4)  { r.has_mask = true; memcpy(r.mask, v, 4); }
        if (code == 51 && l == 4) { r.has_lease = true; r.lease = ((uint32_t)v[0] << 24) | ((uint32_t)v[1] << 16) | ((uint32_t)v[2] << 8) | v[3]; }
        if (code == 3) r.has_router = true;
        if (code == 6) r.has_dns = true;
        o += 2 + l;
    }
    r.ok = ended && r.type != 0;
    return r;
}

static bool is_offer_or_ack(const dhcp_reply_t *r, uint8_t type) {
    static const uint8_t mask[4] = {255, 255, 255, 0};
    return r->ok && r->type == type && memcmp(r->yiaddr, PC_IP, 4) == 0 && r->has_server &&
           memcmp(r->server, BOX_IP, 4) == 0 && r->has_mask && memcmp(r->mask, mask, 4) == 0 &&
           r->has_lease && r->lease == NET_LEASE_S && !r->has_router && !r->has_dns;
}

// ================================================================ 1. network stack, fed directly
static engine_t      t_engine;
static preset_bank_t t_bank;
static ctrl_t        t_ctrl;
static artnet_node_t t_artnet;
static uint8_t       t_reply[CTRL_MAX_PKT];
static int           t_udp_seen;
static uint32_t      t_now = 1000;

static const net_peer_t EVERYBODY = {{0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF}, {255, 255, 255, 255}, DHCP_CLIENT_PORT};

// The same hand-over as in main.c (section 5 runs the one in main.c itself).
static void on_udp(uint16_t port, const net_peer_t *from, const uint8_t *data, int len) {
    const net_peer_t *to = from;
    int reply_len, room = (int)sizeof t_reply;
    t_udp_seen++;
    if (port == ARTNET_PORT)    reply_len = artnet_handle(&t_artnet, data, len, t_now, t_reply, room);
    else if (port == CTRL_PORT) reply_len = ctrl_handle(&t_ctrl, data, len, t_now, t_reply, room);
    else if (port == DHCP_SERVER_PORT) { reply_len = dhcp_server_handle(data, len, t_reply, room); to = &EVERYBODY; }
    else return;
    if (reply_len > 0) net_udp_send(to, port, t_reply, reply_len);
}

static struct { uint8_t data[1600]; int len; } sent[16];    // what the stack sent
static int n_sent;

static bool capture_send(const uint8_t *frame, int len) {
    if (n_sent < 16 && len <= 1600) {
        memcpy(sent[n_sent].data, frame, (size_t)len);
        sent[n_sent].len = len;
    }
    n_sent++;
    return true;
}

static void start_box(void) {
    net_config_t cfg;
    memcpy(cfg.mac, BOX_MAC, 6);
    memcpy(cfg.ip, BOX_IP, 4);
    cfg.netmask[0] = cfg.netmask[1] = cfg.netmask[2] = 255;
    cfg.netmask[3] = 0;
    engine_init(&t_engine);
    presets_clear(&t_bank);
    ctrl_init(&t_ctrl, &t_engine, &t_bank, t_now);
    t_artnet = (artnet_node_t){.eng = &t_engine, .universe = ARTNET_UNIVERSE + BOX_N};
    memcpy(t_artnet.ip, BOX_IP, 4);
    memcpy(t_artnet.mac, BOX_MAC, 6);
    dhcp_config_t dhcp = {.server_ip = {NET_FIRST, NET_SECOND, BOX_N, 1}, .client_ip = {NET_FIRST, NET_SECOND, BOX_N, 2},
                          .netmask = NET_NETMASK, .lease_s = NET_LEASE_S};
    dhcp_server_init(&dhcp);
    net_init(&cfg, capture_send, on_udp);
    t_udp_seen = 0;
    n_sent = 0;
}

// Send one UDP datagram to the box and return its single answer (or NULL).
static const uint8_t *ask(uint16_t port, const uint8_t *data, int dlen, int *rlen) {
    n_sent = 0;
    net_input(pk, udp(BOX_MAC, BOX_IP, 50000, port, data, dlen, true));
    if (n_sent != 1) return NULL;
    return check_udp_reply(sent[0].data, sent[0].len, PC_MAC, PC_IP, port, 50000, rlen);
}

static void test_stack(void) {
    int len, dlen;
    const uint8_t *f, *d;
    uint8_t req[600];

    TEST("ARP request for the box's address is answered, others are ignored");
    start_box();
    net_input(pk, arp_request(BOX_IP));
    CHECK(n_sent == 1 && is_arp_reply(sent[0].data, sent[0].len));
    n_sent = 0;
    uint8_t elsewhere[4] = {NET_FIRST, NET_SECOND, BOX_N, 77};
    net_input(pk, arp_request(elsewhere));
    net_input(pk, arp_request(PC_IP));                                   // the computer checking its own address
    CHECK(n_sent == 0);

    TEST("ping is answered with correct checksums");
    for (int size = 32; size <= 33; size++) {                            // even and odd payload
        n_sent = 0;
        len = ping_request(size);
        uint8_t copy[80];
        memcpy(copy, pk + 34, (size_t)(8 + size));
        net_input(pk, len);
        CHECK(n_sent == 1 && is_ping_reply(sent[0].data, sent[0].len, size));
        CHECK(memcmp(sent[0].data + 38, copy + 4, (size_t)(4 + size)) == 0);   // identifier, sequence, data
    }

    TEST("control protocol over UDP: ping, set, read back");
    d = ask(CTRL_PORT, req, dx_request(req, 0x01, 1, NULL, 0), &dlen);
    CHECK(d && dlen == 29 && d[3] == 0x80 && d[12] == 0 && d[13] == FW_VERSION_MAJOR && d[16] == 255);
    CHECK(d && d[23] == (DMX_CHANNELS >> 8) && d[24] == (DMX_CHANNELS & 0xFF) && d[25] == (5000 >> 8) && d[26] == (5000 & 0xFF));
    CHECK(d && d[8] == 0 && d[9] == 1 && memcmp(d + 4, req + 4, 4) == 0);       // same session and sequence
    uint8_t set[7] = {0, 1, 0, 3, 255, 128, 7};
    d = ask(CTRL_PORT, req, dx_request(req, 0x10, 2, set, 7), &dlen);
    CHECK(d && dlen == 13 && d[12] == 0);
    uint8_t get[4] = {0, 1, 2, 0};                                       // channels 1..512
    d = ask(CTRL_PORT, req, dx_request(req, 0x40, 3, get, 4), &dlen);
    CHECK(d && dlen == 12 + 1 + 512 && d[13] == 255 && d[14] == 128 && d[15] == 7 && d[16] == 0);

    TEST("bad packets are ignored: wrong checksum, fragment, another address, unknown port");
    int before = t_udp_seen;
    n_sent = 0;
    len = udp(BOX_MAC, BOX_IP, 50000, CTRL_PORT, req, dx_request(req, 0x01, 9, NULL, 0), true);
    pk[len - 1] ^= 0x01;                                                 // damage the data
    net_input(pk, len);
    len = udp(BOX_MAC, BOX_IP, 50000, CTRL_PORT, req, 12, true);
    pk[14 + 10] ^= 0x40;                                                 // damage the IP header checksum
    net_input(pk, len);
    static uint8_t u8[20] = {0xC3, 0x50, 0x1B, 0x58, 0, 20};
    net_input(pk, ipv4(BOX_MAC, BOX_IP, 17, u8, 20, 0x2000));            // "more fragments" set
    net_input(pk, udp(BOX_MAC, elsewhere, 50000, CTRL_PORT, req, 12, true));
    net_input(pk, 10);                                                   // not even a whole Ethernet header
    CHECK(t_udp_seen == before && n_sent == 0);
    net_input(pk, udp(BOX_MAC, BOX_IP, 50000, 9999, req, 12, true));     // nobody listens on 9999
    CHECK(t_udp_seen == before + 1 && n_sent == 0);
    CHECK(net_stats()->dropped == 3);

    TEST("a datagram without UDP checksum is accepted");
    n_sent = 0;
    net_input(pk, udp(BOX_MAC, BOX_IP, 50000, CTRL_PORT, req, dx_request(req, 0x01, 10, NULL, 0), false));
    CHECK(n_sent == 1);

    TEST("Art-Net: broadcast ArtPoll is answered to the sender, ArtDmx reaches the engine");
    uint8_t subnet[4] = {NET_FIRST, NET_SECOND, BOX_N, 255}, foreign[4] = {2, 255, 255, 255};
    const uint8_t *targets[2] = {ALL_IP, subnet};
    for (int t = 0; t < 2; t++) {
        n_sent = 0;
        net_input(pk, udp(BCAST, targets[t], 6454, ARTNET_PORT, ARTPOLL, 14, true));
        CHECK(n_sent == 1);
        d = check_udp_reply(sent[0].data, sent[0].len, PC_MAC, PC_IP, ARTNET_PORT, 6454, &dlen);
        CHECK(d && dlen == ARTNET_REPLY_LEN && memcmp(d, "Art-Net", 8) == 0 && d[8] == 0x00 && d[9] == 0x21);
        CHECK(d && memcmp(d + 10, BOX_IP, 4) == 0 && memcmp(d + 201, BOX_MAC, 6) == 0);
        CHECK(d && strcmp((const char *)d + 26, NODE_SHORT_NAME) == 0);
    }
    n_sent = 0;
    net_input(pk, udp(BCAST, foreign, 6454, ARTNET_PORT, ARTPOLL, 14, true));   // not our subnet
    CHECK(n_sent == 0);
    uint8_t values[4] = {9, 200, 0, 77};
    net_input(pk, artdmx_frame(1, values, 4));
    uint8_t get4[4] = {0, 1, 0, 4};
    d = ask(CTRL_PORT, req, dx_request(req, 0x40, 11, get4, 4), &dlen);
    CHECK(d && dlen == 17 && d[13] == 255 && d[14] == 200 && d[15] == 7 && d[16] == 77);   // highest of cue and Art-Net

    TEST("several packets in a row are all handled, in order");
    n_sent = 0;
    for (int i = 0; i < 5; i++) {
        uint8_t one[5] = {0, (uint8_t)(20 + i), 0, 1, (uint8_t)(100 + i)};
        net_input(pk, udp(BOX_MAC, BOX_IP, 50000, CTRL_PORT, req, dx_request(req, 0x10, (uint16_t)(20 + i), one, 5), true));
    }
    CHECK(n_sent == 5);
    uint8_t get5[4] = {0, 20, 0, 5};
    d = ask(CTRL_PORT, req, dx_request(req, 0x40, 30, get5, 4), &dlen);
    CHECK(d && dlen == 18 && d[13] == 100 && d[17] == 104);

    TEST("the announcement is a broadcast ARP for the box's own address");
    n_sent = 0;
    net_announce();
    CHECK(n_sent == 1);
    f = sent[0].data;
    CHECK(sent[0].len == 42 && memcmp(f, BCAST, 6) == 0 && f[21] == 1 && memcmp(f + 28, BOX_IP, 4) == 0 &&
          memcmp(f + 38, BOX_IP, 4) == 0);
}

// ================================================================ 2. address hand-out (DHCP)
// Send a DHCP message through the stack and return the answer's payload (or NULL).
// `renewing`: sent from the computer's own address straight to the box, as when a lease is extended.
static const uint8_t *dhcp_ask(const uint8_t *msg, bool renewing, int *dlen) {
    n_sent = 0;
    if (renewing) {
        net_input(pk, udp(BOX_MAC, BOX_IP, DHCP_CLIENT_PORT, DHCP_SERVER_PORT, msg, 300, true));
    } else {
        src_ip = NO_IP;                                      // the computer has no address yet
        net_input(pk, udp(BCAST, ALL_IP, DHCP_CLIENT_PORT, DHCP_SERVER_PORT, msg, 300, true));
        src_ip = PC_IP;
    }
    if (n_sent != 1) return NULL;
    // every answer is a broadcast from the box, port 67 to port 68
    return check_udp_reply(sent[0].data, sent[0].len, BCAST, ALL_IP, DHCP_SERVER_PORT, DHCP_CLIENT_PORT, dlen);
}

// The same, with the answer taken apart.
static dhcp_reply_t dhcp_exchange(const uint8_t *msg, bool renewing, uint32_t xid) {
    int dlen = 0;
    const uint8_t *d = dhcp_ask(msg, renewing, &dlen);
    return dhcp_parse(d, dlen, xid);
}

static void test_dhcp(void) {
    uint8_t m[400], out[400];
    int dlen = 0;
    dhcp_reply_t r;
    static const uint8_t other_ip[4] = {NET_FIRST, NET_SECOND, BOX_N, 9};
    static const uint8_t other_server[4] = {NET_FIRST, NET_SECOND, BOX_N, 254};
    static const uint8_t old_ip[4] = {192, 168, 1, 10};

    TEST("DHCP: a computer without an address is offered 10.1.7.2");
    start_box();
    dhcp_message(m, D_DISCOVER, 0x11110001, false, NULL, NULL, NULL);
    r = dhcp_exchange(m, false, 0x11110001);
    CHECK(is_offer_or_ack(&r, D_OFFER));
    CHECK(memcmp(r.ciaddr, NO_IP, 4) == 0 && r.flags_hi == 0);

    TEST("DHCP: asking for that address is confirmed");
    dhcp_message(m, D_REQUEST, 0x11110002, true, NULL, PC_IP, BOX_IP);       // after an offer, broadcast flag set
    r = dhcp_exchange(m, false, 0x11110002);
    CHECK(is_offer_or_ack(&r, D_ACK) && r.flags_hi == 0x80);                 // the flag is repeated
    dhcp_message(m, D_REQUEST, 0x11110003, false, NULL, PC_IP, NULL);        // "I had this address last time"
    r = dhcp_exchange(m, false, 0x11110003);
    CHECK(is_offer_or_ack(&r, D_ACK));
    dhcp_message(m, D_REQUEST, 0x11110004, false, PC_IP, NULL, NULL);        // extending the lease
    r = dhcp_exchange(m, true, 0x11110004);
    CHECK(is_offer_or_ack(&r, D_ACK) && memcmp(r.ciaddr, PC_IP, 4) == 0);

    TEST("DHCP: asking for any other address is refused");
    dhcp_message(m, D_REQUEST, 0x11110005, false, NULL, other_ip, BOX_IP);
    r = dhcp_exchange(m, false, 0x11110005);
    CHECK(r.ok && r.type == D_NAK && memcmp(r.yiaddr, NO_IP, 4) == 0 && r.has_server && !r.has_lease && !r.has_mask);
    dhcp_message(m, D_REQUEST, 0x11110006, false, NULL, old_ip, NULL);       // an address from another network
    r = dhcp_exchange(m, false, 0x11110006);
    CHECK(r.ok && r.type == D_NAK);
    dhcp_message(m, D_REQUEST, 0x11110007, false, NULL, NULL, NULL);         // no address named at all
    r = dhcp_exchange(m, false, 0x11110007);
    CHECK(r.ok && r.type == D_NAK);

    TEST("DHCP: a request meant for another server gets no answer");
    dhcp_message(m, D_REQUEST, 0x11110008, false, NULL, PC_IP, other_server);
    CHECK(dhcp_ask(m, false, &dlen) == NULL && n_sent == 0);

    TEST("DHCP: a computer with a fixed address gets the settings without a lease");
    dhcp_message(m, D_INFORM, 0x11110009, false, PC_IP, NULL, NULL);
    r = dhcp_exchange(m, true, 0x11110009);
    CHECK(r.ok && r.type == D_ACK && memcmp(r.yiaddr, NO_IP, 4) == 0 && r.has_mask && !r.has_lease && !r.has_router);

    TEST("DHCP: messages that need no answer, and damaged ones, are ignored");
    dhcp_message(m, D_RELEASE, 0x1111000A, false, PC_IP, NULL, BOX_IP);
    CHECK(dhcp_server_handle(m, 300, out, (int)sizeof out) == 0);
    dhcp_message(m, D_DECLINE, 0x1111000B, false, NULL, PC_IP, BOX_IP);
    CHECK(dhcp_server_handle(m, 300, out, (int)sizeof out) == 0);
    dhcp_message(m, D_OFFER, 0x1111000C, false, NULL, NULL, NULL);           // something only a server would send
    CHECK(dhcp_server_handle(m, 300, out, (int)sizeof out) == 0);
    dhcp_message(m, D_DISCOVER, 0x1111000D, false, NULL, NULL, NULL);
    CHECK(dhcp_server_handle(m, 300, out, (int)sizeof out) == DHCP_REPLY_LEN);    // (this one is fine)
    CHECK(dhcp_server_handle(m, 300, out, DHCP_REPLY_LEN - 1) == 0);         // no room for the answer
    CHECK(dhcp_server_handle(m, 239, out, (int)sizeof out) == 0);                 // cut off before the options
    CHECK(dhcp_server_handle(m, 243, out, (int)sizeof out) == 0);                 // cut off inside the first option
    m[0] = 2;
    CHECK(dhcp_server_handle(m, 300, out, (int)sizeof out) == 0);                 // an answer, not a question
    m[0] = 1; m[236] ^= 0xFF;
    CHECK(dhcp_server_handle(m, 300, out, (int)sizeof out) == 0);                 // the marker before the options is wrong
    m[236] ^= 0xFF; m[2] = 16;
    CHECK(dhcp_server_handle(m, 300, out, (int)sizeof out) == 0);                 // not an Ethernet address
    m[2] = 6; m[242] = 200;
    CHECK(dhcp_server_handle(m, 300, out, (int)sizeof out) == 0);                 // an option longer than the message
    memset(m + 240, 0, 60);
    CHECK(dhcp_server_handle(m, 300, out, (int)sizeof out) == 0);                 // no message type at all
}

// ================================================================ 3. USB adapter code against the model of TinyUSB
static uint8_t got[64][1600];               // frames handed to the firmware
static int     got_len[64], n_got;
static bool    echo;                        // answer each frame with a copy, as the stack answers requests

static void record_frame(const uint8_t *frame, int len) {
    if (n_got < 64 && len <= 1600) {
        memcpy(got[n_got], frame, (size_t)len);
        got_len[n_got] = len;
    }
    n_got++;
    if (echo) usb_net_send(frame, len);
}

static void pattern(uint8_t *f, int len, int seed) {
    for (int i = 0; i < len; i++) f[i] = (uint8_t)(i * 7 + seed);
}

static void poll(int times) {
    for (int i = 0; i < times; i++) {
        usb_net_poll();
        fake_time_set(fake_now() + 1);
    }
}

static bool host_got(int index, int len, int seed) {     // did the computer receive this pattern as frame `index`?
    static uint8_t want[1600];
    int flen;
    if (index >= fake_host_count()) return false;
    const uint8_t *f = fake_host_frame(index, &flen);
    pattern(want, len, seed);
    return flen == len && memcmp(f, want, (size_t)len) == 0;
}

static bool box_got(int index, int len, int seed) {
    static uint8_t want[1600];
    pattern(want, len, seed);
    return index < n_got && got_len[index] == len && memcmp(got[index], want, (size_t)len) == 0;
}

static void test_usb(void) {
    static uint8_t f[1700];
    static const int sizes[5] = {60, 1514, 61, 590, 14};

    TEST("USB: without a computer nothing is sent and nothing breaks");
    fake_reset(false);
    n_got = 0;
    echo = false;
    usb_net_init(PC_MAC, "TEST", record_frame);
    CHECK(fake_usb_starts() == 1);
    pattern(f, 60, 1);
    poll(3);
    CHECK(!usb_net_connected() && !usb_net_send(f, 60));
    poll(3);
    CHECK(fake_host_count() == 0 && n_got == 0);

    TEST("USB: frames from the computer arrive intact, also several in one transfer");
    fake_at(fake_now());
    fake_connect();
    poll(1);
    CHECK(usb_net_connected());
    fake_at(fake_now());
    pattern(f, 60, 2);
    fake_frame(f, 60);
    poll(1);
    CHECK(n_got == 1 && box_got(0, 60, 2));
    fake_at(fake_now());
    fake_block_begin();
    for (int i = 0; i < 5; i++) { pattern(f, sizes[i], 10 + i); fake_frame(f, sizes[i]); }
    fake_block_end();
    poll(1);                                                            // one round of the main loop takes all five
    CHECK(n_got == 6);
    for (int i = 0; i < 5; i++) CHECK(box_got(1 + i, sizes[i], 10 + i));
    CHECK(fake_host_waiting() == 0);

    TEST("USB: a flood is taken in portions, nothing is lost");
    n_got = 0;
    fake_at(fake_now());
    fake_block_begin();
    for (int i = 0; i < 20; i++) { pattern(f, 100 + i, 30 + i); fake_frame(f, 100 + i); }
    fake_block_end();
    fake_block_begin();
    for (int i = 20; i < 26; i++) { pattern(f, 100 + i, 30 + i); fake_frame(f, 100 + i); }
    fake_block_end();
    poll(1);
    CHECK(n_got == 12);                                                 // the buttons and the LED get their turn
    poll(1);
    CHECK(n_got == 20);
    poll(2);
    CHECK(n_got == 26 && fake_host_waiting() == 0);
    bool all = true;
    for (int i = 0; i < 26; i++) all = all && box_got(i, 100 + i, 30 + i);
    CHECK(all);

    TEST("USB: a frame that is too long is skipped, the next one still arrives");
    n_got = 0;
    fake_at(fake_now());
    fake_block_begin();
    pattern(f, 1600, 3); fake_frame(f, 1600);
    pattern(f, 70, 4);   fake_frame(f, 70);
    pattern(f, 1515, 5); fake_frame(f, 1515);
    fake_block_end();
    pattern(f, 80, 6);   fake_frame(f, 80);
    poll(4);
    CHECK(n_got == 2 && box_got(0, 70, 4) && box_got(1, 80, 6) && fake_host_waiting() == 0);

    TEST("USB: frames to the computer arrive intact and in order");
    fake_host_clear();
    for (int i = 0; i < 5; i++) {
        pattern(f, sizes[i], 50 + i);
        CHECK(usb_net_send(f, sizes[i]));
    }
    poll(4);
    CHECK(fake_host_count() == 5);
    for (int i = 0; i < 5; i++) CHECK(host_got(i, sizes[i], 50 + i));
    pattern(f, 1515, 7);
    CHECK(!usb_net_send(f, 1515) && !usb_net_send(f, 13));              // too long, too short
    poll(2);
    CHECK(fake_host_count() == 5);

    TEST("USB: a computer that stops fetching makes sending fail cleanly, and recover");
    fake_host_clear();
    fake_at(fake_now());
    fake_host_fetch(false);
    poll(1);
    int accepted = 0;
    for (int i = 0; i < 8; i++) {
        pattern(f, 1514, 60 + i);
        if (usb_net_send(f, 1514)) accepted++; else break;
    }
    CHECK(accepted == 3);                                               // one block on its way, two frames in the second block
    CHECK(fake_host_count() == 0);
    fake_at(fake_now());
    fake_host_fetch(true);
    poll(4);
    CHECK(fake_host_count() == 3 && host_got(0, 1514, 60) && host_got(1, 1514, 61) && host_got(2, 1514, 62));
    pattern(f, 200, 70);
    CHECK(usb_net_send(f, 200));
    poll(2);
    CHECK(fake_host_count() == 4 && host_got(3, 200, 70));

    TEST("USB: answering while more frames are waiting keeps everything in order");
    fake_host_clear();
    n_got = 0;
    echo = true;
    fake_at(fake_now());
    fake_block_begin();
    for (int i = 0; i < 6; i++) { pattern(f, 300 + i, 80 + i); fake_frame(f, 300 + i); }
    fake_block_end();
    poll(6);
    CHECK(n_got == 6 && fake_host_count() == 6);
    for (int i = 0; i < 6; i++) CHECK(box_got(i, 300 + i, 80 + i) && host_got(i, 300 + i, 80 + i));

    TEST("USB: answers that cannot be sent are dropped, but no incoming frame is lost or seen twice");
    fake_host_clear();
    n_got = 0;
    fake_at(fake_now());
    fake_host_fetch(false);
    fake_block_begin();
    for (int i = 0; i < 6; i++) { pattern(f, 1514, 90 + i); fake_frame(f, 1514); }
    fake_block_end();
    fake_block_begin();
    for (int i = 6; i < 8; i++) { pattern(f, 1514, 90 + i); fake_frame(f, 1514); }
    fake_block_end();
    poll(3);
    fake_at(fake_now());
    fake_host_fetch(true);
    poll(6);
    CHECK(n_got == 8 && fake_host_waiting() == 0);
    bool in_order = true;
    for (int i = 0; i < 8; i++) in_order = in_order && box_got(i, 1514, 90 + i);
    CHECK(in_order);
    CHECK(fake_host_count() >= 3 && fake_host_count() < 8);             // the first three answers fitted, some later ones did not
    CHECK(host_got(0, 1514, 90) && host_got(1, 1514, 91) && host_got(2, 1514, 92));
    echo = false;

    TEST("USB: a sleeping computer counts as not connected");
    fake_at(fake_now());
    fake_suspend(true);
    poll(1);
    pattern(f, 60, 8);
    CHECK(!usb_net_connected() && !usb_net_send(f, 60));
    fake_at(fake_now());
    fake_suspend(false);
    poll(1);
    CHECK(usb_net_connected() && usb_net_send(f, 60));
    poll(2);

    TEST("USB: unplugging in the middle of a flood and plugging in again works");
    fake_host_clear();
    n_got = 0;
    fake_at(fake_now());
    fake_block_begin();
    for (int i = 0; i < 20; i++) { pattern(f, 64, 120 + i); fake_frame(f, 64); }
    fake_block_end();
    poll(1);                                                            // twelve taken, eight still waiting
    fake_at(fake_now());
    fake_disconnect();
    poll(3);
    CHECK(!usb_net_connected() && n_got <= 13);
    fake_at(fake_now());
    fake_connect();
    pattern(f, 99, 9);
    fake_frame(f, 99);
    int before = n_got;
    poll(3);
    CHECK(usb_net_connected() && n_got == before + 1 && box_got(before, 99, 9));
    CHECK(usb_net_send(f, 99));
    poll(2);
    CHECK(fake_host_count() == 1 && host_got(0, 99, 9));

    TEST("USB: TinyUSB was only used the way it allows");
    if (fake_violations()) printf("  complaint: %s\n", fake_last_violation());
    CHECK(fake_violations() == 0);
}

// ================================================================ 4. USB descriptor tables
// The next lines are copied from TinyUSB 0.18.0 (MIT licence, Copyright (c) 2019 Ha Thach, tinyusb.org):
// src/common/tusb_common.h, src/common/tusb_types.h, src/class/cdc/cdc.h, src/class/net/net_device.h and
// src/device/usbd.h. With them the test builds the tables the way TinyUSB's own example does and compares.
#define TU_U16_HIGH(_u16)     ((uint8_t) (((_u16) >> 8) & 0x00ff))
#define TU_U16_LOW(_u16)      ((uint8_t) ((_u16)       & 0x00ff))
#define U16_TO_U8S_LE(_u16)   TU_U16_LOW(_u16), TU_U16_HIGH(_u16)
#define TU_U32_BYTE3(_u32)    ((uint8_t) ((((uint32_t) _u32) >> 24) & 0x000000ff)) // MSB
#define TU_U32_BYTE2(_u32)    ((uint8_t) ((((uint32_t) _u32) >> 16) & 0x000000ff))
#define TU_U32_BYTE1(_u32)    ((uint8_t) ((((uint32_t) _u32) >>  8) & 0x000000ff))
#define TU_U32_BYTE0(_u32)    ((uint8_t) (((uint32_t)  _u32)        & 0x000000ff)) // LSB
#define U32_TO_U8S_LE(_u32)   TU_U32_BYTE0(_u32), TU_U32_BYTE1(_u32), TU_U32_BYTE2(_u32), TU_U32_BYTE3(_u32)
#define TU_BIT(n)             (1UL << (n))
enum {
    TUSB_XFER_BULK = 2, TUSB_XFER_INTERRUPT = 3,
    TUSB_DESC_CONFIGURATION = 0x02, TUSB_DESC_INTERFACE = 0x04, TUSB_DESC_ENDPOINT = 0x05,
    TUSB_DESC_INTERFACE_ASSOCIATION = 0x0B, TUSB_DESC_BOS = 0x0F, TUSB_DESC_DEVICE_CAPABILITY = 0x10,
    TUSB_DESC_CS_INTERFACE = 0x24,
    TUSB_DESC_CS_ENDPOINT = 0x25,
    TUSB_CLASS_AUDIO = 1, TUSB_CLASS_CDC = 2, TUSB_CLASS_CDC_DATA = 10,
    AUDIO_SUBCLASS_CONTROL = 0x01, AUDIO_SUBCLASS_MIDI_STREAMING = 0x03, AUDIO_FUNC_PROTOCOL_CODE_UNDEF = 0x00,
    AUDIO_CS_AC_INTERFACE_HEADER = 0x01,
    MIDI_CS_INTERFACE_HEADER = 0x01, MIDI_CS_INTERFACE_IN_JACK = 0x02, MIDI_CS_INTERFACE_OUT_JACK = 0x03,
    MIDI_CS_ENDPOINT_GENERAL = 0x01, MIDI_JACK_EMBEDDED = 0x01, MIDI_JACK_EXTERNAL = 0x02,
    DEVICE_CAPABILITY_PLATFORM = 0x05,
    CDC_COMM_SUBCLASS_NETWORK_CONTROL_MODEL = 0x0D,
    CDC_FUNC_DESC_HEADER = 0x00, CDC_FUNC_DESC_UNION = 0x06, CDC_FUNC_DESC_ETHERNET_NETWORKING = 0x0F,
    CDC_FUNC_DESC_NCM = 0x1A,
    NCM_DATA_PROTOCOL_NETWORK_TRANSFER_BLOCK = 0x01,
};
#define TUD_BOS_DESC_LEN      5
#define TUD_BOS_DESCRIPTOR(_total_len, _caps_num) \
  5, TUSB_DESC_BOS, U16_TO_U8S_LE(_total_len), _caps_num
#define TUD_BOS_MICROSOFT_OS_DESC_LEN   28
#define TUD_BOS_MS_OS_20_UUID \
    0xDF, 0x60, 0xDD, 0xD8, 0x89, 0x45, 0xC7, 0x4C, \
  0x9C, 0xD2, 0x65, 0x9D, 0x9E, 0x64, 0x8A, 0x9F
#define TUD_CONFIG_DESC_LEN   (9)
#define TUD_CONFIG_DESCRIPTOR(config_num, _itfcount, _stridx, _total_len, _attribute, _power_ma) \
  9, TUSB_DESC_CONFIGURATION, U16_TO_U8S_LE(_total_len), _itfcount, config_num, _stridx, TU_BIT(7) | _attribute, (_power_ma)/2
#define TUD_CDC_NCM_DESC_LEN  (8+9+5+5+13+6+7+9+9+7+7)
#define TUD_CDC_NCM_DESCRIPTOR(_itfnum, _desc_stridx, _mac_stridx, _ep_notif, _ep_notif_size, _epout, _epin, _epsize, _maxsegmentsize) \
  /* Interface Association */\
  8, TUSB_DESC_INTERFACE_ASSOCIATION, _itfnum, 2, TUSB_CLASS_CDC, CDC_COMM_SUBCLASS_NETWORK_CONTROL_MODEL, 0, 0,\
  /* CDC Control Interface */\
  9, TUSB_DESC_INTERFACE, _itfnum, 0, 1, TUSB_CLASS_CDC, CDC_COMM_SUBCLASS_NETWORK_CONTROL_MODEL, 0, _desc_stridx,\
  /* CDC-NCM Header */\
  5, TUSB_DESC_CS_INTERFACE, CDC_FUNC_DESC_HEADER, U16_TO_U8S_LE(0x0110),\
  /* CDC-NCM Union */\
  5, TUSB_DESC_CS_INTERFACE, CDC_FUNC_DESC_UNION, _itfnum, (uint8_t)((_itfnum) + 1),\
  /* CDC-NCM Functional Descriptor */\
  13, TUSB_DESC_CS_INTERFACE, CDC_FUNC_DESC_ETHERNET_NETWORKING, _mac_stridx, 0, 0, 0, 0, U16_TO_U8S_LE(_maxsegmentsize), U16_TO_U8S_LE(0), 0, \
  /* CDC-NCM Functional Descriptor */\
  6, TUSB_DESC_CS_INTERFACE, CDC_FUNC_DESC_NCM, U16_TO_U8S_LE(0x0100), 0, \
  /* Endpoint Notification */\
  7, TUSB_DESC_ENDPOINT, _ep_notif, TUSB_XFER_INTERRUPT, U16_TO_U8S_LE(_ep_notif_size), 50,\
  /* CDC Data Interface (default inactive) */\
  9, TUSB_DESC_INTERFACE, (uint8_t)((_itfnum)+1), 0, 0, TUSB_CLASS_CDC_DATA, 0, NCM_DATA_PROTOCOL_NETWORK_TRANSFER_BLOCK, 0,\
  /* CDC Data Interface (alternative active) */\
  9, TUSB_DESC_INTERFACE, (uint8_t)((_itfnum)+1), 1, 2, TUSB_CLASS_CDC_DATA, 0, NCM_DATA_PROTOCOL_NETWORK_TRANSFER_BLOCK, 0,\
  /* Endpoint In */\
  7, TUSB_DESC_ENDPOINT, _epin, TUSB_XFER_BULK, U16_TO_U8S_LE(_epsize), 0,\
  /* Endpoint Out */\
  7, TUSB_DESC_ENDPOINT, _epout, TUSB_XFER_BULK, U16_TO_U8S_LE(_epsize), 0
#define TUD_MIDI_DESC_HEAD_LEN (9 + 9 + 9 + 7)
#define TUD_MIDI_DESC_HEAD(_itfnum,  _stridx, _numcables) \
  /* Audio Control (AC) Interface */\
  9, TUSB_DESC_INTERFACE, _itfnum, 0, 0, TUSB_CLASS_AUDIO, AUDIO_SUBCLASS_CONTROL, AUDIO_FUNC_PROTOCOL_CODE_UNDEF, _stridx,\
  /* AC Header */\
  9, TUSB_DESC_CS_INTERFACE, AUDIO_CS_AC_INTERFACE_HEADER, U16_TO_U8S_LE(0x0100), U16_TO_U8S_LE(0x0009), 1, (uint8_t)((_itfnum) + 1),\
  /* MIDI Streaming (MS) Interface */\
  9, TUSB_DESC_INTERFACE, (uint8_t)((_itfnum) + 1), 0, 2, TUSB_CLASS_AUDIO, AUDIO_SUBCLASS_MIDI_STREAMING, AUDIO_FUNC_PROTOCOL_CODE_UNDEF, 0,\
  /* MS Header */\
  7, TUSB_DESC_CS_INTERFACE, MIDI_CS_INTERFACE_HEADER, U16_TO_U8S_LE(0x0100), U16_TO_U8S_LE(7 + (_numcables) * TUD_MIDI_DESC_JACK_LEN + 2 * TUD_MIDI_DESC_EP_LEN(_numcables))
#define TUD_MIDI_JACKID_IN_EMB(_cablenum) \
  (uint8_t)(((_cablenum) - 1) * 4 + 1)
#define TUD_MIDI_JACKID_IN_EXT(_cablenum) \
  (uint8_t)(((_cablenum) - 1) * 4 + 2)
#define TUD_MIDI_JACKID_OUT_EMB(_cablenum) \
  (uint8_t)(((_cablenum) - 1) * 4 + 3)
#define TUD_MIDI_JACKID_OUT_EXT(_cablenum) \
  (uint8_t)(((_cablenum) - 1) * 4 + 4)
#define TUD_MIDI_DESC_JACK_LEN (6 + 6 + 9 + 9)
#define TUD_MIDI_DESC_JACK_DESC(_cablenum, _stridx) \
  /* MS In Jack (Embedded) */\
  6, TUSB_DESC_CS_INTERFACE, MIDI_CS_INTERFACE_IN_JACK, MIDI_JACK_EMBEDDED, TUD_MIDI_JACKID_IN_EMB(_cablenum), _stridx,\
  /* MS In Jack (External) */\
  6, TUSB_DESC_CS_INTERFACE, MIDI_CS_INTERFACE_IN_JACK, MIDI_JACK_EXTERNAL, TUD_MIDI_JACKID_IN_EXT(_cablenum), _stridx,\
  /* MS Out Jack (Embedded), connected to In Jack External */\
  9, TUSB_DESC_CS_INTERFACE, MIDI_CS_INTERFACE_OUT_JACK, MIDI_JACK_EMBEDDED, TUD_MIDI_JACKID_OUT_EMB(_cablenum), 1, TUD_MIDI_JACKID_IN_EXT(_cablenum), 1, _stridx,\
  /* MS Out Jack (External), connected to In Jack Embedded */\
  9, TUSB_DESC_CS_INTERFACE, MIDI_CS_INTERFACE_OUT_JACK, MIDI_JACK_EXTERNAL, TUD_MIDI_JACKID_OUT_EXT(_cablenum), 1, TUD_MIDI_JACKID_IN_EMB(_cablenum), 1, _stridx
#define TUD_MIDI_DESC_EP_LEN(_numcables) (9 + 4 + (_numcables))
#define TUD_MIDI_DESC_EP(_epout, _epsize, _numcables) \
  /* Endpoint: Note Audio v1.0's endpoint has 9 bytes instead of 7 */\
  9, TUSB_DESC_ENDPOINT, _epout, TUSB_XFER_BULK, U16_TO_U8S_LE(_epsize), 0, 0, 0, \
  /* MS Endpoint (connected to embedded jack) */\
  (uint8_t)(4 + (_numcables)), TUSB_DESC_CS_ENDPOINT, MIDI_CS_ENDPOINT_GENERAL, _numcables
#define TUD_MIDI_DESC_LEN (TUD_MIDI_DESC_HEAD_LEN + TUD_MIDI_DESC_JACK_LEN + TUD_MIDI_DESC_EP_LEN(1) * 2)
#define TUD_MIDI_DESCRIPTOR(_itfnum, _stridx, _epout, _epin, _epsize) \
  TUD_MIDI_DESC_HEAD(_itfnum, _stridx, 1),\
  TUD_MIDI_DESC_JACK_DESC(1, 0),\
  TUD_MIDI_DESC_EP(_epout, _epsize, 1),\
  TUD_MIDI_JACKID_IN_EMB(1),\
  TUD_MIDI_DESC_EP(_epin, _epsize, 1),\
  TUD_MIDI_JACKID_OUT_EMB(1)
// (end of the lines copied from TinyUSB)

// The configuration as TinyUSB's macros build it: the network adapter of its network example
// (interface 0, strings 4 and 5, endpoints 0x81 / 0x02 / 0x82 with 64 bytes, frames up to 1514
// bytes), then its simple MIDI interface (interface 2, string 6, endpoints 0x03 / 0x83).
static const uint8_t tinyusb_config[] = {
    TUD_CONFIG_DESCRIPTOR(1, 4, 0, (TUD_CONFIG_DESC_LEN + TUD_CDC_NCM_DESC_LEN + TUD_MIDI_DESC_LEN), 0, USB_MAX_POWER_MA),
    TUD_CDC_NCM_DESCRIPTOR(0, 4, 5, 0x81, 64, 0x02, 0x82, 64, 1514),
    TUD_MIDI_DESCRIPTOR(2, 6, 0x03, 0x83, 64),
};

// TinyUSB's TUD_BOS_MS_OS_20_DESCRIPTOR(46, 1). Its first byte is 4 plus the number of bytes after the fourth.
static const uint8_t tinyusb_bos[] = {
    TUD_BOS_DESCRIPTOR(TUD_BOS_DESC_LEN + TUD_BOS_MICROSOFT_OS_DESC_LEN, 1),
    4 + 24, TUSB_DESC_DEVICE_CAPABILITY, DEVICE_CAPABILITY_PLATFORM, 0x00,
    TUD_BOS_MS_OS_20_UUID, U32_TO_U8S_LE(0x06030000), U16_TO_U8S_LE(46), 1, 0,
};

static bool usb_string_is(uint8_t index, const char *want) {
    const uint16_t *s = tud_descriptor_string_cb(index, 0x0409);
    size_t n = strlen(want);
    if (!s || s[0] != (uint16_t)((3 << 8) | (2 * n + 2))) return false;
    for (size_t i = 0; i < n; i++) if (s[1 + i] != (uint16_t)(unsigned char)want[i]) return false;
    return true;
}

// numbers in USB tables: lowest byte first
static unsigned ntb_u16(const uint8_t *p) { return (unsigned)p[0] | ((unsigned)p[1] << 8); }
static unsigned long ntb_u32(const uint8_t *p) { return ntb_u16(p) | ((unsigned long)ntb_u16(p + 2) << 16); }

static void test_descriptors(void) {
    TEST("USB tables: the device says who it is");
    const uint8_t *dev = tud_descriptor_device_cb();
    CHECK(dev[0] == 18 && dev[1] == 1 && dev[2] == 0x01 && dev[3] == 0x02);          // USB 2.01
    CHECK(dev[4] == 0xEF && dev[5] == 0x02 && dev[6] == 0x01 && dev[7] == 64);
    CHECK(dev[8] == (USB_VID & 0xFF) && dev[9] == (USB_VID >> 8) && dev[10] == (USB_PID & 0xFF) && dev[11] == (USB_PID >> 8));
    CHECK(dev[14] == 1 && dev[15] == 2 && dev[16] == 3 && dev[17] == 1);

    TEST("USB tables: network adapter and MIDI are described exactly as TinyUSB's macros do it");
    const uint8_t *cfg = tud_descriptor_configuration_cb(0);
    int tinyusb_size = (int)sizeof tinyusb_config;
    CHECK(tinyusb_size == 186);
    CHECK(cfg && memcmp(cfg, tinyusb_config, sizeof tinyusb_config) == 0);
    CHECK(tud_descriptor_configuration_cb(1) == NULL);
    int total = cfg ? cfg[2] | (cfg[3] << 8) : 0, walked = 0, interfaces = 0, endpoints = 0;
    while (cfg && walked < total && cfg[walked] != 0) {                              // the lengths add up
        if (cfg[walked + 1] == 0x04) interfaces++;
        if (cfg[walked + 1] == 0x05) endpoints++;
        walked += cfg[walked];
    }
    CHECK(total == 186 && walked == total && interfaces == 5 && endpoints == 5);
    CHECK(cfg && cfg[4] == 4 && cfg[7] == 0x80 && cfg[8] * 2 == USB_MAX_POWER_MA);
    _Static_assert(USB_MAX_POWER_MA <= 100, "behind its own hub the box may ask for 100 mA at most");

    TEST("USB tables: Windows is told to use its NCM driver");
    const uint8_t *bos = tud_descriptor_bos_cb();
    CHECK(sizeof tinyusb_bos == 33 && memcmp(bos, tinyusb_bos, sizeof tinyusb_bos) == 0);
    tusb_control_request_t rq;
    memset(&rq, 0, sizeof rq);
    rq.bmRequestType_bit.type = TUSB_REQ_TYPE_VENDOR;
    rq.bmRequestType_bit.direction = 1;
    rq.bRequest = 1;                                                                 // the request number named in the BOS table
    rq.wIndex = 7;
    rq.wLength = 46;
    int len = 0;
    CHECK(tud_vendor_control_xfer_cb(0, CONTROL_STAGE_SETUP, &rq));
    const uint8_t *ms = fake_control_reply(&len);
    CHECK(ms && len == 46);
    if (ms && len == 46) {
        static const uint8_t name[16] = {'W', 'I', 'N', 'N', 'C', 'M'};
        CHECK(ms[0] == 10 && ms[1] == 0 && ms[2] == 0 && ms[3] == 0 && ms[7] == 0x06 && ms[8] == 46 && ms[9] == 0);   // set header
        CHECK(ms[10] == 8 && ms[12] == 1 && ms[14] == 0 && ms[16] == 36);            // for the first configuration: 36 bytes follow
        CHECK(ms[18] == 8 && ms[20] == 2 && ms[22] == 0 && ms[24] == 28);            // for the function at interface 0: 28 bytes
        CHECK(ms[26] == 20 && ms[28] == 3 && memcmp(ms + 30, name, 16) == 0);        // compatible ID
        CHECK(10 + ms[16] == 46 && 18 + ms[24] == 46 && 26 + ms[26] == 46);
    }
    rq.wLength = 10;                                                                 // Windows may first ask for the header only
    CHECK(tud_vendor_control_xfer_cb(0, CONTROL_STAGE_SETUP, &rq));
    fake_control_reply(&len);
    CHECK(len == 10);
    CHECK(tud_vendor_control_xfer_cb(0, CONTROL_STAGE_ACK, &rq));                    // later stages: nothing to do
    rq.bRequest = 2;
    CHECK(!tud_vendor_control_xfer_cb(0, CONTROL_STAGE_SETUP, &rq));                 // unknown requests are refused
    rq.bRequest = 1; rq.wIndex = 4;
    CHECK(!tud_vendor_control_xfer_cb(0, CONTROL_STAGE_SETUP, &rq));
    rq.wIndex = 7; rq.bmRequestType_bit.type = TUSB_REQ_TYPE_CLASS;
    CHECK(!tud_vendor_control_xfer_cb(0, CONTROL_STAGE_SETUP, &rq));

    TEST("USB tables: the block layout numbers are ones Windows accepts (it checks them since September 2026)");
    uint8_t drivers = 0;
    const usbd_class_driver_t *driver = usbd_app_driver_get_cb(&drivers);
    CHECK(drivers == 1 && driver && driver->init && driver->reset && driver->open && driver->control_xfer_cb && driver->xfer_cb);
    if (driver && driver->control_xfer_cb) {
        int handed_on = fake_library_control_requests();
        memset(&rq, 0, sizeof rq);
        rq.bmRequestType = 0xA1;                                                     // to an interface, class request, answer wanted
        rq.bRequest = 0x80;                                                          // GET_NTB_PARAMETERS
        rq.wLength = 28;
        CHECK(driver->control_xfer_cb(0, CONTROL_STAGE_SETUP, &rq));
        const uint8_t *ntb = fake_control_reply(&len);
        CHECK(ntb && len == 28);
        if (ntb && len == 28) {
            CHECK(ntb_u16(ntb) == 28 && (ntb_u16(ntb + 2) & 1));                     // size; the 16-bit block format
            CHECK(ntb_u32(ntb + 4) == CFG_TUD_NCM_IN_NTB_MAX_SIZE && ntb_u32(ntb + 16) == CFG_TUD_NCM_OUT_NTB_MAX_SIZE);
            for (int at = 8; at <= 20; at += 12) {                                   // box -> computer, then computer -> box
                unsigned divisor = ntb_u16(ntb + at), remainder = ntb_u16(ntb + at + 2), alignment = ntb_u16(ntb + at + 4);
                CHECK(divisor >= 4 && (divisor & (divisor - 1)) == 0);               // 4 or more and a power of two: Windows insists
                CHECK(remainder < divisor);
                CHECK(alignment >= 4 && (alignment & (alignment - 1)) == 0);
            }
            CHECK(ntb_u16(ntb + 14) == 0 && ntb_u16(ntb + 26) == CFG_TUD_NCM_OUT_MAX_DATAGRAMS_PER_NTB);
        }
        CHECK(driver->control_xfer_cb(0, CONTROL_STAGE_ACK, &rq));                   // later stages: nothing to do
        CHECK(fake_library_control_requests() == handed_on);                         // TinyUSB's own answer was not used
        rq.bRequest = 0x43;                                                          // every other request goes on to TinyUSB:
        driver->control_xfer_cb(0, CONTROL_STAGE_SETUP, &rq);                        //   another class request
        rq.bmRequestType = 0x01; rq.bRequest = 11; rq.wValue = 1; rq.wIndex = 1;
        driver->control_xfer_cb(0, CONTROL_STAGE_SETUP, &rq);                        //   "switch the data interface on"
        CHECK(fake_library_control_requests() == handed_on + 2);
    }

    TEST("USB tables: the text strings");
    static const uint8_t mac[6] = {0x02, 0xAB, 0x00, 0x9F, 0x10, 0xC7};
    usb_descriptors_set_identity("box-042", mac);
    const uint16_t *lang = tud_descriptor_string_cb(0, 0);
    CHECK(lang && lang[0] == 0x0304 && lang[1] == 0x0409);
    CHECK(usb_string_is(1, USB_MANUFACTURER) && usb_string_is(2, USB_PRODUCT));
    CHECK(usb_string_is(3, "box-042"));
    CHECK(usb_string_is(4, USB_PRODUCT " network adapter"));
    CHECK(usb_string_is(5, "02AB009F10C7"));                                         // the computer's Ethernet address
    CHECK(usb_string_is(6, USB_PRODUCT " MIDI"));                                    // the name of the MIDI port
    CHECK(tud_descriptor_string_cb(7, 0x0409) == NULL && tud_descriptor_string_cb(0xEE, 0x0409) == NULL);
    usb_descriptors_set_identity("0123456789012345678901234567890123456789", mac);    // too long: cut, not overrun
    CHECK(usb_string_is(3, "01234567890123456789012345678901"));
}

// ================================================================ 5. the whole firmware (the real main.c)
static const uint8_t *host_udp_reply(int index, const uint8_t *dst_mac, const uint8_t *dst_ip, uint16_t box_port,
                                     uint16_t pc_port, int *dlen) {
    int len;
    if (index >= fake_host_count()) return NULL;
    const uint8_t *f = fake_host_frame(index, &len);
    return check_udp_reply(f, len, dst_mac, dst_ip, box_port, pc_port, dlen);
}

// A DX answer the computer received: frame `index`, from the control port to the computer's port 50000.
static const uint8_t *dx_answer(int index, int *dlen) {
    return host_udp_reply(index, PC_MAC, PC_IP, CTRL_PORT, 50000, dlen);
}

// Frame `index` the computer received, taken apart as an answer of the address hand-out.
static dhcp_reply_t host_dhcp_reply(int index, uint32_t xid) {
    int dlen = 0;
    const uint8_t *d = host_udp_reply(index, BCAST, ALL_IP, DHCP_SERVER_PORT, DHCP_CLIENT_PORT, &dlen);
    return dhcp_parse(d, dlen, xid);
}

enum { GREEN = 0, YELLOW = 1, RED = 2 };       // the bits of fake_leds()

static bool led_all(int led, uint32_t from, uint32_t to, bool on) {      // the LED had this state the whole time
    for (uint32_t t = from; t <= to; t++) if ((((fake_leds(t) >> led) & 1) != 0) != on) return false;
    return true;
}

static int count_in_log(const char *text) {
    int n = 0;
    for (const char *p = fake_log(); (p = strstr(p, text)) != NULL; p += strlen(text)) n++;
    return n;
}

static bool dmx_is(int slot, uint8_t ch1, uint8_t ch2, uint8_t ch3) {   // a full packet with these first three channels, the rest dark
    int len;
    const uint8_t *d = fake_dmx(slot, &len);
    if (len != 1 + DMX_CHANNELS || d[0] != 0 || d[1] != ch1 || d[2] != ch2 || d[3] != ch3) return false;
    for (int i = 4; i <= DMX_CHANNELS; i++) if (d[i] != 0) return false;
    return true;
}

// The ID of light k (1 to n) of firmware/sim/rdm_lights.c: 7FF1:0000000k
static void light_uid(int k, uint8_t uid[6]) {
    const uint8_t id[6] = {0x7F, 0xF1, 0, 0, 0, (uint8_t)k};
    memcpy(uid, id, 6);
}

// The payload of CT_RDM_REQUEST: dest, sub-device 0, command class, parameter, its data
static int rdm_request(uint8_t *p, const uint8_t uid[6], uint8_t cc, uint16_t pid, const uint8_t *data, uint8_t len) {
    memcpy(p, uid, 6);
    p[6] = 0; p[7] = 0;
    p[8] = cc;
    p[9] = (uint8_t)(pid >> 8); p[10] = (uint8_t)pid;
    p[11] = len;
    if (len) memcpy(p + 12, data, len);
    return 12 + len;
}

static void test_firmware(void) {
    uint8_t m[400];
    int len = 0, dlen = 0;
    const uint8_t *d, *f;
    dhcp_reply_t r;

    TEST("firmware: power on without a computer, then a computer is plugged in and talks to the box");
    fake_reset(false);
    fake_at(3000); fake_connect();
    src_ip = NO_IP;
    fake_at(3010); dhcp_message(m, D_DISCOVER, 0x22220001, false, NULL, NULL, NULL);
                   fake_frame(pk, udp(BCAST, ALL_IP, 68, 67, m, 300, true));
    fake_at(3020); dhcp_message(m, D_REQUEST, 0x22220002, false, NULL, PC_IP, BOX_IP);
                   fake_frame(pk, udp(BCAST, ALL_IP, 68, 67, m, 300, true));
    src_ip = PC_IP;
    fake_at(3030); fake_frame(pk, arp_request(BOX_IP));
    fake_at(3040); fake_frame(pk, ping_request(32));
    fake_at(3050); fake_frame(pk, dx_frame(0x01, 1, NULL, 0));
    fake_at(3060); fake_frame(pk, udp(BCAST, ALL_IP, 6454, ARTNET_PORT, ARTPOLL, 14, true));
    uint8_t set[7] = {0, 1, 0, 3, 255, 128, 7}, get[4] = {0, 1, 0, 4};
    fake_at(3070); fake_block_begin();                                  // two requests in one USB transfer
                   fake_frame(pk, dx_frame(0x10, 2, set, 7));
                   fake_frame(pk, dx_frame(0x40, 3, get, 4));
                   fake_block_end();
    fake_at(3200); fake_sample_dmx(0);
    fake_run_firmware(3300);

    CHECK(fake_usb_starts() == 1 && fake_board_ready() && fake_core1_flash_ready());
    CHECK(strstr(fake_log(), "dmxbox 3 firmware") && strstr(fake_log(), "box 7  ip 10.1.7.1  artnet universe 7"));
    CHECK(usb_string_is(3, "box-007") && usb_string_is(5, "02000A010702"));   // serial number, computer's address
    // Green: slow blink while nobody is there, steady once the computer has connected.
    CHECK(led_all(GREEN, 10, 490, false) && led_all(GREEN, 510, 990, true) && led_all(GREEN, 1010, 1490, false));
    CHECK(led_all(GREEN, 2010, 2490, false) && led_all(GREEN, 3000, 3299, true));
    // Yellow: steady, not disturbed by the address hand-out, flickering when lighting commands arrive.
    CHECK(led_all(YELLOW, 2000, 3049, true) && !led_all(YELLOW, 3050, 3110, true) && led_all(YELLOW, 3130, 3299, true));
    CHECK(led_all(RED, 100, 3299, false));                               // no MIDI, no lights to talk to
    CHECK(count_in_log("computer connected") == 1 && count_in_log("computer gone") == 0);

    CHECK(fake_host_count() == 8 && fake_host_waiting() == 0);
    r = host_dhcp_reply(0, 0x22220001);
    CHECK(is_offer_or_ack(&r, D_OFFER));
    r = host_dhcp_reply(1, 0x22220002);
    CHECK(is_offer_or_ack(&r, D_ACK));
    f = fake_host_count() > 3 ? fake_host_frame(2, &len) : NULL;
    CHECK(f && is_arp_reply(f, len));
    f = fake_host_count() > 3 ? fake_host_frame(3, &len) : NULL;
    CHECK(f && is_ping_reply(f, len, 32));
    d = dx_answer(4, &dlen);                                             // PING: since dmxbox 3 with 4 more bytes
    CHECK(d && dlen == 29 && d[3] == 0x80 && d[12] == 0 && d[13] == FW_VERSION_MAJOR && d[15] == 0 && d[16] == 255);
    CHECK(d && d[25] == (5000 >> 8) && d[26] == (5000 & 0xFF) && d[28] == BOX_N &&
          d[27] == (CTRL_CAN_RDM | CTRL_CAN_MIDI | CTRL_CAN_CHAIN));
    d = host_udp_reply(5, PC_MAC, PC_IP, ARTNET_PORT, 6454, &dlen);
    CHECK(d && dlen == ARTNET_REPLY_LEN && memcmp(d + 10, BOX_IP, 4) == 0 && memcmp(d + 201, BOX_MAC, 6) == 0);
    CHECK(d && strcmp((const char *)d + 26, NODE_SHORT_NAME) == 0 && strcmp((const char *)d + 44, NODE_LONG_NAME) == 0);
    CHECK(d && d[18] == 0 && d[19] == 0 && d[190] == BOX_N && (d[23] & 0x02));   // universe 7; it can do RDM
    d = dx_answer(6, &dlen);
    CHECK(d && dlen == 13 && d[12] == 0);
    d = dx_answer(7, &dlen);
    CHECK(d && dlen == 17 && d[13] == 255 && d[14] == 128 && d[15] == 7 && d[16] == 0);
    static const uint32_t asked_at[8] = {3010, 3020, 3030, 3040, 3050, 3060, 3070, 3070};
    bool quick = fake_host_count() == 8;
    for (int i = 0; quick && i < 8; i++) quick = fake_host_frame_ms(i) - asked_at[i] <= 2;
    CHECK(quick);                                                        // every answer within 2 ms
    CHECK(dmx_is(0, 255, 128, 7));                                       // and the values are on the DMX line
    CHECK(fake_violations() == 0);

    TEST("firmware: an Art-Net stream for its universe reaches the DMX line, another universe does not");
    fake_reset(false);
    fake_at(100); fake_connect();
    for (int k = 0; k < 50; k++) {                                       // 40 packets per second
        static uint8_t values[512];
        values[0] = (uint8_t)(k * 5); values[1] = 200; values[511] = 99;
        fake_at(200 + 25 * (uint32_t)k);
        fake_frame(pk, artdmx_frame((uint8_t)(k + 1), values, 512));
    }
    static uint8_t other[512] = {1, 2, 3};
    fake_at(1460); fake_frame(pk, artdmx_frame_to(ARTNET_UNIVERSE, 1, other, 512));       // universe 0: box 0's
    fake_at(1470); fake_frame(pk, artdmx_frame_to(ARTNET_UNIVERSE + BOX_N + 1, 1, other, 512));
    fake_at(1500); fake_sample_dmx(0);
    fake_at(1600); fake_disconnect();
    fake_at(2500); fake_sample_dmx(1);
    fake_run_firmware(2600);
    d = fake_dmx(0, &len);
    CHECK(len == 513 && d[0] == 0 && d[1] == 245 && d[2] == 200 && d[3] == 0 && d[511] == 0 && d[512] == 99);
    f = fake_dmx(1, &dlen);
    CHECK(dlen == 513 && memcmp(d, f, 513) == 0);                        // unchanged without the computer
    int dark = 0;
    for (uint32_t t = 200; t < 1425; t++) dark += ((fake_leds(t) >> YELLOW) & 1) ? 0 : 1;
    CHECK(dark > 300 && dark < 900);                                     // yellow flickers while the stream runs
    CHECK(led_all(GREEN, 200, 1599, true));                              // green stays on
    CHECK(led_all(GREEN, 2010, 2490, false) && ((fake_leds(1700) >> GREEN) & 1));   // and blinks again after unplugging
    CHECK(count_in_log("computer gone") == 1 && fake_host_count() == 0 && fake_violations() == 0);

    TEST("firmware: a preset survives power-off and fades in when the desk calls it");
    fake_reset(false);
    uint8_t warm[7] = {0, 1, 0, 3, 240, 120, 60}, slot0[1] = {0};
    fake_at(50); fake_connect();
    fake_at(60); fake_frame(pk, dx_frame(0x10, 1, warm, 7));
    fake_at(70); fake_frame(pk, dx_frame(0x20, 2, slot0, 1));            // save as preset 0
    fake_run_firmware(200);
    d = dx_answer(1, &dlen);
    CHECK(d && dlen == 13 && d[12] == 0 && fake_saves() == 1);

    fake_reset(true);                                                    // power off and on again: only the flash is kept
    uint8_t go[5] = {0, 0, 0, 0x07, 0xD0};                               // preset 0 in 2000 ms
    fake_at(500);  fake_sample_dmx(0);
    fake_at(900);  fake_connect();
    fake_at(1000); fake_frame(pk, dx_frame(0x21, 1, go, 5));
    fake_at(2000); fake_sample_dmx(1);                                   // half way through the 2 s fade
    fake_at(3200); fake_sample_dmx(2);
    fake_run_firmware(3300);
    CHECK(dmx_is(0, 0, 0, 0));
    d = fake_dmx(1, &len);
    CHECK(len == 513 && d[1] >= 116 && d[1] <= 124 && d[2] >= 58 && d[2] <= 62 && d[3] >= 28 && d[3] <= 32);
    CHECK(dmx_is(2, 240, 120, 60));
    d = dx_answer(0, &dlen);
    CHECK(d && dlen == 13 && d[12] == 0);
    CHECK(fake_violations() == 0);

    TEST("firmware: a sleeping computer, and plugging in again");
    fake_reset(false);
    fake_at(100);  fake_connect();
    src_ip = NO_IP;
    fake_at(110);  dhcp_message(m, D_DISCOVER, 0x33330001, false, NULL, NULL, NULL);
                   fake_frame(pk, udp(BCAST, ALL_IP, 68, 67, m, 300, true));
    fake_at(1000); fake_suspend(true);
    fake_at(2600); fake_suspend(false);
    fake_at(3000); fake_disconnect();
    fake_at(3600); fake_connect();
    fake_at(3610); dhcp_message(m, D_REQUEST, 0x33330002, false, NULL, PC_IP, NULL);   // "the address I had before, please"
                   fake_frame(pk, udp(BCAST, ALL_IP, 68, 67, m, 300, true));
    src_ip = PC_IP;
    fake_run_firmware(3700);
    CHECK(fake_host_count() == 2);
    r = host_dhcp_reply(0, 0x33330001);
    CHECK(is_offer_or_ack(&r, D_OFFER));
    r = host_dhcp_reply(1, 0x33330002);
    CHECK(is_offer_or_ack(&r, D_ACK));
    CHECK(led_all(GREEN, 150, 999, true));                               // connected
    CHECK(led_all(GREEN, 1010, 1490, false) && led_all(GREEN, 1510, 1990, true));   // asleep: blinking
    CHECK(led_all(GREEN, 2600, 2999, true));                             // awake again
    CHECK(led_all(GREEN, 3010, 3490, false));                            // unplugged
    CHECK(led_all(GREEN, 3600, 3699, true));                             // plugged in again
    CHECK(count_in_log("computer connected") == 3 && count_in_log("computer gone") == 2);
    CHECK(fake_violations() == 0);

    TEST("firmware: RDM: the box finds the lights on its line by itself, the desk reads the list and asks them");
    fake_reset(false);
    fake_rdm_lights(3);
    uint8_t q[64], uid[6], start[2] = {0x01, 0x2C};                      // 300
    fake_at(100);  fake_connect();
    fake_at(1000); fake_frame(pk, dx_frame(0x51, 1, NULL, 0));          // the list
    light_uid(2, uid);
    fake_at(1010); fake_frame(pk, dx_frame(0x52, 2, q, rdm_request(q, uid, 0x20, 0x0060, NULL, 0)));    // GET DEVICE_INFO
    light_uid(3, uid);
    fake_at(1020); fake_frame(pk, dx_frame(0x52, 3, q, rdm_request(q, uid, 0x30, 0x00F0, start, 2)));   // SET DMX_START_ADDRESS
    fake_at(1030); fake_frame(pk, dx_frame(0x52, 4, q, rdm_request(q, uid, 0x20, 0x00F0, NULL, 0)));    // GET it again
    light_uid(9, uid);
    fake_at(1040); fake_frame(pk, dx_frame(0x52, 5, q, rdm_request(q, uid, 0x20, 0x0060, NULL, 0)));    // a light that is not there
    fake_at(1050); fake_frame(pk, dx_frame(0x50, 6, NULL, 0));          // search again
    fake_at(1500); fake_frame(pk, dx_frame(0x51, 7, NULL, 0));
    fake_run_firmware(1600);
    CHECK(count_in_log("rdm: 3 lights found") == 2);
    CHECK(strstr(fake_log(), "rdm id 7FF0:03123456") != NULL);          // 7FF0, then the last four bytes of the flash's number
    int rl = 0;
    const uint8_t *last = fake_rdm_last(&rl);
    CHECK(rl >= 26 && last[0] == 0xCC && last[9] == 0x7F && last[10] == 0xF0 && last[11] == 0x03 && last[14] == 0x56);
    CHECK(fake_rdm_packets() > 20 && fake_host_count() == 7);
    d = dx_answer(0, &dlen);
    CHECK(d && dlen == 12 + 4 + 18 && d[12] == 0 && d[13] == 0 && d[14] == 0 && d[15] == 3);
    for (int k = 1; d && k <= 3; k++) {
        light_uid(k, uid);
        CHECK(memcmp(d + 16 + 6 * (k - 1), uid, 6) == 0);               // in the order of their IDs
    }
    d = dx_answer(1, &dlen);                                             // ACK, GET_COMMAND_RESPONSE, DEVICE_INFO, 19 bytes
    CHECK(d && dlen == 12 + 6 + 19 && d[12] == 0 && d[13] == 0 && d[14] == 0x21 && d[15] == 0x00 && d[16] == 0x60 &&
          d[17] == 19 && d[18] == 0x01 && d[20] == 0x00 && d[21] == 0x02);   // RDM 1.0, model 2 (the moving head)
    d = dx_answer(2, &dlen);
    CHECK(d && dlen == 12 + 6 && d[12] == 0 && d[13] == 0 && d[14] == 0x31 && d[17] == 0);
    d = dx_answer(3, &dlen);
    CHECK(d && dlen == 12 + 6 + 2 && d[12] == 0 && d[18] == 0x01 && d[19] == 0x2C);
    d = dx_answer(4, &dlen);
    CHECK(d && dlen == 13 && d[12] == 6);                                // ST_RDM_TIMEOUT: no light with that ID
    d = dx_answer(5, &dlen);
    CHECK(d && dlen == 13 && d[12] == 0);
    d = dx_answer(6, &dlen);
    CHECK(d && dlen == 12 + 4 + 18 && d[13] == 0 && d[15] == 3);
    for (int i = 1; i <= 4; i++) CHECK(fake_host_count() > i && fake_host_frame_ms(i) - (1000 + 10 * (uint32_t)i) <= 3);
    CHECK(!led_all(RED, 0, 100, false) && led_all(RED, 1300, 1599, false));   // red flickers while the line is busy
    CHECK(fake_violations() == 0);

    TEST("firmware: box 200: its own addresses, serial number and universe");
    use_box(200);
    fake_reset(false);
    fake_box_number(200);
    fake_at(50); fake_connect();
    src_ip = NO_IP;
    fake_at(60); dhcp_message(m, D_DISCOVER, 0x44440001, false, NULL, NULL, NULL);
                 fake_frame(pk, udp(BCAST, ALL_IP, 68, 67, m, 300, true));
    src_ip = PC_IP;
    fake_at(70); fake_frame(pk, udp(BCAST, ALL_IP, 6454, ARTNET_PORT, ARTPOLL, 14, true));
    fake_at(80); fake_frame(pk, dx_frame(0x01, 1, NULL, 0));
    fake_run_firmware(200);
    r = host_dhcp_reply(0, 0x44440001);
    CHECK(is_offer_or_ack(&r, D_OFFER));                                 // 10.1.200.2 from 10.1.200.1
    d = host_udp_reply(1, PC_MAC, PC_IP, ARTNET_PORT, 6454, &dlen);
    CHECK(d && d[18] == 0 && d[19] == 200 / 16 && d[190] == 200 % 16);  // universe 200: sub-net 12, universe 8
    d = dx_answer(2, &dlen);
    CHECK(d && dlen == 29 && d[28] == 200);
    CHECK(usb_string_is(3, "box-200") && strstr(fake_log(), "ip 10.1.200.1"));
    CHECK(fake_violations() == 0);
    use_box(BOX_N);

    // The last test: main.c keeps the moment it was asked in a variable that no
    // pretend power-on clears, as the real one is cleared by the restart.
    TEST("firmware: asked for the USB boot loader, the box answers first and restarts into it");
    fake_reset(false);
    fake_at(50);  fake_connect();
    fake_at(100); fake_frame(pk, dx_frame(0x60, 1, NULL, 0));
    fake_run_firmware(2000);
    d = dx_answer(0, &dlen);
    CHECK(d && dlen == 13 && d[12] == 0 && fake_host_frame_ms(0) - 100 <= 2);
    CHECK(fake_bootloader_ms() >= 400 && fake_bootloader_ms() <= 430);   // 300 ms later, after the log line
    CHECK(strstr(fake_log(), "restarting into the USB boot loader") != NULL);
    CHECK(fake_violations() == 0);
}

int main(void) {
    use_box(BOX_N);
    test_stack();
    test_dhcp();
    test_usb();
    test_descriptors();
    test_firmware();
    if (fake_violations()) printf("  complaint from the pretend hardware: %s\n", fake_last_violation());
    printf("%d checks, %d failed\n", checks, failures);
    return failures ? 1 : 0;
}
