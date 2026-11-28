// UART1 and the DMX line's direction pin, for dmx_line.c: the only place where
// it touches the PIC's registers. dmx_line.c takes this file with angle
// brackets, so the PC test can put its own uart1.h first on the include path
// (firmware/test/fake_pic32: a pretend line with lights on it).
#pragma once
#include <stdbool.h>
#include <stdint.h>
#include <xc.h>
#include "board.h"
#include "pins.h"

#define UART_BRG_DATA  (PBCLK_HZ / 4u / 250000u - 1u)   // 39: 250 000 baud (BRGH = 1)
#define UART_BRG_BREAK (PBCLK_HZ / 4u / 50000u - 1u)    // 199: 50 000 baud

// When the UART asks for bytes (UTXISEL in U1STA)
#define UART_ASK_EMPTY (2u << 14)        // while its buffer is empty
#define UART_ASK_DONE  (1u << 14)        // when everything has gone out

// What can be wrong with a byte received
#define UART_FRAMING 1u                  // its stop bit was low: a break, or a collision
#define UART_PARITY  2u

#define LINE_TICKS_PER_US BOARD_TICKS_PER_US
static inline uint32_t line_now(void) { return _CP0_GET_COUNT(); }      // LINE_TICKS_PER_US a microsecond
static inline void line_send(bool send) { pin_set(PIN_DMX_DE, send); }   // false: the driver lets go, the line is heard

// Interrupts off around a few lines of the main loop, and back as they were
static inline unsigned line_irq_off(void) {
    unsigned state = __builtin_get_isr_state();
    __builtin_disable_interrupts();
    return state;
}
static inline void line_irq_restore(unsigned state) { __builtin_set_isr_state(state); }

// 8 data bits, no parity, 2 stop bits, at 250 000 baud; sending and receiving.
// The UART's interrupt (board.c) gets priority 5, above USB (4) and the timer
// (2): no byte may wait.
static inline void uart_start(void) {
    U1MODE = 0;
    U1STA = 0;
    U1BRG = UART_BRG_DATA;
    U1MODE = _U1MODE_BRGH_MASK | _U1MODE_STSEL_MASK;
    U1MODESET = _U1MODE_ON_MASK;
    U1STASET = _U1STA_UTXEN_MASK | _U1STA_URXEN_MASK;   // the TX pin is the UART's now: it rests high
    IPC8bits.U1IP = 5;
    IPC8bits.U1IS = 0;
}

// The speed of the next bytes: 50 000 baud for a break, else 250 000. Only
// while nothing is going out.
static inline void uart_speed(bool for_break) { U1BRG = for_break ? UART_BRG_BREAK : UART_BRG_DATA; }

static inline void uart_ask(uint32_t when) {
    U1STACLR = 3u << 14;
    U1STASET = when;
}

static inline bool uart_full(void) { return (U1STA & _U1STA_UTXBF_MASK) != 0; }
static inline void uart_put(uint8_t b) { U1TXREG = b; }

// The sending side's interrupt: on (its old flag cleared first) or off, and
// whether it is due. The flag stays set while the reason lasts: uart_tx_served
// clears it after the buffer was filled.
static inline void uart_tx_irq(bool on) {
    if (on) { IFS1CLR = _IFS1_U1TXIF_MASK; IEC1SET = _IEC1_U1TXIE_MASK; }
    else    IEC1CLR = _IEC1_U1TXIE_MASK;
}
static inline bool uart_tx_due(void) { return (IFS1 & _IFS1_U1TXIF_MASK) && (IEC1 & _IEC1_U1TXIE_MASK); }
static inline void uart_tx_served(void) { IFS1CLR = _IFS1_U1TXIF_MASK; }

// The receiving side's interrupt, likewise; uart_rx_irq(true) also throws
// away what came before and clears an old overrun.
static inline void uart_rx_irq(bool on) {
    if (on) {
        while (U1STA & _U1STA_URXDA_MASK) (void)U1RXREG;
        U1STACLR = _U1STA_OERR_MASK;
        IFS1CLR = _IFS1_U1RXIF_MASK;
        IEC1SET = _IEC1_U1RXIE_MASK;
    } else {
        IEC1CLR = _IEC1_U1RXIE_MASK;
    }
}
static inline bool uart_rx_due(void) { return (IFS1 & _IFS1_U1RXIF_MASK) && (IEC1 & _IEC1_U1RXIE_MASK); }
static inline void uart_rx_served(void) { IFS1CLR = _IFS1_U1RXIF_MASK; }

static inline bool uart_rx_ready(void) { return (U1STA & _U1STA_URXDA_MASK) != 0; }

// The oldest byte received, and what was wrong with it (UART_FRAMING ...): the
// flags in U1STA belong to that byte, so they are read first.
static inline uint8_t uart_rx_get(uint32_t *errors) {
    uint32_t sta = U1STA;
    *errors = ((sta & _U1STA_FERR_MASK) ? UART_FRAMING : 0u) | ((sta & _U1STA_PERR_MASK) ? UART_PARITY : 0u);
    return (uint8_t)U1RXREG;
}

// True (once) if bytes were lost because the buffer was full.
static inline bool uart_rx_overrun(void) {
    if (!(U1STA & _U1STA_OERR_MASK)) return false;
    U1STACLR = _U1STA_OERR_MASK;
    return true;
}
