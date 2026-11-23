// NOT the Pico SDK: stand-in for pico/critical_section.h, see fake_types.h.
#pragma once
#include "fake_types.h"

typedef struct { int depth; } critical_section_t;
void critical_section_init(critical_section_t *cs);
void critical_section_enter_blocking(critical_section_t *cs);
void critical_section_exit(critical_section_t *cs);
