// PC test of box 2's DMX line: firmware/pic32/src/dmx_line.c, the code the
// PIC32 runs, on a pretend UART (the functions of fake_pic32/uart1.h are made
// here) with the lights in memory of the simulator (firmware/sim/rdm_lights.c)
// on the line. Time goes by in microseconds: a byte takes 44 us, the break byte
// 220 us; the UART's interrupt comes the moment it is due, the main loop every
// 50 us and every 20th time 2.5 ms late (a frame worked out, USB).
//
//   1. DMX frames: the break, the mark after it, the levels, one frame after the other
//   2. the RDM search through the line: every light found, frames in between
//   3. requests: an answer, a refusal, a light that is not there, a broadcast, a slow light
//   4. an answer while the PIC stood still (a flash write): its bytes overflow the buffer
//
//   Windows:      build.cmd (with the other tests)
//   Linux/macOS:  cc -std=c11 -Wall -Wextra -Ifake_pic32 -I../pic32/src -I../../boxes/box2/firmware -I../common \
//                    -I../sim test_line.c ../pic32/src/dmx_line.c ../common/rdm.c ../sim/rdm_lights.c \
//                    -o test_line && ./test_line
#include <stdio.h>
#include <string.h>

#include "config.h"
#include "dmx_line.h"
#include "rdm.h"
#include "rdm_lights.h"
#include "uart1.h"

static int failures, checks;
static const char *current = "";

#define CHECK(cond) do { checks++; if (!(cond)) { failures++; \
    printf("  FAIL %s (line %d): %s\n", current, __LINE__, #cond); } } while (0)
#define TEST(name) do { current = name; printf("- %s\n", name); } while (0)

#define FRAME_US (220 + (1 + DMX_CHANNELS) * 44)   // the break byte and 513 bytes: 22.79 ms

// ================================================================ time
static unsigned long long sim_us;                  // microseconds since the start
static unsigned long long stall_until;             // the PIC stands still until then (no interrupt, no main loop)

uint32_t line_now(void) { return (uint32_t)(sim_us * LINE_TICKS_PER_US); }
unsigned line_irq_off(void) { return 0; }          // nothing runs in between here
void line_irq_restore(unsigned state) { (void)state; }

// ================================================================ what the line saw
static struct {
    int frames, requests;
    int bad_break, bad_mab;                 // a break byte not 0 at 50 000 baud; a mark after break not 12 to 88 us
    int bad_frames;                         // a frame not made of one level (the frame changed while it went out)
    int sent_while_listening;               // bytes out while the driver listened
    int conflicts;                          // the box began a packet while an answer was still on the line
    int late_listen;                        // listening began later than 10 us after a request
    int missed;                             // answer bytes that came while the box did not listen
    unsigned long long last_frame_at, longest_frame_gap;
    unsigned long long gap_after_request;   // quiet line between the last request (or its answer) and the next packet
    unsigned long long min_gap_after_request;
    unsigned long long listen_tail;         // from the last byte of the last answer to the driver sending again
} seen;

static unsigned long long line_quiet_since;  // the end of the last byte on the line, either way
static unsigned long long request_end, last_answer_byte_at;
static bool last_was_request;

// ================================================================ the line driver (DE)
static bool de_send = true;
static bool tx_busy;                                   // a byte in the UART's shift register
static unsigned long long tx_end;                      // ... its last stop bit ends then

void line_send(bool send) {
    if (!send && de_send && (tx_busy || sim_us - tx_end > 10)) seen.late_listen++;   // listening follows the request at once
    if (send && !de_send && last_answer_byte_at > request_end) seen.listen_tail = sim_us - last_answer_byte_at;
    de_send = send;
}

// ================================================================ the pretend UART: receiving
static struct { unsigned long long at; uint8_t b; uint32_t errors; } answer[RDM_MAX_PKT + 16];
static int answer_n, answer_next;
static uint8_t  rx_fifo[8];
static uint32_t rx_errors[8];
static int      rx_count;
static bool     rx_overrun, rx_irq;

static void answer_byte(unsigned long long at, uint8_t b, uint32_t errors) {
    if (answer_n < (int)(sizeof answer / sizeof answer[0])) {
        answer[answer_n].at = at;
        answer[answer_n].b = b;
        answer[answer_n].errors = errors;
        answer_n++;
    }
}

static bool answer_on_line(void) { return answer_next < answer_n; }

// The answer's bytes reach the box as they end on the line
static void rx_step(void) {
    while (answer_next < answer_n && answer[answer_next].at <= sim_us) {
        uint8_t b = answer[answer_next].b;
        uint32_t errors = answer[answer_next].errors;
        answer_next++;
        line_quiet_since = sim_us;
        last_answer_byte_at = sim_us;
        if (de_send) { seen.missed++; continue; }      // the receiver is off while the driver sends
        if (rx_count == 8) { rx_overrun = true; continue; }
        rx_fifo[rx_count] = b;
        rx_errors[rx_count] = errors;
        rx_count++;
    }
}

void uart_rx_irq(bool on) {
    rx_irq = on;
    if (on) { rx_count = 0; rx_overrun = false; }
}
bool uart_rx_due(void) { return rx_irq && rx_count > 0; }
void uart_rx_served(void) {}
bool uart_rx_ready(void) { return rx_count > 0; }

uint8_t uart_rx_get(uint32_t *errors) {
    uint8_t b = rx_fifo[0];
    *errors = rx_errors[0];
    rx_count--;
    memmove(rx_fifo, rx_fifo + 1, (size_t)rx_count);
    memmove(rx_errors, rx_errors + 1, (size_t)rx_count * sizeof rx_errors[0]);
    return b;
}

bool uart_rx_overrun(void) {
    if (!rx_overrun) return false;
    rx_overrun = false;
    rx_count = 0;                                      // clearing OERR empties the buffer, as on the PIC
    return true;
}

// ================================================================ the lights
static int  answer_delay_us = 300;                     // how soon a light answers (E1.20: 176 us to 2 ms)
static bool lights_deaf;                               // nobody answers (the cable is out)

static uint8_t pkt[1 + DMX_CHANNELS + 8];              // the packet going out
static int     pkt_len;
static bool    pkt_open;
static unsigned long long pkt_break_at, pkt_data_at;

static void packet_done(void) {
    pkt_open = false;
    unsigned long long mab = pkt_data_at - pkt_break_at - 180;
    if (mab < 12 || mab > 88) seen.bad_mab++;
    if (pkt[0] == 0x00) {                              // a DMX frame
        seen.frames++;
        if (pkt_len != 1 + DMX_CHANNELS) seen.bad_frames++;
        for (int i = 2; i < pkt_len; i++) if (pkt[i] != pkt[1]) { seen.bad_frames++; break; }
        if (seen.last_frame_at && pkt_break_at - seen.last_frame_at > seen.longest_frame_gap)
            seen.longest_frame_gap = pkt_break_at - seen.last_frame_at;
        seen.last_frame_at = pkt_break_at;
        last_was_request = false;
        return;
    }
    // an RDM request: every light hears it, and the answer comes back on the line
    seen.requests++;
    last_was_request = true;
    request_end = sim_us;
    uint8_t out[RDM_MAX_PKT + 8];
    bool garbled = false;
    int n = lights_deaf ? 0 : lights_hear(pkt, pkt_len, out, (int)sizeof out, &garbled);
    answer_n = answer_next = 0;
    if (n <= 0) return;
    unsigned long long t = sim_us + (unsigned long long)answer_delay_us;
    uint32_t errors = garbled ? UART_FRAMING : 0;      // lights that answer at once garble each other's bits
    if (out[0] == RDM_START_CODE) {                    // a normal answer: break 176 us, mark after break 12 us
        answer_byte(t + 44, 0x00, UART_FRAMING);       // the UART sees the break as a 0 whose stop bit is low
        t += 176 + 12;
    }
    for (int i = 0; i < n; i++) answer_byte(t + 44u * (unsigned)(i + 1), out[i], errors);
}

// ================================================================ the pretend UART: sending
static bool     tx_break_speed, tx_irq;
static uint32_t tx_ask;
static uint8_t  tx_fifo[8];
static int      tx_count;

static void tx_load(uint8_t b) {
    tx_busy = true;
    tx_end = sim_us + 11u * (tx_break_speed ? 20u : 4u);   // start bit, 8 data bits, 2 stop bits
    if (!de_send) seen.sent_while_listening++;
    if (tx_break_speed) {                              // a break: a packet begins
        if (b != 0) seen.bad_break++;
        if (answer_on_line()) seen.conflicts++;
        if (last_was_request) {
            seen.gap_after_request = sim_us - line_quiet_since;
            if (seen.gap_after_request < seen.min_gap_after_request) seen.min_gap_after_request = seen.gap_after_request;
            last_was_request = false;
        }
        pkt_open = true;
        pkt_len = 0;
        pkt_break_at = sim_us;
    } else {
        if (pkt_len == 0) pkt_data_at = sim_us;
        if (pkt_len < (int)sizeof pkt) pkt[pkt_len++] = b;
    }
}

static void tx_step(void) {
    if (!tx_busy || sim_us < tx_end) return;
    tx_busy = false;
    line_quiet_since = sim_us;
    if (tx_count > 0) {
        uint8_t b = tx_fifo[0];
        tx_count--;
        memmove(tx_fifo, tx_fifo + 1, (size_t)tx_count);
        tx_load(b);
    }
}

void uart_start(void) {}
void uart_speed(bool for_break) { tx_break_speed = for_break; }
void uart_ask(uint32_t when) { tx_ask = when; }
bool uart_full(void) { return tx_count == 8; }
void uart_put(uint8_t b) {
    if (!tx_busy) tx_load(b);                          // straight into the shift register
    else if (tx_count < 8) tx_fifo[tx_count++] = b;
}
void uart_tx_irq(bool on) { tx_irq = on; }
bool uart_tx_due(void) {
    if (!tx_irq) return false;
    return tx_ask == UART_ASK_EMPTY ? tx_count == 0 : (tx_count == 0 && !tx_busy);
}
void uart_tx_served(void) {}

// ================================================================ the box: its main loop, as in main.c
static rdm_t g_rdm;
static uint8_t g_frames[2][1 + DMX_CHANNELS];
static int     g_next, g_passes;
static bool    g_ready;
static uint8_t g_level;
static unsigned long long g_next_pass;
static const uint8_t own_uid[6] = {0x7F, 0xF0, 0x00, 0x02, 0x00, 0x01};

static void main_pass(void) {
    rdm_poll(&g_rdm, (uint32_t)(sim_us / 1000));
    if (!g_ready) {
        g_frames[g_next][0] = 0x00;
        memset(g_frames[g_next] + 1, g_level++, DMX_CHANNELS);   // every frame one level, the next one more
        g_ready = true;
    }
    if (dmx_line_poll(g_frames[g_next])) {
        g_next ^= 1;
        g_ready = false;
    }
}

static void step(void) {
    sim_us++;
    tx_step();
    rx_step();
    if (sim_us >= stall_until) {
        for (int guard = 0; guard < 64 && (uart_rx_due() || uart_tx_due()); guard++) dmx_line_interrupt();
        if (sim_us >= g_next_pass) {
            main_pass();
            g_passes++;
            g_next_pass = sim_us + (g_passes % 20 == 0 ? 2500u : 50u);
        }
    }
    if (pkt_open && !tx_busy && tx_count == 0 && pkt_len > 0) packet_done();
}

static void run_us(unsigned long long us) {
    unsigned long long end = sim_us + us;
    while (sim_us < end) step();
}

static bool rdm_idle(void) {
    return !g_rdm.discovering && g_rdm.req_owner == RDM_OWNER_NONE && !g_rdm.line_busy;
}

// Runs until the search and any request are over (at most a simulated minute), then 50 ms more.
static void settle(void) {
    for (unsigned long long end = sim_us + 60000000u; sim_us < end && !rdm_idle();) step();
    run_us(50000);
}

static void fresh_counts(void) {
    memset(&seen, 0, sizeof seen);
    seen.min_gap_after_request = ~0ull;
}

static bool tod_has_every_light(void) {
    if (g_rdm.tod_count != light_count) return false;
    for (int i = 0; i < light_count; i++) {
        bool found = false;
        for (int k = 0; k < g_rdm.tod_count; k++) if (memcmp(g_rdm.tod[k], lights[i].uid, 6) == 0) found = true;
        if (!found) return false;
    }
    return true;
}

// One request to a light, run until its answer is there. Returns its status.
static uint8_t ask(const uint8_t uid[6], uint8_t cc, uint16_t pid, const uint8_t *pd, uint8_t pdl,
                   const rdm_answer_t **a) {
    uint8_t status = 0xFF;
    if (!rdm_submit(&g_rdm, RDM_OWNER_CTRL, uid, 0, cc, pid, pd, pdl)) return 0xFE;
    for (unsigned long long end = sim_us + 2000000u; sim_us < end;) {
        step();
        if (rdm_take(&g_rdm, RDM_OWNER_CTRL, &status, a, NULL, NULL)) break;
    }
    run_us(30000);                                     // the next packet after it, for the gap
    return status;
}

// ================================================================ the tests
static void test_frames(void) {
    TEST("DMX frames: break 180 us, mark after break 40 us, the levels, one after the other");
    fresh_counts();
    lights_make(0);
    rdm_init(&g_rdm, own_uid);
    run_us(300000);
    CHECK(seen.frames >= 12);
    CHECK(seen.bad_break == 0 && seen.bad_mab == 0 && seen.bad_frames == 0);
    CHECK(seen.longest_frame_gap >= FRAME_US && seen.longest_frame_gap <= FRAME_US + 2600);
    CHECK(seen.requests == 0 && seen.sent_while_listening == 0);
}

static void test_search(void) {
    TEST("RDM search through the line: 0, 1, 2, 7 and 30 lights, frames in between");
    static const int sizes[] = {0, 1, 2, 7, 30};
    for (int s = 0; s < (int)(sizeof sizes / sizeof sizes[0]); s++) {
        fresh_counts();
        lights_make(sizes[s]);
        rdm_start_discovery(&g_rdm);
        settle();
        CHECK(!g_rdm.discovering && tod_has_every_light() && !g_rdm.tod_overflow);
        CHECK(seen.requests > 0 && seen.frames > 0);
        // a frame, the RDM slot (20 ms, its last transaction up to 7 ms more), the main loop 2.5 ms late
        CHECK(seen.longest_frame_gap <= FRAME_US + 20000 + 7500 + 2600);
        CHECK(seen.conflicts == 0 && seen.sent_while_listening == 0 && seen.late_listen == 0 && seen.missed == 0);
        CHECK(seen.min_gap_after_request >= 176);
        CHECK(seen.bad_break == 0 && seen.bad_mab == 0 && seen.bad_frames == 0);
        if (failures) printf("    %d lights: %d found, %d requests, %d frames, longest frame gap %llu us\n",
                             sizes[s], g_rdm.tod_count, seen.requests, seen.frames, seen.longest_frame_gap);
    }
}

static void test_requests(void) {
    TEST("RDM requests: an answer, a refusal, a light that is not there, a broadcast, a slow light");
    lights_make(3);
    rdm_start_discovery(&g_rdm);
    settle();
    CHECK(tod_has_every_light());

    const rdm_answer_t *a = NULL;
    fresh_counts();
    CHECK(ask(lights[1].uid, RDM_CC_GET, 0x0060, NULL, 0, &a) == RDM_DONE_OK);   // DEVICE_INFO
    CHECK(a && a->response_type == RDM_ACK && a->pid == 0x0060 && memcmp(a->src, lights[1].uid, 6) == 0);
    CHECK(seen.listen_tail <= 10);                     // the whole answer is there: the driver sends again at once
    CHECK(seen.gap_after_request >= 176);
    CHECK(seen.missed == 0 && seen.conflicts == 0);

    fresh_counts();
    CHECK(ask(lights[2].uid, RDM_CC_GET, 0x7777, NULL, 0, &a) == RDM_DONE_OK);   // a PID it does not know
    CHECK(a && a->response_type == RDM_NACK_REASON);

    static const uint8_t nobody[6] = {0x7F, 0xF1, 0x12, 0x34, 0x56, 0x78};
    fresh_counts();
    CHECK(ask(nobody, RDM_CC_GET, 0x0060, NULL, 0, &a) == RDM_DONE_TIMEOUT);
    CHECK(seen.gap_after_request >= 3000);             // the quiet line after an answer that did not come

    static const uint8_t everybody[6] = {0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF};
    const uint8_t on = 1;
    fresh_counts();
    CHECK(ask(everybody, RDM_CC_SET, 0x1000, &on, 1, &a) == RDM_DONE_OK);        // IDENTIFY_DEVICE to all
    CHECK(lights[0].identify && lights[1].identify && lights[2].identify);
    CHECK(seen.gap_after_request >= 176);

    answer_delay_us = 1900;                            // a slow light, still within E1.20's 2 ms
    fresh_counts();
    CHECK(ask(lights[0].uid, RDM_CC_GET, 0x0060, NULL, 0, &a) == RDM_DONE_OK);
    CHECK(seen.missed == 0 && seen.conflicts == 0);
    answer_delay_us = 300;

    lights_deaf = true;                                // the cable is out: every request goes unanswered
    fresh_counts();
    CHECK(ask(lights[0].uid, RDM_CC_GET, 0x0060, NULL, 0, &a) == RDM_DONE_TIMEOUT);
    lights_deaf = false;
    CHECK(ask(lights[0].uid, RDM_CC_GET, 0x0060, NULL, 0, &a) == RDM_DONE_OK);   // and back
}

static void test_stall(void) {
    TEST("An answer while the PIC stands still (a flash write): too much for the buffer, then the line goes on");
    fresh_counts();
    const rdm_answer_t *a = NULL;
    CHECK(rdm_submit(&g_rdm, RDM_OWNER_CTRL, lights[1].uid, 0, RDM_CC_GET, 0x0060, NULL, 0));
    for (unsigned long long end = sim_us + 1000000u; sim_us < end && de_send;) step();   // until it listens
    CHECK(!de_send);
    stall_until = sim_us + 3000;                       // the answer comes, 8 bytes fit, the rest is lost
    uint8_t status = 0xFF;
    for (unsigned long long end = sim_us + 2000000u; sim_us < end;) {
        step();
        if (rdm_take(&g_rdm, RDM_OWNER_CTRL, &status, &a, NULL, NULL)) break;
    }
    CHECK(status == RDM_DONE_BAD || status == RDM_DONE_TIMEOUT);
    run_us(100000);
    CHECK(seen.frames >= 3 && seen.bad_frames == 0);   // frames go on
    CHECK(ask(lights[1].uid, RDM_CC_GET, 0x0060, NULL, 0, &a) == RDM_DONE_OK);   // and the next request is answered
}

int main(void) {
    dmx_line_init();
    test_frames();
    test_search();
    test_requests();
    test_stall();
    printf("\n%d checks, %d failed\n", checks, failures);
    return failures ? 1 : 0;
}
