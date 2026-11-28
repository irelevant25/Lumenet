// Stands in for firmware/pic32/src/uart1.h in the PC test of box 2's DMX line:
// the same functions, on a pretend UART and line with lights on it, which
// test_line.c makes.
#pragma once
#include <stdbool.h>
#include <stdint.h>

#define LINE_TICKS_PER_US 20u            // as the PIC's core timer
#define UART_ASK_EMPTY (2u << 14)
#define UART_ASK_DONE  (1u << 14)
#define UART_FRAMING 1u
#define UART_PARITY  2u

uint32_t line_now(void);
void     line_send(bool send);
unsigned line_irq_off(void);
void     line_irq_restore(unsigned state);
void     uart_start(void);
void     uart_speed(bool for_break);
void     uart_ask(uint32_t when);
bool     uart_full(void);
void     uart_put(uint8_t b);
void     uart_tx_irq(bool on);
bool     uart_tx_due(void);
void     uart_tx_served(void);
void     uart_rx_irq(bool on);
bool     uart_rx_due(void);
void     uart_rx_served(void);
bool     uart_rx_ready(void);
uint8_t  uart_rx_get(uint32_t *errors);
bool     uart_rx_overrun(void);
