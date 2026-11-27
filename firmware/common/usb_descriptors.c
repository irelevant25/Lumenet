// USB descriptors: tables of bytes the computer reads when the box is plugged
// in. Their layout is fixed by the USB standards; the comments name each field.
//
// The tables are written out byte by byte instead of with TinyUSB's helper
// macros, because those macros changed between TinyUSB versions. The bytes for
// the network adapter are the ones TinyUSB 0.18 produces for its own example,
// those of the MIDI interface the ones of its TUD_MIDI_DESCRIPTOR.
#include "usb_descriptors.h"
#include <stdbool.h>
#include <stdint.h>
#include <string.h>
#include "config.h"
#include "tusb.h"

#define DESC_LO(x) ((uint8_t)((x) & 0xFF))
#define DESC_HI(x) ((uint8_t)(((x) >> 8) & 0xFF))

// Numbers of the text strings the computer can ask for
enum { STRING_LANGUAGE = 0, STRING_MANUFACTURER, STRING_PRODUCT, STRING_SERIAL, STRING_INTERFACE, STRING_MAC,
       STRING_MIDI };

// USB endpoints = the separate "pipes" inside the cable (0x80 = towards the computer).
// config.h can choose others: the USB port of a PIC32 cannot use one number in
// both directions, so the PIC32 box sends its frames on 0x83.
#ifndef NET_EP_NOTIFY
#define NET_EP_NOTIFY 0x81        // "the link is up" messages
#endif
#ifndef NET_EP_OUT
#define NET_EP_OUT    0x02        // frames from the computer
#endif
#ifndef NET_EP_IN
#define NET_EP_IN     0x82        // frames to the computer
#endif
#define NET_EP_SIZE   64          // packet size of full-speed USB
#define NET_MAX_FRAME 1514        // largest Ethernet frame
#define MIDI_EP_OUT   0x03        // MIDI from the computer (to MIDI OUT)
#define MIDI_EP_IN    0x83        // MIDI to the computer (from MIDI IN)
#define MIDI_EP_SIZE  64

// ---------------------------------------------------------------- device
static const uint8_t device_descriptor[18] = {
    18, 0x01,                               // length, type = DEVICE
    0x01, 0x02,                             // USB version 2.01 (2.01 makes Windows ask for the BOS table below)
    0xEF, 0x02, 0x01,                       // class "miscellaneous": the interfaces below say what it really is
    CFG_TUD_ENDPOINT0_SIZE,                 // packet size of the control pipe
    DESC_LO(USB_VID), DESC_HI(USB_VID),
    DESC_LO(USB_PID), DESC_HI(USB_PID),
    0x00, 0x01,                             // device version 1.00
    STRING_MANUFACTURER, STRING_PRODUCT, STRING_SERIAL,
    1,                                      // one configuration
};

const uint8_t *tud_descriptor_device_cb(void) { return device_descriptor; }

// ---------------------------------------------------------------- configuration: network adapter and MIDI
#if CFG_TUD_MIDI
#define MIDI_DESC_LEN   92
#define INTERFACES      4                   // network: control and data; MIDI: audio control and MIDI streaming
#else
#define MIDI_DESC_LEN   0
#define INTERFACES      2
#endif
#define CONFIG_DESC_LEN (94 + MIDI_DESC_LEN)    // all the bytes of this table

static const uint8_t config_descriptor[CONFIG_DESC_LEN] = {
    // Configuration header
    9, 0x02, DESC_LO(CONFIG_DESC_LEN), DESC_HI(CONFIG_DESC_LEN),
    INTERFACES,
    1,                                      // configuration number
    0,                                      // no name
    0x80,                                   // powered from the USB cable
    USB_MAX_POWER_MA / 2,                   // current, counted in steps of 2 mA

    // Interface association: interfaces 0 and 1 together are one function
    8, 0x0B, 0, 2, 0x02, 0x0D, 0x00, 0,

    // Interface 0, control: class 02h "communications", subclass 0Dh "NCM"
    9, 0x04, 0, 0, 1, 0x02, 0x0D, 0x00, STRING_INTERFACE,
    5, 0x24, 0x00, 0x10, 0x01,              // CDC header: version 1.10
    5, 0x24, 0x06, 0, 1,                    // CDC union: control = interface 0, data = interface 1
    13, 0x24, 0x0F, STRING_MAC,             // Ethernet: where to read the adapter's address ...
        0, 0, 0, 0,                         //   no statistics
        DESC_LO(NET_MAX_FRAME), DESC_HI(NET_MAX_FRAME),      //   largest frame
        0, 0, 0,                            //   no multicast or power filters
    6, 0x24, 0x1A, 0x00, 0x01, 0x00,        // NCM: version 1.00, no extras
    7, 0x05, NET_EP_NOTIFY, 0x03, DESC_LO(NET_EP_SIZE), DESC_HI(NET_EP_SIZE), 50,   // interrupt pipe, asked every 50 ms

    // Interface 1, data, setting 0: no pipes (adapter switched off)
    9, 0x04, 1, 0, 0, 0x0A, 0x00, 0x01, 0,
    // Interface 1, data, setting 1: two bulk pipes (adapter switched on by the computer)
    9, 0x04, 1, 1, 2, 0x0A, 0x00, 0x01, 0,
    7, 0x05, NET_EP_IN, 0x02, DESC_LO(NET_EP_SIZE), DESC_HI(NET_EP_SIZE), 0,
    7, 0x05, NET_EP_OUT, 0x02, DESC_LO(NET_EP_SIZE), DESC_HI(NET_EP_SIZE), 0,
#if CFG_TUD_MIDI
    // Interface 2, audio control: class 01h "audio", subclass 01h; it only names the MIDI interface
    9, 0x04, 2, 0, 0, 0x01, 0x01, 0x00, STRING_MIDI,
    9, 0x24, 0x01, 0x00, 0x01, 9, 0,        // audio control header: version 1.00, 9 bytes ...
        1, 3,                               //   one streaming interface: number 3
    // Interface 3, MIDI streaming: subclass 03h, two bulk pipes
    9, 0x04, 3, 0, 2, 0x01, 0x03, 0x00, 0,
    7, 0x24, 0x01, 0x00, 0x01, 65, 0,       // MIDI streaming header: version 1.00, 65 bytes up to the end
    // The "jacks": 1 and 4 carry what the computer sends out to MIDI OUT, 2 and 3
    // what comes in at MIDI IN to the computer (embedded = the USB side).
    6, 0x24, 0x02, 0x01, 1, 0,              // IN jack 1, embedded
    6, 0x24, 0x02, 0x02, 2, 0,              // IN jack 2, external: the MIDI IN socket
    9, 0x24, 0x03, 0x01, 3, 1, 2, 1, 0,     // OUT jack 3, embedded, fed by jack 2
    9, 0x24, 0x03, 0x02, 4, 1, 1, 1, 0,     // OUT jack 4, external: the MIDI OUT socket, fed by jack 1
    9, 0x05, MIDI_EP_OUT, 0x02, DESC_LO(MIDI_EP_SIZE), DESC_HI(MIDI_EP_SIZE), 0, 0, 0,
    5, 0x25, 0x01, 1, 1,                    //   goes to jack 1
    9, 0x05, MIDI_EP_IN, 0x02, DESC_LO(MIDI_EP_SIZE), DESC_HI(MIDI_EP_SIZE), 0, 0, 0,
    5, 0x25, 0x01, 1, 3,                    //   comes from jack 3
#endif
};

const uint8_t *tud_descriptor_configuration_cb(uint8_t index) {
    return index == 0 ? config_descriptor : NULL;
}

// ---------------------------------------------------------------- hint for Windows: which driver to use
// Windows 11 has a driver for this kind of network adapter and picks it by
// itself. Windows 10 is not on Microsoft's list for it. TinyUSB's own example
// makes Windows 10 load the driver by asking for it by name ("WINNCM") through
// the two tables below (Microsoft OS 2.0 descriptors), so the box does the same.
// If a Windows 10 PC still shows an unknown device, pick the driver by hand in
// Device Manager: Update driver > Let me pick > Network adapters > Microsoft >
// UsbNcm Host Device.
#define MS_OS_VENDOR_CODE 1                 // request number Windows uses to fetch the second table
#define MS_OS_DESC_INDEX  7                 // fixed by Microsoft: "send the descriptor set"
#define MS_OS_DESC_LEN    46

static const uint8_t bos_descriptor[33] = {
    5, 0x0F, 33, 0, 1,                      // BOS header: 33 bytes, one entry
    28, 0x10, 0x05, 0x00,                   // entry: "platform capability"
    0xDF, 0x60, 0xDD, 0xD8, 0x89, 0x45, 0xC7, 0x4C,       // Microsoft's ID for "OS 2.0 descriptors"
    0x9C, 0xD2, 0x65, 0x9D, 0x9E, 0x64, 0x8A, 0x9F,
    0x00, 0x00, 0x03, 0x06,                 // for Windows 8.1 and newer
    DESC_LO(MS_OS_DESC_LEN), DESC_HI(MS_OS_DESC_LEN),     // size of the second table
    MS_OS_VENDOR_CODE, 0,
};

const uint8_t *tud_descriptor_bos_cb(void) { return bos_descriptor; }

static const uint8_t ms_os_descriptor[MS_OS_DESC_LEN] = {
    // set header: 10 bytes, for Windows 8.1 and newer, size of the whole table
    10, 0,  0x00, 0x00,  0x00, 0x00, 0x03, 0x06,  DESC_LO(MS_OS_DESC_LEN), DESC_HI(MS_OS_DESC_LEN),
    // "what follows is for configuration 0": 8 bytes, then the size of the rest
    8, 0,   0x01, 0x00,  0, 0,  DESC_LO(MS_OS_DESC_LEN - 10), DESC_HI(MS_OS_DESC_LEN - 10),
    // "what follows is for the function that starts at interface 0": 8 bytes, then the size of the rest
    8, 0,   0x02, 0x00,  0, 0,  DESC_LO(MS_OS_DESC_LEN - 18), DESC_HI(MS_OS_DESC_LEN - 18),
    // the driver name: 20 bytes
    20, 0,  0x03, 0x00,  'W', 'I', 'N', 'N', 'C', 'M', 0, 0,
    0, 0, 0, 0, 0, 0, 0, 0,
};

// TinyUSB calls this for requests that only this device understands. The one
// handled here is Windows fetching the table above.
bool tud_vendor_control_xfer_cb(uint8_t rhport, uint8_t stage, tusb_control_request_t const *request) {
    if (stage != CONTROL_STAGE_SETUP) return true;         // nothing to do in the later stages
    bool wants_ms_os_table = request->bmRequestType_bit.type == TUSB_REQ_TYPE_VENDOR &&
                             request->bRequest == MS_OS_VENDOR_CODE && request->wIndex == MS_OS_DESC_INDEX;
    if (!wants_ms_os_table) return false;                  // unknown request: refuse it
    return tud_control_xfer(rhport, request, (void *)(uintptr_t)ms_os_descriptor, MS_OS_DESC_LEN);
}

// ---------------------------------------------------------------- text strings
static const uint8_t NO_MAC[6] = {0x02, 0, 0, 0, 0, 0x02};
static const char    *serial_text = "0";
static const uint8_t *computer_mac = NO_MAC;

void usb_descriptors_set_identity(const char *serial, const uint8_t *mac) {
    serial_text = serial;
    computer_mac = mac;
}

#define USB_STRING_MAX 32
static uint16_t string_buffer[1 + USB_STRING_MAX];         // USB strings are 16-bit characters after a 2-byte header

const uint16_t *tud_descriptor_string_cb(uint8_t index, uint16_t langid) {
    (void)langid;
    static const char hex_digits[] = "0123456789ABCDEF";
    const char *text = NULL;
    unsigned count = 0;

    switch (index) {
    case STRING_LANGUAGE:
        string_buffer[1] = 0x0409;                         // the strings are in English
        count = 1;
        break;
    case STRING_MAC:                                       // the computer reads its address as twelve hex digits
        for (unsigned i = 0; i < 6; i++) {
            string_buffer[1 + count++] = (uint16_t)hex_digits[computer_mac[i] >> 4];
            string_buffer[1 + count++] = (uint16_t)hex_digits[computer_mac[i] & 0x0F];
        }
        break;
    case STRING_MANUFACTURER: text = USB_MANUFACTURER; break;
    case STRING_PRODUCT:      text = USB_PRODUCT; break;
    case STRING_SERIAL:       text = serial_text; break;
    case STRING_INTERFACE:    text = USB_PRODUCT " network adapter"; break;
    case STRING_MIDI:         text = USB_PRODUCT " MIDI"; break;
    default:
        return NULL;                                       // no such string
    }

    if (text) {
        count = (unsigned)strlen(text);
        if (count > USB_STRING_MAX) count = USB_STRING_MAX;
        for (unsigned i = 0; i < count; i++) string_buffer[1 + i] = (uint8_t)text[i];
    }
    string_buffer[0] = (uint16_t)((0x03 << 8) | (2 * count + 2));     // type = STRING, length in bytes
    return string_buffer;
}
