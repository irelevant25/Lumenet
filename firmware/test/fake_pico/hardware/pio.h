// NOT the Pico SDK: stand-in for hardware/pio.h, see fake_types.h.
#pragma once
#include "fake_types.h"

typedef struct fake_pio *PIO;
#define pio0 ((PIO)0)
