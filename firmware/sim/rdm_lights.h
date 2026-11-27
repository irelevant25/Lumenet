// Lights with RDM that exist only in memory: they answer the box's RDM packets
// the way real ones would. Used by the simulator (sim.c) and the tests (firmware/test).
#pragma once
#include <stdbool.h>
#include <stdint.h>

#define LIGHTS_MAX 64

typedef struct {
    uint8_t  uid[6];
    bool     muted;
    uint16_t start;                 // DMX start address, 1 to 512
    uint8_t  personality;           // the mode in use, 1 to personalities
    uint8_t  personalities;
    uint16_t footprint[3];          // channels of each mode
    const char *mode_name[3];
    char     label[33];
    const char *maker;
    const char *model;
    uint16_t model_id;
    bool     identify;
} light_t;

extern light_t lights[LIGHTS_MAX];
extern int     light_count;

// n lights of three kinds, with IDs of the "prototype" range 7FF1:00000001 on.
void lights_make(int n);

// The line: everybody hears the packet; the answer (if any) is written to out.
// Returns its length; *garbled is set when several lights answered at once.
int lights_hear(const uint8_t *pkt, int len, uint8_t *out, int cap, bool *garbled);
