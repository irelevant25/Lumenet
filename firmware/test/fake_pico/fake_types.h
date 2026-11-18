// NOT the Pico SDK.
//
// The files in this folder have the names of Pico SDK and TinyUSB headers but
// contain only the few declarations the firmware uses. They let the firmware
// (including the real main.c) be compiled and run on a PC, where fake_pico.c
// plays the role of the hardware. They prove that the firmware's own logic
// works; they do not prove that it builds against the real SDK.
#pragma once
#include <stdbool.h>
#include <stddef.h>
#include <stdint.h>

typedef unsigned int uint;
