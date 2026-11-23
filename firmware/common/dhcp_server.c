#include "dhcp_server.h"
#include <stdbool.h>
#include <string.h>

// Where the fields sit inside a DHCP message (RFC 2131)
#define AT_OP       0     // 1 = question from a computer, 2 = answer from a server
#define AT_HTYPE    1     // 1 = Ethernet
#define AT_HLEN     2     // 6 = length of an Ethernet address
#define AT_XID      4     // number chosen by the computer; the answer repeats it
#define AT_FLAGS    10
#define AT_CIADDR   12    // address the computer already uses (0.0.0.0 if none)
#define AT_YIADDR   16    // "your address": filled in by the server
#define AT_GIADDR   24
#define AT_CHADDR   28    // the computer's Ethernet address (16 bytes are reserved for it)
#define AT_COOKIE   236   // four fixed bytes that mark the start of the options
#define AT_OPTIONS  240

// Kinds of message (the value of option 53)
enum { DISCOVER = 1, OFFER = 2, REQUEST = 3, ACK = 5, NAK = 6, INFORM = 8 };

// The options this server reads or writes
enum {
    OPT_PAD = 0, OPT_NETMASK = 1, OPT_REQUESTED_IP = 50, OPT_LEASE_TIME = 51,
    OPT_MESSAGE_TYPE = 53, OPT_SERVER_ID = 54, OPT_END = 255,
};

static const uint8_t MAGIC_COOKIE[4] = {0x63, 0x82, 0x53, 0x63};
static dhcp_config_t cfg;

void dhcp_server_init(const dhcp_config_t *c) { cfg = *c; }

// Look for option `code`. Returns a pointer to its data and stores the data
// length in *data_len, or returns NULL if the message does not have it.
static const uint8_t *find_option(const uint8_t *msg, int len, uint8_t code, int *data_len) {
    int i = AT_OPTIONS;
    while (i < len) {
        uint8_t this_code = msg[i];
        if (this_code == OPT_END) break;
        if (this_code == OPT_PAD) { i++; continue; }       // padding is a single byte without a length
        if (i + 1 >= len) break;                           // the length byte is missing
        int this_len = msg[i + 1];
        if (i + 2 + this_len > len) break;                 // runs past the end: damaged message
        if (this_code == code) {
            *data_len = this_len;
            return msg + i + 2;
        }
        i += 2 + this_len;
    }
    return NULL;
}

// Write an answer of kind `type` to `r`. `your_ip` is the address handed to
// the computer (NULL = none). The lease time is only sent along with an address.
static int build_reply(const uint8_t *msg, uint8_t type, const uint8_t *your_ip, uint8_t *r) {
    memset(r, 0, DHCP_REPLY_LEN);
    r[AT_OP] = 2;
    r[AT_HTYPE] = 1;
    r[AT_HLEN] = 6;
    memcpy(r + AT_XID, msg + AT_XID, 4);
    memcpy(r + AT_FLAGS, msg + AT_FLAGS, 2);
    if (type == ACK) memcpy(r + AT_CIADDR, msg + AT_CIADDR, 4);
    if (your_ip) memcpy(r + AT_YIADDR, your_ip, 4);
    memcpy(r + AT_GIADDR, msg + AT_GIADDR, 4);
    memcpy(r + AT_CHADDR, msg + AT_CHADDR, 16);
    memcpy(r + AT_COOKIE, MAGIC_COOKIE, 4);

    uint8_t *o = r + AT_OPTIONS;
    *o++ = OPT_MESSAGE_TYPE; *o++ = 1; *o++ = type;
    *o++ = OPT_SERVER_ID;    *o++ = 4; memcpy(o, cfg.server_ip, 4); o += 4;
    if (your_ip) {
        *o++ = OPT_LEASE_TIME; *o++ = 4;
        *o++ = (uint8_t)(cfg.lease_s >> 24);
        *o++ = (uint8_t)(cfg.lease_s >> 16);
        *o++ = (uint8_t)(cfg.lease_s >> 8);
        *o++ = (uint8_t)cfg.lease_s;
    }
    if (type != NAK) {
        *o++ = OPT_NETMASK; *o++ = 4; memcpy(o, cfg.netmask, 4); o += 4;
    }
    // No "router" and no "name server" option on purpose: the computer must
    // keep using its normal network for the Internet, not this box.
    *o++ = OPT_END;
    return DHCP_REPLY_LEN;                                 // the unused rest stays zero
}

int dhcp_server_handle(const uint8_t *msg, int len, uint8_t *reply, int reply_cap) {
    if (len < AT_OPTIONS || reply_cap < DHCP_REPLY_LEN) return 0;
    bool question_from_ethernet = msg[AT_OP] == 1 && msg[AT_HTYPE] == 1 && msg[AT_HLEN] == 6;
    if (!question_from_ethernet || memcmp(msg + AT_COOKIE, MAGIC_COOKIE, 4) != 0) return 0;

    int data_len;
    const uint8_t *type = find_option(msg, len, OPT_MESSAGE_TYPE, &data_len);
    if (!type || data_len != 1) return 0;

    switch (type[0]) {
    case DISCOVER:                                         // "is there a server?" -> "yes, you could have this address"
        return build_reply(msg, OFFER, cfg.client_ip, reply);

    case REQUEST: {                                        // "I would like this address" -> yes (ACK) or no (NAK)
        // A computer that got offers from several servers names the one it chose.
        // If that is another server, the request is not meant for us.
        const uint8_t *server = find_option(msg, len, OPT_SERVER_ID, &data_len);
        if (server && (data_len != 4 || memcmp(server, cfg.server_ip, 4) != 0)) return 0;

        // The wanted address is in an option when the computer is new here,
        // and in the "ciaddr" field when it only extends what it already has.
        const uint8_t *wanted = find_option(msg, len, OPT_REQUESTED_IP, &data_len);
        if (!wanted || data_len != 4) wanted = msg + AT_CIADDR;
        if (memcmp(wanted, cfg.client_ip, 4) == 0) return build_reply(msg, ACK, cfg.client_ip, reply);
        return build_reply(msg, NAK, NULL, reply);         // any other address: "no, start over"
    }

    case INFORM:                                           // "I have an address already, anything else to know?"
        return build_reply(msg, ACK, NULL, reply);

    default:                                               // nothing else needs an answer
        return 0;
    }
}
