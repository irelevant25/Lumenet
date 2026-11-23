// PC test of the Ethernet side of the firmware: the real driver (enc28j60.c),
// the real mini stack (net.c) and the real protocol code (artnet.c, ctrl.c),
// talking to a simulated ENC28J60 chip (enc_model.c).
//
//   Windows:      build.cmd
//   Linux/macOS:  cc -std=c11 -Wall -Wextra -I../src *.c ../src/enc28j60.c ../src/net.c ../src/engine.c \
//                    ../src/artnet.c ../src/ctrl.c ../src/presets.c -o test_net && ./test_net
#include <stdio.h>
#include <string.h>

#include "artnet.h"
#include "config.h"
#include "ctrl.h"
#include "enc28j60.h"
#include "enc_model.h"
#include "engine.h"
#include "net.h"
#include "platform.h"

static const uint8_t BOX_MAC[6] = {0x02, 0x11, 0x22, 0x33, 0x44, 0x55};
static const uint8_t BOX_IP[4]  = {192, 168, 1, 50};
static const uint8_t PC_MAC[6]  = {0x00, 0x1B, 0x21, 0xAA, 0xBB, 0xCC};
static const uint8_t PC_IP[4]   = {192, 168, 1, 10};
static const uint8_t BCAST[6]   = {0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF};

static int failures, checks;
static const char *current = "";

#define CHECK(cond) do { checks++; if (!(cond)) { failures++; \
    printf("  FAIL %s (line %d): %s\n", current, __LINE__, #cond); } } while (0)
#define TEST(name) do { current = name; printf("- %s\n", name); } while (0)

// ---------------------------------------------------------------- firmware glue (as in main.c)
static engine_t      g_engine;
static preset_bank_t g_bank;
static ctrl_t        g_ctrl;
static artnet_node_t g_artnet;
static uint8_t       g_reply[CTRL_MAX_PKT];
static int           g_udp_seen;
static uint32_t      g_now = 1000;

void plat_lock(void) {}
void plat_unlock(void) {}
bool plat_save_presets(const preset_bank_t *b) { (void)b; return true; }

static void on_udp(uint16_t port, const net_peer_t *from, const uint8_t *data, int len) {
    int reply_len;
    g_udp_seen++;
    if (port == ARTNET_PORT)    reply_len = artnet_handle(&g_artnet, data, len, g_now, g_reply, sizeof g_reply);
    else if (port == CTRL_PORT) reply_len = ctrl_handle(&g_ctrl, data, len, g_now, g_reply, sizeof g_reply);
    else return;
    if (reply_len > 0) net_udp_send(from, port, g_reply, reply_len);
}

static uint8_t g_rx[ENC_MAX_FRAME];

static int drain(void) {          // what the main loop does: chip -> stack, until nothing is waiting
    int n = 0, len;
    while ((len = enc_receive(g_rx, sizeof g_rx)) != 0) {
        if (len > 0) net_input(g_rx, len);
        n++;
    }
    return n;
}

// ---------------------------------------------------------------- packet builders (independent of net.c)
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

static uint8_t pk[1600];

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

// IPv4 packet from the PC; `payload` is the ICMP or UDP part, already complete.
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
    memcpy(ip + 12, PC_IP, 4);
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
        memcpy(pseudo, PC_IP, 4);
        memcpy(pseudo + 4, dst_ip, 4);
        pseudo[8] = 0; pseudo[9] = 17; pseudo[10] = u[4]; pseudo[11] = u[5];
        uint16_t c = checksum(pseudo, 12, u, ulen);
        if (c == 0) c = 0xFFFF;
        u[6] = (uint8_t)(c >> 8); u[7] = (uint8_t)c;
    }
    return ipv4(dst_mac, dst_ip, 17, u, ulen, 0);
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

// Is `f` a correct IPv4/UDP frame from the box to the PC? Returns the UDP payload (or NULL).
static const uint8_t *check_udp_reply(const uint8_t *f, int len, uint16_t box_port, uint16_t pc_port, int *dlen) {
    if (len < 42) return NULL;
    const uint8_t *ip = f + 14, *u = f + 34;
    int total = (ip[2] << 8) | ip[3], ulen = (u[4] << 8) | u[5];
    uint8_t pseudo[12];
    memcpy(pseudo, ip + 12, 8);
    pseudo[8] = 0; pseudo[9] = 17; pseudo[10] = u[4]; pseudo[11] = u[5];
    bool ok = memcmp(f, PC_MAC, 6) == 0 && memcmp(f + 6, BOX_MAC, 6) == 0 && f[12] == 0x08 && f[13] == 0x00 &&
              ip[0] == 0x45 && ip[9] == 17 && total == len - 14 && ulen == total - 20 &&
              memcmp(ip + 12, BOX_IP, 4) == 0 && memcmp(ip + 16, PC_IP, 4) == 0 &&
              checksum(ip, 20, NULL, 0) == 0 && checksum(pseudo, 12, u, ulen) == 0 &&
              ((u[0] << 8) | u[1]) == box_port && ((u[2] << 8) | u[3]) == pc_port;
    if (!ok) return NULL;
    *dlen = ulen - 8;
    return u + 8;
}

// Send one UDP datagram to the box and return its single answer (or NULL).
static const uint8_t *ask(uint16_t port, const uint8_t *data, int dlen, int *rlen) {
    int flen;
    model_clear_sent();
    CHECK(model_wire_in(pk, udp(BOX_MAC, BOX_IP, 50000, port, data, dlen, true)));
    drain();
    if (model_sent_count() != 1) return NULL;
    const uint8_t *f = model_sent(0, &flen);
    return check_udp_reply(f, flen, port, 50000, rlen);
}

// ---------------------------------------------------------------- tests
static void start_box(void) {
    net_config_t cfg;
    memcpy(cfg.mac, BOX_MAC, 6);
    memcpy(cfg.ip, BOX_IP, 4);
    cfg.netmask[0] = cfg.netmask[1] = cfg.netmask[2] = 255;
    cfg.netmask[3] = 0;
    engine_init(&g_engine);
    presets_clear(&g_bank);
    ctrl_init(&g_ctrl, &g_engine, &g_bank, g_now);
    g_artnet = (artnet_node_t){.eng = &g_engine, .universe = ARTNET_UNIVERSE};
    memcpy(g_artnet.ip, BOX_IP, 4);
    memcpy(g_artnet.mac, BOX_MAC, 6);
    model_power_on(true);
    CHECK(enc_init(&MODEL_HAL, BOX_MAC));
    net_init(&cfg, enc_send, on_udp);
    g_udp_seen = 0;
}

static void test_driver(void) {
    TEST("a missing chip is reported");
    model_power_on(false);
    CHECK(!enc_init(&MODEL_HAL, BOX_MAC));

    TEST("init programs the chip as intended");
    start_box();
    CHECK(enc_revision() == 6);
    CHECK(model_reg(0, 0x08) == 0x00 && model_reg(0, 0x09) == 0x00);     // ERXST = 0x0000
    CHECK(model_reg(0, 0x0A) == 0xFF && model_reg(0, 0x0B) == 0x19);     // ERXND = 0x19FF
    CHECK(model_reg(0, 0x0C) == 0xFF && model_reg(0, 0x0D) == 0x19);     // ERXRDPT = 0x19FF (odd)
    CHECK(model_reg(1, 0x18) == 0xA1);                                   // unicast + broadcast, CRC checked
    CHECK(model_reg(2, 0x00) == 0x01);                                   // MACON1: receive enabled
    CHECK(model_reg(2, 0x02) == 0x32);                                   // MACON3: pad + CRC, half duplex
    CHECK(model_reg(2, 0x03) == 0x40);                                   // MACON4: DEFER
    CHECK(model_reg(2, 0x04) == 0x12 && model_reg(2, 0x06) == 0x12 && model_reg(2, 0x07) == 0x0C);
    CHECK(model_reg(2, 0x0A) == 0xEE && model_reg(2, 0x0B) == 0x05);     // max frame 1518
    CHECK(model_reg(3, 0x04) == 0x02 && model_reg(3, 0x05) == 0x11 && model_reg(3, 0x02) == 0x22 &&
          model_reg(3, 0x03) == 0x33 && model_reg(3, 0x00) == 0x44 && model_reg(3, 0x01) == 0x55);   // MAC address
    CHECK(model_phy(0x00) == 0x0000 && model_phy(0x10) == 0x0100);       // PHY half duplex, no loopback
    CHECK((model_reg(0, 0x1F) & 0x04) != 0);                             // receiver on

    TEST("link state follows the PHY");
    model_set_link(false);
    CHECK(!enc_link_up());
    model_set_link(true);
    CHECK(enc_link_up());

    TEST("frames for other stations are filtered, ours and broadcasts arrive intact");
    uint8_t f[100], other[6] = {0x00, 0x1B, 0x21, 1, 2, 3};
    for (int i = 0; i < 100; i++) f[i] = (uint8_t)(i * 7);
    memcpy(f, other, 6);
    CHECK(!model_wire_in(f, 100));
    CHECK(enc_receive(g_rx, sizeof g_rx) == 0);
    memcpy(f, BOX_MAC, 6);
    CHECK(model_wire_in(f, 100));
    CHECK(enc_receive(g_rx, sizeof g_rx) == 100 && memcmp(g_rx, f, 100) == 0);
    memcpy(f, BCAST, 6);
    CHECK(model_wire_in(f, 61));                                         // odd length: padded in the ring
    CHECK(enc_receive(g_rx, sizeof g_rx) == 61 && memcmp(g_rx, f, 61) == 0);
    CHECK(enc_receive(g_rx, sizeof g_rx) == 0);

    TEST("the receive ring wraps correctly over many frames");
    static uint8_t big[ENC_MAX_FRAME];
    int sizes[] = {60, 1514, 61, 590, 999, 64, 1513, 130, 777, 1200};
    int serial = 0, received = 0;
    for (int round = 0; round < 30; round++) {
        int burst = 1 + round % 3;                                       // 1, 2 or 3 frames waiting at once
        for (int b = 0; b < burst; b++) {
            int len = sizes[(serial + b) % 10];
            memcpy(big, BOX_MAC, 6);
            for (int i = 6; i < len; i++) big[i] = (uint8_t)(i * 31 + serial + b);
            CHECK(model_wire_in(big, len));
        }
        for (int b = 0; b < burst; b++, serial++) {
            int len = sizes[serial % 10];
            int got = enc_receive(g_rx, sizeof g_rx);
            bool same = got == len;
            for (int i = 6; same && i < len; i++) same = g_rx[i] == (uint8_t)(i * 31 + serial);
            CHECK(same);
            received += got > 0 ? got : 0;
        }
    }
    CHECK(received > 3 * 0x1A00);                                        // went round the 6.6 KB ring several times
    CHECK(enc_receive(g_rx, sizeof g_rx) == 0);

    TEST("a frame that does not fit the caller's buffer is dropped, the next one still arrives");
    memcpy(big, BOX_MAC, 6);
    CHECK(model_wire_in(big, 900));
    CHECK(model_wire_in(f, 61));
    CHECK(enc_receive(g_rx, 500) == -1);
    CHECK(enc_receive(g_rx, sizeof g_rx) == 61);

    TEST("transmit hands the frame to the chip unchanged");
    int len;
    model_clear_sent();
    CHECK(enc_send(big, 1514) && enc_send(f, 61));
    CHECK(model_sent_count() == 2);
    CHECK(model_sent(0, &len) && len == 1514 && memcmp(model_sent(0, &len), big, 1514) == 0);
    CHECK(model_sent(1, &len) && len == 61 && memcmp(model_sent(1, &len), f, 61) == 0);
    CHECK(!enc_send(big, 1515) && !enc_send(big, 13));
}

static void test_stack(void) {
    int len, dlen;
    const uint8_t *f, *d;
    uint8_t req[600];

    TEST("ARP request for the box's address is answered, others are ignored");
    start_box();
    model_clear_sent();
    CHECK(model_wire_in(pk, arp_request(BOX_IP)));
    drain();
    CHECK(model_sent_count() == 1);
    f = model_sent(0, &len);
    CHECK(len == 42 && memcmp(f, PC_MAC, 6) == 0 && memcmp(f + 6, BOX_MAC, 6) == 0 && f[12] == 0x08 && f[13] == 0x06);
    CHECK(f[20] == 0 && f[21] == 2);                                     // reply
    CHECK(memcmp(f + 22, BOX_MAC, 6) == 0 && memcmp(f + 28, BOX_IP, 4) == 0);
    CHECK(memcmp(f + 32, PC_MAC, 6) == 0 && memcmp(f + 38, PC_IP, 4) == 0);
    model_clear_sent();
    uint8_t elsewhere[4] = {192, 168, 1, 77};
    CHECK(model_wire_in(pk, arp_request(elsewhere)));
    drain();
    CHECK(model_sent_count() == 0);

    TEST("ping is answered with correct checksums");
    for (int size = 32; size <= 33; size++) {                            // even and odd payload
        uint8_t icmp[64] = {8, 0, 0, 0, 0x00, 0x01, 0x00, 0x2A};
        for (int i = 8; i < 8 + size; i++) icmp[i] = (uint8_t)('a' + i % 23);
        uint16_t c = checksum(icmp, 8 + size, NULL, 0);
        icmp[2] = (uint8_t)(c >> 8); icmp[3] = (uint8_t)c;
        model_clear_sent();
        CHECK(model_wire_in(pk, ipv4(BOX_MAC, BOX_IP, 1, icmp, 8 + size, 0)));
        drain();
        CHECK(model_sent_count() == 1);
        f = model_sent(0, &len);
        CHECK(len == 14 + 20 + 8 + size && memcmp(f, PC_MAC, 6) == 0);
        CHECK(checksum(f + 14, 20, NULL, 0) == 0 && f[23] == 1);
        CHECK(memcmp(f + 26, BOX_IP, 4) == 0 && memcmp(f + 30, PC_IP, 4) == 0);
        CHECK(f[34] == 0 && f[35] == 0 && checksum(f + 34, 8 + size, NULL, 0) == 0);
        CHECK(memcmp(f + 38, icmp + 4, (size_t)(4 + size)) == 0);        // identifier, sequence, data
    }

    TEST("control protocol over UDP: ping, set, read back");
    d = ask(CTRL_PORT, req, dx_request(req, 0x01, 1, NULL, 0), &dlen);
    CHECK(d && dlen == 25 && d[3] == 0x80 && d[12] == 0 && d[13] == FW_VERSION_MAJOR && d[16] == 255);
    CHECK(d && d[8] == 0 && d[9] == 1 && memcmp(d + 4, req + 4, 4) == 0);       // same session and sequence
    uint8_t set[7] = {0, 1, 0, 3, 255, 128, 7};
    d = ask(CTRL_PORT, req, dx_request(req, 0x10, 2, set, 7), &dlen);
    CHECK(d && dlen == 13 && d[12] == 0);
    uint8_t get[4] = {0, 1, 2, 0};                                       // channels 1..512
    d = ask(CTRL_PORT, req, dx_request(req, 0x40, 3, get, 4), &dlen);
    CHECK(d && dlen == 12 + 1 + 512 && d[13] == 255 && d[14] == 128 && d[15] == 7 && d[16] == 0);

    TEST("bad packets are ignored: wrong checksum, fragment, another address, unknown port");
    int before = g_udp_seen;
    model_clear_sent();
    len = udp(BOX_MAC, BOX_IP, 50000, CTRL_PORT, req, dx_request(req, 0x01, 9, NULL, 0), true);
    pk[len - 1] ^= 0x01;                                                 // damage the data
    CHECK(model_wire_in(pk, len));
    len = udp(BOX_MAC, BOX_IP, 50000, CTRL_PORT, req, 12, true);
    pk[14 + 10] ^= 0x40;                                                 // damage the IP header checksum
    CHECK(model_wire_in(pk, len));
    static uint8_t u8[20] = {0xC3, 0x50, 0x1B, 0x58, 0, 20};
    CHECK(model_wire_in(pk, ipv4(BOX_MAC, BOX_IP, 17, u8, 20, 0x2000)));          // "more fragments" set
    CHECK(model_wire_in(pk, udp(BOX_MAC, elsewhere, 50000, CTRL_PORT, req, 12, true)));
    drain();
    CHECK(g_udp_seen == before && model_sent_count() == 0);
    CHECK(model_wire_in(pk, udp(BOX_MAC, BOX_IP, 50000, 9999, req, 12, true)));   // nobody listens on 9999
    drain();
    CHECK(g_udp_seen == before + 1 && model_sent_count() == 0);
    CHECK(net_stats()->dropped == 3);

    TEST("a datagram without UDP checksum is accepted");
    model_clear_sent();
    CHECK(model_wire_in(pk, udp(BOX_MAC, BOX_IP, 50000, CTRL_PORT, req, dx_request(req, 0x01, 10, NULL, 0), false)));
    drain();
    CHECK(model_sent_count() == 1);

    TEST("Art-Net: broadcast ArtPoll is answered to the sender, ArtDmx reaches the engine");
    static const uint8_t poll[14] = {'A', 'r', 't', '-', 'N', 'e', 't', 0, 0x00, 0x20, 0, 14, 0, 0};
    uint8_t limited[4] = {255, 255, 255, 255}, subnet[4] = {192, 168, 1, 255}, foreign[4] = {2, 255, 255, 255};
    const uint8_t *targets[2] = {limited, subnet};
    for (int t = 0; t < 2; t++) {
        model_clear_sent();
        CHECK(model_wire_in(pk, udp(BCAST, targets[t], 6454, ARTNET_PORT, poll, 14, true)));
        drain();
        CHECK(model_sent_count() == 1);
        f = model_sent(0, &len);
        d = check_udp_reply(f, len, ARTNET_PORT, 6454, &dlen);
        CHECK(d && dlen == ARTNET_REPLY_LEN && memcmp(d, "Art-Net", 8) == 0 && d[8] == 0x00 && d[9] == 0x21);
        CHECK(d && memcmp(d + 10, BOX_IP, 4) == 0 && memcmp(d + 201, BOX_MAC, 6) == 0);
        CHECK(d && strcmp((const char *)d + 26, NODE_SHORT_NAME) == 0);
    }
    model_clear_sent();
    CHECK(model_wire_in(pk, udp(BCAST, foreign, 6454, ARTNET_PORT, poll, 14, true)));   // not our subnet
    drain();
    CHECK(model_sent_count() == 0);
    uint8_t dmx[18 + 4] = {'A', 'r', 't', '-', 'N', 'e', 't', 0, 0x00, 0x50, 0, 14, 1, 0, 0, 0, 0, 4, 9, 200, 0, 77};
    CHECK(model_wire_in(pk, udp(BOX_MAC, BOX_IP, 6454, ARTNET_PORT, dmx, sizeof dmx, true)));
    drain();
    uint8_t get4[4] = {0, 1, 0, 4};
    d = ask(CTRL_PORT, req, dx_request(req, 0x40, 11, get4, 4), &dlen);
    CHECK(d && dlen == 17 && d[13] == 255 && d[14] == 200 && d[15] == 7 && d[16] == 77);   // highest of cue and Art-Net

    TEST("several packets waiting in the chip are all handled, in order");
    model_clear_sent();
    for (int i = 0; i < 5; i++) {
        uint8_t one[5] = {0, (uint8_t)(20 + i), 0, 1, (uint8_t)(100 + i)};
        CHECK(model_wire_in(pk, udp(BOX_MAC, BOX_IP, 50000, CTRL_PORT, req, dx_request(req, 0x10, (uint16_t)(20 + i), one, 5), true)));
    }
    CHECK(drain() == 5 && model_sent_count() == 5);
    uint8_t get5[4] = {0, 20, 0, 5};
    d = ask(CTRL_PORT, req, dx_request(req, 0x40, 30, get5, 4), &dlen);
    CHECK(d && dlen == 18 && d[13] == 100 && d[17] == 104);

    TEST("announcement after link-up is a broadcast ARP for the box's own address");
    model_clear_sent();
    net_announce();
    CHECK(model_sent_count() == 1);
    f = model_sent(0, &len);
    CHECK(len == 42 && memcmp(f, BCAST, 6) == 0 && f[21] == 1 && memcmp(f + 28, BOX_IP, 4) == 0 &&
          memcmp(f + 38, BOX_IP, 4) == 0);
}

int main(void) {
    test_driver();
    test_stack();
    current = "chip rules";
    if (model_rule_violations()) printf("  chip model complaint: %s\n", model_last_violation());
    CHECK(model_rule_violations() == 0);
    printf("%d checks, %d failed\n", checks, failures);
    return failures ? 1 : 0;
}
