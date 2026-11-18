// The smallest network stack that does the job: ARP replies, ping replies and
// UDP, on top of raw Ethernet frames. Where the frames come from does not
// matter to this file: in the box they travel inside the USB cable (usb_net.c).
//
// The box never starts a conversation. Every packet it sends is an answer, and
// it goes straight back to the Ethernet address the request came from (or to
// everybody, for the address hand-out in dhcp_server.c). That is why there is
// no ARP cache, no routing table and no gateway setting.
//
// Pure C, no SDK calls: tested on a PC in firmware/test.
#pragma once
#include <stdbool.h>
#include <stdint.h>

#define NET_MAX_UDP 1472   // largest UDP payload that fits in one Ethernet frame

typedef struct {
    uint8_t mac[6];
    uint8_t ip[4];
    uint8_t netmask[4];   // only used to recognise the subnet's broadcast address
} net_config_t;

// Where a datagram came from; pass it back to net_udp_send() to answer.
typedef struct {
    uint8_t  mac[6];
    uint8_t  ip[4];
    uint16_t port;
} net_peer_t;

typedef bool (*net_send_fn)(const uint8_t *frame, int len);
// Called for each UDP datagram sent to this box's address or to broadcast.
typedef void (*net_udp_fn)(uint16_t port, const net_peer_t *from, const uint8_t *data, int len);

typedef struct {
    uint32_t frames, arp_replies, ping_replies, udp_in, udp_out, dropped;
} net_stats_t;

void net_init(const net_config_t *cfg, net_send_fn send_frame, net_udp_fn on_udp);

// Feed one received Ethernet frame (without CRC) into the stack.
void net_input(const uint8_t *frame, int len);

bool net_udp_send(const net_peer_t *to, uint16_t from_port, const uint8_t *data, int len);

// Broadcast "this IP address is at this MAC address" (gratuitous ARP), so
// switches and PCs learn where the box is right after a link comes up.
// Not needed on the USB link (the computer simply asks), so the box does not
// call it; it is kept for use on a real network.
void net_announce(void);

const net_stats_t *net_stats(void);
