// PC test of the parts of the dmxbox firmware that need no hardware at all:
//
//   1. RDM packets: building, checking, the coded answers of the search (rdm.c)
//   2. the search for the lights on the line, and requests to them (rdm.c),
//      against the lights in memory of the simulator (firmware/sim/rdm_lights.c)
//   3. RDM over the DX protocol: answers that come later, repeats, busy (ctrl.c)
//   4. RDM over Art-Net: ArtTodRequest, ArtTodControl, ArtRdm (artnet.c)
//   5. MIDI bytes to USB-MIDI packets and back (midi_parse.c)
//   6. DMX IN: frames from the receiver's bytes (dmx_in.c), and to the desk over the DX protocol
//
//   Windows:      build.cmd (also builds and runs test_net.c)
//   Linux/macOS:  cc -std=c11 -Wall -Wextra -I../../boxes/box4/firmware -I../common -I../sim test_rdm.c \
//                    ../common/rdm.c ../common/ctrl.c ../common/artnet.c ../common/engine.c ../common/presets.c \
//                    ../common/dmx_in.c ../common/midi_parse.c ../sim/rdm_lights.c -o test_rdm && ./test_rdm
#include <stdio.h>
#include <string.h>

#include "artnet.h"
#include "config.h"
#include "ctrl.h"
#include "engine.h"
#include "midi_parse.h"
#include "platform.h"
#include "rdm.h"
#include "rdm_lights.h"

static int failures, checks;
static const char *current = "";

#define CHECK(cond) do { checks++; if (!(cond)) { failures++; \
    printf("  FAIL %s (line %d): %s\n", current, __LINE__, #cond); } } while (0)
#define TEST(name) do { current = name; printf("- %s\n", name); } while (0)

// ================================================================ the platform: a DMX line with the lights of rdm_lights.c
static uint8_t line_answer[RDM_MAX_PKT], line_last[RDM_MAX_PKT];
static int     line_len, line_last_len, line_sends;
static uint8_t line_last_expect;
static bool    line_garbled, line_waiting, line_dead;
static int     bootloader_requests;

void plat_lock(void) {}
void plat_unlock(void) {}
bool plat_save_presets(const preset_bank_t *b) { (void)b; return true; }
uint16_t plat_supply_mv(void) { return 4900; }
void plat_request_bootloader(void) { bootloader_requests++; }

void plat_rdm_send(const uint8_t *pkt, int len, uint8_t expect) {
    memcpy(line_last, pkt, (size_t)len);
    line_last_len = len;
    line_last_expect = expect;
    line_sends++;
    line_len = line_dead ? 0 : lights_hear(pkt, len, line_answer, (int)sizeof line_answer, &line_garbled);
    line_waiting = true;
}

int plat_rdm_poll(uint8_t *raw, int cap, int *n, bool *garbled) {
    if (!line_waiting) return 0;
    line_waiting = false;
    *n = line_len < cap ? line_len : cap;
    memcpy(raw, line_answer, (size_t)*n);
    *garbled = line_garbled;
    return 1;
}

static const uint8_t BOX_UID[6] = {0x7F, 0xF0, 0x12, 0x34, 0x56, 0x78};
static uint32_t t_now = 1000;

// Runs the RDM engine until it has nothing left to do; returns the transactions it took.
static int settle(rdm_t *r) {
    int before = line_sends;
    for (int i = 0; i < 200000 && (r->discovering || line_waiting ||
                                    (r->req_owner != RDM_OWNER_NONE && !r->req_done)); i++)
        rdm_poll(r, t_now++);
    return line_sends - before;
}

static bool in_tod(const rdm_t *r, const uint8_t uid[6]) {
    for (int i = 0; i < r->tod_count; i++) if (memcmp(r->tod[i], uid, 6) == 0) return true;
    return false;
}

static bool tod_has_every_light(const rdm_t *r) {
    if (r->tod_count != light_count) return false;
    for (int i = 0; i < light_count; i++) if (!in_tod(r, lights[i].uid)) return false;
    for (int i = 0; i < r->tod_count; i++)
        for (int k = i + 1; k < r->tod_count; k++) if (memcmp(r->tod[i], r->tod[k], 6) == 0) return false;
    return true;
}

// ================================================================ 1. packets
static void test_packets(void) {
    uint8_t p[RDM_MAX_PKT], data[3] = {1, 2, 3};
    rdm_answer_t a;

    TEST("RDM packet: built, then read back; a changed byte or a wrong length is noticed");
    static const uint8_t dest[6] = {0x7F, 0xF1, 0, 0, 0, 5};
    int n = rdm_build(p, dest, BOX_UID, 9, 1, 0, 0, RDM_CC_GET, 0x0082, data, 3);
    CHECK(n == 29 && p[0] == 0xCC && p[1] == 0x01 && p[2] == 27 && p[15] == 9 && p[20] == 0x20 && p[23] == 3);
    CHECK(rdm_parse(p, n, &a) && a.cc == 0x20 && a.pid == 0x0082 && a.pdl == 3 && memcmp(a.pd, data, 3) == 0 &&
          memcmp(a.src, BOX_UID, 6) == 0);
    uint8_t noisy[RDM_MAX_PKT + 2] = {0x00, 0x00};                       // a break reads as a zero byte first
    memcpy(noisy + 2, p, (size_t)n);
    CHECK(rdm_parse(noisy, n + 2, &a));
    p[25] ^= 0x10;
    CHECK(!rdm_parse(p, n, &a));                                         // checksum
    p[25] ^= 0x10;
    CHECK(!rdm_parse(p, n - 1, &a));                                     // cut short
    p[23] = 4;
    CHECK(!rdm_parse(p, n, &a));                                         // data length does not fit the length

    TEST("RDM search answer: coded and decoded, with any number of FE before it; two at once do not decode");
    uint8_t e[RDM_DISC_RESPONSE], uid[6];
    static const uint8_t one[6] = {0x12, 0x34, 0xAB, 0xCD, 0x00, 0xFF}, two[6] = {0x7F, 0xF1, 0, 0, 0, 2};
    CHECK(rdm_encode_disc(e, one) == RDM_DISC_RESPONSE);
    CHECK(rdm_decode_disc(e, RDM_DISC_RESPONSE, uid) && memcmp(uid, one, 6) == 0);
    for (int skip = 1; skip <= 7; skip++)                                // preamble shortened by `skip` bytes
        CHECK(rdm_decode_disc(e + skip, RDM_DISC_RESPONSE - skip, uid) && memcmp(uid, one, 6) == 0);
    uint8_t e2[RDM_DISC_RESPONSE];
    rdm_encode_disc(e2, two);
    for (int i = 0; i < RDM_DISC_RESPONSE; i++) e2[i] |= e[i];           // both on the wire
    CHECK(!rdm_decode_disc(e2, RDM_DISC_RESPONSE, uid));
    CHECK(!rdm_decode_disc(e, RDM_DISC_RESPONSE - 1, uid));              // the last byte missing
}

// ================================================================ 2. the search and requests
static void test_search(void) {
    static rdm_t r;

    static const int sizes[] = {0, 1, 2, 3, 17, 64};
    for (unsigned s = 0; s < sizeof sizes / sizeof sizes[0]; s++) {
        char name[96];
        snprintf(name, sizeof name, "RDM search: %d light%s with IDs one after the other, all found once",
                 sizes[s], sizes[s] == 1 ? "" : "s");
        TEST(name);
        lights_make(sizes[s]);
        rdm_init(&r, BOX_UID);
        rdm_start_discovery(&r);
        int steps = settle(&r);
        CHECK(!r.discovering && r.discoveries == 1 && tod_has_every_light(&r) && !r.tod_overflow);
        CHECK(steps < 20 + 100 * (sizes[s] + 1));                        // no endless searching
        for (int i = 0; i < light_count; i++) CHECK(lights[i].muted);    // each one told to be quiet once found
    }

    TEST("RDM search: 40 lights with IDs from all over the 48 bits, all found once");
    lights_make(40);
    uint64_t x = 0x2545F4914F6CDD1Dull;
    for (int i = 0; i < light_count; i++) {
        x ^= x << 13; x ^= x >> 7; x ^= x << 17;                         // xorshift: any IDs, the same every run
        rdm_u64_to_uid(x % 0xFFFFFFFFFFFEull, lights[i].uid);
    }
    rdm_init(&r, BOX_UID);
    rdm_start_discovery(&r);
    settle(&r);
    CHECK(!r.discovering && tod_has_every_light(&r));

    TEST("RDM search: a second search finds them again (they are unmuted first), and an empty line ends quickly");
    rdm_start_discovery(&r);
    settle(&r);
    CHECK(r.discoveries == 2 && tod_has_every_light(&r));
    lights_make(0);
    rdm_start_discovery(&r);
    CHECK(settle(&r) == 3 && r.tod_count == 0);                          // two un-mutes, one question to everybody

    TEST("RDM request: an answer, a refusal (NACK), a light that is not there, a broadcast, and one at a time");
    lights_make(3);
    rdm_init(&r, BOX_UID);
    rdm_start_discovery(&r);
    settle(&r);
    uint8_t status;
    const rdm_answer_t *a;
    const uint8_t *raw;
    int raw_len;
    CHECK(rdm_submit(&r, RDM_OWNER_CTRL, lights[1].uid, 0, RDM_CC_SET, 0x0082, (const uint8_t *)"Back", 4));
    CHECK(!rdm_submit(&r, RDM_OWNER_ARTNET, lights[0].uid, 0, RDM_CC_GET, 0x0060, NULL, 0));   // busy
    CHECK(!rdm_take(&r, RDM_OWNER_CTRL, &status, &a, &raw, &raw_len));   // not yet
    settle(&r);
    CHECK(!rdm_take(&r, RDM_OWNER_ARTNET, &status, &a, &raw, &raw_len)); // not for Art-Net
    CHECK(rdm_take(&r, RDM_OWNER_CTRL, &status, &a, &raw, &raw_len) && status == RDM_DONE_OK &&
          a->response_type == RDM_ACK && a->cc == RDM_CC_SET_RESPONSE && strcmp(lights[1].label, "Back") == 0);
    CHECK(raw_len == 26 && raw[0] == 0xCC && memcmp(raw + 9, lights[1].uid, 6) == 0);
    CHECK(!rdm_take(&r, RDM_OWNER_CTRL, &status, &a, &raw, &raw_len));  // taken once only
    CHECK(rdm_submit(&r, RDM_OWNER_CTRL, lights[1].uid, 0, RDM_CC_GET, 0x7777, NULL, 0));
    settle(&r);
    CHECK(rdm_take(&r, RDM_OWNER_CTRL, &status, &a, NULL, NULL) && status == RDM_DONE_OK &&
          a->response_type == RDM_NACK_REASON && a->pdl == 2 && a->pd[1] == 0x00);   // unknown PID
    static const uint8_t nobody[6] = {0x7F, 0xF1, 0, 0, 0, 0x99};
    CHECK(rdm_submit(&r, RDM_OWNER_CTRL, nobody, 0, RDM_CC_GET, 0x0060, NULL, 0));
    settle(&r);
    CHECK(rdm_take(&r, RDM_OWNER_CTRL, &status, &a, NULL, NULL) && status == RDM_DONE_TIMEOUT);
    static const uint8_t everybody[6] = {0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF};
    uint8_t on = 1;
    CHECK(rdm_submit(&r, RDM_OWNER_CTRL, everybody, 0, RDM_CC_SET, 0x1000, &on, 1));
    settle(&r);
    CHECK(line_last_expect == RDM_EXPECT_NONE);                          // nobody answers a broadcast
    CHECK(rdm_take(&r, RDM_OWNER_CTRL, &status, &a, NULL, NULL) && status == RDM_DONE_OK);
    CHECK(lights[0].identify && lights[1].identify && lights[2].identify);

    TEST("RDM request: a request waiting during a search goes out first; the search goes on afterwards");
    rdm_start_discovery(&r);
    rdm_poll(&r, t_now++);                                               // the search has the line
    CHECK(rdm_submit(&r, RDM_OWNER_CTRL, lights[2].uid, 0, RDM_CC_GET, 0x00F0, NULL, 0));
    rdm_poll(&r, t_now++);                                               // the search's answer, then the request
    CHECK(line_last[20] == RDM_CC_GET && memcmp(line_last + 3, lights[2].uid, 6) == 0);
    settle(&r);
    CHECK(rdm_take(&r, RDM_OWNER_CTRL, &status, &a, NULL, NULL) && status == RDM_DONE_OK && a->pdl == 2);
    CHECK(!r.discovering && tod_has_every_light(&r));

    TEST("RDM line: one that never reports back is freed after a while");
    line_dead = true;
    CHECK(rdm_submit(&r, RDM_OWNER_CTRL, lights[0].uid, 0, RDM_CC_GET, 0x0060, NULL, 0));
    rdm_poll(&r, t_now);                                                 // sent
    line_waiting = false;                                                // ... and the platform never answers
    rdm_poll(&r, t_now + 100);
    CHECK(r.line_busy);
    rdm_poll(&r, t_now + 600);
    CHECK(!r.line_busy && rdm_take(&r, RDM_OWNER_CTRL, &status, &a, NULL, NULL) && status == RDM_DONE_TIMEOUT);
    line_dead = false;
    t_now += 1000;
}

// ================================================================ 3. RDM over the DX protocol
static engine_t      t_engine;
static preset_bank_t t_bank;
static ctrl_t        t_ctrl;
static rdm_t         t_rdm;
static uint8_t       resp[CTRL_MAX_PKT];

static int dx(uint8_t *out, uint8_t type, uint32_t session, uint16_t seq, const uint8_t *payload, int plen) {
    out[0] = 'D'; out[1] = 'X'; out[2] = 1; out[3] = type;
    out[4] = (uint8_t)(session >> 24); out[5] = (uint8_t)(session >> 16); out[6] = (uint8_t)(session >> 8);
    out[7] = (uint8_t)session;
    out[8] = (uint8_t)(seq >> 8); out[9] = (uint8_t)seq;
    out[10] = (uint8_t)(plen >> 8); out[11] = (uint8_t)plen;
    if (plen) memcpy(out + 12, payload, (size_t)plen);
    return 12 + plen;
}

static int rdm_payload(uint8_t *p, const uint8_t uid[6], uint8_t cc, uint16_t pid, const uint8_t *data, uint8_t len) {
    memcpy(p, uid, 6);
    p[6] = 0; p[7] = 0;
    p[8] = cc;
    p[9] = (uint8_t)(pid >> 8); p[10] = (uint8_t)pid;
    p[11] = len;
    if (len) memcpy(p + 12, data, len);
    return 12 + len;
}

static void test_ctrl(void) {
    uint8_t req[300], pl[64];
    int n;

    lights_make(3);
    engine_init(&t_engine);
    presets_clear(&t_bank);
    ctrl_init(&t_ctrl, &t_engine, &t_bank, t_now);

    TEST("DX: a box without RDM says so");
    n = ctrl_handle(&t_ctrl, req, dx(req, CT_RDM_TOD, 0, 1, NULL, 0), t_now, resp, sizeof resp);
    CHECK(n == 13 && resp[12] == ST_NOT_SUPPORTED);

    rdm_init(&t_rdm, BOX_UID);
    t_ctrl.rdm = &t_rdm;
    t_ctrl.can = CTRL_CAN_RDM | CTRL_CAN_MIDI | CTRL_CAN_CHAIN;
    t_ctrl.box_number = 42;
    t_ctrl.model = 4;

    TEST("DX: PING tells the supply voltage, what the box can do, its number and which box it is");
    n = ctrl_handle(&t_ctrl, req, dx(req, CT_PING, 0, 1, NULL, 0), t_now, resp, sizeof resp);
    CHECK(n == 12 + 18 && resp[12] == ST_OK && resp[25] == (4900 >> 8) && resp[26] == (4900 & 0xFF) &&
          resp[27] == 7 && resp[28] == 42 && resp[29] == 4);

    TEST("DX: the search: started, reported while it runs, its list afterwards");
    n = ctrl_handle(&t_ctrl, req, dx(req, CT_RDM_DISCOVER, 0, 2, NULL, 0), t_now, resp, sizeof resp);
    CHECK(n == 13 && resp[12] == ST_OK && t_rdm.discovering);
    rdm_poll(&t_rdm, t_now++);
    n = ctrl_handle(&t_ctrl, req, dx(req, CT_RDM_TOD, 0, 3, NULL, 0), t_now, resp, sizeof resp);
    CHECK(n >= 16 && resp[12] == ST_OK && resp[13] == 1);                // still searching
    n = ctrl_handle(&t_ctrl, req, dx(req, CT_RDM_DISCOVER, 0, 4, NULL, 0), t_now, resp, sizeof resp);
    CHECK(n == 13 && resp[12] == ST_OK);                                 // asking again does not start over
    settle(&t_rdm);
    CHECK(t_rdm.discoveries == 1);
    n = ctrl_handle(&t_ctrl, req, dx(req, CT_RDM_TOD, 0, 5, NULL, 0), t_now, resp, sizeof resp);
    CHECK(n == 12 + 4 + 18 && resp[13] == 0 && resp[14] == 0 && resp[15] == 3);
    for (int i = 0; i < 3; i++) CHECK(in_tod(&t_rdm, resp + 16 + 6 * i));

    TEST("DX: an RDM request is answered later, to its session and sequence; a repeat meanwhile gets nothing");
    const uint32_t S = 0xCAFEBABE;
    n = rdm_payload(pl, lights[0].uid, RDM_CC_GET, 0x0081, NULL, 0);     // MANUFACTURER_LABEL
    int len = dx(req, CT_RDM_REQUEST, S, 10, pl, n);
    t_ctrl.deferred_new = 0;
    CHECK(ctrl_handle(&t_ctrl, req, len, t_now, resp, sizeof resp) == 0 && t_ctrl.deferred_new);
    CHECK(ctrl_poll(&t_ctrl, t_now, resp, sizeof resp) == 0);            // not there yet
    uint32_t dups = t_ctrl.duplicates;
    CHECK(ctrl_handle(&t_ctrl, req, len, t_now, resp, sizeof resp) == 0 && t_ctrl.duplicates == dups + 1);
    settle(&t_rdm);
    n = ctrl_poll(&t_ctrl, t_now, resp, sizeof resp);
    const char *maker = lights[0].maker;
    int ml = (int)strlen(maker);
    CHECK(n == 12 + 6 + ml && resp[3] == CT_ACK && resp[4] == 0xCA && resp[7] == 0xBE && resp[8] == 0 && resp[9] == 10);
    CHECK(resp[12] == ST_OK && resp[13] == RDM_ACK && resp[14] == RDM_CC_GET_RESPONSE && resp[15] == 0x00 &&
          resp[16] == 0x81 && resp[17] == ml && memcmp(resp + 18, maker, (size_t)ml) == 0);
    CHECK(ctrl_poll(&t_ctrl, t_now, resp, sizeof resp) == 0);            // once
    uint8_t first[CTRL_MAX_PKT];
    memcpy(first, resp, (size_t)n);
    int n2 = ctrl_handle(&t_ctrl, req, len, t_now, resp, sizeof resp);  // the answer got lost: the repeat gets it again
    CHECK(n2 == n && memcmp(resp, first, (size_t)n) == 0);
    CHECK(t_rdm.req_owner == RDM_OWNER_NONE);                            // and nothing went out on the line again

    TEST("DX: while one RDM request is on its way, another is refused as busy");
    n = rdm_payload(pl, lights[1].uid, RDM_CC_GET, 0x0060, NULL, 0);
    CHECK(ctrl_handle(&t_ctrl, req, dx(req, CT_RDM_REQUEST, S, 11, pl, n), t_now, resp, sizeof resp) == 0);
    int nb = ctrl_handle(&t_ctrl, req, dx(req, CT_RDM_REQUEST, 0x1234, 1, pl, n), t_now, resp, sizeof resp);
    CHECK(nb == 13 && resp[12] == ST_BUSY);
    settle(&t_rdm);
    n = ctrl_poll(&t_ctrl, t_now, resp, sizeof resp);
    CHECK(n == 12 + 6 + 19 && resp[9] == 11 && resp[12] == ST_OK && resp[17] == 19);

    TEST("DX: a light that does not answer, and a request with a wrong length");
    static const uint8_t nobody[6] = {0x7F, 0xF1, 0, 0, 0, 0x55};
    n = rdm_payload(pl, nobody, RDM_CC_GET, 0x0060, NULL, 0);
    CHECK(ctrl_handle(&t_ctrl, req, dx(req, CT_RDM_REQUEST, S, 12, pl, n), t_now, resp, sizeof resp) == 0);
    settle(&t_rdm);
    n = ctrl_poll(&t_ctrl, t_now, resp, sizeof resp);
    CHECK(n == 13 && resp[12] == ST_RDM_TIMEOUT);
    n = rdm_payload(pl, nobody, RDM_CC_GET, 0x0060, NULL, 0);
    pl[11] = 5;                                                          // says 5 bytes of data, has none
    n = ctrl_handle(&t_ctrl, req, dx(req, CT_RDM_REQUEST, S, 13, pl, n), t_now, resp, sizeof resp);
    CHECK(n == 13 && resp[12] == ST_BAD_LENGTH);

    TEST("DX: the boot loader is asked for after the answer");
    n = ctrl_handle(&t_ctrl, req, dx(req, CT_BOOTLOADER, S, 14, NULL, 0), t_now, resp, sizeof resp);
    CHECK(n == 13 && resp[12] == ST_OK && bootloader_requests == 1);
}

// ================================================================ 4. RDM over Art-Net
static artnet_node_t t_art;

static int art_header(uint8_t *p, uint16_t op) {
    memset(p, 0, 24);
    memcpy(p, "Art-Net", 8);
    p[8] = (uint8_t)op; p[9] = (uint8_t)(op >> 8);
    p[11] = 14;
    return 24;
}

static void test_artnet(void) {
    uint8_t p[600], reply[600];
    int n;
    const uint16_t U = ARTNET_UNIVERSE + 0x123;                         // net 1, sub-net 2, universe 3

    lights_make(5);
    engine_init(&t_engine);
    rdm_init(&t_rdm, BOX_UID);
    t_art = (artnet_node_t){.eng = &t_engine, .universe = U, .rdm = &t_rdm};
    rdm_start_discovery(&t_rdm);
    settle(&t_rdm);

    TEST("Art-Net: ArtPollReply says the box does RDM");
    uint8_t poll[14] = {'A', 'r', 't', '-', 'N', 'e', 't', 0, 0x00, 0x20, 0, 14, 0, 0};
    n = artnet_handle(&t_art, poll, 14, t_now, reply, sizeof reply);
    CHECK(n == ARTNET_REPLY_LEN && (reply[23] & 0x02) && reply[18] == 1 && reply[19] == 2);

    TEST("Art-Net: ArtTodRequest for its port gets the list at once; for another one nothing");
    art_header(p, 0x8000);
    p[21] = 1;                                                           // net
    p[23] = 2;                                                           // two addresses asked for
    p[24] = 0x99;
    p[25] = (uint8_t)(U & 0xFF);
    n = artnet_handle(&t_art, p, 26, t_now, reply, sizeof reply);
    CHECK(n == 28 + 6 * 5 && reply[8] == 0x00 && reply[9] == 0x81 && reply[21] == 1 && reply[23] == (U & 0xFF) &&
          reply[25] == 5 && reply[27] == 5);
    for (int i = 0; i < 5; i++) CHECK(in_tod(&t_rdm, reply + 28 + 6 * i));
    p[25] = 0x98;
    CHECK(artnet_handle(&t_art, p, 26, t_now, reply, sizeof reply) == 0);
    p[25] = (uint8_t)(U & 0xFF); p[21] = 0;                              // the right address in another net
    CHECK(artnet_handle(&t_art, p, 26, t_now, reply, sizeof reply) == 0);

    TEST("Art-Net: ArtTodControl flush searches again, and the list follows when the search is over");
    art_header(p, 0x8200);
    p[21] = 1; p[22] = 0x01; p[23] = (uint8_t)(U & 0xFF);
    t_art.deferred_new = 0;
    CHECK(artnet_handle(&t_art, p, 24, t_now, reply, sizeof reply) == 0 && t_art.deferred_new && t_rdm.discovering);
    CHECK(artnet_poll(&t_art, reply, sizeof reply) == 0);                // not yet
    settle(&t_rdm);
    n = artnet_poll(&t_art, reply, sizeof reply);
    CHECK(n == 28 + 6 * 5 && reply[9] == 0x81 && t_rdm.discoveries == 2);
    CHECK(artnet_poll(&t_art, reply, sizeof reply) == 0);

    TEST("Art-Net: ArtRdm reaches the light, and its answer goes back as ArtRdm, without the start code");
    art_header(p, 0x8300);
    p[12] = 1;                                                           // RDM version
    p[21] = 1; p[22] = 0; p[23] = (uint8_t)(U & 0xFF);
    static const uint8_t controller[6] = {0x41, 0x4C, 0, 0, 0, 7};       // another controller's ID
    uint8_t full[RDM_MAX_PKT];
    int fl = rdm_build(full, lights[3].uid, controller, 0x21, 1, 0, 0, RDM_CC_GET, 0x0082, NULL, 0);
    memcpy(p + 24, full + 1, (size_t)(fl - 1));
    t_art.deferred_new = 0;
    CHECK(artnet_handle(&t_art, p, 24 + fl - 1, t_now, reply, sizeof reply) == 0 && t_art.deferred_new);
    CHECK(t_rdm.req_owner == RDM_OWNER_ARTNET);
    settle(&t_rdm);
    CHECK(memcmp(line_last + 9, controller, 6) == 0 && line_last[15] == 0x21);   // went out as it came
    n = artnet_poll(&t_art, reply, sizeof reply);
    rdm_answer_t a;
    uint8_t back[RDM_MAX_PKT + 1] = {0xCC};
    memcpy(back + 1, reply + 24, (size_t)(n - 24));
    CHECK(n > 24 && reply[9] == 0x83 && reply[21] == 1 && reply[23] == (U & 0xFF) && reply[24] == 0x01);
    CHECK(rdm_parse(back, n - 24 + 1, &a) && a.cc == RDM_CC_GET_RESPONSE && a.pid == 0x0082 &&
          memcmp(back + 3, controller, 6) == 0 && back[15] == 0x21);     // to the controller, same transaction number
    p[23] = 0x77;                                                        // another port: not ours
    CHECK(artnet_handle(&t_art, p, 24 + fl - 1, t_now, reply, sizeof reply) == 0 && t_rdm.req_owner == RDM_OWNER_NONE);
}

// ================================================================ 5. MIDI
static midi_parser_t mp;
static uint8_t packets[64][4];
static int     n_packets;

static void feed(const uint8_t *bytes, int n) {
    n_packets = 0;
    for (int i = 0; i < n; i++)
        if (midi_parse(&mp, bytes[i], packets[n_packets]) && n_packets < 63) n_packets++;
}

static bool packet_is(int i, uint8_t cin, uint8_t a, uint8_t b, uint8_t c) {
    return i < n_packets && packets[i][0] == cin && packets[i][1] == a && packets[i][2] == b && packets[i][3] == c;
}

static void test_midi(void) {
    midi_parser_init(&mp);

    TEST("MIDI: note on, then running status, with a clock tick in the middle of a message");
    static const uint8_t notes[] = {0x90, 60, 100, 62, 0xF8, 90, 0x80, 60, 0};
    feed(notes, sizeof notes);
    CHECK(n_packets == 4 && packet_is(0, 0x09, 0x90, 60, 100) && packet_is(1, 0x0F, 0xF8, 0, 0) &&
          packet_is(2, 0x09, 0x90, 62, 90) && packet_is(3, 0x08, 0x80, 60, 0));

    TEST("MIDI: program change and channel pressure have one data byte, also with running status");
    static const uint8_t pc[] = {0xC3, 5, 6, 0xD0, 40};
    feed(pc, sizeof pc);
    CHECK(n_packets == 3 && packet_is(0, 0x0C, 0xC3, 5, 0) && packet_is(1, 0x0C, 0xC3, 6, 0) &&
          packet_is(2, 0x0D, 0xD0, 40, 0));

    TEST("MIDI: SysEx of every length is cut into packets of three, the end says how many");
    for (int len = 2; len <= 9; len++) {
        uint8_t sx[16] = {0xF0};
        for (int i = 1; i < len - 1; i++) sx[i] = (uint8_t)i;
        sx[len - 1] = 0xF7;
        feed(sx, len);
        int whole = (len - 1) / 3, rest = len - 3 * whole;               // packets of three, then the end with 1 to 3
        CHECK(n_packets == whole + 1 && packets[whole][0] == 0x04 + rest);
        uint8_t back[16];
        int k = 0;
        for (int i = 0; i < n_packets; i++) k += midi_packet_bytes(packets[i], back + k);
        CHECK(k == len && memcmp(back, sx, (size_t)len) == 0);           // and back to the same bytes
    }

    TEST("MIDI: system common messages, and bytes without a status are dropped");
    static const uint8_t common[] = {0xF1, 0x23, 0xF2, 1, 2, 0xF3, 7, 0xF6, 55, 0xF7};
    feed(common, sizeof common);
    CHECK(n_packets == 4 && packet_is(0, 0x02, 0xF1, 0x23, 0) && packet_is(1, 0x03, 0xF2, 1, 2) &&
          packet_is(2, 0x02, 0xF3, 7, 0) && packet_is(3, 0x05, 0xF6, 0, 0));

    TEST("MIDI: a status byte in the middle of a SysEx ends it");
    static const uint8_t cut[] = {0xF0, 1, 2, 3, 4, 0x90, 1, 2};
    feed(cut, sizeof cut);
    CHECK(n_packets == 2 && packet_is(0, 0x04, 0xF0, 1, 2) && packet_is(1, 0x09, 0x90, 1, 2));

    TEST("MIDI: packets from the computer give the bytes their kind has");
    uint8_t out[3];
    static const uint8_t sizes[16] = {0, 0, 2, 3, 3, 1, 2, 3, 3, 3, 3, 3, 2, 2, 3, 1};
    for (int cin = 0; cin < 16; cin++) {
        uint8_t pkt[4] = {(uint8_t)cin, 0x90, 1, 2};
        CHECK(midi_packet_bytes(pkt, out) == sizes[cin]);
    }
    uint8_t cable1[4] = {0x19, 0x91, 64, 1};                             // cable 1: the kind is in the low four bits only
    CHECK(midi_packet_bytes(cable1, out) == 3 && out[0] == 0x91 && out[2] == 1);
}

// ================================================================ 6. DMX IN
static void bytes_in(dmx_in_t *in, const uint8_t *bytes, int n, uint32_t now) {
    for (int i = 0; i < n; i++) dmx_in_byte(in, bytes[i], false, now);
}

static void test_dmx_in(void) {
    static dmx_in_t in;
    static uint8_t  got[DMX_IN_SLOTS];
    uint16_t count = 99;
    uint32_t when = 99;
    dmx_in_init(&in);

    TEST("DMX IN: nothing before the first break; a frame from a break to the next");
    static const uint8_t noise[] = {0x00, 0x55, 0x10};
    bytes_in(&in, noise, 3, 10);
    CHECK(dmx_in_read(&in, got, &count, &when) == 0 && count == 0 && when == 0);
    dmx_in_byte(&in, 0, true, 20);                                       // a break
    static const uint8_t three[] = {0x00, 10, 20, 30};                   // start code 0, three levels
    bytes_in(&in, three, 4, 21);
    CHECK(dmx_in_read(&in, got, &count, &when) == 0);                    // complete only at the next break
    dmx_in_byte(&in, 0, true, 45);
    CHECK(dmx_in_read(&in, got, &count, &when) == 1 && count == 3 && got[0] == 10 && got[2] == 30 && when == 45);

    TEST("DMX IN: another start code (RDM, text) is passed over; a break right after a break is no frame");
    static const uint8_t rdm[] = {0xCC, 0x01, 0x18, 1, 2, 3};
    bytes_in(&in, rdm, 6, 50);
    dmx_in_byte(&in, 0, true, 60);
    dmx_in_byte(&in, 0, true, 61);
    CHECK(in.frames == 1 && in.spoiled == 0);

    TEST("DMX IN: 512 levels make a frame at once; what follows waits for the next break");
    dmx_in_byte(&in, 0x00, false, 70);                                   // start code
    for (int k = 1; k <= DMX_IN_SLOTS; k++) dmx_in_byte(&in, (uint8_t)k, false, 70);
    CHECK(dmx_in_read(&in, got, &count, &when) == 2 && count == 512 && got[0] == 1 && got[510] == 255 && got[511] == 0);
    dmx_in_byte(&in, 0x77, false, 80);
    dmx_in_byte(&in, 0, true, 90);
    CHECK(in.frames == 2);

    TEST("DMX IN: a framing error inside a frame, or bytes the receiver lost, spoil it");
    static const uint8_t two[] = {0x00, 5};
    bytes_in(&in, two, 2, 100);
    dmx_in_byte(&in, 0x40, true, 100);                                   // a broken byte
    dmx_in_byte(&in, 6, false, 100);
    dmx_in_byte(&in, 0, true, 110);
    CHECK(in.frames == 2 && in.spoiled == 1);
    bytes_in(&in, two, 2, 120);
    dmx_in_lost(&in);
    dmx_in_byte(&in, 0, true, 130);
    CHECK(in.frames == 2 && in.spoiled == 2);

    TEST("DMX IN over DX: the last frame once, live while frames come; a box without DMX IN says so");
    static ctrl_t c;
    uint8_t req[64];
    static const uint8_t none[4] = {0, 0, 0, 0}, third[4] = {0, 0, 0, 3};
    ctrl_init(&c, &t_engine, &t_bank, 0);
    int n = ctrl_handle(&c, req, dx(req, CT_GET_INPUT, 0, 1, none, 4), 1000, resp, sizeof resp);
    CHECK(n == 13 && resp[12] == ST_NOT_SUPPORTED);
    c.dmx_in = &in;
    static const uint8_t levels[2] = {200, 100};
    dmx_in_frame(&in, levels, 2, 2000);                                  // frame 3, as the simulator's console gives it
    n = ctrl_handle(&c, req, dx(req, CT_GET_INPUT, 0, 2, none, 4), 2500, resp, sizeof resp);
    CHECK(n == 12 + 8 + 2 && resp[12] == ST_OK && resp[13] == 1 && resp[17] == 3 && resp[19] == 2 &&
          resp[20] == 200 && resp[21] == 100);
    n = ctrl_handle(&c, req, dx(req, CT_GET_INPUT, 0, 3, third, 4), 2600, resp, sizeof resp);
    CHECK(n == 12 + 8 && resp[13] == 1 && resp[17] == 3 && resp[19] == 0);   // the desk has it
    n = ctrl_handle(&c, req, dx(req, CT_GET_INPUT, 0, 4, third, 4), 3100, resp, sizeof resp);
    CHECK(n == 12 + 8 && resp[13] == 0);                                 // a second without a frame: quiet
    n = ctrl_handle(&c, req, dx(req, CT_GET_INPUT, 0, 5, third, 3), 3100, resp, sizeof resp);
    CHECK(n == 13 && resp[12] == ST_BAD_LENGTH);
}

int main(void) {
    test_packets();
    test_search();
    test_ctrl();
    test_artnet();
    test_midi();
    test_dmx_in();
    printf("%d checks, %d failed\n", checks, failures);
    return failures ? 1 : 0;
}
