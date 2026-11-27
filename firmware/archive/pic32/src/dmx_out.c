// Two DMX512 outputs on the two UARTs of the PIC32, sent at the same time.
//
// Every frame on an output: a break (the line low for 180 us), the mark after
// the break (high for 40 us), then the start code 0 and the 512 levels, each a
// byte at 250 000 baud with two stop bits: about 23 ms, so 44 frames a second.
//
// The break is a 0 sent at 50 000 baud: its start bit and 8 data bits are the
// 180 us low, its two stop bits the 40 us high. So the UART times the break as
// well, and the pin never has to be switched over.
//
// Interrupts do the work. The UART asks for bytes when its 8-byte buffer is
// empty (the last byte is then still on its way out, 44 us: time enough to
// refill), and says when the last bit of the frame has gone out. main.c starts
// a frame on both outputs at once and the next one when both are done.
#include "dmx_out.h"
#include <xc.h>
#include <sys/attribs.h>
#include "engine.h"          // DMX_OUTPUTS, DMX_OUTPUT_CHANNELS

_Static_assert(DMX_OUTPUTS == 1 || DMX_OUTPUTS == 2, "the PIC32MX270 has two UARTs: one or two outputs");

#define PBCLK_HZ  40000000u
#define BRG_DATA  (PBCLK_HZ / 4u / 250000u - 1u)    // 39: 250 000 baud (BRGH = 1)
#define BRG_BREAK (PBCLK_HZ / 4u / 50000u - 1u)     // 199: 50 000 baud

// The registers of one UART, in the order they lie in memory (datasheet table
// 21-1: UxMODE at 0x00, UxSTA at 0x10, UxTXREG 0x20, UxRXREG 0x30, UxBRG 0x40;
// each of MODE, STA and BRG with its CLR, SET and INV register after it).
typedef struct {
    volatile uint32_t mode, mode_clr, mode_set, mode_inv;
    volatile uint32_t sta, sta_clr, sta_set, sta_inv;
    volatile uint32_t txreg, unused_tx[3];
    volatile uint32_t rxreg, unused_rx[3];
    volatile uint32_t brg, brg_clr, brg_set, brg_inv;
} uart_t;

#define MODE_ON           (1u << 15)
#define MODE_BRGH         (1u << 3)     // the fast baud rate formula: PBCLK / 4 / (BRG + 1)
#define MODE_STSEL        (1u << 0)     // two stop bits
#define STA_UTXISEL       (3u << 14)    // when the UART asks for bytes:
#define STA_UTXISEL_EMPTY (2u << 14)    //   while its buffer is empty
#define STA_UTXISEL_DONE  (1u << 14)    //   when everything has gone out
#define STA_UTXEN         (1u << 10)
#define STA_UTXBF         (1u << 9)     // the buffer is full

enum { IDLE, BREAK, DATA, LAST };

typedef struct {
    uart_t        *uart;
    uint32_t       flag;                 // its TX interrupt in IFS1 and IEC1
    const uint8_t *levels;               // the 512 levels of this output
    uint16_t       next;                 // the next slot: 0 = start code, 1 to 512 = the levels
    volatile uint8_t state;
} port_t;

static port_t ports[DMX_OUTPUTS] = {
    {(uart_t *)&U1MODE, _IFS1_U1TXIF_MASK, 0, 0, IDLE},
#if DMX_OUTPUTS == 2
    {(uart_t *)&U2MODE, _IFS1_U2TXIF_MASK, 0, 0, IDLE},
#endif
};

static void ask_when(uart_t *u, uint32_t when) {
    u->sta_clr = STA_UTXISEL;
    u->sta_set = when;
}

// The work of one interrupt of an output.
static void serve(port_t *p) {
    uart_t *u = p->uart;
    switch (p->state) {
    case BREAK:                          // the break and the mark after it are out
        u->brg = BRG_DATA;
        ask_when(u, STA_UTXISEL_EMPTY);
        p->next = 0;
        p->state = DATA;
        /* fall through: fill the buffer right away */
    case DATA:
        while (!(u->sta & STA_UTXBF)) {
            u->txreg = p->next == 0 ? 0u : p->levels[p->next - 1];
            if (++p->next > DMX_OUTPUT_CHANNELS) {   // the last level is in the buffer
                ask_when(u, STA_UTXISEL_DONE);
                p->state = LAST;
                break;
            }
        }
        break;
    case LAST:                           // the frame has gone out
        IEC1CLR = p->flag;
        p->state = IDLE;
        break;
    default:
        IEC1CLR = p->flag;
        break;
    }
    // The flag stays set while the reason lasts: cleared after the buffer was filled.
    IFS1CLR = p->flag;
}

void __ISR(_UART_1_VECTOR, IPL5SOFT) uart1_isr(void) { serve(&ports[0]); }
#if DMX_OUTPUTS == 2
void __ISR(_UART_2_VECTOR, IPL5SOFT) uart2_isr(void) { serve(&ports[1]); }
#endif

void dmx_out_init(void) {
    for (int i = 0; i < DMX_OUTPUTS; i++) {
        uart_t *u = ports[i].uart;
        IEC1CLR = ports[i].flag;
        u->mode = 0;
        u->sta = 0;
        u->brg = BRG_DATA;
        u->mode = MODE_BRGH | MODE_STSEL;            // 8 data bits, no parity, 2 stop bits
        u->mode_set = MODE_ON;
        u->sta_set = STA_UTXEN;                      // the pin is the UART's now: it rests high
    }
    IPC8bits.U1IP = 5;                               // above USB (4) and the timer (2)
#if DMX_OUTPUTS == 2
    IPC9bits.U2IP = 5;
#endif
}

bool dmx_out_idle(void) {
    for (int i = 0; i < DMX_OUTPUTS; i++)
        if (ports[i].state != IDLE) return false;
    return true;
}

void dmx_out_start(const uint8_t *levels) {
    for (int i = 0; i < DMX_OUTPUTS; i++) {
        port_t *p = &ports[i];
        uart_t *u = p->uart;
        p->levels = levels + i * DMX_OUTPUT_CHANNELS;
        p->state = BREAK;
        u->brg = BRG_BREAK;
        ask_when(u, STA_UTXISEL_DONE);
        u->txreg = 0;                                // the break: 9 bits low at 50 000 baud
        IFS1CLR = p->flag;
        IEC1SET = p->flag;
    }
}
