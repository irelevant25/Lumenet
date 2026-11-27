// MIDI between the DIN sockets and USB: bytes of a MIDI cable to USB-MIDI
// event packets (4 bytes each, USB MIDI 1.0 chapter 4) and back.
//
// The cable side may use running status (a message without its status byte
// repeats the status of the one before), real-time bytes in the middle of
// other messages, and SysEx of any length. USB carries every message whole.
//
// Pure C, no SDK calls: tested on a PC in firmware/test.
#pragma once
#include <stdbool.h>
#include <stdint.h>

typedef struct {
    uint8_t status;         // running status, 0 = none
    uint8_t data[2];
    uint8_t count, need;    // data bytes so far and wanted
    bool    sysex;
    uint8_t sx[3];          // SysEx bytes not sent yet
    uint8_t sxn;
} midi_parser_t;

void midi_parser_init(midi_parser_t *p);

// One byte from the cable. Returns true when it completed a packet, written to pkt (cable 0).
bool midi_parse(midi_parser_t *p, uint8_t b, uint8_t pkt[4]);

// The bytes one USB-MIDI packet carries to the cable: 0 to 3 of them.
int midi_packet_bytes(const uint8_t pkt[4], uint8_t out[3]);
