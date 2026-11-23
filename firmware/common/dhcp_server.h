// A very small DHCP server for the USB link.
//
// DHCP is how a computer asks "which address may I use on this network?".
// The link inside the USB cable has only two ends: this box and the computer
// it is plugged into. So the box always answers with the same single address.
// There is no table of clients, because there can only be one.
//
// Pure C, no SDK calls: tested on a PC in firmware/pico/test.
#pragma once
#include <stdint.h>

#define DHCP_SERVER_PORT 67     // the box listens on this UDP port
#define DHCP_CLIENT_PORT 68     // answers go to this port, as a broadcast
#define DHCP_REPLY_LEN   300    // every answer has this length

typedef struct {
    uint8_t  server_ip[4];      // the box itself
    uint8_t  client_ip[4];      // the one address that is handed out
    uint8_t  netmask[4];
    uint32_t lease_s;           // how long the computer may keep the address before it asks again
} dhcp_config_t;

void dhcp_server_init(const dhcp_config_t *cfg);

// Handle one datagram that arrived on UDP port 67. If an answer is due, it is
// written to `reply` (which must have room for DHCP_REPLY_LEN bytes) and its
// length is returned. Otherwise the result is 0.
//
// Send the answer from port 67 to the address 255.255.255.255, port 68: the
// computer has no address of its own yet, so it can only hear a broadcast.
int dhcp_server_handle(const uint8_t *msg, int len, uint8_t *reply, int reply_cap);
