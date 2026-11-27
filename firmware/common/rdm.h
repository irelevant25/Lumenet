// RDM (ANSI E1.20) on the box's DMX line: the controller side.
//
// The box is the RDM controller of its one DMX line. It finds the lights on the
// line by itself (discovery: a binary search over their 48-bit IDs) and keeps
// them in a table (the "TOD"). Requests from the desk (DX protocol) or from an
// Art-Net program (ArtRdm) go out one at a time; the answer comes back to the
// one who asked.
//
// This file knows the packets and the search, nothing about pins or time
// beyond milliseconds. The line itself is the platform's (plat_rdm_send and
// plat_rdm_poll in platform.h): the RP2040 sends between two DMX frames, the
// simulator answers with lights that exist only in its memory.
#pragma once
#include <stdbool.h>
#include <stdint.h>

#define RDM_START_CODE     0xCC
#define RDM_SUB_START_CODE 0x01
#define RDM_MAX_PD         231          // parameter data: at most 231 bytes
#define RDM_MAX_PKT        (24 + RDM_MAX_PD + 2)
#define RDM_DISC_RESPONSE  24           // longest answer to DISC_UNIQUE_BRANCH: 7 x FE, AA, 16 bytes
#define RDM_TOD_MAX        64           // lights the box remembers

// Command classes
enum {
    RDM_CC_DISCOVERY = 0x10, RDM_CC_DISCOVERY_RESPONSE = 0x11,
    RDM_CC_GET = 0x20, RDM_CC_GET_RESPONSE = 0x21,
    RDM_CC_SET = 0x30, RDM_CC_SET_RESPONSE = 0x31,
};

// Parameter IDs the box itself uses (the desk may send any other)
enum {
    RDM_PID_DISC_UNIQUE_BRANCH = 0x0001,
    RDM_PID_DISC_MUTE = 0x0002,
    RDM_PID_DISC_UN_MUTE = 0x0003,
};

// Response types (the "port ID" byte of an answer)
enum { RDM_ACK = 0x00, RDM_ACK_TIMER = 0x01, RDM_NACK_REASON = 0x02, RDM_ACK_OVERFLOW = 0x03 };

// What a transaction on the line expects back
enum { RDM_EXPECT_NONE = 0, RDM_EXPECT_RESPONSE = 1, RDM_EXPECT_DISCOVERY = 2 };

// How a transaction ended
enum { RDM_DONE_OK = 0, RDM_DONE_TIMEOUT = 1, RDM_DONE_BAD = 2, RDM_DONE_BUSY = 3 };

// Who asked
enum { RDM_OWNER_NONE = 0, RDM_OWNER_CTRL = 1, RDM_OWNER_ARTNET = 2 };

typedef struct {
    uint8_t  response_type;             // RDM_ACK ...
    uint8_t  cc;                        // command class of the answer
    uint16_t pid;
    uint8_t  pdl;
    uint8_t  pd[RDM_MAX_PD];
    uint8_t  src[6];                    // who answered
} rdm_answer_t;

typedef struct {
    uint8_t  own_uid[6];                // the box's own RDM ID (manufacturer 7FF0, "prototype")
    uint8_t  tn;                        // transaction number

    uint8_t  tod[RDM_TOD_MAX][6];       // the lights found, in the order found
    uint16_t tod_count;
    bool     tod_overflow;              // more lights answered than fit

    // discovery
    bool     discovering;
    uint8_t  disc_step;                 // what the last transaction of the search was
    uint64_t stack_lo[50], stack_hi[50];  // ranges still to search (48 bits each)
    int      sp;
    uint64_t found;                     // ID being muted
    uint8_t  unmute_left;               // DISC_UN_MUTE broadcasts still to send
    uint8_t  mute_tries;
    uint32_t disc_transactions;         // statistics
    uint32_t disc_collisions;
    uint32_t discoveries;               // finished searches

    // the line: one transaction at a time
    bool     line_busy;
    uint8_t  line_for;                  // 0 = discovery, 1 = a request
    uint8_t  line_expect;
    uint32_t line_started_ms;

    // the request of the desk or of Art-Net
    uint8_t  req[RDM_MAX_PKT];
    int      req_len;
    uint8_t  req_owner;                 // RDM_OWNER_..., NONE = no request
    bool     req_sent;
    bool     req_done;
    uint8_t  req_status;                // RDM_DONE_...
    uint8_t  resp_raw[RDM_MAX_PKT];     // the answer as it came: from the start code on
    int      resp_raw_len;
    rdm_answer_t answer;
} rdm_t;

void rdm_init(rdm_t *r, const uint8_t own_uid[6]);

// Start a full discovery: the table is emptied, every light is unmuted and found again.
void rdm_start_discovery(rdm_t *r);

// Run the next step. Call it often (every pass of the main loop).
void rdm_poll(rdm_t *r, uint32_t now);

// Queue a request built from its parts (the box adds its own ID, the transaction
// number and the checksum). Returns false while another request waits.
bool rdm_submit(rdm_t *r, uint8_t owner, const uint8_t dest[6], uint16_t sub_device, uint8_t cc,
                uint16_t pid, const uint8_t *pd, uint8_t pdl);

// Queue a complete packet (Art-Net hands one over without the start code; the
// box puts it back). The source ID is replaced by the box's own.
bool rdm_submit_raw(rdm_t *r, uint8_t owner, const uint8_t *pkt_without_sc, int len);

// The answer to the request, once it is there: returns true once, then the slot is free.
bool rdm_take(rdm_t *r, uint8_t owner, uint8_t *status, const rdm_answer_t **answer,
              const uint8_t **raw, int *raw_len);

// ---------------------------------------------------------------- packets (also used by the simulator)
uint16_t rdm_checksum(const uint8_t *p, int n);
int  rdm_build(uint8_t *out, const uint8_t dest[6], const uint8_t src[6], uint8_t tn, uint8_t port_or_type,
               uint8_t msg_count, uint16_t sub_device, uint8_t cc, uint16_t pid, const uint8_t *pd, uint8_t pdl);
// Checks start codes, length and checksum of a received packet (from the start code on).
bool rdm_parse(const uint8_t *p, int n, rdm_answer_t *a);
// The answer to DISC_UNIQUE_BRANCH: false if garbled (a collision).
bool rdm_decode_disc(const uint8_t *p, int n, uint8_t uid[6]);
int  rdm_encode_disc(uint8_t *out, const uint8_t uid[6]);
uint64_t rdm_uid_to_u64(const uint8_t uid[6]);
void rdm_u64_to_uid(uint64_t v, uint8_t uid[6]);
