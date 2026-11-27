#include "midi.h"
#include "pico/stdlib.h"
#include "hardware/irq.h"
#include "hardware/uart.h"
#include "tusb.h"
#include "config.h"
#include "midi_parse.h"

#define MIDI_BAUD 31250
#define RING 256                                    // a power of two

// MIDI IN: the UART interrupt fills this ring, the main loop empties it.
static uint8_t           in_ring[RING];
static volatile uint16_t in_head;                  // written by the interrupt
static uint16_t          in_tail;
// MIDI OUT: bytes from the computer wait here until the UART takes them.
static uint8_t           out_ring[RING];
static uint16_t          out_head, out_tail;

static midi_parser_t g_parser;
static uint32_t      g_last_ms;

static void on_uart1(void) {
    while (uart_is_readable(uart1)) {
        uint8_t b = (uint8_t)uart_get_hw(uart1)->dr;
        uint16_t next = (uint16_t)((in_head + 1) & (RING - 1));
        if (next != in_tail) {                      // full: the byte is lost
            in_ring[in_head] = b;
            in_head = next;
        }
    }
}

void midi_init(void) {
    midi_parser_init(&g_parser);
    uart_init(uart1, MIDI_BAUD);
    gpio_set_function(PIN_MIDI_TX, GPIO_FUNC_UART);
    gpio_set_function(PIN_MIDI_RX, GPIO_FUNC_UART);
    irq_set_exclusive_handler(UART1_IRQ, on_uart1);
    irq_set_enabled(UART1_IRQ, true);
    uart_set_irqs_enabled(uart1, true, false);      // an interrupt when bytes have come in
}

static uint32_t now_ms(void) { return to_ms_since_boot(get_absolute_time()); }

void midi_poll(void) {
    // MIDI IN -> computer
    while (in_tail != in_head) {
        uint8_t b = in_ring[in_tail];
        in_tail = (uint16_t)((in_tail + 1) & (RING - 1));
        g_last_ms = now_ms();
        uint8_t pkt[4];
        if (midi_parse(&g_parser, b, pkt) && tud_midi_mounted())
            tud_midi_packet_write(pkt);             // false: the computer does not read; then it is lost
    }

    // computer -> MIDI OUT
    while (tud_midi_available()) {
        uint16_t room = (uint16_t)((out_tail - out_head - 1) & (RING - 1));
        if (room < 3) break;                        // the rest waits in TinyUSB
        uint8_t pkt[4], bytes[3];
        if (!tud_midi_packet_read(pkt)) break;
        int n = midi_packet_bytes(pkt, bytes);
        for (int i = 0; i < n; i++) {
            out_ring[out_head] = bytes[i];
            out_head = (uint16_t)((out_head + 1) & (RING - 1));
        }
        if (n) g_last_ms = now_ms();
    }
    while (out_tail != out_head && uart_is_writable(uart1)) {
        uart_get_hw(uart1)->dr = out_ring[out_tail];
        out_tail = (uint16_t)((out_tail + 1) & (RING - 1));
    }
}

uint32_t midi_last_ms(void) { return g_last_ms; }
