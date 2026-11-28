// The pins of box.h (PA(n) is RAn, PB(n) is RBn) on the PIC's port registers.
#pragma once
#include <stdbool.h>
#include <stdint.h>
#include <xc.h>
#include "box.h"

#define PIN_BIT(pin)  (1u << ((pin) & 15))
#define PIN_ON_B(pin) ((pin) >= 16)

static inline void pin_set(int pin, bool high) {
    if (PIN_ON_B(pin)) { if (high) LATBSET = PIN_BIT(pin); else LATBCLR = PIN_BIT(pin); }
    else               { if (high) LATASET = PIN_BIT(pin); else LATACLR = PIN_BIT(pin); }
}

// An output, at the given level from the start.
static inline void pin_output(int pin, bool high) {
    pin_set(pin, high);
    if (PIN_ON_B(pin)) TRISBCLR = PIN_BIT(pin); else TRISACLR = PIN_BIT(pin);
}

// An input with the chip's own pull-up (about 10 uA).
static inline void pin_input_pulled_up(int pin) {
    if (PIN_ON_B(pin)) { TRISBSET = PIN_BIT(pin); CNPUBSET = PIN_BIT(pin); }
    else               { TRISASET = PIN_BIT(pin); CNPUASET = PIN_BIT(pin); }
}

static inline bool pin_is_low(int pin) {
    return ((PIN_ON_B(pin) ? PORTB : PORTA) & PIN_BIT(pin)) == 0;
}
