// The DMX line of box 2 on UART1 (dmx_line.h).
//
// A packet, DMX frame or RDM request: the break (the line low for 180 us), the
// mark after the break (40 us), then its bytes at 250 000 baud with two stop
// bits. The break is a 0 sent at 50 000 baud: its start bit and 8 data bits are
// the 180 us low, its two stop bits the 40 us high. So the UART times the break
// as well, and the pin never has to be switched over.
//
// The interrupt fills the UART's buffer whenever it runs empty (the last byte is
// then still on its way out, 44 us: time enough to refill), and hears when the
// last bit of a packet has gone out. After a request that gets an answer it
// switches the line driver to listening at once and collects the answer's
// bytes. The main loop ends the listening when the answer is late or over, waits
// out the quiet line E1.20 wants after it (table 3-2), and starts the next
// packet: a waiting RDM request in the 20 ms after a frame, else the next frame.
//
// The registers are uart1.h's (taken with angle brackets: the PC test,
// firmware/test/test_line.c, brings its own).
#include "dmx_line.h"
#include <string.h>
#include <uart1.h>
#include "config.h"
#include "platform.h"
#include "rdm.h"

// E1.20 timing, in microseconds (as rp2040/src/dmx_line.c)
#define ANSWER_BEGINS_US   2800   // an answer starts this soon after the request (responders: 2 ms)
#define ANSWER_GAP_US      2100   // longest pause inside an answer
#define AFTER_ANSWER_US     176   // quiet line after an answer, before the next packet
#define AFTER_BROADCAST_US  176   // after a request nobody answers
#define AFTER_LOST_US      3000   // after a request whose answer did not come
#define AFTER_DISCOVERY_US 5800   // after DISC_UNIQUE_BRANCH, whatever came back
#define RDM_SLOT_US       20000   // RDM after a DMX frame: at most this long before the next frame
#define NEXT_REQUEST_US    1000   // how long the slot waits for rdm.c's next request

#define US(us) ((uint32_t)(us) * LINE_TICKS_PER_US)

enum { IDLE, BREAK, DATA, LAST, LISTEN, QUIET };   // the line
enum { FRAME, REQUEST };                           // what the packet is

static volatile uint8_t  g_state = IDLE;
static volatile uint8_t  g_what = FRAME;
static volatile uint8_t  g_expect;                 // the request's RDM_EXPECT_...
static const uint8_t    *g_tx;
static volatile uint16_t g_tx_len, g_tx_next;

// The answer, collected by the interrupt while LISTEN (times: line_now)
static volatile uint32_t g_sent, g_last_byte, g_quiet_until;
static volatile int      g_rx_n, g_rx_start, g_rx_total;
static volatile bool     g_rx_garbled;

// The RDM slot after a frame (main loop only)
static bool     g_slot_open, g_slot_any;
static uint32_t g_slot_begin, g_slot_last;

// ---------------------------------------------------------------- the mailbox of rdm.c
// rdm.c posts one transaction (plat_rdm_send), the line carries it out in the
// next slot, rdm.c takes what came back (plat_rdm_poll). All of it in the main
// loop; the interrupt only writes mb_raw while it listens.
enum { MB_FREE, MB_POSTED, MB_RUNNING, MB_DONE };
static uint8_t mb_state = MB_FREE;
static uint8_t mb_pkt[RDM_MAX_PKT];
static int     mb_len;
static uint8_t mb_expect;
static uint8_t mb_raw[RDM_MAX_PKT + 8];      // a little room for noise before the answer
static int     mb_n;
static bool    mb_garbled;

void plat_rdm_send(const uint8_t *pkt, int len, uint8_t expect) {
    if (len > (int)sizeof mb_pkt) len = (int)sizeof mb_pkt;
    memcpy(mb_pkt, pkt, (size_t)len);
    mb_len = len;
    mb_expect = expect;
    mb_state = MB_POSTED;
}

int plat_rdm_poll(uint8_t *raw, int cap, int *n, bool *garbled) {
    if (mb_state != MB_DONE) return 0;
    int k = mb_n < cap ? mb_n : cap;
    memcpy(raw, mb_raw, (size_t)k);
    *n = k;
    *garbled = mb_garbled;
    mb_state = MB_FREE;
    return 1;
}

// ---------------------------------------------------------------- the interrupt
// The answer is over, or late: the driver takes the line again (it rests at
// mark), and the quiet time before the next packet begins. In the interrupt, or
// with interrupts off.
static void stop_listening(void) {
    uart_rx_irq(false);
    line_send(true);
    if (g_expect == RDM_EXPECT_DISCOVERY) g_quiet_until = g_sent + US(AFTER_DISCOVERY_US);
    else if (g_rx_n == 0)                 g_quiet_until = g_sent + US(AFTER_LOST_US);
    else                                  g_quiet_until = g_last_byte + US(AFTER_ANSWER_US);
    g_state = QUIET;
}

// The last stop bit of a packet has left the pin.
static void packet_sent(void) {
    uint32_t now = line_now();
    if (g_what == FRAME) {
        g_state = IDLE;
        return;
    }
    g_sent = now;
    if (g_expect == RDM_EXPECT_NONE) {
        g_quiet_until = now + US(AFTER_BROADCAST_US);
        g_state = QUIET;
        return;
    }
    line_send(false);                                // listen: the driver lets go, the receiver hears the line
    g_rx_n = 0;
    g_rx_start = -1;
    g_rx_total = 0;
    g_rx_garbled = false;
    g_last_byte = now;
    g_state = LISTEN;
    uart_rx_irq(true);
}

// The UART wants bytes, or has sent them all.
static void feed(void) {
    switch (g_state) {
    case BREAK:                                      // the break and the mark after it are out
        uart_speed(false);
        uart_ask(UART_ASK_EMPTY);
        g_tx_next = 0;
        g_state = DATA;
        /* fall through: fill the buffer right away */
    case DATA:
        while (!uart_full()) {
            uart_put(g_tx[g_tx_next]);
            if (++g_tx_next >= g_tx_len) {           // the last byte is in the buffer
                uart_ask(UART_ASK_DONE);
                g_state = LAST;
                break;
            }
        }
        break;
    case LAST:
        uart_tx_irq(false);
        packet_sent();
        break;
    default:
        uart_tx_irq(false);
        break;
    }
    uart_tx_served();
}

// Bytes of an answer. A normal answer begins with a break (a 0 with a framing
// error, left out) and ends after the length its third byte gives; an answer
// to DISC_UNIQUE_BRANCH has no break and may come from several lights at once,
// so the main loop listens for the whole 5.8 ms. Framing errors mark the bytes
// as garbled (a collision).
static void heard(void) {
    while (uart_rx_ready()) {
        uint32_t errors;
        uint8_t b = uart_rx_get(&errors);
        g_last_byte = line_now();
        if ((errors & UART_FRAMING) && b == 0) {     // a break
            if (g_rx_n > 0) g_rx_garbled = true;     // ... in the middle of an answer
            continue;
        }
        if (errors) g_rx_garbled = true;
        if (g_rx_n >= (int)sizeof mb_raw) continue;  // too much: keep listening, keep nothing
        int n = g_rx_n;
        mb_raw[n++] = b;
        g_rx_n = n;
        if (g_expect == RDM_EXPECT_DISCOVERY) continue;
        if (g_rx_start < 0 && b == RDM_START_CODE) g_rx_start = n - 1;
        if (g_rx_start >= 0 && n == g_rx_start + 3) g_rx_total = g_rx_start + b + 2;   // the length counts from the start code, the checksum is 2 more
    }
    if (uart_rx_overrun()) g_rx_garbled = true;      // bytes were lost
    uart_rx_served();
    if (g_rx_total && g_rx_n >= g_rx_total) stop_listening();   // the whole answer is here
}

void dmx_line_interrupt(void) {
    if (uart_rx_due()) heard();
    if (uart_tx_due()) feed();
}

// ---------------------------------------------------------------- the main loop's part
static void start_packet(const uint8_t *p, uint16_t len) {
    g_tx = p;
    g_tx_len = len;
    g_tx_next = 0;
    g_state = BREAK;
    uart_speed(true);
    uart_ask(UART_ASK_DONE);
    uart_put(0);                                     // the break: 9 bits low at 50 000 baud
    uart_tx_irq(true);
}

static bool answer_over(uint32_t now) {
    if (g_expect == RDM_EXPECT_DISCOVERY)
        return now - g_sent >= US(AFTER_DISCOVERY_US) ||
               (g_rx_n == 0 && now - g_sent >= US(ANSWER_BEGINS_US));   // nobody in that range
    return now - g_last_byte >= US(g_rx_n == 0 ? ANSWER_BEGINS_US : ANSWER_GAP_US);
}

void dmx_line_init(void) {
    // The driver sends from the start (the line rests at mark), as it does
    // while the PIC is in reset: the DE optocoupler's LED is dark then.
    line_send(true);
    uart_start();
}

bool dmx_line_poll(const uint8_t *frame) {
    uint32_t now = line_now();
    switch (g_state) {
    case LISTEN: {
        unsigned interrupts = line_irq_off();
        if (g_state == LISTEN && answer_over(line_now())) stop_listening();
        line_irq_restore(interrupts);
        return false;
    }
    case QUIET:
        if ((int32_t)(now - g_quiet_until) < 0) return false;
        mb_n = g_rx_n;                               // the transaction is over: rdm.c may take it
        mb_garbled = g_rx_garbled;
        mb_state = MB_DONE;
        g_state = IDLE;
        g_slot_any = true;
        g_slot_last = now;
        break;
    case IDLE:
        break;
    default:                                         // a packet is on its way out
        return false;
    }

    // The line is free. After a frame: RDM, one transaction after the other
    // while rdm.c hands them over, but no longer than RDM_SLOT_US.
    if (g_what == FRAME && !g_slot_open) {
        g_slot_open = true;
        g_slot_any = false;
        g_slot_begin = now;
    }
    if (now - g_slot_begin < US(RDM_SLOT_US)) {
        if (mb_state == MB_POSTED) {
            mb_state = MB_RUNNING;
            g_what = REQUEST;
            g_expect = mb_expect;
            g_rx_n = 0;
            g_rx_garbled = false;
            start_packet(mb_pkt, (uint16_t)mb_len);
            return false;
        }
        if (g_slot_any && now - g_slot_last < US(NEXT_REQUEST_US)) return false;   // the next one may come
    }
    if (!frame) return false;
    g_slot_open = false;
    g_what = FRAME;
    start_packet(frame, 1 + DMX_CHANNELS);
    return true;
}
