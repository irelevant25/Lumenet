// ENC28J60 Ethernet controller (10BASE-T over SPI): raw Ethernet frames in and out.
//
// Unlike the W5500 this chip has no TCP/IP stack inside. It only moves whole
// Ethernet frames; net.c adds the little bit of ARP / IPv4 / UDP the box needs.
//
// The driver does not touch the Pico SDK directly. It talks to the chip through
// three small functions (enc_hal_t), so the same file also runs on a PC against
// a simulated chip (see firmware/test).
#pragma once
#include <stdbool.h>
#include <stddef.h>
#include <stdint.h>

#define ENC_MAX_FRAME 1514   // largest Ethernet frame without its 4-byte CRC

typedef struct {
    void (*select)(bool active);                                    // true: pull CS low, false: release it
    void (*transfer)(const uint8_t *tx, uint8_t *rx, size_t len);   // SPI; tx NULL = send zeros, rx NULL = ignore
    void (*delay_us)(uint32_t us);
} enc_hal_t;

// Soft-resets the chip and configures it: receive our MAC address + broadcast,
// half duplex (the chip cannot auto-negotiate; the other end detects 10 Mbit
// half duplex by itself). Returns false if no chip answers (wiring, 3.3 V).
// Pulse the RESET pin before calling this.
bool enc_init(const enc_hal_t *hal, const uint8_t mac[6]);

uint8_t enc_revision(void);   // silicon revision read during enc_init (6 = B7)
bool    enc_link_up(void);

// Fetch one received frame (without CRC). Returns its length, 0 if nothing is
// waiting, -1 if a frame was thrown away (damaged or longer than `cap`).
int enc_receive(uint8_t *frame, int cap);

// Queue one frame for transmission (the chip pads short frames and adds the CRC).
bool enc_send(const uint8_t *frame, int len);
