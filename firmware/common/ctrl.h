// "DX" control protocol: reliable request/ACK over UDP.
//
// Every request carries (session, seq). The box executes each (session, seq)
// at most once: a retransmitted request gets the cached ACK back, an older
// seq is dropped. Clients do stop-and-wait (one outstanding request per
// session) and retransmit on timeout. See DEV.md for the wire format.
#pragma once
#include <stdint.h>
#include "dmx_in.h"
#include "engine.h"
#include "presets.h"
#include "rdm.h"

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
    CT_GET_INPUT      = 0x42,  // since u32 -> live u8, frame u32, count u16, levels[count]
                               //   (DMX IN: the last complete frame, if it is not frame `since`; else count 0)
    CT_RDM_DISCOVER   = 0x50,  // -            (starts a full search of the lights on the line)
    CT_RDM_TOD        = 0x51,  // -            -> searching u8, count u16, ids[count][6]
    CT_RDM_REQUEST    = 0x52,  // dest[6], sub-device u16, command class u8, pid u16, pdl u8, data[pdl]
                               //   -> response type u8, command class u8, pid u16, pdl u8, data[pdl]
    CT_BOOTLOADER     = 0x60,  // -            (restarts into the USB boot loader after this answer)
    CT_ACK            = 0x80,  // status u8, data...
};

enum {
    ST_OK = 0, ST_BAD_LENGTH = 1, ST_BAD_ARG = 2, ST_UNKNOWN_TYPE = 3,
    ST_STORAGE_ERROR = 4, ST_EMPTY_SLOT = 5,
    ST_RDM_TIMEOUT = 6,        // the light did not answer
    ST_RDM_BAD = 7,            // an answer came, but spoiled
    ST_BUSY = 8,               // another RDM request is on its way
    ST_NOT_SUPPORTED = 9,      // this box has no RDM (or no DMX IN)
};

// What PING tells about the box (bits of its "can do" byte)
#define CTRL_CAN_RDM   0x01
#define CTRL_CAN_MIDI  0x02    // a USB MIDI port
#define CTRL_CAN_CHAIN 0x04    // a USB hub with an OUT socket for the next box
#define CTRL_CAN_DMX_IN 0x08   // a DMX input: a console's levels (CT_GET_INPUT)

#define CTRL_SESSIONS 8

typedef struct {
    uint8_t  used;
    uint32_t session;
    uint16_t last_seq;
    uint32_t last_ms;
    uint16_t resp_len;
    uint8_t  pending;          // the answer to last_seq comes later (an RDM request)
    uint8_t  resp[CTRL_MAX_PKT];
} ctrl_session_t;

typedef struct {
    engine_t      *eng;
    preset_bank_t *bank;     // owned by the network core; never touched by the DMX core
    rdm_t         *rdm;      // NULL: no RDM
    dmx_in_t      *dmx_in;   // NULL: no DMX IN
    uint8_t        can;      // CTRL_CAN_...
    uint8_t        box_number;
    uint8_t        model;    // which dmxbox: 1 to 4 (BOX_MODEL of its box.h)
    uint32_t       boot_ms;
    ctrl_session_t sess[CTRL_SESSIONS];
    uint32_t       rx_packets, executed, duplicates, stale;

    // an answer that comes later: whom it is for
    uint8_t        deferred;         // an RDM request is on its way
    uint8_t        deferred_new;     // set by ctrl_handle: the caller notes where the answer goes
    uint8_t        deferred_hdr[CTRL_HDR_LEN];
    uint32_t       deferred_session;
    uint16_t       deferred_seq;
} ctrl_t;

void ctrl_init(ctrl_t *c, engine_t *eng, preset_bank_t *bank, uint32_t now);

// An answer that was not ready when its request came (CT_RDM_REQUEST): returns
// its length once it is (0 = not yet). It goes to the sender of that request.
int ctrl_poll(ctrl_t *c, uint32_t now, uint8_t *resp, int resp_cap);

// Handle one datagram; returns length of the reply written to `resp` (0 = none).
int ctrl_handle(ctrl_t *c, const uint8_t *pkt, int len, uint32_t now,
                uint8_t *resp, int resp_cap);

// Shared with the physical buttons.
uint8_t ctrl_preset_go(ctrl_t *c, uint8_t slot, uint32_t dur_ms, uint32_t now);
void    ctrl_toggle_blackout(ctrl_t *c);
