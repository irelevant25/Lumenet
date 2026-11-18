// The Pico as a USB network adapter.
//
// To the computer the box looks like a network card on a USB cable. This file
// moves whole Ethernet frames between that cable and the small network stack
// in net.c. It knows nothing about IP addresses or protocols.
//
// It is the only file (with usb_descriptors.c) that talks to TinyUSB, the USB
// library that comes with the Pico SDK.
#pragma once
#include <stdbool.h>
#include <stdint.h>

#define USB_NET_MAX_FRAME 1514      // largest Ethernet frame (without its 4-byte CRC)

// Called for every Ethernet frame that arrives from the computer.
typedef void (*usb_net_frame_fn)(const uint8_t *frame, int len);

// Start the USB side.
//   computer_mac  Ethernet address of the computer's end of the link. It must
//                 differ from the box's own address.
//   serial        text shown as the USB serial number (must stay valid)
//   on_frame      gets the frames that arrive
void usb_net_init(const uint8_t computer_mac[6], const char *serial, usb_net_frame_fn on_frame);

// Call this all the time from the main loop. It keeps the USB connection
// alive and hands received frames to `on_frame`. It never waits.
void usb_net_poll(void);

// Send one Ethernet frame to the computer. Returns false if that was not
// possible (no computer attached, or the computer does not fetch data).
bool usb_net_send(const uint8_t *frame, int len);

// True while a computer has the adapter in use. False on a charger or a power
// bank, and while the computer sleeps.
bool usb_net_connected(void);
