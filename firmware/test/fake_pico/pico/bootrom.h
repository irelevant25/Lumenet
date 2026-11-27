// NOT the Pico SDK: stand-in for pico/bootrom.h, see fake_types.h.
#pragma once
#include "fake_types.h"

// The real one never returns: the RP2040 restarts into its USB boot loader.
// The fake notes the time and ends the run of the firmware.
void reset_usb_boot(uint32_t usb_activity_gpio_pin_mask, uint32_t disable_interface_mask);
