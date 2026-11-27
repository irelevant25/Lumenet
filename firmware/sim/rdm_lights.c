#include "rdm_lights.h"
#include <string.h>
#include "rdm.h"

light_t lights[LIGHTS_MAX];
int     light_count;

enum {
    PID_SUPPORTED_PARAMETERS = 0x0050, PID_DEVICE_INFO = 0x0060, PID_DEVICE_MODEL_DESCRIPTION = 0x0080,
    PID_MANUFACTURER_LABEL = 0x0081, PID_DEVICE_LABEL = 0x0082, PID_SOFTWARE_VERSION_LABEL = 0x00C0,
    PID_DMX_PERSONALITY = 0x00E0, PID_DMX_PERSONALITY_DESCRIPTION = 0x00E1, PID_DMX_START_ADDRESS = 0x00F0,
    PID_IDENTIFY_DEVICE = 0x1000,
};
enum { NR_UNKNOWN_PID = 0x0000, NR_FORMAT_ERROR = 0x0001, NR_UNSUPPORTED_COMMAND_CLASS = 0x0005,
       NR_DATA_OUT_OF_RANGE = 0x0006, NR_SUB_DEVICE_OUT_OF_RANGE = 0x0009 };

static const struct {
    const char *model;
    uint16_t id, category;
    uint8_t modes;
    uint16_t footprint[3];
    const char *mode_name[3];
} KINDS[] = {
    {"LED Par RGBW", 0x0001, 0x0101, 2, {4, 8}, {"4 channels: R G B W", "8 channels: dimmer, R G B W, strobe, mode, speed"}},
    {"Moving head", 0x0002, 0x0102, 2, {16, 11}, {"16 channels", "11 channels, basic"}},
    {"Dimmer pack", 0x0003, 0x0500, 2, {4, 1}, {"4 channels", "1 channel for all four"}},
};

void lights_make(int n) {
    if (n > LIGHTS_MAX) n = LIGHTS_MAX;
    if (n < 0) n = 0;
    memset(lights, 0, sizeof lights);
    light_count = n;
    uint16_t next = 1;
    for (int i = 0; i < n; i++) {
        light_t *l = &lights[i];
        int k = i % 3;
        rdm_u64_to_uid(0x7FF100000000ull + (uint64_t)(i + 1), l->uid);
        l->maker = "Lumenet test";
        l->model = KINDS[k].model;
        l->model_id = KINDS[k].id;
        l->personalities = KINDS[k].modes;
        l->personality = 1;
        for (int m = 0; m < 3; m++) {
            l->footprint[m] = KINDS[k].footprint[m];
            l->mode_name[m] = KINDS[k].mode_name[m];
        }
        l->start = next;
        next = (uint16_t)(next + l->footprint[0]);
        if (next > 512) next = 1;
    }
}

static int reply(light_t *l, const uint8_t *req, uint8_t type, const uint8_t *pd, uint8_t pdl, uint8_t *out) {
    uint16_t sub = (uint16_t)((req[18] << 8) | req[19]);
    uint16_t pid = (uint16_t)((req[21] << 8) | req[22]);
    return rdm_build(out, req + 9, l->uid, req[15], type, 0, sub, (uint8_t)(req[20] + 1), pid, pd, pdl);
}

static int nack(light_t *l, const uint8_t *req, uint16_t reason, uint8_t *out) {
    uint8_t pd[2] = {(uint8_t)(reason >> 8), (uint8_t)reason};
    return reply(l, req, RDM_NACK_REASON, pd, 2, out);
}

static int text(light_t *l, const uint8_t *req, const char *s, uint8_t *out) {
    size_t n = strlen(s);
    if (n > 32) n = 32;
    return reply(l, req, RDM_ACK, (const uint8_t *)s, (uint8_t)n, out);
}

// One light's answer to a request addressed to it (or to everybody: then answer = false).
static int handle(light_t *l, const uint8_t *req, bool answer, uint8_t *out) {
    uint8_t cc = req[20];
    uint16_t pid = (uint16_t)((req[21] << 8) | req[22]);
    uint8_t pdl = req[23];
    const uint8_t *pd = req + 24;
    uint16_t sub = (uint16_t)((req[18] << 8) | req[19]);
    uint8_t buf[RDM_MAX_PD];
    int n = 0;
    if (cc == RDM_CC_DISCOVERY) {
        if (pid == RDM_PID_DISC_MUTE || pid == RDM_PID_DISC_UN_MUTE) {
            l->muted = pid == RDM_PID_DISC_MUTE;
            uint8_t control[2] = {0, 0};
            return answer ? reply(l, req, RDM_ACK, control, 2, out) : 0;
        }
        return 0;
    }
    if (sub != 0) return answer ? nack(l, req, NR_SUB_DEVICE_OUT_OF_RANGE, out) : 0;
    bool get = cc == RDM_CC_GET, set = cc == RDM_CC_SET;
    switch (pid) {
    case PID_SUPPORTED_PARAMETERS: {
        static const uint16_t list[] = {PID_DEVICE_MODEL_DESCRIPTION, PID_MANUFACTURER_LABEL, PID_DEVICE_LABEL,
                                        PID_DMX_PERSONALITY, PID_DMX_PERSONALITY_DESCRIPTION};
        if (!get) break;
        for (size_t i = 0; i < sizeof list / sizeof list[0]; i++) {
            buf[n++] = (uint8_t)(list[i] >> 8);
            buf[n++] = (uint8_t)list[i];
        }
        return answer ? reply(l, req, RDM_ACK, buf, (uint8_t)n, out) : 0;
    }
    case PID_DEVICE_INFO: {
        if (!get) break;
        uint16_t fp = l->footprint[l->personality - 1];
        uint8_t info[19] = {0x01, 0x00, (uint8_t)(l->model_id >> 8), (uint8_t)l->model_id,
                            (uint8_t)(KINDS[l->model_id - 1].category >> 8), (uint8_t)KINDS[l->model_id - 1].category,
                            0, 0, 0, 1,                      // software version 1
                            (uint8_t)(fp >> 8), (uint8_t)fp, l->personality, l->personalities,
                            (uint8_t)(l->start >> 8), (uint8_t)l->start, 0, 0, 0};
        return answer ? reply(l, req, RDM_ACK, info, 19, out) : 0;
    }
    case PID_DEVICE_MODEL_DESCRIPTION:
        if (!get) break;
        return answer ? text(l, req, l->model, out) : 0;
    case PID_MANUFACTURER_LABEL:
        if (!get) break;
        return answer ? text(l, req, l->maker, out) : 0;
    case PID_SOFTWARE_VERSION_LABEL:
        if (!get) break;
        return answer ? text(l, req, "1.0 (simulated)", out) : 0;
    case PID_DEVICE_LABEL:
        if (get) return answer ? text(l, req, l->label, out) : 0;
        if (set) {
            if (pdl > 32) return answer ? nack(l, req, NR_FORMAT_ERROR, out) : 0;
            memcpy(l->label, pd, pdl);
            l->label[pdl] = 0;
            return answer ? reply(l, req, RDM_ACK, NULL, 0, out) : 0;
        }
        break;
    case PID_DMX_START_ADDRESS:
        if (get) {
            uint8_t a[2] = {(uint8_t)(l->start >> 8), (uint8_t)l->start};
            return answer ? reply(l, req, RDM_ACK, a, 2, out) : 0;
        }
        if (set) {
            if (pdl != 2) return answer ? nack(l, req, NR_FORMAT_ERROR, out) : 0;
            uint16_t a = (uint16_t)((pd[0] << 8) | pd[1]);
            if (a < 1 || a > 512) return answer ? nack(l, req, NR_DATA_OUT_OF_RANGE, out) : 0;
            l->start = a;
            return answer ? reply(l, req, RDM_ACK, NULL, 0, out) : 0;
        }
        break;
    case PID_IDENTIFY_DEVICE:
        if (get) {
            uint8_t on = l->identify ? 1 : 0;
            return answer ? reply(l, req, RDM_ACK, &on, 1, out) : 0;
        }
        if (set) {
            if (pdl != 1 || pd[0] > 1) return answer ? nack(l, req, NR_FORMAT_ERROR, out) : 0;
            l->identify = pd[0] == 1;
            return answer ? reply(l, req, RDM_ACK, NULL, 0, out) : 0;
        }
        break;
    case PID_DMX_PERSONALITY:
        if (get) {
            uint8_t p[2] = {l->personality, l->personalities};
            return answer ? reply(l, req, RDM_ACK, p, 2, out) : 0;
        }
        if (set) {
            if (pdl != 1) return answer ? nack(l, req, NR_FORMAT_ERROR, out) : 0;
            if (pd[0] < 1 || pd[0] > l->personalities) return answer ? nack(l, req, NR_DATA_OUT_OF_RANGE, out) : 0;
            l->personality = pd[0];
            return answer ? reply(l, req, RDM_ACK, NULL, 0, out) : 0;
        }
        break;
    case PID_DMX_PERSONALITY_DESCRIPTION:
        if (get) {
            if (pdl != 1) return answer ? nack(l, req, NR_FORMAT_ERROR, out) : 0;
            uint8_t m = pd[0];
            if (m < 1 || m > l->personalities) return answer ? nack(l, req, NR_DATA_OUT_OF_RANGE, out) : 0;
            const char *name = l->mode_name[m - 1];
            size_t len = strlen(name);
            if (len > 32) len = 32;
            buf[0] = m;
            buf[1] = (uint8_t)(l->footprint[m - 1] >> 8);
            buf[2] = (uint8_t)l->footprint[m - 1];
            memcpy(buf + 3, name, len);
            return answer ? reply(l, req, RDM_ACK, buf, (uint8_t)(3 + len), out) : 0;
        }
        break;
    default:
        return answer ? nack(l, req, NR_UNKNOWN_PID, out) : 0;
    }
    return answer ? nack(l, req, NR_UNSUPPORTED_COMMAND_CLASS, out) : 0;
}

int lights_hear(const uint8_t *pkt, int len, uint8_t *out, int cap, bool *garbled) {
    *garbled = false;
    rdm_answer_t req;
    if (!rdm_parse(pkt, len, &req) || cap < RDM_MAX_PKT) return 0;
    const uint8_t *dest = pkt + 3;
    bool everybody = dest[2] == 0xFF && dest[3] == 0xFF && dest[4] == 0xFF && dest[5] == 0xFF;
    uint16_t pid = req.pid;

    if (req.cc == RDM_CC_DISCOVERY && pid == RDM_PID_DISC_UNIQUE_BRANCH) {
        if (req.pdl != 12) return 0;
        uint64_t lo = rdm_uid_to_u64(req.pd), hi = rdm_uid_to_u64(req.pd + 6);
        int answers = 0;
        uint8_t one[RDM_DISC_RESPONSE];
        memset(out, 0, RDM_DISC_RESPONSE);
        for (int i = 0; i < light_count; i++) {
            uint64_t u = rdm_uid_to_u64(lights[i].uid);
            if (lights[i].muted || u < lo || u > hi) continue;
            rdm_encode_disc(one, lights[i].uid);
            for (int k = 0; k < RDM_DISC_RESPONSE; k++) out[k] |= one[k];   // two answers at once overlap on the wire
            answers++;
        }
        if (answers > 1) *garbled = true;
        return answers ? RDM_DISC_RESPONSE : 0;
    }

    int n = 0;
    for (int i = 0; i < light_count; i++) {
        light_t *l = &lights[i];
        bool mine = memcmp(dest, l->uid, 6) == 0;
        bool maker_all = everybody && (dest[0] == 0xFF || (dest[0] == l->uid[0] && dest[1] == l->uid[1]));
        if (!mine && !maker_all) continue;
        int r = handle(l, pkt, mine, out);
        if (mine) n = r;
    }
    return n;
}
