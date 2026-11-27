#include "rdm.h"
#include <string.h>
#include "platform.h"

#define UID_LAST   0xFFFFFFFFFFFEull   // FFFF:FFFFFFFF is "everybody", no light has it
#define NOT_FOUND  UINT64_MAX
#define LINE_DISCOVERY 0
#define LINE_REQUEST   1
// The platform ends every transaction by itself (2.8 ms without an answer is "no
// answer"); this only frees a line that never reports back.
#define LINE_GUARD_MS 500

enum { STEP_UNMUTE, STEP_BRANCH, STEP_MUTE };

static const uint8_t EVERYBODY[6] = {0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF};
static uint8_t line_raw[RDM_MAX_PKT];

// ---------------------------------------------------------------- packets
uint16_t rdm_checksum(const uint8_t *p, int n) {
    uint16_t sum = 0;
    for (int i = 0; i < n; i++) sum = (uint16_t)(sum + p[i]);
    return sum;
}

uint64_t rdm_uid_to_u64(const uint8_t uid[6]) {
    uint64_t v = 0;
    for (int i = 0; i < 6; i++) v = (v << 8) | uid[i];
    return v;
}

void rdm_u64_to_uid(uint64_t v, uint8_t uid[6]) {
    for (int i = 5; i >= 0; i--) { uid[i] = (uint8_t)v; v >>= 8; }
}

int rdm_build(uint8_t *out, const uint8_t dest[6], const uint8_t src[6], uint8_t tn, uint8_t port_or_type,
              uint8_t msg_count, uint16_t sub_device, uint8_t cc, uint16_t pid, const uint8_t *pd, uint8_t pdl) {
    if (pdl > RDM_MAX_PD) pdl = RDM_MAX_PD;
    out[0] = RDM_START_CODE;
    out[1] = RDM_SUB_START_CODE;
    out[2] = (uint8_t)(24 + pdl);                  // message length: start code to the end of the data
    memcpy(out + 3, dest, 6);
    memcpy(out + 9, src, 6);
    out[15] = tn;
    out[16] = port_or_type;
    out[17] = msg_count;
    out[18] = (uint8_t)(sub_device >> 8);
    out[19] = (uint8_t)sub_device;
    out[20] = cc;
    out[21] = (uint8_t)(pid >> 8);
    out[22] = (uint8_t)pid;
    out[23] = pdl;
    if (pdl) memcpy(out + 24, pd, pdl);
    uint16_t cs = rdm_checksum(out, 24 + pdl);
    out[24 + pdl] = (uint8_t)(cs >> 8);
    out[25 + pdl] = (uint8_t)cs;
    return 26 + pdl;
}

bool rdm_parse(const uint8_t *p, int n, rdm_answer_t *a) {
    int i = 0;
    while (i < n && p[i] != RDM_START_CODE) i++;   // a break before the packet reads as zero bytes
    p += i;
    n -= i;
    if (n < 26 || p[1] != RDM_SUB_START_CODE) return false;
    int len = p[2];
    if (len < 24 || n < len + 2) return false;
    if (rdm_checksum(p, len) != (uint16_t)((p[len] << 8) | p[len + 1])) return false;
    if (p[23] != len - 24) return false;
    a->response_type = p[16];
    a->cc = p[20];
    a->pid = (uint16_t)((p[21] << 8) | p[22]);
    a->pdl = p[23];
    memcpy(a->pd, p + 24, a->pdl);
    memcpy(a->src, p + 9, 6);
    return true;
}

// The answer to DISC_UNIQUE_BRANCH has no break and no start code: up to seven
// FE, then AA, then the ID and its checksum, every byte sent twice (b | AA, b | 55),
// so that a collision of two answers spoils the pattern.
bool rdm_decode_disc(const uint8_t *p, int n, uint8_t uid[6]) {
    int i = 0;
    while (i < n && i < 7 && p[i] == 0xFE) i++;
    if (i >= n || p[i] != 0xAA) return false;
    i++;
    if (n - i < 16) return false;
    const uint8_t *e = p + i;
    for (int k = 0; k < 16; k++) {
        uint8_t mask = (k & 1) ? 0x55 : 0xAA;
        if ((e[k] & mask) != mask) return false;
    }
    for (int k = 0; k < 6; k++) uid[k] = (uint8_t)(e[2 * k] & e[2 * k + 1]);
    uint16_t sum = rdm_checksum(e, 12);
    uint16_t cs = (uint16_t)(((e[12] & e[13]) << 8) | (e[14] & e[15]));
    return cs == sum;
}

int rdm_encode_disc(uint8_t *out, const uint8_t uid[6]) {
    int i = 0;
    for (int k = 0; k < 7; k++) out[i++] = 0xFE;
    out[i++] = 0xAA;
    for (int k = 0; k < 6; k++) {
        out[i++] = uid[k] | 0xAA;
        out[i++] = uid[k] | 0x55;
    }
    uint16_t sum = rdm_checksum(out + 8, 12);
    out[i++] = (uint8_t)((sum >> 8) | 0xAA);
    out[i++] = (uint8_t)((sum >> 8) | 0x55);
    out[i++] = (uint8_t)((sum & 0xFF) | 0xAA);
    out[i++] = (uint8_t)((sum & 0xFF) | 0x55);
    return i;
}

static bool is_broadcast(const uint8_t dest[6]) {
    return dest[2] == 0xFF && dest[3] == 0xFF && dest[4] == 0xFF && dest[5] == 0xFF;   // all, or all of one maker
}

// ---------------------------------------------------------------- the engine
void rdm_init(rdm_t *r, const uint8_t own_uid[6]) {
    memset(r, 0, sizeof *r);
    memcpy(r->own_uid, own_uid, 6);
    r->found = NOT_FOUND;
}

static void push(rdm_t *r, uint64_t lo, uint64_t hi) {
    if (r->sp < (int)(sizeof r->stack_lo / sizeof r->stack_lo[0])) {
        r->stack_lo[r->sp] = lo;
        r->stack_hi[r->sp] = hi;
        r->sp++;
    }
}

void rdm_start_discovery(rdm_t *r) {
    r->tod_count = 0;
    r->tod_overflow = false;
    r->sp = 0;
    push(r, 0, UID_LAST);
    r->found = NOT_FOUND;
    r->unmute_left = 2;              // twice: a light busy with something else may miss the first
    r->discovering = true;
}

static void send(rdm_t *r, uint8_t line_for, uint8_t expect, const uint8_t *pkt, int len, uint32_t now) {
    r->line_busy = true;
    r->line_for = line_for;
    r->line_expect = expect;
    r->line_started_ms = now;
    plat_rdm_send(pkt, len, expect);
}

static void disc_next(rdm_t *r, uint32_t now) {
    uint8_t pkt[RDM_MAX_PKT];
    int len;
    if (r->unmute_left) {
        r->unmute_left--;
        r->disc_step = STEP_UNMUTE;
        len = rdm_build(pkt, EVERYBODY, r->own_uid, r->tn++, 1, 0, 0, RDM_CC_DISCOVERY, RDM_PID_DISC_UN_MUTE, NULL, 0);
        send(r, LINE_DISCOVERY, RDM_EXPECT_NONE, pkt, len, now);
        return;
    }
    if (r->found != NOT_FOUND) {     // the one that answered alone: tell it to be quiet from now on
        uint8_t uid[6];
        rdm_u64_to_uid(r->found, uid);
        r->disc_step = STEP_MUTE;
        len = rdm_build(pkt, uid, r->own_uid, r->tn++, 1, 0, 0, RDM_CC_DISCOVERY, RDM_PID_DISC_MUTE, NULL, 0);
        send(r, LINE_DISCOVERY, RDM_EXPECT_RESPONSE, pkt, len, now);
        return;
    }
    if (r->sp == 0) {                // nothing left to search
        r->discovering = false;
        r->discoveries++;
        return;
    }
    uint8_t pd[12];
    rdm_u64_to_uid(r->stack_lo[r->sp - 1], pd);
    rdm_u64_to_uid(r->stack_hi[r->sp - 1], pd + 6);
    r->disc_step = STEP_BRANCH;
    len = rdm_build(pkt, EVERYBODY, r->own_uid, r->tn++, 1, 0, 0, RDM_CC_DISCOVERY, RDM_PID_DISC_UNIQUE_BRANCH, pd, 12);
    send(r, LINE_DISCOVERY, RDM_EXPECT_DISCOVERY, pkt, len, now);
}

static void split_top(rdm_t *r) {
    uint64_t lo = r->stack_lo[r->sp - 1], hi = r->stack_hi[r->sp - 1];
    r->sp--;
    if (lo < hi) {
        uint64_t mid = lo + (hi - lo) / 2;
        push(r, mid + 1, hi);
        push(r, lo, mid);
    }
}

static void add_to_tod(rdm_t *r, uint64_t u) {
    uint8_t uid[6];
    rdm_u64_to_uid(u, uid);
    for (int i = 0; i < r->tod_count; i++)
        if (memcmp(r->tod[i], uid, 6) == 0) return;
    if (r->tod_count < RDM_TOD_MAX) memcpy(r->tod[r->tod_count++], uid, 6);
    else r->tod_overflow = true;
}

static void disc_result(rdm_t *r, const uint8_t *raw, int n, bool garbled) {
    r->disc_transactions++;
    if (r->disc_step == STEP_UNMUTE) return;
    if (r->disc_step == STEP_BRANCH) {
        if (r->sp == 0) return;
        if (n == 0) {                // nobody in this range
            r->sp--;
            return;
        }
        uint8_t uid[6];
        if (!garbled && rdm_decode_disc(raw, n, uid)) {
            uint64_t u = rdm_uid_to_u64(uid);
            if (u >= r->stack_lo[r->sp - 1] && u <= r->stack_hi[r->sp - 1]) {
                r->found = u;        // the range stays: once this one is quiet, others may answer
                r->mute_tries = 0;
                return;
            }
        }
        r->disc_collisions++;        // several answered at once: search both halves
        split_top(r);
        return;
    }
    // STEP_MUTE
    rdm_answer_t a;
    if (n > 0 && rdm_parse(raw, n, &a) && a.cc == RDM_CC_DISCOVERY_RESPONSE && a.pid == RDM_PID_DISC_MUTE &&
        rdm_uid_to_u64(a.src) == r->found) {
        add_to_tod(r, r->found);
        r->found = NOT_FOUND;
    } else if (++r->mute_tries >= 3) {
        // It does not answer by name: the ID was made up by two answers that
        // happened to fit together. Search the halves instead.
        r->found = NOT_FOUND;
        if (r->sp) split_top(r);
    }
}

static void request_result(rdm_t *r, const uint8_t *raw, int n) {
    r->req_done = true;
    r->resp_raw_len = 0;
    if (n == 0) {
        r->req_status = r->line_expect == RDM_EXPECT_NONE ? RDM_DONE_OK : RDM_DONE_TIMEOUT;
        return;
    }
    if (!rdm_parse(raw, n, &r->answer)) {
        r->req_status = RDM_DONE_BAD;
        return;
    }
    int i = 0;
    while (i < n && raw[i] != RDM_START_CODE) i++;
    r->resp_raw_len = raw[i + 2] + 2;
    memcpy(r->resp_raw, raw + i, (size_t)r->resp_raw_len);
    r->req_status = RDM_DONE_OK;
}

void rdm_poll(rdm_t *r, uint32_t now) {
    if (r->line_busy) {
        int n = 0;
        bool garbled = false;
        if (!plat_rdm_poll(line_raw, (int)sizeof line_raw, &n, &garbled)) {
            if ((uint32_t)(now - r->line_started_ms) < LINE_GUARD_MS) return;
            n = 0;                   // the line never reported back: count it as no answer
        }
        r->line_busy = false;
        if (r->line_for == LINE_REQUEST) request_result(r, line_raw, n);
        else disc_result(r, line_raw, n, garbled);
    }
    if (r->req_owner != RDM_OWNER_NONE && !r->req_sent) {      // requests go before the search
        r->req_sent = true;
        uint8_t expect = is_broadcast(r->req + 3) ? RDM_EXPECT_NONE : RDM_EXPECT_RESPONSE;
        if (r->req[20] == RDM_CC_DISCOVERY && ((r->req[21] << 8) | r->req[22]) == RDM_PID_DISC_UNIQUE_BRANCH)
            expect = RDM_EXPECT_DISCOVERY;
        send(r, LINE_REQUEST, expect, r->req, r->req_len, now);
        return;
    }
    if (r->discovering) disc_next(r, now);
}

bool rdm_submit(rdm_t *r, uint8_t owner, const uint8_t dest[6], uint16_t sub_device, uint8_t cc, uint16_t pid,
                const uint8_t *pd, uint8_t pdl) {
    if (r->req_owner != RDM_OWNER_NONE || pdl > RDM_MAX_PD) return false;
    r->req_len = rdm_build(r->req, dest, r->own_uid, r->tn++, 1, 0, sub_device, cc, pid, pd, pdl);
    r->req_owner = owner;
    r->req_sent = false;
    r->req_done = false;
    return true;
}

bool rdm_submit_raw(rdm_t *r, uint8_t owner, const uint8_t *pkt, int len) {
    if (r->req_owner != RDM_OWNER_NONE || len < 23 || pkt[0] != RDM_SUB_START_CODE) return false;
    int msg_len = pkt[1];            // from the start code on, without the checksum
    if (msg_len < 24 || msg_len - 1 > len || msg_len + 2 > (int)sizeof r->req) return false;
    r->req[0] = RDM_START_CODE;
    memcpy(r->req + 1, pkt, (size_t)(msg_len - 1));
    uint16_t cs = rdm_checksum(r->req, msg_len);
    r->req[msg_len] = (uint8_t)(cs >> 8);
    r->req[msg_len + 1] = (uint8_t)cs;
    r->req_len = msg_len + 2;
    r->req_owner = owner;
    r->req_sent = false;
    r->req_done = false;
    return true;
}

bool rdm_take(rdm_t *r, uint8_t owner, uint8_t *status, const rdm_answer_t **answer, const uint8_t **raw,
              int *raw_len) {
    if (r->req_owner != owner || !r->req_done) return false;
    *status = r->req_status;
    if (answer) *answer = &r->answer;
    if (raw) *raw = r->resp_raw;
    if (raw_len) *raw_len = r->resp_raw_len;
    r->req_owner = RDM_OWNER_NONE;
    r->req_done = false;
    r->req_sent = false;
    return true;
}
