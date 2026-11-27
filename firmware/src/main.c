// dmxbox 3 firmware - RP2040: USB in, one DMX512 output with RDM, MIDI in and out
//
// The box is plugged into a computer (or into the OUT socket of another box)
// and shows up there as a network adapter and a MIDI interface. Art-Net and
// the DX control protocol travel over that "network cable inside the USB
// cable", exactly as they would over real Ethernet. Its address comes from the
// switch SW1: box n is 10.1.n.1 (config.h).
//
//   core 0: USB, network (Art-Net, DX protocol, address hand-out), RDM search
//           and requests, MIDI, LEDs, the 5 V of the OUT socket, flash writes
//   core 1: engine render, the DMX line (frames, and RDM between them)
//
// The two cores share the engine under a critical section, and the RDM line
// through a mailbox (dmx_line.c).
#include <stdio.h>
#include <string.h>
#include "pico/stdlib.h"
#include "pico/bootrom.h"
#include "pico/critical_section.h"
#include "pico/flash.h"
#include "pico/multicore.h"
#include "pico/unique_id.h"

#include "config.h"
#include "platform.h"
#include "engine.h"
#include "artnet.h"
#include "ctrl.h"
#include "rdm.h"
#include "storage.h"
#include "net.h"
#include "dhcp_server.h"
#include "usb_net.h"
#include "board.h"
#include "dmx_line.h"
#include "midi.h"

static engine_t           g_engine;
static preset_bank_t      g_bank;
static ctrl_t             g_ctrl;
static artnet_node_t      g_artnet;
static rdm_t              g_rdm;
static critical_section_t g_cs;

static inline uint32_t now_ms(void) { return to_ms_since_boot(get_absolute_time()); }

// ---------------------------------------------------------------- platform hooks
void plat_lock(void)   { critical_section_enter_blocking(&g_cs); }
void plat_unlock(void) { critical_section_exit(&g_cs); }
bool plat_save_presets(const preset_bank_t *b) { return storage_save(b); }
uint16_t plat_supply_mv(void) { return board_supply_mv(); }

// The USB boot loader, a moment after the answer has gone out (main loop).
static uint32_t g_bootloader_at;
void plat_request_bootloader(void) { g_bootloader_at = now_ms() + 300; }

// ---------------------------------------------------------------- core 1: DMX
static void render(uint8_t *channels) {
    uint32_t now = now_ms();
    plat_lock();
    engine_render(&g_engine, now, channels, true);
    plat_unlock();
}

static void core1_main(void) {
    flash_safe_execute_core_init();   // lets core 0 pause us while writing flash
    dmx_line_run(render);
}

// ---------------------------------------------------------------- network
static uint8_t    g_reply[CTRL_MAX_PKT];
static uint32_t   g_last_rx_ms;
// Where an answer goes that comes later (an RDM request takes a few milliseconds)
static net_peer_t g_ctrl_peer, g_artnet_peer;

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
        if (g_artnet.deferred_new) { g_artnet.deferred_new = 0; g_artnet_peer = *from; }
    } else if (port == CTRL_PORT) {
        reply_len = ctrl_handle(&g_ctrl, data, len, now, g_reply, room);
        if (g_ctrl.deferred_new) { g_ctrl.deferred_new = 0; g_ctrl_peer = *from; }
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
    board_init();                     // OUT stays without 5 V, LEDs dark
    stdio_uart_init_full(uart0, LOG_BAUD, PIN_LOG_TX, -1);   // the log, on the service pads
    critical_section_init(&g_cs);
    dmx_line_init();                  // the driver sends, the line idles at mark

    uint8_t number = board_box_number();

    engine_init(&g_engine);
    storage_load(&g_bank);
    ctrl_init(&g_ctrl, &g_engine, &g_bank, now_ms());

    // The box's own RDM ID: 7FF0, then four bytes of the flash chip's serial number.
    pico_unique_board_id_t uid;
    pico_get_unique_board_id(&uid);
    const uint8_t rdm_uid[6] = {RDM_MANUFACTURER >> 8, RDM_MANUFACTURER & 0xFF,
                                uid.id[4], uid.id[5], uid.id[6], uid.id[7]};
    rdm_init(&g_rdm, rdm_uid);
    g_ctrl.rdm = &g_rdm;
    g_ctrl.can = CTRL_CAN_RDM | CTRL_CAN_MIDI | CTRL_CAN_CHAIN;
    g_ctrl.box_number = number;

    // The addresses of box n: 10.1.n.1, the computer 10.1.n.2. Both Ethernet
    // addresses carry the same numbers; the first byte 0x02 marks them as
    // "made up locally".
    net_config_t cfg = {
        .mac = {0x02, 0x00, NET_FIRST, NET_SECOND, number, 0x01},
        .ip = {NET_FIRST, NET_SECOND, number, 1},
        .netmask = NET_NETMASK,
    };
    static uint8_t computer_mac[6] = {0x02, 0x00, NET_FIRST, NET_SECOND, 0, 0x02};
    computer_mac[4] = number;

    // The USB serial number: "box-007". Computers tell boxes apart by it.
    static char serial[] = "box-000";                  // static: the USB code keeps using it
    serial[4] = (char)('0' + number / 100);
    serial[5] = (char)('0' + number / 10 % 10);
    serial[6] = (char)('0' + number % 10);

    g_artnet = (artnet_node_t){.eng = &g_engine, .universe = (uint16_t)(ARTNET_UNIVERSE + number), .rdm = &g_rdm};
    memcpy(g_artnet.ip, cfg.ip, 4);
    memcpy(g_artnet.mac, cfg.mac, 6);

    dhcp_config_t dhcp = {
        .server_ip = {NET_FIRST, NET_SECOND, number, 1},
        .client_ip = {NET_FIRST, NET_SECOND, number, 2},
        .netmask = NET_NETMASK,
        .lease_s = NET_LEASE_S,
    };
    dhcp_server_init(&dhcp);

    multicore_launch_core1(core1_main);   // DMX runs from here on, computer or not

    // Frames from the computer go into the network stack (net_input), frames
    // from the stack go out over USB (usb_net_send).
    net_init(&cfg, usb_net_send, on_udp);
    usb_net_init(computer_mac, serial, net_input);
    midi_init();
    rdm_start_discovery(&g_rdm);          // the box looks for the lights on its line by itself

    printf("dmxbox 3 firmware %d.%d  box %u  ip %d.%d.%d.%d  artnet universe %d  rdm id %02X%02X:%02X%02X%02X%02X\n",
           FW_VERSION_MAJOR, FW_VERSION_MINOR, number, cfg.ip[0], cfg.ip[1], cfg.ip[2], cfg.ip[3],
           ARTNET_UNIVERSE + number, rdm_uid[0], rdm_uid[1], rdm_uid[2], rdm_uid[3], rdm_uid[4], rdm_uid[5]);

    bool connected = false, searching = true, out_powered = false;

    for (;;) {
        uint32_t now = now_ms();

        usb_net_poll();               // USB housekeeping and all received packets
        midi_poll();
        rdm_poll(&g_rdm, now);

        // RDM answers go back to whoever asked
        int later = ctrl_poll(&g_ctrl, now, g_reply, (int)sizeof g_reply);
        if (later > 0) net_udp_send(&g_ctrl_peer, CTRL_PORT, g_reply, later);
        later = artnet_poll(&g_artnet, g_reply, (int)sizeof g_reply);
        if (later > 0) net_udp_send(&g_artnet_peer, ARTNET_PORT, g_reply, later);

        board_poll(now);              // 5 V for the next box, when there is one

        if (g_bootloader_at && (int32_t)(now - g_bootloader_at) >= 0) {
            printf("restarting into the USB boot loader\n");
            sleep_ms(20);             // the log line goes out first
            reset_usb_boot(0, 0);
        }

        bool c = usb_net_connected();
        if (c != connected) {
            connected = c;
            const net_stats_t *s = net_stats();
            printf("computer %s  (frames %lu, udp in %lu, udp out %lu, dropped %lu)\n",
                   c ? "connected" : "gone", (unsigned long)s->frames, (unsigned long)s->udp_in,
                   (unsigned long)s->udp_out, (unsigned long)s->dropped);
        }
        if (searching != g_rdm.discovering) {
            searching = g_rdm.discovering;
            if (!searching) printf("rdm: %u light%s found\n", g_rdm.tod_count, g_rdm.tod_count == 1 ? "" : "s");
        }
        if (out_powered != board_out_powered()) {
            out_powered = board_out_powered();
            printf("OUT socket: 5 V %s\n", out_powered ? "on (a box is plugged in)" : "off");
        }

        // LEDs. Green: slow blink = powered, but no computer is using the box;
        // on = connected. Yellow: on = DMX goes out, flicker = lighting data
        // arriving. Red: flicker = MIDI or RDM traffic.
        bool flicker = (now / 40) & 1;
        bool usb = connected || ((now / 500) & 1);
        bool dmx = (now - g_last_rx_ms) > 40 || flicker;
        bool busy = (now - midi_last_ms()) < 40 || g_rdm.discovering || g_rdm.req_owner != RDM_OWNER_NONE;
        board_leds(usb, dmx, busy && flicker);
    }
}
