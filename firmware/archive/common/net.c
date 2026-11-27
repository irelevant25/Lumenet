#include "net.h"
#include <string.h>

// Sizes and offsets inside an Ethernet frame
#define ETH_HDR   14
#define IP_HDR    20
#define UDP_HDR   8
#define ARP_LEN   28
#define TYPE_ARP  0x0806
#define TYPE_IPV4 0x0800
#define PROTO_ICMP 1
#define PROTO_UDP  17

static net_config_t cfg;
static net_send_fn  send_frame;
static net_udp_fn   on_udp;
static net_stats_t  stats;
static uint16_t     ip_id;
static uint8_t      tx[ETH_HDR + IP_HDR + UDP_HDR + NET_MAX_UDP];

static const uint8_t BROADCAST_MAC[6] = {0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF};

static uint16_t be16(const uint8_t *p) { return (uint16_t)((p[0] << 8) | p[1]); }
static void put16(uint8_t *p, uint16_t v) { p[0] = (uint8_t)(v >> 8); p[1] = (uint8_t)v; }

// Internet checksum: add up 16-bit big-endian words, then fold and invert.
static uint32_t sum_words(const uint8_t *p, int len, uint32_t sum) {
    for (; len > 1; p += 2, len -= 2) sum += (uint32_t)((p[0] << 8) | p[1]);
    if (len) sum += (uint32_t)(p[0] << 8);
    return sum;
}
static uint16_t finish_sum(uint32_t sum) {
    while (sum >> 16) sum = (sum & 0xFFFF) + (sum >> 16);
    return (uint16_t)~sum;
}

static bool is_broadcast_ip(const uint8_t *ip) {
    static const uint8_t all[4] = {255, 255, 255, 255};
    if (memcmp(ip, all, 4) == 0) return true;
    for (int i = 0; i < 4; i++)                       // our subnet with all host bits set
        if (ip[i] != (uint8_t)(cfg.ip[i] | (uint8_t)~cfg.netmask[i])) return false;
    return true;
}

void net_init(const net_config_t *c, net_send_fn send, net_udp_fn udp) {
    cfg = *c;
    send_frame = send;
    on_udp = udp;
    memset(&stats, 0, sizeof stats);
    ip_id = 0;
}

const net_stats_t *net_stats(void) { return &stats; }

// ---------------------------------------------------------------- building frames
static void eth_header(const uint8_t dst_mac[6], uint16_t type) {
    memcpy(tx, dst_mac, 6);
    memcpy(tx + 6, cfg.mac, 6);
    put16(tx + 12, type);
}

// IPv4 header without options, in tx. `payload_len` bytes follow it.
static void ip_header(const uint8_t dst_ip[4], uint8_t protocol, int payload_len) {
    uint8_t *ip = tx + ETH_HDR;
    ip[0] = 0x45;                                     // version 4, header of 5 words
    ip[1] = 0;
    put16(ip + 2, (uint16_t)(IP_HDR + payload_len));
    put16(ip + 4, ip_id++);
    put16(ip + 6, 0x4000);                            // don't fragment
    ip[8] = 64;                                       // time to live
    ip[9] = protocol;
    put16(ip + 10, 0);
    memcpy(ip + 12, cfg.ip, 4);
    memcpy(ip + 16, dst_ip, 4);
    put16(ip + 10, finish_sum(sum_words(ip, IP_HDR, 0)));
}

static void arp_packet(uint16_t operation, const uint8_t target_mac[6], const uint8_t target_ip[4]) {
    uint8_t *a = tx + ETH_HDR;
    put16(a, 1);                                      // hardware type: Ethernet
    put16(a + 2, TYPE_IPV4);
    a[4] = 6;
    a[5] = 4;
    put16(a + 6, operation);                          // 1 = request, 2 = reply
    memcpy(a + 8, cfg.mac, 6);
    memcpy(a + 14, cfg.ip, 4);
    memcpy(a + 18, target_mac, 6);
    memcpy(a + 24, target_ip, 4);
}

void net_announce(void) {
    static const uint8_t zero_mac[6] = {0};
    eth_header(BROADCAST_MAC, TYPE_ARP);
    arp_packet(1, zero_mac, cfg.ip);
    send_frame(tx, ETH_HDR + ARP_LEN);
}

bool net_udp_send(const net_peer_t *to, uint16_t from_port, const uint8_t *data, int len) {
    if (len < 0 || len > NET_MAX_UDP) return false;
    uint8_t *udp = tx + ETH_HDR + IP_HDR;
    int udp_len = UDP_HDR + len;

    eth_header(to->mac, TYPE_IPV4);
    ip_header(to->ip, PROTO_UDP, udp_len);
    put16(udp, from_port);
    put16(udp + 2, to->port);
    put16(udp + 4, (uint16_t)udp_len);
    put16(udp + 6, 0);
    memcpy(udp + UDP_HDR, data, (size_t)len);
    // The UDP checksum also covers both IP addresses, the protocol number and the length.
    uint32_t sum = sum_words(cfg.ip, 4, 0);
    sum = sum_words(to->ip, 4, sum) + PROTO_UDP + (uint32_t)udp_len;
    uint16_t check = finish_sum(sum_words(udp, udp_len, sum));
    put16(udp + 6, check ? check : 0xFFFF);           // 0 would mean "no checksum"

    stats.udp_out++;
    return send_frame(tx, ETH_HDR + IP_HDR + udp_len);
}

// ---------------------------------------------------------------- receiving
static void arp_input(const uint8_t *frame, int len) {
    const uint8_t *a = frame + ETH_HDR;
    if (len < ETH_HDR + ARP_LEN) return;
    bool ethernet_ipv4 = be16(a) == 1 && be16(a + 2) == TYPE_IPV4 && a[4] == 6 && a[5] == 4;
    bool request_for_us = be16(a + 6) == 1 && memcmp(a + 24, cfg.ip, 4) == 0;
    if (!ethernet_ipv4 || !request_for_us) return;

    uint8_t asker_mac[6], asker_ip[4];                // copy first: the answer is built in tx
    memcpy(asker_mac, a + 8, 6);
    memcpy(asker_ip, a + 14, 4);
    eth_header(asker_mac, TYPE_ARP);
    arp_packet(2, asker_mac, asker_ip);
    stats.arp_replies++;
    send_frame(tx, ETH_HDR + ARP_LEN);
}

static void ping_input(const uint8_t *frame, const uint8_t *ip, const uint8_t *icmp, int len) {
    bool echo_request = len >= 8 && icmp[0] == 8 && icmp[1] == 0;
    if (!echo_request || finish_sum(sum_words(icmp, len, 0)) != 0) return;

    uint8_t src_mac[6], src_ip[4];
    memcpy(src_mac, frame + 6, 6);
    memcpy(src_ip, ip + 12, 4);
    uint8_t *reply = tx + ETH_HDR + IP_HDR;
    memmove(reply, icmp, (size_t)len);                // same identifier, sequence number and data
    reply[0] = 0;                                     // echo reply
    put16(reply + 2, 0);
    put16(reply + 2, finish_sum(sum_words(reply, len, 0)));
    eth_header(src_mac, TYPE_IPV4);
    ip_header(src_ip, PROTO_ICMP, len);
    stats.ping_replies++;
    send_frame(tx, ETH_HDR + IP_HDR + len);
}

static void udp_input(const uint8_t *frame, const uint8_t *ip, const uint8_t *udp, int len) {
    if (len < UDP_HDR) return;
    int udp_len = be16(udp + 4);
    if (udp_len < UDP_HDR || udp_len > len) return;
    if (be16(udp + 6) != 0) {                         // checksum is optional in IPv4; check it when present
        uint32_t sum = sum_words(ip + 12, 8, 0) + PROTO_UDP + (uint32_t)udp_len;
        if (finish_sum(sum_words(udp, udp_len, sum)) != 0) { stats.dropped++; return; }
    }
    net_peer_t from;
    memcpy(from.mac, frame + 6, 6);
    memcpy(from.ip, ip + 12, 4);
    from.port = be16(udp);
    stats.udp_in++;
    on_udp(be16(udp + 2), &from, udp + UDP_HDR, udp_len - UDP_HDR);
}

static void ip_input(const uint8_t *frame, int len) {
    const uint8_t *ip = frame + ETH_HDR;
    int avail = len - ETH_HDR;
    if (avail < IP_HDR || (ip[0] >> 4) != 4) return;
    int header = (ip[0] & 0x0F) * 4;
    int total = be16(ip + 2);
    if (header < IP_HDR || total < header || total > avail) return;
    if (finish_sum(sum_words(ip, header, 0)) != 0) { stats.dropped++; return; }
    if (be16(ip + 6) & 0x3FFF) { stats.dropped++; return; }     // a fragment: not supported

    bool to_us = memcmp(ip + 16, cfg.ip, 4) == 0;
    if (!to_us && !is_broadcast_ip(ip + 16)) return;

    const uint8_t *payload = ip + header;
    int payload_len = total - header;
    if (ip[9] == PROTO_UDP) udp_input(frame, ip, payload, payload_len);
    else if (ip[9] == PROTO_ICMP && to_us) ping_input(frame, ip, payload, payload_len);
}

void net_input(const uint8_t *frame, int len) {
    if (len < ETH_HDR) return;
    stats.frames++;
    uint16_t type = be16(frame + 12);
    if (type == TYPE_ARP) arp_input(frame, len);
    else if (type == TYPE_IPV4) ip_input(frame, len);
}
