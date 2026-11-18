// dmxbox firmware - Raspberry Pi Pico: USB in, DMX512 out
//
// The Pico is plugged into a computer with a USB cable and shows up there as a
// network adapter. Art-Net and the control protocol travel over that "network
// cable inside the USB cable", exactly as they would over real Ethernet. The
// same cable powers the box.
//
//   core 0: USB, network (Art-Net, control protocol, address hand-out),
//           buttons, LED, flash writes
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

#include "config.h"
#include "platform.h"
#include "engine.h"
#include "artnet.h"
#include "ctrl.h"
#include "storage.h"
#include "net.h"
#include "dhcp_server.h"
#include "usb_net.h"
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

// ---------------------------------------------------------------- on-board LED
// The Pico SDK knows the LED pin of the board it builds for (GP25 on a Pico
// and a Pico 2). A Pico W has its LED on the radio chip instead of a normal
// pin; built for that board, the LED simply stays dark.
#ifdef PICO_DEFAULT_LED_PIN
static void led_init(void) {
    gpio_init(PICO_DEFAULT_LED_PIN);
    gpio_set_dir(PICO_DEFAULT_LED_PIN, GPIO_OUT);
}
static void led_set(bool on) { gpio_put(PICO_DEFAULT_LED_PIN, on); }
#else
static void led_init(void) {}
static void led_set(bool on) { (void)on; }
#endif

// ---------------------------------------------------------------- network
static uint8_t  g_reply[CTRL_MAX_PKT];
static uint32_t g_last_rx_ms;

// Answers of the address hand-out (DHCP) go to "everybody": the computer that
// asked has no address of its own yet.
static const net_peer_t DHCP_EVERYBODY = {
    .mac = {0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF},
    .ip = {255, 255, 255, 255},
    .port = DHCP_CLIENT_PORT,
};

// One UDP datagram for this box has arrived: hand it to the right protocol
// and send the answer (if any).
static void on_udp(uint16_t port, const net_peer_t *from, const uint8_t *data, int len) {
    uint32_t now = now_ms();
    const net_peer_t *to = from;      // answers normally go back to where the request came from
    int reply_len;
    int room = (int)sizeof g_reply;
    if (port == ARTNET_PORT) {
        reply_len = artnet_handle(&g_artnet, data, len, now, g_reply, room);
    } else if (port == CTRL_PORT) {
        reply_len = ctrl_handle(&g_ctrl, data, len, now, g_reply, room);
    } else if (port == DHCP_SERVER_PORT) {
        reply_len = dhcp_server_handle(data, len, g_reply, room);
        to = &DHCP_EVERYBODY;
    } else {
        return;
    }
    if (port != DHCP_SERVER_PORT) g_last_rx_ms = now;     // the LED flickers for lighting data only
    if (reply_len > 0) net_udp_send(to, port, g_reply, reply_len);
}

// ---------------------------------------------------------------- main
int main(void) {
    stdio_init_all();                 // log text on the serial pins GP16/GP17 (see config.h)
    critical_section_init(&g_cs);

    led_init();
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

    // Two Ethernet addresses, both made from the Pico's unique serial number:
    // one for the box, one for the computer's end of the USB link. They differ
    // in the last bit. The first byte 0x02 marks them as "made up locally".
    pico_unique_board_id_t uid;
    pico_get_unique_board_id(&uid);
    net_config_t cfg = {
        .mac = {0x02, uid.id[3], uid.id[4], uid.id[5], uid.id[6], uid.id[7]},
        .ip = NET_IP, .netmask = NET_NETMASK,
    };
    uint8_t computer_mac[6];
    memcpy(computer_mac, cfg.mac, 6);
    computer_mac[5] ^= 0x01;

    static char serial[2 * PICO_UNIQUE_BOARD_ID_SIZE_BYTES + 1];   // static: the USB code keeps using it
    pico_get_unique_board_id_string(serial, sizeof serial);

    g_artnet = (artnet_node_t){.eng = &g_engine, .universe = ARTNET_UNIVERSE};
    memcpy(g_artnet.ip, cfg.ip, 4);
    memcpy(g_artnet.mac, cfg.mac, 6);

    dhcp_config_t dhcp = {
        .server_ip = NET_IP, .client_ip = NET_COMPUTER_IP, .netmask = NET_NETMASK, .lease_s = NET_LEASE_S,
    };
    dhcp_server_init(&dhcp);

    multicore_launch_core1(core1_main);   // DMX runs from here on, computer or not

    // Frames from the computer go into the network stack (net_input), frames
    // from the stack go out over USB (usb_net_send).
    net_init(&cfg, usb_net_send, on_udp);
    usb_net_init(computer_mac, serial, net_input);

    printf("dmxbox %d.%d  usb network adapter  ip %d.%d.%d.%d  artnet universe %d  ctrl port %d\n",
           FW_VERSION_MAJOR, FW_VERSION_MINOR, cfg.ip[0], cfg.ip[1], cfg.ip[2], cfg.ip[3],
           ARTNET_UNIVERSE, CTRL_PORT);

    bool connected = false;

    for (;;) {
        uint32_t now = now_ms();

        usb_net_poll();               // USB housekeeping and all received packets

        bool c = usb_net_connected();
        if (c != connected) {
            connected = c;
            const net_stats_t *s = net_stats();
            printf("computer %s  (frames %lu, udp in %lu, udp out %lu, dropped %lu)\n",
                   c ? "connected" : "gone", (unsigned long)s->frames, (unsigned long)s->udp_in,
                   (unsigned long)s->udp_out, (unsigned long)s->dropped);
        }

        // The buttons work with or without a computer (for example on a charger).
        if (button_pressed(&btn_blackout, now)) ctrl_toggle_blackout(&g_ctrl);
        if (button_pressed(&btn_preset, now))
            ctrl_preset_go(&g_ctrl, BUTTON_PRESET_SLOT, BUTTON_PRESET_FADE_MS, now);

        // LED: slow blink = powered, but no computer is using the box (charger,
        // power bank, sleeping computer); on = connected; flicker = lighting
        // data arriving.
        bool led;
        if (!connected) led = (now / 500) & 1;
        else            led = (now - g_last_rx_ms) > 40 || ((now / 40) & 1);
        led_set(led);
    }
}
