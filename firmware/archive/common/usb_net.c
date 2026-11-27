#include "usb_net.h"
#include <string.h>
#include "tusb.h"
#include "device/usbd_pvt.h"     // TinyUSB: how to put a driver of one's own in front of its drivers (end of this file)
#include "usb_descriptors.h"

// At most this many frames are handled in one usb_net_poll(), so the buttons
// and the LED stay responsive under a flood of packets.
#define RX_BURST 12

// How often usb_net_send() lets TinyUSB work before it gives up on a frame.
#define SEND_ROUNDS 200

// One received frame waits here until usb_net_poll() has dealt with it.
// TinyUSB hands frames over in the middle of its own work; answering right
// there would call back into TinyUSB while it is busy.
static uint8_t  rx_frame[USB_NET_MAX_FRAME];
static uint16_t rx_len;          // length of the waiting frame; 0 = it was unusable and is only acknowledged
static bool     rx_waiting;      // a frame was taken from TinyUSB and "done" has not been said yet

static usb_net_frame_fn on_frame;

// The Ethernet address of the computer's end of the link. TinyUSB's rules fix
// the name of this variable; usb_descriptors.c hands the address to the computer.
uint8_t tud_network_mac_address[6];

void usb_net_init(const uint8_t computer_mac[6], const char *serial, usb_net_frame_fn frame_fn) {
    memcpy(tud_network_mac_address, computer_mac, 6);
    usb_descriptors_set_identity(serial, tud_network_mac_address);
    on_frame = frame_fn;
    rx_waiting = false;
    tud_init(0);                 // start TinyUSB on USB port 0, as a device
}

void usb_net_poll(void) {
    tud_task();                  // TinyUSB's housekeeping; it calls tud_network_recv_cb() when a frame came in

    // Several frames can arrive in one go. After each one TinyUSB has to be
    // told "done" (tud_network_recv_renew): only then it hands over the next.
    for (int i = 0; i < RX_BURST && rx_waiting; i++) {
        if (rx_len > 0) on_frame(rx_frame, rx_len);
        rx_waiting = false;
        tud_network_recv_renew();
    }
}

bool usb_net_send(const uint8_t *frame, int len) {
    if (len < 14 || len > USB_NET_MAX_FRAME) return false;
    if (!tud_ready()) return false;

    // TinyUSB collects outgoing frames in a buffer. That buffer is only full
    // when the computer has stopped fetching data. Let TinyUSB work for a
    // moment; if there is still no room, drop the frame. Whoever waits for
    // this answer asks again.
    for (int round = 0; round < SEND_ROUNDS; round++) {
        if (tud_network_can_xmit((uint16_t)len)) {
            tud_network_xmit((void *)(uintptr_t)frame, (uint16_t)len);   // copies the frame at once, see tud_network_xmit_cb()
            return true;
        }
        tud_task();
    }
    return false;
}

bool usb_net_connected(void) {
    return tud_ready();          // a computer has set the device up and is not asleep
}

// ---------------------------------------------------------------- called by TinyUSB
// The names and arguments of these three functions are fixed by TinyUSB.

// A frame from the computer. Return true if it was taken; false means "not
// now", and TinyUSB offers the same frame again after tud_network_recv_renew().
bool tud_network_recv_cb(const uint8_t *src, uint16_t size) {
    if (rx_waiting) return false;                 // the previous frame has not been dealt with yet
    bool usable = size > 0 && size <= USB_NET_MAX_FRAME;
    if (usable) memcpy(rx_frame, src, size);
    rx_len = usable ? size : 0;
    rx_waiting = true;
    return true;
}

// TinyUSB wants the bytes of the frame given to tud_network_xmit(): copy them
// into its buffer and say how many there were.
uint16_t tud_network_xmit_cb(uint8_t *dst, void *ref, uint16_t arg) {
    memcpy(dst, ref, arg);
    return arg;
}

// TinyUSB calls this when the network side starts from scratch (only its
// older adapter kinds do; it has to exist for them).
void tud_network_init_cb(void) {
    rx_waiting = false;
}

// ---------------------------------------------------------------- a correction for Windows
// When a computer switches the adapter on, it first asks how the blocks of
// frames are laid out (the request "GET_NTB_PARAMETERS"). TinyUSB answers that
// by itself. Up to version 0.18, the one in Pico SDK 2.3.1, two numbers in its
// answer are 1. The standard allows that, but since a Windows update of
// September 2026 (KB5124008) Windows wants them to be 4 or more and a power of
// two. Otherwise the adapter does not start (Device Manager: "This device
// cannot start", code 10). Newer TinyUSB answers 4.
//
// So the box answers this one question itself, with the numbers newer TinyUSB
// uses, and hands every other request to TinyUSB. Once the Pico SDK brings a
// TinyUSB that answers 4 by itself, this part can go.
#define REQUEST_GET_NTB_PARAMETERS 0x80      // the number of that request in the NCM standard

#define LE16(x) (uint8_t)((x) & 0xFF), (uint8_t)(((x) >> 8) & 0xFF)
#define LE32(x) LE16(x), LE16((x) >> 16)

static const uint8_t ntb_parameters[28] = {
    LE16(28),                                        // size of this table
    LE16(0x0001),                                    // block format: the 16-bit one
    LE32(CFG_TUD_NCM_IN_NTB_MAX_SIZE),               // largest block, box -> computer
    LE16(4), LE16(0), LE16(4),                       // box -> computer: divisor (the number Windows checks), remainder, alignment
    LE16(0),                                         //   (not used)
    LE32(CFG_TUD_NCM_OUT_NTB_MAX_SIZE),              // largest block, computer -> box
    LE16(4), LE16(0), LE16(4),                       // computer -> box: divisor, remainder, alignment
    LE16(CFG_TUD_NCM_OUT_MAX_DATAGRAMS_PER_NTB),     // most frames in one block, computer -> box
};

static bool net_control_request(uint8_t rhport, uint8_t stage, tusb_control_request_t const *request) {
    bool wants_ntb_parameters = request->bmRequestType_bit.type == TUSB_REQ_TYPE_CLASS &&
                                request->bRequest == REQUEST_GET_NTB_PARAMETERS;
    if (!wants_ntb_parameters) return netd_control_xfer_cb(rhport, stage, request);   // TinyUSB's own handling
    if (stage != CONTROL_STAGE_SETUP) return true;                                    // nothing to do in the later stages
    return tud_control_xfer(rhport, request, (void *)(uintptr_t)ntb_parameters, sizeof ntb_parameters);
}

// TinyUSB's network adapter driver (the netd_ functions), with the one request
// above taken out.
static const usbd_class_driver_t net_driver = {
    .init            = netd_init,
    .deinit          = netd_deinit,
    .reset           = netd_reset,
    .open            = netd_open,
    .control_xfer_cb = net_control_request,
    .xfer_cb         = netd_xfer_cb,
};

// TinyUSB asks for extra drivers through this function (its name is fixed) and
// tries them before its own. So the driver above gets the network adapter.
const usbd_class_driver_t *usbd_app_driver_get_cb(uint8_t *driver_count) {
    *driver_count = 1;
    return &net_driver;
}
