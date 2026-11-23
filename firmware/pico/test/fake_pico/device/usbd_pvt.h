// NOT TinyUSB: stand-in for device/usbd_pvt.h, see fake_types.h.
//
// Copied from TinyUSB 0.18.0, src/device/usbd_pvt.h: the table of functions
// that makes up one "class driver", and the function through which a firmware
// hands TinyUSB a driver of its own.
#pragma once
#include "tusb.h"

typedef struct {
    char const* name;
    void     (* init             ) (void);
    bool     (* deinit           ) (void);
    void     (* reset            ) (uint8_t rhport);
    uint16_t (* open             ) (uint8_t rhport, tusb_desc_interface_t const * desc_intf, uint16_t max_len);
    bool     (* control_xfer_cb  ) (uint8_t rhport, uint8_t stage, tusb_control_request_t const * request);
    bool     (* xfer_cb          ) (uint8_t rhport, uint8_t ep_addr, xfer_result_t result, uint32_t xferred_bytes);
    void     (* sof              ) (uint8_t rhport, uint32_t frame_count); // optional
} usbd_class_driver_t;

usbd_class_driver_t const* usbd_app_driver_get_cb(uint8_t* driver_count);
