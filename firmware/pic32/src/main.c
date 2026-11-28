// The firmware of dmxbox 2 (PIC32MX270F256B): USB in, one DMX512 output with RDM.
//
// Like the RP2040 boxes, the box shows up on the computer as a USB network
// adapter and speaks Art-Net and the DX protocol over it, with the same code
// (firmware/common) and the same settings (common/config.h). Its address comes
// from the switch SW1: box n is 10.1.n.1.
//
// One core does everything. The main loop serves USB and the network, the RDM
// search and requests, works out the next DMX frame while the last one goes
// out, and starts the next packet on the line; UART1's interrupt sends the
// bytes and collects the RDM answers (dmx_line.c).
#include <string.h>

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

static engine_t      g_engine;
static preset_bank_t g_bank;
static ctrl_t        g_ctrl;
static artnet_node_t g_artnet;
static rdm_t         g_rdm;
static uint8_t       g_frames[2][1 + DMX_CHANNELS];   // start code + channels: one goes out while the next is worked out

// ---------------------------------------------------------------- platform hooks
// The engine is only ever touched from the main loop, never from an
// interrupt: there is nothing to lock.
void plat_lock(void) {}
void plat_unlock(void) {}
bool plat_save_presets(const preset_bank_t *b) { return storage_save(b); }
uint16_t plat_supply_mv(void) { return 0; }   // box 2 does not measure its supply
// The PIC has no USB boot loader: its firmware comes through J2 from a programmer.
void plat_request_bootloader(void) {}

// TinyUSB asks the time through this function (its name is fixed).
uint32_t tusb_time_millis_api(void) { return board_millis(); }

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
    uint32_t now = board_millis();
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
    board_init();                     // LEDs dark, the log, the time
    dmx_line_init();                  // the driver sends, the line rests at mark

    uint8_t number = board_box_number();

    engine_init(&g_engine);
    storage_load(&g_bank);
    ctrl_init(&g_ctrl, &g_engine, &g_bank, board_millis());

    // The box's own RDM ID: 7FF0, then 0002 and the box number (the PIC32 has
    // no serial number; the ID only has to differ from the lights' on its line).
    const uint8_t rdm_uid[6] = {RDM_MANUFACTURER >> 8, RDM_MANUFACTURER & 0xFF, 0x00, BOX_MODEL, 0x00, number};
    rdm_init(&g_rdm, rdm_uid);
    g_ctrl.rdm = &g_rdm;
    g_ctrl.can = CTRL_CAN_RDM;
    g_ctrl.box_number = number;
    g_ctrl.model = BOX_MODEL;

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

    // Frames from the computer go into the network stack (net_input), frames
    // from the stack go out over USB (usb_net_send).
    net_init(&cfg, usb_net_send, on_udp);
    usb_net_init(computer_mac, serial, net_input);
    rdm_start_discovery(&g_rdm);          // the box looks for the lights on its line by itself

    board_log(BOX_NAME " firmware %d.%d  box %u  ip %d.%d.%d.%d  artnet universe %d  rdm id %02X%02X:%02X%02X%02X%02X\n",
              FW_VERSION_MAJOR, FW_VERSION_MINOR, number, cfg.ip[0], cfg.ip[1], cfg.ip[2], cfg.ip[3],
              ARTNET_UNIVERSE + number, rdm_uid[0], rdm_uid[1], rdm_uid[2], rdm_uid[3], rdm_uid[4], rdm_uid[5]);

    bool connected = false, searching = true, ready = false;
    int next = 0;                         // the half of g_frames the next frame is worked out in

    for (;;) {
        uint32_t now = board_millis();

        usb_net_poll();                   // USB housekeeping and all received packets
        rdm_poll(&g_rdm, now);

        // RDM answers go back to whoever asked
        int later = ctrl_poll(&g_ctrl, now, g_reply, (int)sizeof g_reply);
        if (later > 0) net_udp_send(&g_ctrl_peer, CTRL_PORT, g_reply, later);
        later = artnet_poll(&g_artnet, g_reply, (int)sizeof g_reply);
        if (later > 0) net_udp_send(&g_artnet_peer, ARTNET_PORT, g_reply, later);

        // DMX: the next frame is worked out while the last one goes out, and
        // goes out when the line is free (after RDM, if any waits).
        if (!ready) {
            g_frames[next][0] = 0x00;     // DMX512 null start code
            engine_render(&g_engine, board_millis(), g_frames[next] + 1, true);
            ready = true;
        }
        if (dmx_line_poll(g_frames[next])) {
            next ^= 1;
            ready = false;
        }

        bool c = usb_net_connected();
        if (c != connected) {
            connected = c;
            const net_stats_t *s = net_stats();
            board_log("computer %s  (frames %lu, udp in %lu, udp out %lu, dropped %lu)\n",
                      c ? "connected" : "gone", (unsigned long)s->frames, (unsigned long)s->udp_in,
                      (unsigned long)s->udp_out, (unsigned long)s->dropped);
        }
        if (searching != g_rdm.discovering) {
            searching = g_rdm.discovering;
            if (!searching) board_log("rdm: %u light%s found\n", g_rdm.tod_count, g_rdm.tod_count == 1 ? "" : "s");
        }

        // LEDs. Green: slow blink = powered, but no computer is using the box;
        // on = connected. Yellow: on = DMX goes out, flicker = lighting data
        // arriving. Red: flicker = RDM traffic.
        bool flicker = (now / 40) & 1;
        bool usb = connected || ((now / 500) & 1);
        bool dmx = (now - g_last_rx_ms) > 40 || flicker;
        bool busy = g_rdm.discovering || g_rdm.req_owner != RDM_OWNER_NONE;
        board_leds(usb, dmx, busy && flicker);
    }
}
