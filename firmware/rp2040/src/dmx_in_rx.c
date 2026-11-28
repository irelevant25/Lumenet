#include "dmx_in_rx.h"
#include "config.h"

#if BOX_DMX_IN
#include "pico/stdlib.h"
#include "hardware/clocks.h"
#include "hardware/irq.h"
#include "hardware/pio.h"
#include "dmx_rx.pio.h"

static const PIO g_pio = pio1;       // pio0 sends (dmx_tx.pio)
static uint      g_sm;
static dmx_in_t *g_in;

// The RX FIFO has words: every byte that came, with its framing error (dmx_rx.pio).
static void on_rx(void) {
    uint32_t now = to_ms_since_boot(get_absolute_time());
    uint32_t stalled = 1u << (PIO_FDEBUG_RXSTALL_LSB + g_sm);
    if (g_pio->fdebug & stalled) {   // the FIFO was full (interrupts were off, a flash write): bytes were lost
        g_pio->fdebug = stalled;
        dmx_in_lost(g_in);
    }
    while (!pio_sm_is_rx_fifo_empty(g_pio, g_sm)) {
        uint32_t w = pio_sm_get(g_pio, g_sm);
        dmx_in_byte(g_in, (uint8_t)(w >> 23), (w >> 31) != 0, now);
    }
}

void dmx_in_rx_start(dmx_in_t *in) {
    g_in = in;
    g_sm = pio_claim_unused_sm(g_pio, true);
    uint offset = pio_add_program(g_pio, &dmx_rx_program);

    pio_sm_config c = dmx_rx_program_get_default_config(offset);
    sm_config_set_in_pins(&c, PIN_DMX_IN);
    sm_config_set_jmp_pin(&c, PIN_DMX_IN);
    sm_config_set_in_shift(&c, true, false, 32);           // shift right = LSB first, pushed by hand
    sm_config_set_fifo_join(&c, PIO_FIFO_JOIN_RX);          // room for 8 bytes
    sm_config_set_clkdiv(&c, (float)clock_get_hz(clk_sys) / (8.0f * 250000.0f));   // 8 cycles a bit

    // The optocoupler's output, pulled up by R38 (and by the RP2040's own pull-up)
    pio_sm_set_consecutive_pindirs(g_pio, g_sm, PIN_DMX_IN, 1, false);
    pio_gpio_init(g_pio, PIN_DMX_IN);
    gpio_pull_up(PIN_DMX_IN);
    pio_sm_init(g_pio, g_sm, offset, &c);

    pio_set_irq0_source_enabled(g_pio, pio_get_rx_fifo_not_empty_interrupt_source(g_sm), true);
    irq_set_exclusive_handler(PIO1_IRQ_0, on_rx);
    irq_set_enabled(PIO1_IRQ_0, true);
    pio_sm_set_enabled(g_pio, g_sm, true);
}

#else   // a box without DMX IN never starts it
void dmx_in_rx_start(dmx_in_t *in) { (void)in; }
#endif
