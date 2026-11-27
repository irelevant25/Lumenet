#include "midi_parse.h"
#include <string.h>

// Code index numbers: the low four bits of a packet's first byte tell what it carries
enum {
    CIN_COMMON_2 = 0x2,     // two-byte system common (F1, F3)
    CIN_COMMON_3 = 0x3,     // three-byte system common (F2)
    CIN_SYSEX = 0x4,        // SysEx starts or goes on: three bytes
    CIN_END_1 = 0x5,        // SysEx ends with one byte, or a one-byte system common (F6)
    CIN_END_2 = 0x6,
    CIN_END_3 = 0x7,
    CIN_BYTE = 0xF,         // a single byte (real time)
};

void midi_parser_init(midi_parser_t *p) { memset(p, 0, sizeof *p); }

static bool packet(uint8_t pkt[4], uint8_t cin, uint8_t a, uint8_t b, uint8_t c) {
    pkt[0] = cin;           // cable 0 in the high four bits
    pkt[1] = a;
    pkt[2] = b;
    pkt[3] = c;
    return true;
}

bool midi_parse(midi_parser_t *p, uint8_t b, uint8_t pkt[4]) {
    if (b >= 0xF8) return packet(pkt, CIN_BYTE, b, 0, 0);   // real time: anywhere, changes nothing

    if (b == 0xF0) {                                         // SysEx begins
        p->sysex = true;
        p->sx[0] = b;
        p->sxn = 1;
        p->status = 0;
        return false;
    }
    if (b == 0xF7) {                                         // SysEx ends
        if (!p->sysex) return false;
        p->sx[p->sxn++] = b;
        p->sysex = false;
        uint8_t n = p->sxn;
        p->sxn = 0;
        return packet(pkt, (uint8_t)(CIN_END_1 + n - 1), p->sx[0], n > 1 ? p->sx[1] : 0, n > 2 ? p->sx[2] : 0);
    }
    if (b & 0x80) {                                          // any other status byte
        p->sysex = false;                                    // ends a SysEx without F7: what is left is dropped
        p->sxn = 0;
        p->count = 0;
        if (b == 0xF6) {                                     // tune request: one byte
            p->status = 0;
            return packet(pkt, CIN_END_1, b, 0, 0);
        }
        if (b == 0xF4 || b == 0xF5) {                        // not defined
            p->status = 0;
            return false;
        }
        p->status = b;
        uint8_t kind = b & 0xF0;
        p->need = (kind == 0xC0 || kind == 0xD0 || b == 0xF1 || b == 0xF3) ? 1 : 2;
        return false;
    }

    // a data byte
    if (p->sysex) {
        p->sx[p->sxn++] = b;
        if (p->sxn < 3) return false;
        p->sxn = 0;
        return packet(pkt, CIN_SYSEX, p->sx[0], p->sx[1], p->sx[2]);
    }
    if (!p->status) return false;                            // no status to go with it
    p->data[p->count++] = b;
    if (p->count < p->need) return false;
    p->count = 0;
    uint8_t s = p->status;
    uint8_t d1 = p->need > 1 ? p->data[1] : 0;
    if (s >= 0xF0) {                                         // system common: no running status
        p->status = 0;
        return packet(pkt, s == 0xF2 ? CIN_COMMON_3 : CIN_COMMON_2, s, p->data[0], d1);
    }
    return packet(pkt, (uint8_t)(s >> 4), s, p->data[0], d1);   // a channel message: its CIN is its kind
}

int midi_packet_bytes(const uint8_t pkt[4], uint8_t out[3]) {
    static const uint8_t size[16] = {0, 0, 2, 3, 3, 1, 2, 3, 3, 3, 3, 3, 2, 2, 3, 1};
    int n = size[pkt[0] & 0x0F];
    for (int i = 0; i < n && i < 3; i++) out[i] = pkt[1 + i];
    return n;
}
