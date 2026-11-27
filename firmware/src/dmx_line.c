#include "dmx_line.h"
#include <string.h>
#include "pico/stdlib.h"
#include "hardware/clocks.h"
#include "hardware/dma.h"
#include "hardware/pio.h"
#include "hardware/sync.h"
#include "hardware/uart.h"
#include "dmx_tx.pio.h"

#include "config.h"
#include "platform.h"
#include "rdm.h"

// E1.20 timing, in microseconds
#define ANSWER_BEGINS_US   2800   // an answer starts this soon after the request (responders: 2 ms)
#define ANSWER_GAP_US      2100   // longest pause inside an answer
#define AFTER_ANSWER_US     176   // quiet line after an answer, before the next packet
#define AFTER_BROADCAST_US  176   // after a request nobody answers
#define AFTER_LOST_US      3000   // after a request whose answer did not come
#define AFTER_DISCOVERY_US 5800   // after DISC_UNIQUE_BRANCH, whatever came back
#define RDM_SLOT_US       20000   // RDM after a DMX frame: at most this long before the next frame

static PIO      g_pio = pio0;
static uint     g_sm, g_offset;
static int      g_dma;
static volatile uint32_t g_frames;

// ---------------------------------------------------------------- the mailbox between the cores
// Core 0 (rdm.c, through plat_rdm_send) posts one transaction, core 1 carries
// it out between two frames and posts what came back, core 0 takes it
// (plat_rdm_poll). Only `mb_state` is shared while it changes hands.
enum { MB_FREE, MB_POSTED, MB_DONE };
static volatile uint8_t mb_state = MB_FREE;
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
    __dmb();
    mb_state = MB_POSTED;
}

int plat_rdm_poll(uint8_t *raw, int cap, int *n, bool *garbled) {
    if (mb_state != MB_DONE) return 0;
    __dmb();
    int k = mb_n < cap ? mb_n : cap;
    memcpy(raw, mb_raw, (size_t)k);
    *n = k;
    *garbled = mb_garbled;
    __dmb();
    mb_state = MB_FREE;
    return 1;
}

// ---------------------------------------------------------------- pins
void dmx_line_init(void) {
    // The driver sends from the start (the line idles at mark), as it does
    // while the RP2040 is off: the DE optocoupler's LED is dark then.
    gpio_init(PIN_DMX_DE);
    gpio_set_dir(PIN_DMX_DE, GPIO_OUT);
    gpio_put(PIN_DMX_DE, 1);

    // The answers come in on UART0's RX. Its TX is the log, set up by main.c
    // at the same speed (LOG_BAUD).
    gpio_set_function(PIN_DMX_RX, GPIO_FUNC_UART);
}

static void listen(bool on) { gpio_put(PIN_DMX_DE, on ? 0 : 1); }

static void line_start(void) {
    g_sm = pio_claim_unused_sm(g_pio, true);
    g_offset = pio_add_program(g_pio, &dmx_tx_program);

    pio_sm_config c = dmx_tx_program_get_default_config(g_offset);
    sm_config_set_out_pins(&c, PIN_DMX_TX, 1);
    sm_config_set_sideset_pins(&c, PIN_DMX_TX);
    sm_config_set_out_shift(&c, true, false, 32);          // shift right = LSB first, manual pull
    sm_config_set_fifo_join(&c, PIO_FIFO_JOIN_TX);          // 8-deep TX FIFO
    sm_config_set_clkdiv(&c, (float)clock_get_hz(clk_sys) / 1000000.0f);   // 1 MHz

    pio_sm_set_pins_with_mask(g_pio, g_sm, 1u << PIN_DMX_TX, 1u << PIN_DMX_TX);   // idle high
    pio_sm_set_consecutive_pindirs(g_pio, g_sm, PIN_DMX_TX, 1, true);
    pio_gpio_init(g_pio, PIN_DMX_TX);
    // The pin sinks the optocoupler LED current (about 10 mA) when it is low.
    gpio_set_drive_strength(PIN_DMX_TX, GPIO_DRIVE_STRENGTH_12MA);

    pio_sm_init(g_pio, g_sm, g_offset + dmx_tx_offset_idle, &c);
    pio_sm_set_enabled(g_pio, g_sm, true);

    g_dma = dma_claim_unused_channel(true);
    dma_channel_config d = dma_channel_get_default_config((uint)g_dma);
    channel_config_set_transfer_data_size(&d, DMA_SIZE_8);   // the byte is copied into all four of the word
    channel_config_set_read_increment(&d, true);
    channel_config_set_write_increment(&d, false);
    channel_config_set_dreq(&d, pio_get_dreq(g_pio, g_sm, true));
    dma_channel_configure((uint)g_dma, &d, &g_pio->txf[g_sm], NULL, 0, false);
}

// One packet: break, mark after break, the bytes. Returns when the last stop
// bit has left the pin.
static void send_packet(const uint8_t *p, uint len) {
    pio_sm_exec(g_pio, g_sm, pio_encode_jmp(g_offset + dmx_tx_offset_frame_start));
    dma_channel_transfer_from_buffer_now((uint)g_dma, p, len);
    dma_channel_wait_for_finish_blocking((uint)g_dma);
    // TXSTALL is set while the SM waits on an empty FIFO, that is after the
    // last stop bit. Clear it, then wait for it to come back.
    uint32_t mask = 1u << (PIO_FDEBUG_TXSTALL_LSB + g_sm);
    g_pio->fdebug = mask;
    while (!(g_pio->fdebug & mask)) tight_loop_contents();
}

static void wait_until(uint32_t t) {
    while ((int32_t)(time_us_32() - t) < 0) tight_loop_contents();
}

// ---------------------------------------------------------------- RDM
// What comes back after a request. A normal answer begins with a break (it
// shows as a byte with the break flag, which is left out) and ends after the
// length its third byte gives; an answer to DISC_UNIQUE_BRANCH has no break
// and may come from several lights at once, so the line is heard for the whole
// 5.8 ms. Framing errors mark the bytes as garbled (a collision).
static int receive(uint8_t *buf, int cap, uint32_t sent, bool discovery, bool *garbled, uint32_t *last_byte) {
    uart_hw_t *u = uart_get_hw(uart0);
    while (uart_is_readable(uart0)) (void)u->dr;            // nothing old
    u->rsr = 0;                                             // clears the error flags

    int n = 0, start = -1, total = 0;
    uint32_t last = sent;
    *garbled = false;
    *last_byte = 0;
    for (;;) {
        uint32_t now = time_us_32();
        if (!uart_is_readable(uart0)) {
            if (discovery) {
                if (now - sent >= AFTER_DISCOVERY_US) break;
                if (n == 0 && now - sent >= ANSWER_BEGINS_US) break;   // nobody in that range
            } else if (now - last >= (n == 0 ? ANSWER_BEGINS_US : ANSWER_GAP_US)) {
                break;
            }
            continue;
        }
        uint32_t dr = u->dr;
        last = now;
        *last_byte = now;
        if (dr & UART_UARTDR_BE_BITS) {                     // a break
            if (n > 0) *garbled = true;                     // ... in the middle of an answer
            continue;
        }
        if (dr & (UART_UARTDR_FE_BITS | UART_UARTDR_OE_BITS | UART_UARTDR_PE_BITS)) *garbled = true;
        if (n >= cap) continue;                             // too much: keep listening, keep nothing
        uint8_t b = (uint8_t)dr;
        buf[n++] = b;
        if (discovery) continue;
        if (start < 0 && b == RDM_START_CODE) start = n - 1;
        if (start >= 0 && n == start + 3) total = start + b + 2;   // the length counts from the start code, the checksum is 2 more
        if (total && n >= total) break;
    }
    return n;
}

static void transaction(void) {
    send_packet(mb_pkt, (uint)mb_len);
    uint32_t sent = time_us_32();
    int n = 0;
    bool garbled = false;
    uint32_t last_byte = 0;
    if (mb_expect != RDM_EXPECT_NONE) {
        listen(true);
        n = receive(mb_raw, (int)sizeof mb_raw, sent, mb_expect == RDM_EXPECT_DISCOVERY, &garbled, &last_byte);
        listen(false);
    }
    // the quiet line before the next packet
    if (mb_expect == RDM_EXPECT_NONE)           wait_until(sent + AFTER_BROADCAST_US);
    else if (mb_expect == RDM_EXPECT_DISCOVERY) wait_until(sent + AFTER_DISCOVERY_US);
    else if (n == 0)                            wait_until(sent + AFTER_LOST_US);
    else                                        wait_until(last_byte + AFTER_ANSWER_US);

    mb_n = n;
    mb_garbled = garbled;
    __dmb();
    mb_state = MB_DONE;
}

// ---------------------------------------------------------------- core 1
void dmx_line_run(dmx_render_fn render) {
    static uint8_t frame[1 + DMX_CHANNELS];                 // start code + channels
    line_start();
    for (;;) {
        frame[0] = 0x00;                                    // DMX512 null start code
        render(frame + 1);
        send_packet(frame, sizeof frame);
        g_frames++;

        // RDM until the next frame is due: one transaction after the other
        // while core 0 hands them over (a search sends many), but not longer
        // than RDM_SLOT_US. A short wait for the next one, none if nothing came.
        uint32_t begin = time_us_32(), last = 0;
        bool any = false;
        for (;;) {
            uint32_t now = time_us_32();
            if (now - begin >= RDM_SLOT_US) break;
            if (mb_state == MB_POSTED) {
                __dmb();
                transaction();
                any = true;
                last = time_us_32();
            } else if (!any || now - last >= 1000) {
                break;
            }
        }
    }
}

uint32_t dmx_line_frames(void) { return g_frames; }
