// NOT TinyUSB: stand-in for tusb.h, see fake_types.h.
//
// The declarations below are copied from TinyUSB 0.18.0 (the version in Pico
// SDK 2.1.1), so that the firmware's use of them is checked by the compiler:
//   src/common/tusb_types.h, src/device/usbd.h, src/class/net/net_device.h
// In the real library tud_init(), tud_task() and tud_ready() are small inline
// functions; they are called in the same way.
#pragma once
#include "fake_types.h"

// what the Pico SDK passes on the compiler command line
#define CFG_TUSB_MCU           1
#define OPT_OS_NONE            1
#define OPT_MODE_DEFAULT_SPEED 0
#include "tusb_config.h"       // the firmware's own TinyUSB settings

// ---- src/common/tusb_types.h
typedef enum {
    TUSB_REQ_TYPE_STANDARD = 0,
    TUSB_REQ_TYPE_CLASS,
    TUSB_REQ_TYPE_VENDOR,
    TUSB_REQ_TYPE_INVALID
} tusb_request_type_t;

typedef enum {
    CONTROL_STAGE_IDLE = 0,
    CONTROL_STAGE_SETUP,
    CONTROL_STAGE_DATA,
    CONTROL_STAGE_ACK
} control_stage_t;

typedef struct {
    union {
        struct {
            uint8_t recipient : 5;
            uint8_t type      : 2;
            uint8_t direction : 1;
        } bmRequestType_bit;
        uint8_t bmRequestType;
    };
    uint8_t  bRequest;
    uint16_t wValue;
    uint16_t wIndex;
    uint16_t wLength;
} tusb_control_request_t;

// ---- src/device/usbd.h: functions the firmware calls
bool tud_init(uint8_t rhport);
void tud_task(void);
bool tud_mounted(void);
bool tud_suspended(void);
bool tud_ready(void);
bool tud_control_xfer(uint8_t rhport, tusb_control_request_t const * request, void* buffer, uint16_t len);

// ---- src/device/usbd.h: functions the firmware provides
uint8_t const * tud_descriptor_device_cb(void);
uint8_t const * tud_descriptor_configuration_cb(uint8_t index);
uint16_t const* tud_descriptor_string_cb(uint8_t index, uint16_t langid);
uint8_t const * tud_descriptor_bos_cb(void);
bool tud_vendor_control_xfer_cb(uint8_t rhport, uint8_t stage, tusb_control_request_t const * request);

// ---- src/class/net/net_device.h
void tud_network_recv_renew(void);
bool tud_network_can_xmit(uint16_t size);
void tud_network_xmit(void *ref, uint16_t arg);
bool tud_network_recv_cb(const uint8_t *src, uint16_t size);
uint16_t tud_network_xmit_cb(uint8_t *dst, void *ref, uint16_t arg);
void tud_network_init_cb(void);
extern uint8_t tud_network_mac_address[6];

// ---- src/class/net/net_device.h: TinyUSB's own network adapter driver.
// The firmware wraps it (end of usb_net.c); see also device/usbd_pvt.h.
typedef enum {
    XFER_RESULT_SUCCESS = 0,
    XFER_RESULT_FAILED,
    XFER_RESULT_STALLED,
    XFER_RESULT_TIMEOUT,
    XFER_RESULT_INVALID
} xfer_result_t;
typedef struct tusb_desc_interface tusb_desc_interface_t;      // only passed through, never looked into

void     netd_init            (void);
bool     netd_deinit          (void);
void     netd_reset           (uint8_t rhport);
uint16_t netd_open            (uint8_t rhport, tusb_desc_interface_t const * itf_desc, uint16_t max_len);
bool     netd_control_xfer_cb (uint8_t rhport, uint8_t stage, tusb_control_request_t const * request);
bool     netd_xfer_cb         (uint8_t rhport, uint8_t ep_addr, xfer_result_t result, uint32_t xferred_bytes);
