#include "ctrl.h"
#include <string.h>
#include "platform.h"

void ctrl_init(ctrl_t *c, engine_t *eng, preset_bank_t *bank, uint32_t now) {
    memset(c, 0, sizeof *c);
    c->eng = eng;
    c->bank = bank;
    c->boot_ms = now;
}

uint8_t ctrl_preset_go(ctrl_t *c, uint8_t slot, uint32_t dur_ms, uint32_t now) {
    if (slot >= PRESET_SLOTS) return ST_BAD_ARG;
    if (!(c->bank->valid & (1u << slot))) return ST_EMPTY_SLOT;
    plat_lock();
    engine_fade(c->eng, now, 0, DMX_CHANNELS, c->bank->data[slot], dur_ms);
    plat_unlock();
    return ST_OK;
}

void ctrl_toggle_blackout(ctrl_t *c) {
    plat_lock();
    c->eng->blackout = !c->eng->blackout;
    plat_unlock();
}

// Range check for 1-based [start, start+count).
static bool range_ok(uint16_t start, uint16_t count) {
    return start >= 1 && count >= 1 && (uint32_t)start - 1 + count <= DMX_CHANNELS;
}

// Execute one request. Writes status + data into out (out[0] = status),
// returns number of bytes written.
static int execute(ctrl_t *c, uint8_t type, const uint8_t *p, uint16_t plen,
                   uint32_t now, uint8_t *out) {
    engine_t *e = c->eng;
    out[0] = ST_OK;

    switch (type) {
    case CT_PING: {
        plat_lock();
        uint8_t flags = (engine_artnet_live(e, now) ? 1 : 0) | (e->blackout ? 2 : 0) |
                        (e->merge_mode == MERGE_ARTNET_OVERRIDE ? 4 : 0) |
                        (e->artnet_has_data ? 8 : 0);
        uint8_t master = e->master;
        plat_unlock();
        out[1] = FW_VERSION_MAJOR;
        out[2] = FW_VERSION_MINOR;
        out[3] = flags;
        out[4] = master;
        wr_be32(out + 5, now - c->boot_ms);
        wr_be16(out + 9, (uint16_t)c->bank->valid);
        wr_be16(out + 11, DMX_CHANNELS);
        return 13;
    }
    case CT_SET:
    case CT_FADE: {
        uint16_t hdr = type == CT_SET ? 4 : 8;
        if (plen < hdr) { out[0] = ST_BAD_LENGTH; return 1; }
        uint16_t start = rd_be16(p), count = rd_be16(p + 2);
        uint32_t dur = type == CT_SET ? 0 : rd_be32(p + 4);
        if (plen != hdr + count) { out[0] = ST_BAD_LENGTH; return 1; }
        if (!range_ok(start, count)) { out[0] = ST_BAD_ARG; return 1; }
        plat_lock();
        engine_fade(e, now, (uint16_t)(start - 1), count, p + hdr, dur);
        plat_unlock();
        return 1;
    }
    case CT_PRESET_SAVE: {
        if (plen != 1) { out[0] = ST_BAD_LENGTH; return 1; }
        uint8_t slot = p[0];
        if (slot >= PRESET_SLOTS) { out[0] = ST_BAD_ARG; return 1; }
        plat_lock();
        engine_render(e, now, c->bank->data[slot], false);
        plat_unlock();
        c->bank->valid |= 1u << slot;
        presets_seal(c->bank);
        if (!plat_save_presets(c->bank)) out[0] = ST_STORAGE_ERROR;
        return 1;
    }
    case CT_PRESET_GO:
        if (plen != 5) { out[0] = ST_BAD_LENGTH; return 1; }
        out[0] = ctrl_preset_go(c, p[0], rd_be32(p + 1), now);
        return 1;
    case CT_PRESET_CLEAR: {
        if (plen != 1) { out[0] = ST_BAD_LENGTH; return 1; }
        if (p[0] >= PRESET_SLOTS) { out[0] = ST_BAD_ARG; return 1; }
        c->bank->valid &= ~(1u << p[0]);
        memset(c->bank->data[p[0]], 0, DMX_CHANNELS);
        presets_seal(c->bank);
        if (!plat_save_presets(c->bank)) out[0] = ST_STORAGE_ERROR;
        return 1;
    }
    case CT_MASTER:
    case CT_BLACKOUT:
    case CT_MERGE_MODE:
        if (plen != 1) { out[0] = ST_BAD_LENGTH; return 1; }
        if (type == CT_MERGE_MODE && p[0] > MERGE_ARTNET_OVERRIDE) { out[0] = ST_BAD_ARG; return 1; }
        plat_lock();
        if (type == CT_MASTER) e->master = p[0];
        else if (type == CT_BLACKOUT) e->blackout = p[0] != 0;
        else e->merge_mode = p[0];
        plat_unlock();
        return 1;
    case CT_RELEASE_ARTNET:
        plat_lock();
        engine_release_artnet(e);
        plat_unlock();
        return 1;
    case CT_GET_OUTPUT: {
        if (plen != 4) { out[0] = ST_BAD_LENGTH; return 1; }
        uint16_t start = rd_be16(p), count = rd_be16(p + 2);
        if (!range_ok(start, count)) { out[0] = ST_BAD_ARG; return 1; }
        static uint8_t frame[DMX_CHANNELS];
        plat_lock();
        engine_render(e, now, frame, true);
        plat_unlock();
        memcpy(out + 1, frame + start - 1, count);
        return 1 + count;
    }
    case CT_GET_PRESET:
        if (plen != 1) { out[0] = ST_BAD_LENGTH; return 1; }
        if (p[0] >= PRESET_SLOTS) { out[0] = ST_BAD_ARG; return 1; }
        if (!(c->bank->valid & (1u << p[0]))) { out[0] = ST_EMPTY_SLOT; return 1; }
        memcpy(out + 1, c->bank->data[p[0]], DMX_CHANNELS);
        return 1 + DMX_CHANNELS;
    default:
        out[0] = ST_UNKNOWN_TYPE;
        return 1;
    }
}

static ctrl_session_t *find_session(ctrl_t *c, uint32_t session) {
    for (int i = 0; i < CTRL_SESSIONS; i++)
        if (c->sess[i].used && c->sess[i].session == session) return &c->sess[i];
    return NULL;
}

static ctrl_session_t *alloc_session(ctrl_t *c, uint32_t session, uint32_t now) {
    ctrl_session_t *victim = &c->sess[0];
    for (int i = 0; i < CTRL_SESSIONS; i++) {
        ctrl_session_t *s = &c->sess[i];
        if (!s->used) { victim = s; break; }
        if ((uint32_t)(now - s->last_ms) > (uint32_t)(now - victim->last_ms)) victim = s;  // LRU
    }
    victim->used = 1;
    victim->session = session;
    victim->resp_len = 0;
    return victim;
}

int ctrl_handle(ctrl_t *c, const uint8_t *pkt, int len, uint32_t now,
                uint8_t *resp, int resp_cap) {
    if (len < CTRL_HDR_LEN || pkt[0] != CTRL_MAGIC0 || pkt[1] != CTRL_MAGIC1 ||
        pkt[2] != CTRL_VERSION || resp_cap < CTRL_MAX_PKT)
        return 0;
    c->rx_packets++;
    uint8_t  type    = pkt[3];
    uint32_t session = rd_be32(pkt + 4);
    uint16_t seq     = rd_be16(pkt + 8);
    uint16_t plen    = rd_be16(pkt + 10);
    if (type & 0x80) return 0;  // never answer a response

    // Reply header mirrors the request's session + seq.
    resp[0] = CTRL_MAGIC0; resp[1] = CTRL_MAGIC1; resp[2] = CTRL_VERSION; resp[3] = CT_ACK;
    memcpy(resp + 4, pkt + 4, 6);

    if (plen != len - CTRL_HDR_LEN) {        // truncated / garbage: answer, don't remember
        resp[CTRL_HDR_LEN] = ST_BAD_LENGTH;
        wr_be16(resp + 10, 1);
        return CTRL_HDR_LEN + 1;
    }

    ctrl_session_t *s = NULL;
    if (session != 0) {                      // session 0 = fire-and-forget, no dedupe
        s = find_session(c, session);
        if (s) {
            int16_t d = (int16_t)(uint16_t)(seq - s->last_seq);
            if (d == 0 && s->resp_len) {     // retransmit: replay cached ACK
                c->duplicates++;
                s->last_ms = now;
                memcpy(resp, s->resp, s->resp_len);
                return s->resp_len;
            }
            if (d < 0) { c->stale++; return 0; }  // old packet wandering the network
        } else {
            s = alloc_session(c, session, now);
        }
    }

    int n = execute(c, type, pkt + CTRL_HDR_LEN, plen, now, resp + CTRL_HDR_LEN);
    c->executed++;
    wr_be16(resp + 10, (uint16_t)n);
    int total = CTRL_HDR_LEN + n;

    if (s) {
        s->last_seq = seq;
        s->last_ms = now;
        s->resp_len = (uint16_t)total;
        memcpy(s->resp, resp, (size_t)total);
    }
    return total;
}
