// Pretend hardware for the PC test of the RP2040 firmware: see fake_pico.h.
#include "fake_pico.h"

#include <setjmp.h>
#include <stdarg.h>
#include <stdio.h>
#include <string.h>

#include "pico/stdlib.h"
#undef printf                       // this file uses the real one
#include "pico/bootrom.h"
#include "pico/critical_section.h"
#include "pico/flash.h"
#include "pico/multicore.h"
#include "pico/unique_id.h"
#include "tusb.h"

#include "board.h"
#include "config.h"
#include "dmx_in_rx.h"
#include "dmx_line.h"
#include "midi.h"
#include "platform.h"
#include "rdm.h"
#include "rdm_lights.h"
#include "storage.h"

#define MAX_EVENTS 600
#define MAX_IN     600              // frames from the computer in one scenario
#define MAX_OUT    600              // frames to the computer
#define ARENA_SIZE (1024 * 1024)
#define MAX_MS     40000
#define DMX_SLOTS  8

// How TinyUSB's NCM driver packs frames into blocks ("NTB") for the computer
#define TX_BLOCKS        CFG_TUD_NCM_IN_NTB_N
#define BLOCK_SIZE       CFG_TUD_NCM_IN_NTB_MAX_SIZE
#define BLOCK_MAX_FRAMES 8                              // CFG_TUD_NCM_IN_MAX_DATAGRAMS_PER_NTB
#define BLOCK_HEADER     (12 + 8 + 4 * (BLOCK_MAX_FRAMES + 1))

// ---------------------------------------------------------------- scenario storage
typedef enum { EV_CONNECT, EV_DISCONNECT, EV_SUSPEND, EV_FETCH, EV_BLOCK, EV_SAMPLE, EV_CONSOLE } ev_kind_t;
typedef struct { uint32_t ms; ev_kind_t kind; int a, b; } event_t;
typedef struct { int offset, len; } blob_t;

static event_t events[MAX_EVENTS];
static int     n_events, next_event;
static uint32_t add_ms;                                 // time for the events being added
static int     block_first = -1;                        // >= 0 while fake_block_begin() is open

static uint8_t arena[ARENA_SIZE];
static int     arena_used;
static blob_t  in_frames[MAX_IN];
static int     n_in;

typedef struct { blob_t data; int block; bool delivered; uint32_t ms; } out_frame_t;
static out_frame_t out_frames[MAX_OUT];
static int         n_out;
static int         host_index[MAX_OUT];                 // frames in the order the computer received them
static int         n_host;

static uint32_t now_ms;
static uint8_t  led_history[MAX_MS + 1];                // bit 0 green, 1 yellow, 2 red
static uint8_t  dmx_samples[DMX_SLOTS][1 + DMX_CHANNELS];
static int      dmx_sample_len[DMX_SLOTS];
static char     log_text[8192];
static int      log_len;
static int      violations;
static const char *last_violation = "";

static void violation(const char *what) {
    violations++;
    last_violation = what;
}

static blob_t store(const uint8_t *data, int len) {
    blob_t b = {arena_used, len};
    if (arena_used + len > ARENA_SIZE) { violation("test: arena full"); b.len = 0; return b; }
    memcpy(arena + arena_used, data, (size_t)len);
    arena_used += len;
    return b;
}

static void add_event(ev_kind_t kind, int a, int b) {
    if (n_events >= MAX_EVENTS) { violation("test: too many events"); return; }
    if (n_events > 0 && events[n_events - 1].ms > add_ms) violation("test: events must be added in order of time");
    events[n_events++] = (event_t){add_ms, kind, a, b};
}

// ---------------------------------------------------------------- the RP2040: clock, log, flash, serial number
static preset_bank_t flash_copy;
static bool          flash_has_data;
static int           saves;
static bool          in_main;
static uint32_t      run_until;
static jmp_buf       main_exit;
static uint32_t      bootloader_ms;

absolute_time_t get_absolute_time(void) { return (absolute_time_t)now_ms * 1000u; }
uint32_t to_ms_since_boot(absolute_time_t t) { return (uint32_t)(t / 1000u); }
void sleep_ms(uint32_t ms) { now_ms += ms; }
void stdio_uart_init_full(uart_inst_t *uart, uint baud_rate, int tx_pin, int rx_pin) {
    (void)uart; (void)rx_pin;
    if (baud_rate != LOG_BAUD || tx_pin != PIN_LOG_TX) violation("the log is not on its pin or not at its speed");
}

void reset_usb_boot(uint32_t usb_activity_gpio_pin_mask, uint32_t disable_interface_mask) {
    (void)usb_activity_gpio_pin_mask; (void)disable_interface_mask;
    bootloader_ms = now_ms;
    if (in_main) longjmp(main_exit, 1);                 // the real one never returns
}

int fake_printf(const char *format, ...) {
    va_list args;
    va_start(args, format);
    int room = (int)sizeof log_text - log_len;
    int n = vsnprintf(log_text + log_len, (size_t)room, format, args);
    va_end(args);
    if (n > 0) log_len += n < room ? n : room - 1;
    return n;
}

// ---------------------------------------------------------------- the board (board.h, midi.h)
static uint8_t box_number;
static bool    board_ready, line_ready, midi_ready;

void board_init(void) { board_ready = true; }
uint8_t board_box_number(void) { return box_number; }
uint16_t board_supply_mv(void) { return 5000; }
void board_poll(uint32_t now) { (void)now; }
bool board_out_powered(void) { return false; }

// main() sets the LEDs as the last thing in each round of its loop. The fake
// clock ticks there: one round = one millisecond. This is also where a run ends.
void board_leds(bool usb, bool dmx, bool midi) {
    if (!in_main) return;
    if (now_ms <= MAX_MS) led_history[now_ms] = (uint8_t)((usb ? 1 : 0) | (dmx ? 2 : 0) | (midi ? 4 : 0));
    now_ms++;
    if (now_ms >= run_until) longjmp(main_exit, 1);
}

// ---------------------------------------------------------------- DMX IN (dmx_in_rx.h): a console
static dmx_in_t *dmx_in;

void dmx_in_rx_start(dmx_in_t *in) { dmx_in = in; }

void midi_init(void) { midi_ready = true; }
void midi_poll(void) {}
uint32_t midi_last_ms(void) { return 0; }

void critical_section_init(critical_section_t *cs) { cs->depth = 0; }
void critical_section_enter_blocking(critical_section_t *cs) { if (cs->depth++ != 0) violation("critical section entered twice"); }
void critical_section_exit(critical_section_t *cs) { if (--cs->depth != 0) violation("critical section left twice"); }

void pico_get_unique_board_id(pico_unique_board_id_t *id_out) {
    static const uint8_t id[8] = {0xE6, 0x61, 0x41, 0x04, 0x03, 0x12, 0x34, 0x56};
    memcpy(id_out->id, id, 8);
}

void pico_get_unique_board_id_string(char *id_out, uint len) {
    snprintf(id_out, len, "E661410403123456");
}

void storage_load(preset_bank_t *b) {
    if (flash_has_data) *b = flash_copy;
    else memset(b, 0xFF, sizeof *b);                    // empty flash reads as all ones
    presets_validate(b);
}

bool storage_save(const preset_bank_t *b) {
    flash_copy = *b;
    flash_has_data = true;
    saves++;
    return true;
}

// ---------------------------------------------------------------- core 1 and the DMX line (dmx_line.h)
static void (*core1_entry)(void);
static jmp_buf core1_exit;
static uint8_t dmx_packet[1 + DMX_CHANNELS];
static int     dmx_packet_len;
static bool    core1_flash_ready;
static uint32_t frames;

void multicore_launch_core1(void (*entry)(void)) { core1_entry = entry; }
bool flash_safe_execute_core_init(void) { core1_flash_ready = true; return true; }
void dmx_line_init(void) { line_ready = true; }
uint32_t dmx_line_frames(void) { return frames; }

// Core 1 never returns from its loop. The fake lets it work out exactly one
// frame and then jumps back out.
void dmx_line_run(dmx_render_fn render) {
    dmx_packet[0] = 0x00;
    render(dmx_packet + 1);
    dmx_packet_len = (int)sizeof dmx_packet;
    frames++;
    longjmp(core1_exit, 1);
}

// The RDM side of the line: the lights in memory answer at once, as in the simulator.
static uint8_t rdm_answer[RDM_MAX_PKT], rdm_last[RDM_MAX_PKT];
static int     rdm_answer_len, rdm_last_len, rdm_packets;
static bool    rdm_garbled, rdm_waiting;

void plat_rdm_send(const uint8_t *pkt, int len, uint8_t expect) {
    (void)expect;
    if (rdm_waiting) violation("an RDM packet was sent before the line had answered the one before");
    memcpy(rdm_last, pkt, (size_t)len);
    rdm_last_len = len;
    rdm_packets++;
    rdm_answer_len = lights_hear(pkt, len, rdm_answer, (int)sizeof rdm_answer, &rdm_garbled);
    rdm_waiting = true;
}

int plat_rdm_poll(uint8_t *raw, int cap, int *n, bool *garbled) {
    if (!rdm_waiting) return 0;
    rdm_waiting = false;
    *n = rdm_answer_len < cap ? rdm_answer_len : cap;
    memcpy(raw, rdm_answer, (size_t)*n);
    *garbled = rdm_garbled;
    return 1;
}

static void sample_dmx(int slot) {
    if (!core1_entry) { violation("test: core 1 was never started"); return; }
    dmx_packet_len = 0;
    if (setjmp(core1_exit) == 0) core1_entry();
    memcpy(dmx_samples[slot], dmx_packet, (size_t)dmx_packet_len);
    dmx_sample_len[slot] = dmx_packet_len;
}

// ---------------------------------------------------------------- TinyUSB and the computer
typedef enum { B_FREE, B_FILLING, B_READY, B_ON_ITS_WAY } block_state_t;

static struct {
    int  inits;
    bool mounted, suspended, data_on, host_fetching;

    // towards the box: TinyUSB keeps one block (CFG_TUD_NCM_OUT_NTB_N = 1)
    int  rx_first, rx_count, rx_next;                   // the block TinyUSB holds, and the next frame to hand over
    bool rx_have_block;
    bool rx_listening;                                  // a USB transfer for the next block has been started
    bool rx_renew_active, rx_renew_again;
    bool in_recv_cb;
    int  wait_first[MAX_EVENTS], wait_count[MAX_EVENTS];   // transfers the computer could not deliver yet
    int  n_wait, next_wait;

    // towards the computer
    block_state_t tx_state[TX_BLOCKS];
    int  tx_frames[TX_BLOCKS], tx_bytes[TX_BLOCKS];
    int  tx_ready[TX_BLOCKS], n_ready;                  // blocks waiting to be sent, oldest first
    int  tx_filling, tx_on_its_way;                     // block numbers, -1 = none
    bool tx_allowed;                                    // tud_network_can_xmit() said yes and no frame followed yet
} usb;

static void usb_driver_reset(void) {                    // netd_reset(): on a bus reset everything starts over
    usb.data_on = false;
    usb.rx_have_block = usb.rx_listening = usb.rx_renew_active = usb.rx_renew_again = false;
    usb.n_wait = usb.next_wait = 0;
    for (int i = 0; i < TX_BLOCKS; i++) usb.tx_state[i] = B_FREE;
    usb.n_ready = 0;
    usb.tx_filling = usb.tx_on_its_way = -1;
    usb.tx_allowed = false;
}

bool tud_init(uint8_t rhport) { (void)rhport; usb.inits++; return true; }
bool tud_mounted(void) { return usb.mounted; }
bool tud_suspended(void) { return usb.suspended; }
bool tud_ready(void) { return usb.mounted && !usb.suspended; }

static void xmit_start_if_possible(void) {
    if (usb.tx_on_its_way >= 0 || !usb.data_on) return;
    int b = -1;
    if (usb.n_ready > 0) {
        b = usb.tx_ready[0];
        for (int i = 1; i < usb.n_ready; i++) usb.tx_ready[i - 1] = usb.tx_ready[i];
        usb.n_ready--;
    } else if (usb.tx_filling >= 0 && usb.tx_frames[usb.tx_filling] > 0) {
        b = usb.tx_filling;
        usb.tx_filling = -1;
    }
    if (b < 0) return;
    usb.tx_state[b] = B_ON_ITS_WAY;
    usb.tx_on_its_way = b;
}

bool tud_network_can_xmit(uint16_t size) {
    if (usb.in_recv_cb) violation("tud_network_can_xmit() called from inside tud_network_recv_cb()");
    int padded = (size + 3) & ~3;
    int f = usb.tx_filling;
    if (f >= 0 && usb.tx_frames[f] < BLOCK_MAX_FRAMES && usb.tx_bytes[f] + padded <= BLOCK_SIZE) {
        usb.tx_allowed = true;
        return true;
    }
    if (f >= 0) {                                       // the frame does not fit: this block is complete
        usb.tx_state[f] = B_READY;
        usb.tx_ready[usb.n_ready++] = f;
        usb.tx_filling = -1;
    }
    for (int i = 0; i < TX_BLOCKS; i++) {
        if (usb.tx_state[i] == B_FREE) {
            usb.tx_state[i] = B_FILLING;
            usb.tx_frames[i] = 0;
            usb.tx_bytes[i] = BLOCK_HEADER;
            usb.tx_filling = i;
            usb.tx_allowed = true;
            return true;
        }
    }
    xmit_start_if_possible();
    usb.tx_allowed = false;
    return false;
}

void tud_network_xmit(void *ref, uint16_t arg) {
    if (usb.in_recv_cb) violation("tud_network_xmit() called from inside tud_network_recv_cb()");
    if (!usb.tx_allowed || usb.tx_filling < 0) { violation("tud_network_xmit() without a yes from tud_network_can_xmit()"); return; }
    usb.tx_allowed = false;
    static uint8_t buffer[BLOCK_SIZE];
    uint16_t size = tud_network_xmit_cb(buffer, ref, arg);
    if (n_out >= MAX_OUT) { violation("test: too many frames sent"); return; }
    int f = usb.tx_filling;
    out_frames[n_out++] = (out_frame_t){store(buffer, size), f, false, 0};
    usb.tx_frames[f]++;
    usb.tx_bytes[f] += (size + 3) & ~3;
    if (usb.tx_bytes[f] > BLOCK_SIZE) violation("a frame was put into a block it does not fit in");
    xmit_start_if_possible();
}

// Hand the next waiting frame to the firmware: one frame per call, as in TinyUSB.
static void recv_hand_over_one(void) {
    if (!usb.rx_have_block) return;
    blob_t f = in_frames[usb.rx_first + usb.rx_next];
    usb.in_recv_cb = true;
    bool taken = tud_network_recv_cb(arena + f.offset, (uint16_t)f.len);
    usb.in_recv_cb = false;
    if (!taken) return;                                 // offered again at the next tud_network_recv_renew()
    if (++usb.rx_next == usb.rx_count) usb.rx_have_block = false;   // block used up: TinyUSB's buffer is free again
}

void tud_network_recv_renew(void) {
    usb.rx_renew_again = true;
    if (usb.rx_renew_active) return;                    // called from inside the callback: handled by the loop below
    while (usb.rx_renew_again) {
        usb.rx_renew_again = false;
        usb.rx_renew_active = true;
        recv_hand_over_one();
        usb.rx_renew_active = false;
    }
    // With a single receive buffer, the next transfer can only start once the block is used up.
    if (usb.data_on && !usb.rx_have_block) usb.rx_listening = true;
}

static void fire(const event_t *e) {
    switch (e->kind) {
    case EV_CONNECT:                                    // set up by the computer, then the adapter is switched on
        usb_driver_reset();
        usb.mounted = true;
        usb.suspended = false;
        usb.data_on = true;
        tud_network_recv_renew();
        break;
    case EV_DISCONNECT:
        usb_driver_reset();
        usb.mounted = false;
        break;
    case EV_SUSPEND: usb.suspended = e->a != 0; break;
    case EV_FETCH:   usb.host_fetching = e->a != 0; break;
    case EV_BLOCK:
        usb.wait_first[usb.n_wait] = e->a;
        usb.wait_count[usb.n_wait++] = e->b;
        break;
    case EV_SAMPLE:  sample_dmx(e->a); break;
    case EV_CONSOLE:                                    // what the receiver of DMX IN would hand over
        if (dmx_in) dmx_in_frame(dmx_in, arena + e->a, e->b, now_ms);
        break;
    }
}

void tud_task(void) {
    if (usb.in_recv_cb) violation("tud_task() called from inside tud_network_recv_cb()");
    while (next_event < n_events && events[next_event].ms <= now_ms) fire(&events[next_event++]);
    if (!usb.mounted) return;

    // a USB transfer from the computer has arrived
    if (usb.rx_listening && usb.data_on && usb.next_wait < usb.n_wait) {
        usb.rx_first = usb.wait_first[usb.next_wait];
        usb.rx_count = usb.wait_count[usb.next_wait++];
        usb.rx_next = 0;
        usb.rx_have_block = true;
        usb.rx_listening = false;
        tud_network_recv_renew();
    }

    // the computer has fetched the block that was on its way
    if (usb.tx_on_its_way >= 0 && usb.host_fetching && !usb.suspended) {
        for (int i = 0; i < n_out; i++) {
            if (out_frames[i].block == usb.tx_on_its_way && !out_frames[i].delivered) {
                out_frames[i].delivered = true;
                out_frames[i].ms = now_ms;
                host_index[n_host++] = i;
            }
        }
        usb.tx_state[usb.tx_on_its_way] = B_FREE;
        usb.tx_on_its_way = -1;
        xmit_start_if_possible();
    }
}

static struct { void *buffer; uint16_t len; } control_reply;

bool tud_control_xfer(uint8_t rhport, tusb_control_request_t const *request, void *buffer, uint16_t len) {
    (void)rhport;
    control_reply.buffer = buffer;
    control_reply.len = len < request->wLength ? len : request->wLength;   // never more than was asked for
    return true;
}

// for test_net.c: what the last tud_control_xfer() handed over
const uint8_t *fake_control_reply(int *len) {
    *len = control_reply.len;
    return (const uint8_t *)control_reply.buffer;
}

// TinyUSB's own network adapter driver. The firmware wraps it and hands on
// every request it does not answer itself; here they are only counted.
static int library_control_requests;

void netd_init(void) {}
bool netd_deinit(void) { return true; }
void netd_reset(uint8_t rhport) { (void)rhport; }
uint16_t netd_open(uint8_t rhport, tusb_desc_interface_t const *itf_desc, uint16_t max_len) {
    (void)rhport; (void)itf_desc; (void)max_len;
    return 0;
}
bool netd_control_xfer_cb(uint8_t rhport, uint8_t stage, tusb_control_request_t const *request) {
    (void)rhport; (void)stage; (void)request;
    library_control_requests++;
    return true;
}
bool netd_xfer_cb(uint8_t rhport, uint8_t ep_addr, xfer_result_t result, uint32_t xferred_bytes) {
    (void)rhport; (void)ep_addr; (void)result; (void)xferred_bytes;
    return true;
}

// for test_net.c: how many requests the firmware handed on to TinyUSB's driver
int fake_library_control_requests(void) { return library_control_requests; }

// ---------------------------------------------------------------- remote control
void fake_reset(bool keep_flash) {
    n_events = next_event = 0;
    add_ms = 0;
    block_first = -1;
    arena_used = n_in = n_out = n_host = 0;
    now_ms = 0;
    memset(led_history, 0, sizeof led_history);
    memset(dmx_sample_len, 0, sizeof dmx_sample_len);
    log_len = 0;
    log_text[0] = 0;
    violations = 0;
    last_violation = "";
    if (!keep_flash) flash_has_data = false;
    saves = 0;
    core1_entry = NULL;
    core1_flash_ready = false;
    frames = 0;
    box_number = 7;
    board_ready = line_ready = midi_ready = false;
    bootloader_ms = 0;
    dmx_in = NULL;
    lights_make(0);
    rdm_packets = rdm_last_len = 0;
    rdm_waiting = false;
    memset(&usb, 0, sizeof usb);
    usb.host_fetching = true;
    usb_driver_reset();
    control_reply.buffer = NULL;
    control_reply.len = 0;
}

void fake_at(uint32_t ms) { add_ms = ms; }
void fake_connect(void) { add_event(EV_CONNECT, 0, 0); }
void fake_disconnect(void) { add_event(EV_DISCONNECT, 0, 0); }
void fake_suspend(bool asleep) { add_event(EV_SUSPEND, asleep, 0); }
void fake_host_fetch(bool on) { add_event(EV_FETCH, on, 0); }
void fake_box_number(uint8_t n) { box_number = n; }
void fake_rdm_lights(int n) { lights_make(n); }
void fake_sample_dmx(int slot) { add_event(EV_SAMPLE, slot, 0); }

void fake_console(const uint8_t *levels, int count) {
    blob_t b = store(levels, count);
    add_event(EV_CONSOLE, b.offset, b.len);
}

void fake_frame(const uint8_t *frame, int len) {
    if (n_in >= MAX_IN) { violation("test: too many frames"); return; }
    in_frames[n_in++] = store(frame, len);
    if (block_first < 0) add_event(EV_BLOCK, n_in - 1, 1);
}

void fake_block_begin(void) { block_first = n_in; }

void fake_block_end(void) {
    if (block_first >= 0 && n_in > block_first) add_event(EV_BLOCK, block_first, n_in - block_first);
    block_first = -1;
}

int firmware_main(void);                                // the real main() of the firmware, see firmware_main.c

void fake_run_firmware(uint32_t until_ms) {
    run_until = until_ms;
    in_main = true;
    if (setjmp(main_exit) == 0) firmware_main();
    in_main = false;
}

void fake_time_set(uint32_t ms) { now_ms = ms; }
uint32_t fake_now(void) { return now_ms; }

int fake_host_count(void) { return n_host; }
const uint8_t *fake_host_frame(int i, int *len) {
    const out_frame_t *f = &out_frames[host_index[i]];
    *len = f->data.len;
    return arena + f->data.offset;
}
uint32_t fake_host_frame_ms(int i) { return out_frames[host_index[i]].ms; }
void fake_host_clear(void) { n_host = 0; }
int fake_host_waiting(void) { return (usb.n_wait - usb.next_wait) + (usb.rx_have_block ? 1 : 0); }
int fake_leds(uint32_t ms) { return ms <= MAX_MS ? led_history[ms] : 0; }
const uint8_t *fake_dmx(int slot, int *len) { *len = dmx_sample_len[slot]; return dmx_samples[slot]; }
const char *fake_log(void) { return log_text; }
int fake_saves(void) { return saves; }
int fake_usb_starts(void) { return usb.inits; }
bool fake_board_ready(void) { return board_ready && line_ready && midi_ready; }
bool fake_dmx_in_started(void) { return dmx_in != NULL; }
bool fake_core1_flash_ready(void) { return core1_flash_ready; }
int fake_rdm_packets(void) { return rdm_packets; }
const uint8_t *fake_rdm_last(int *len) { *len = rdm_last_len; return rdm_last; }
uint32_t fake_bootloader_ms(void) { return bootloader_ms; }
int fake_violations(void) { return violations; }
const char *fake_last_violation(void) { return last_violation; }
