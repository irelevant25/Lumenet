// What the box tells a computer about itself when it is plugged in
// ("USB descriptors"): who made it, what kind of device it is, how much
// current it wants.
#pragma once
#include <stdint.h>

// The two things that differ from box to box. Call this before the USB stack
// starts. Neither value is copied, so both must stay valid.
//   serial        text shown as the USB serial number
//   computer_mac  Ethernet address of the computer's end of the link
void usb_descriptors_set_identity(const char *serial, const uint8_t *computer_mac);
