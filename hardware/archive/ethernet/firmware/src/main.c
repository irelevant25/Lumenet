// dmxbox firmware - Raspberry Pi Pico + ENC28J60 -> DMX512
//
//   core 0: network (Art-Net + control protocol), buttons, LED, flash writes
//   core 1: engine render + DMX output, ~43 frames/s, nothing else
//
// The two cores share the engine under a critical section.
#include <stdio.h>
#include <string.h>
#include "pico/stdlib.h"
#include "pico/multicore.h"
#include "pico/critical_section.h"
#include "pico/flash.h"
#include "pico/unique_id.h"
#include "hardware/spi.h"

#include "config.h"
#include "platform.h"
#include "engine.h"
#include "artnet.h"
#include "ctrl.h"
#include "storage.h"
#include "enc28j60.h"
#include "net.h"
#include "dmx_out.h"

static engine_t           g_engine;
static preset_bank_t      g_bank;
static ctrl_t             g_ctrl;
static artnet_node_t      g_artnet;
static critical_section_t g_cs;
static uint8_t            g_frame[1 + DMX_CHANNELS];   // start code + channels

// ---------------------------------------------------------------- platform hooks
void plat_lock(void)   { critical_section_enter_blocking(&g_cs); }
void plat_unlock(void) { critical_section_exit(&g_cs); }
bool plat_save_presets(const preset_bank_t *b) { return storage_save(b); }

static inline uint32_t now_ms(void) { return to_ms_since_boot(get_absolute_time()); }

// ---------------------------------------------------------------- core 1: DMX
static void core1_main(void) {
    flash_safe_execute_core_init();   // lets core 0 pause us while writing flash
    dmx_out_init(pio0, PIN_DMX_TX, PIN_DMX_DE);
    g_frame[0] = 0x00;                // DMX512 null start code
    for (;;) {
        uint32_t now = now_ms();
        plat_lock();
        engine_render(&g_engine, now, g_frame + 1, true);
        plat_unlock();
        dmx_out_send(g_frame, sizeof g_frame);
    }
}

// ---------------------------------------------------------------- buttons
typedef struct { uint pin; bool stable, raw; uint32_t changed_ms; } button_t;

static void button_init(button_t *b, uint pin) {
    *b = (button_t){.pin = pin};
    gpio_init(pin);
    gpio_set_dir(pin, GPIO_IN);
    gpio_pull_up(pin);
}

// Returns true once per press (debounced, active low).
static bool button_pressed(button_t *b, uint32_t now) {
    bool raw = !gpio_get(b->pin);
    if (raw != b->raw) { b->raw = raw; b->changed_ms = now; }
    if (now - b->changed_ms >= 30 && raw != b->stable) {
        b->stable = raw;
        return raw;
    }
    return false;
}

// ---------------------------------------------------------------- ENC28J60 on SPI0
// The chip wants CS low >= 50 ns before the clock starts and, for some
// registers, >= 210 ns after it stops. One microsecond covers all of that.
static void enc_select(bool active) {
    if (active) {
        gpio_put(PIN_ENC_CS, 0);
        busy_wait_us_32(1);
    } else {
        busy_wait_us_32(1);
        gpio_put(PIN_ENC_CS, 1);
        busy_wait_us_32(1);
    }
}

static void enc_transfer(const uint8_t *tx, uint8_t *rx, size_t len) {
    if (tx && rx) spi_write_read_blocking(spi0, tx, rx, len);
    else if (tx)  spi_write_blocking(spi0, tx, len);
    else          spi_read_blocking(spi0, 0x00, rx, len);
}

static void enc_delay_us(uint32_t us) { busy_wait_us_32(us); }

static const enc_hal_t ENC_HAL = {enc_select, enc_transfer, enc_delay_us};

// ---------------------------------------------------------------- network
static uint8_t  g_rx_frame[ENC_MAX_FRAME];
static uint8_t  g_reply[CTRL_MAX_PKT];
static uint32_t g_last_rx_ms;

// One UDP datagram for this box has arrived: hand it to the right protocol
// and send the answer (if any) back to where it came from.
static void on_udp(uint16_t port, const net_peer_t *from, const uint8_t *data, int len) {
    uint32_t now = now_ms();
    int reply_len;
    if (port == ARTNET_PORT)    reply_len = artnet_handle(&g_artnet, data, len, now, g_reply, sizeof g_reply);
    else if (port == CTRL_PORT) reply_len = ctrl_handle(&g_ctrl, data, len, now, g_reply, sizeof g_reply);
    else return;
    g_last_rx_ms = now;
    if (reply_len > 0) net_udp_send(from, port, g_reply, reply_len);
}

static bool net_start(const net_config_t *cfg) {
    static bool pins_ready;
    if (!pins_ready) {
        spi_init(spi0, ENC_SPI_HZ);
        spi_set_format(spi0, 8, SPI_CPOL_0, SPI_CPHA_0, SPI_MSB_FIRST);
        gpio_set_function(PIN_ENC_MISO, GPIO_FUNC_SPI);
        gpio_set_function(PIN_ENC_SCK, GPIO_FUNC_SPI);
        gpio_set_function(PIN_ENC_MOSI, GPIO_FUNC_SPI);
        gpio_init(PIN_ENC_CS);  gpio_set_dir(PIN_ENC_CS, GPIO_OUT);  gpio_put(PIN_ENC_CS, 1);
        gpio_init(PIN_ENC_RST); gpio_set_dir(PIN_ENC_RST, GPIO_OUT); gpio_put(PIN_ENC_RST, 1);
        gpio_init(PIN_ENC_INT); gpio_set_dir(PIN_ENC_INT, GPIO_IN);  gpio_pull_up(PIN_ENC_INT);
        pins_ready = true;
    }
    gpio_put(PIN_ENC_RST, 0);  sleep_ms(1);    // reset pulse (the chip needs >= 400 ns)
    gpio_put(PIN_ENC_RST, 1);  sleep_ms(2);
    if (!enc_init(&ENC_HAL, cfg->mac)) return false;
    net_init(cfg, enc_send, on_udp);
    return true;
}

// Move waiting Ethernet frames from the chip into the stack. Bounded, so the
// buttons and the LED stay responsive under a flood of packets.
static void net_service(void) {
    for (int i = 0; i < 12; i++) {
        int len = enc_receive(g_rx_frame, sizeof g_rx_frame);
        if (len == 0) break;
        if (len > 0) net_input(g_rx_frame, len);
    }
}

// ---------------------------------------------------------------- main
int main(void) {
    stdio_init_all();
    critical_section_init(&g_cs);

    gpio_init(PIN_LED);
    gpio_set_dir(PIN_LED, GPIO_OUT);
#if PIN_DMX_DE >= 0
    gpio_init(PIN_DMX_DE);            // keep the line driver off until core 1 is ready
    gpio_set_dir(PIN_DMX_DE, GPIO_OUT);
    gpio_put(PIN_DMX_DE, 0);
#endif
    button_t btn_blackout, btn_preset;
    button_init(&btn_blackout, PIN_BTN_BLACKOUT);
    button_init(&btn_preset, PIN_BTN_PRESET);

    engine_init(&g_engine);
    storage_load(&g_bank);
    ctrl_init(&g_ctrl, &g_engine, &g_bank, now_ms());

    pico_unique_board_id_t uid;
    pico_get_unique_board_id(&uid);
    net_config_t cfg = {
        .mac = {0x02, uid.id[3], uid.id[4], uid.id[5], uid.id[6], uid.id[7]},  // locally administered
        .ip = NET_IP, .netmask = NET_NETMASK,
    };
    g_artnet = (artnet_node_t){.eng = &g_engine, .universe = ARTNET_UNIVERSE};
    memcpy(g_artnet.ip, cfg.ip, 4);
    memcpy(g_artnet.mac, cfg.mac, 6);

    multicore_launch_core1(core1_main);   // DMX runs from here on, network or not

    bool net_ok = net_start(&cfg);
    printf("dmxbox %d.%d  ip %d.%d.%d.%d  artnet universe %d  ctrl port %d  enc28j60 %s\n",
           FW_VERSION_MAJOR, FW_VERSION_MINOR, cfg.ip[0], cfg.ip[1], cfg.ip[2], cfg.ip[3],
           ARTNET_UNIVERSE, CTRL_PORT, net_ok ? "ok" : "NOT FOUND");
    if (net_ok) printf("enc28j60 silicon revision %d\n", enc_revision());

    uint32_t last_check = 0;
    bool link = false;

    for (;;) {
        uint32_t now = now_ms();

        if (net_ok) net_service();

        if (now - last_check >= 1000) {
            last_check = now;
            if (!net_ok) {
                net_ok = net_start(&cfg);       // retry: module plugged in late?
                if (net_ok) printf("enc28j60 found, silicon revision %d\n", enc_revision());
            } else {
                bool l = enc_link_up();
                if (l != link) {
                    const net_stats_t *s = net_stats();
                    printf("link %s  (frames %lu, udp in %lu, udp out %lu, dropped %lu)\n", l ? "up" : "down",
                           (unsigned long)s->frames, (unsigned long)s->udp_in, (unsigned long)s->udp_out,
                           (unsigned long)s->dropped);
                    if (l) net_announce();      // tell the network where this IP address lives
                }
                link = l;
            }
        }

        if (button_pressed(&btn_blackout, now)) ctrl_toggle_blackout(&g_ctrl);
        if (button_pressed(&btn_preset, now))
            ctrl_preset_go(&g_ctrl, BUTTON_PRESET_SLOT, BUTTON_PRESET_FADE_MS, now);

        // LED: fast blink = no ENC28J60, slow blink = no link, on = link,
        // flicker = packets arriving.
        bool led;
        if (!net_ok)      led = (now / 100) & 1;
        else if (!link)   led = (now / 500) & 1;
        else              led = (now - g_last_rx_ms) > 40 || ((now / 40) & 1);
        gpio_put(PIN_LED, led);
    }
}
