// "DX" control protocol: reliable request/ACK over UDP.
//
// Every request carries (session, seq). The box executes each (session, seq)
// at most once: a retransmitted request gets the cached ACK back, an older
// seq is dropped. Clients do stop-and-wait (one outstanding request per
// session) and retransmit on timeout. See DEV.md for the wire format.
#pragma once
#include <stdint.h>
#include "engine.h"
#include "presets.h"

#define CTRL_MAGIC0  'D'
#define CTRL_MAGIC1  'X'
#define CTRL_VERSION 1
#define CTRL_HDR_LEN 12
#define CTRL_MAX_PKT (CTRL_HDR_LEN + 1 + DMX_CHANNELS + 16)

enum {
    CT_PING           = 0x01,
    CT_SET            = 0x10,  // start u16 (1-based), count u16, values[count]
    CT_FADE           = 0x11,  // start u16, count u16, dur_ms u32, values[count]
    CT_PRESET_SAVE    = 0x20,  // slot u8           (stores current look, pre-master)
    CT_PRESET_GO      = 0x21,  // slot u8, dur_ms u32
    CT_PRESET_CLEAR   = 0x22,  // slot u8
    CT_MASTER         = 0x30,  // level u8
    CT_BLACKOUT       = 0x31,  // on u8
    CT_RELEASE_ARTNET = 0x32,  // -
    CT_MERGE_MODE     = 0x33,  // mode u8 (0 = HTP, 1 = Art-Net override)
    CT_GET_OUTPUT     = 0x40,  // start u16, count u16 -> values
    CT_GET_PRESET     = 0x41,  // slot u8 -> values[DMX_CHANNELS]
    CT_ACK            = 0x80,  // status u8, data...
};

enum {
    ST_OK = 0, ST_BAD_LENGTH = 1, ST_BAD_ARG = 2, ST_UNKNOWN_TYPE = 3,
    ST_STORAGE_ERROR = 4, ST_EMPTY_SLOT = 5,
};

#define CTRL_SESSIONS 8

typedef struct {
    uint8_t  used;
    uint32_t session;
    uint16_t last_seq;
    uint32_t last_ms;
    uint16_t resp_len;
    uint8_t  resp[CTRL_MAX_PKT];
} ctrl_session_t;

typedef struct {
    engine_t      *eng;
    preset_bank_t *bank;     // owned by the network core; never touched by the DMX core
    uint32_t       boot_ms;
    ctrl_session_t sess[CTRL_SESSIONS];
    uint32_t       rx_packets, executed, duplicates, stale;
} ctrl_t;

void ctrl_init(ctrl_t *c, engine_t *eng, preset_bank_t *bank, uint32_t now);

// Handle one datagram; returns length of the reply written to `resp` (0 = none).
int ctrl_handle(ctrl_t *c, const uint8_t *pkt, int len, uint32_t now,
                uint8_t *resp, int resp_cap);

// Shared with the physical buttons.
uint8_t ctrl_preset_go(ctrl_t *c, uint8_t slot, uint32_t dur_ms, uint32_t now);
void    ctrl_toggle_blackout(ctrl_t *c);
