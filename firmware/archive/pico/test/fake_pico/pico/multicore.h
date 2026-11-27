// NOT the Pico SDK: stand-in for pico/multicore.h, see fake_types.h.
#pragma once
#include "fake_types.h"

void multicore_launch_core1(void (*entry)(void));
