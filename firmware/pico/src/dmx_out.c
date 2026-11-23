#include "dmx_out.h"
#include "dmx_tx.pio.h"
#include "hardware/clocks.h"
#include "hardware/dma.h"
#include "hardware/gpio.h"

static PIO  g_pio;
static uint g_sm;
static uint g_offset;
static int  g_dma;

void dmx_out_init(PIO pio, uint pin_tx, int pin_de) {
    g_pio = pio;
    g_sm = pio_claim_unused_sm(pio, true);
    g_offset = pio_add_program(pio, &dmx_tx_program);

    pio_sm_config c = dmx_tx_program_get_default_config(g_offset);
    sm_config_set_out_pins(&c, pin_tx, 1);
    sm_config_set_sideset_pins(&c, pin_tx);
    sm_config_set_out_shift(&c, true, false, 32);         // shift right = LSB first, manual pull
    sm_config_set_fifo_join(&c, PIO_FIFO_JOIN_TX);         // 8-deep TX FIFO
    sm_config_set_clkdiv(&c, (float)clock_get_hz(clk_sys) / 1000000.0f);  // 1 MHz

    pio_sm_set_pins_with_mask(pio, g_sm, 1u << pin_tx, 1u << pin_tx);    // idle high
    pio_sm_set_consecutive_pindirs(pio, g_sm, pin_tx, 1, true);
    pio_gpio_init(pio, pin_tx);
    // The pin sinks the optocoupler LED current (about 9 mA) when it is low.
    gpio_set_drive_strength(pin_tx, GPIO_DRIVE_STRENGTH_12MA);

    pio_sm_init(pio, g_sm, g_offset + dmx_tx_offset_idle, &c);
    pio_sm_set_enabled(pio, g_sm, true);

    g_dma = dma_claim_unused_channel(true);
    dma_channel_config d = dma_channel_get_default_config((uint)g_dma);
    channel_config_set_transfer_data_size(&d, DMA_SIZE_8);  // byte is replicated across the word
    channel_config_set_read_increment(&d, true);
    channel_config_set_write_increment(&d, false);
    channel_config_set_dreq(&d, pio_get_dreq(pio, g_sm, true));
    dma_channel_configure((uint)g_dma, &d, &pio->txf[g_sm], NULL, 0, false);

    // With a switchable RS-485 driver, enable it only now: during boot DE is
    // held low by its pull-down so the line never sees garbage. (pin_de < 0:
    // DE is wired high. The line then idles high from power-on, because an
    // unpowered optocoupler LED means "mark".)
    if (pin_de >= 0) {
        gpio_init((uint)pin_de);
        gpio_set_dir((uint)pin_de, GPIO_OUT);
        gpio_put((uint)pin_de, 1);
    }
}

static void wait_idle(void) {
    // TXSTALL is set while the SM is stalled on an empty FIFO, i.e. after
    // the final stop bit. Clear it, then wait for it to come back.
    uint32_t mask = 1u << (PIO_FDEBUG_TXSTALL_LSB + g_sm);
    g_pio->fdebug = mask;
    while (!(g_pio->fdebug & mask)) tight_loop_contents();
}

void dmx_out_send(const uint8_t *frame, uint len) {
    pio_sm_exec(g_pio, g_sm, pio_encode_jmp(g_offset + dmx_tx_offset_frame_start));
    dma_channel_transfer_from_buffer_now((uint)g_dma, frame, len);
    dma_channel_wait_for_finish_blocking((uint)g_dma);
    wait_idle();
}
