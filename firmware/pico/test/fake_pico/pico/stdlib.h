// NOT the Pico SDK: stand-in for pico/stdlib.h, see fake_types.h.
#pragma once
#include "fake_types.h"

// time
typedef uint64_t absolute_time_t;
absolute_time_t get_absolute_time(void);
uint32_t to_ms_since_boot(absolute_time_t t);

// log text: collected by fake_pico.c instead of going to a serial pin
bool stdio_init_all(void);
int  fake_printf(const char *format, ...);
#define printf fake_printf

// pins
#define GPIO_OUT true
#define GPIO_IN  false
#define PICO_DEFAULT_LED_PIN 25       // as on a Pico and a Pico 2
void gpio_init(uint gpio);
void gpio_set_dir(uint gpio, bool out);
void gpio_pull_up(uint gpio);
bool gpio_get(uint gpio);
void gpio_put(uint gpio, bool value);
