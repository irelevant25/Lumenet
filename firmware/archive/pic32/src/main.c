// dmxbox 2 firmware - PIC32MX270F256B: USB in, two DMX512 outputs out
//
// Like the Pico box, the box shows up on the computer as a USB network
// adapter and speaks Art-Net and the DX protocol over it (firmware/common).
// It has two outputs and so 1024 channels: 1 to 512 go out of output 1, 513
// to 1024 out of output 2. Its address comes from the switch on the board:
// box n is 10.1.n.1 (config.h).
//
// One core does everything. The main loop serves USB and the network, the
// buttons and the LED, and works out the next DMX frame while interrupts send
// the one before it (dmx_out.c).
#include <string.h>

#include "config.h"
#include "board.h"
#include "platform.h"
#include "engine.h"
#include "artnet.h"
#include "ctrl.h"
#include "storage.h"
#include "net.h"
#include "dhcp_server.h"
#include "usb_net.h"
#include "dmx_out.h"

static engine_t      g_engine;
static preset_bank_t g_bank;
static ctrl_t        g_ctrl;
static artnet_node_t g_artnet;
static uint8_t       g_levels[2][DMX_CHANNELS];   // one goes out while the next is worked out

// ---------------------------------------------------------------- platform hooks
// The engine is only ever touched from the main loop, never from an
// interrupt: there is nothing to lock.
void plat_lock(void) {}
void plat_unlock(void) {}
bool plat_save_presets(const preset_bank_t *b) { return storage_save(b); }

// TinyUSB asks the time through this function (its name is fixed).
uint32_t tusb_time_millis_api(void) { return board_millis(); }

// ---------------------------------------------------------------- buttons
typedef struct { bool stable, raw; uint32_t changed_ms; } button_t;

// Returns true once per press (debounced).
static bool button_pressed(button_t *b, bool down, uint32_t now) {
    if (down != b->raw) { b->raw = down; b->changed_ms = now; }
    if (now - b->changed_ms >= 30 && down != b->stable) {
        b->stable = down;
        return down;
    }
    return false;
}

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

static void on_udp(uint16_t port, const net_peer_t *from, const uint8_t *data, int len) {
    uint32_t now = board_millis();
    const net_peer_t *to = from;
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
    board_init();
    uint8_t number = board_box_number();

    engine_init(&g_engine);
    storage_load(&g_bank);
    ctrl_init(&g_ctrl, &g_engine, &g_bank, board_millis());

    // The addresses of box n: 10.1.n.1, the computer 10.1.n.2. Both Ethernet
    // addresses carry the same numbers; the first byte 0x02 marks them as
    // "made up locally".
    net_config_t cfg = {
        .mac = {0x02, 0x00, NET_FIRST, NET_SECOND, number, 0x01},
        .ip = {NET_FIRST, NET_SECOND, number, 1},
        .netmask = NET_NETMASK,
    };
    uint8_t computer_mac[6] = {0x02, 0x00, NET_FIRST, NET_SECOND, number, 0x02};

    // The USB serial number: "box-07". Windows tells boxes apart by it.
    static char serial[] = "box-00";                   // static: the USB code keeps using it
    serial[4] = (char)('0' + number / 10);
    serial[5] = (char)('0' + number % 10);

    g_artnet = (artnet_node_t){.eng = &g_engine, .universe = (uint16_t)(ARTNET_UNIVERSE + number * DMX_OUTPUTS)};
    memcpy(g_artnet.ip, cfg.ip, 4);
    memcpy(g_artnet.mac, cfg.mac, 6);

    dhcp_config_t dhcp = {
        .server_ip = {NET_FIRST, NET_SECOND, number, 1},
        .client_ip = {NET_FIRST, NET_SECOND, number, 2},
        .netmask = NET_NETMASK,
        .lease_s = NET_LEASE_S,
    };
    dhcp_server_init(&dhcp);

    dmx_out_init();                       // DMX runs from here on, computer or not
    net_init(&cfg, usb_net_send, on_udp);
    usb_net_init(computer_mac, serial, net_input);

    button_t btn_blackout = {0}, btn_preset = {0};
    uint8_t next = 0;                     // the half of g_levels the next frame is worked out in
    bool ready = false;

    for (;;) {
        uint32_t now = board_millis();

        usb_net_poll();                   // USB housekeeping and all received packets

        // DMX: work out the next frame while the last one goes out, and send
        // it as soon as both outputs are done.
        if (!ready) {
            engine_render(&g_engine, now, g_levels[next], true);
            ready = true;
        }
        if (dmx_out_idle()) {
            dmx_out_start(g_levels[next]);
            next ^= 1;
            ready = false;
        }

        // The buttons work with or without a computer (for example on a charger).
        if (button_pressed(&btn_blackout, board_button_blackout(), now)) ctrl_toggle_blackout(&g_ctrl);
        if (button_pressed(&btn_preset, board_button_preset(), now))
            ctrl_preset_go(&g_ctrl, BUTTON_PRESET_SLOT, BUTTON_PRESET_FADE_MS, now);

        // LED: slow blink = powered, but no computer is using the box; on =
        // connected; flicker = lighting data arriving.
        bool led;
        if (!usb_net_connected()) led = (now / 500) & 1;
        else                      led = (now - g_last_rx_ms) > 40 || ((now / 40) & 1);
        board_led(led);
    }
}
