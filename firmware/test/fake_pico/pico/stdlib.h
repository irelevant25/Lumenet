// NOT the Pico SDK: stand-in for pico/stdlib.h, see fake_types.h.
#pragma once
#include "fake_types.h"

// time
typedef uint64_t absolute_time_t;
absolute_time_t get_absolute_time(void);
uint32_t to_ms_since_boot(absolute_time_t t);
void sleep_ms(uint32_t ms);

// log text: collected by fake_pico.c instead of going to a serial pin
typedef struct uart_inst uart_inst_t;
#define uart0 ((uart_inst_t *)0)
void stdio_uart_init_full(uart_inst_t *uart, uint baud_rate, int tx_pin, int rx_pin);
int  fake_printf(const char *format, ...);
#define printf fake_printf
