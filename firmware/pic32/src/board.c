#include "board.h"
#include <stdarg.h>
#include <stdio.h>
#include <xc.h>
#include <sys/attribs.h>
#include "config.h"          // LOG_BAUD, and box.h with the pins
#include "dmx_line.h"
#include "pins.h"
#include "tusb.h"

// ---------------------------------------------------------------- configuration bits
// They are programmed into the chip with the firmware and set it up before it starts.
#pragma config FNOSC = PRIPLL       // the crystal through the PLL
#pragma config POSCMOD = XT         // an 8 MHz crystal (XT: 3 to 10 MHz)
#pragma config FPLLIDIV = DIV_2     // 8 MHz / 2 = 4 MHz into the PLL
#pragma config FPLLMUL = MUL_20     // x 20 = 80 MHz
#pragma config FPLLODIV = DIV_2     // / 2 = 40 MHz, the fastest the -I part may run
#pragma config FPBDIV = DIV_1       // the peripherals (UARTs, timer) at 40 MHz too
#pragma config UPLLIDIV = DIV_2     // 4 MHz into the USB PLL, which makes the 48 MHz of USB
#pragma config UPLLEN = ON
#pragma config FSOSCEN = OFF        // no second crystal: RB4 and RA4 read the switch
#pragma config OSCIOFNC = OFF
#pragma config IESO = OFF
#pragma config FCKSM = CSDCMD
#pragma config FWDTEN = OFF         // no watchdog
#pragma config JTAGEN = OFF         // RB5, RB7, RB8 and RB9 are pins of the board (switch, LEDs), not JTAG
#pragma config ICESEL = ICS_PGx1    // the programmer is on PGED1/PGEC1 (pins 4 and 5, J2)
#pragma config PWP = OFF
#pragma config BWP = OFF
#pragma config CP = OFF
#pragma config FUSBIDIO = OFF       // RB5 reads the switch, it is not USBID
#pragma config FVBUSONIO = OFF      // RB14 is the DMX direction, not VBUSON
#pragma config IOL1WAY = OFF
#pragma config PMDL1WAY = OFF

// The UARTs reach these pins through peripheral pin select (set below).
_Static_assert(PIN_DMX_TX == PB(15) && PIN_DMX_RX == PB(13) && PIN_LOG_TX == PB(0),
               "board_init maps U1TX to RB15, U1RX to RB13 and U2TX to RB0");

static volatile uint32_t ticks;   // milliseconds, counted by timer 1

void __ISR(_TIMER_1_VECTOR, IPL2SOFT) timer1_isr(void) {
    ticks++;
    IFS0CLR = _IFS0_T1IF_MASK;
}

// The USB port: TinyUSB's driver does the work (and clears the flag).
void __ISR(_USB_1_VECTOR, IPL4SOFT) usb_isr(void) {
    tud_int_handler(0);
}

// The DMX line: its bytes must not wait, so it comes before USB (uart1.h gives it priority 5).
void __ISR(_UART_1_VECTOR, IPL5SOFT) uart1_isr(void) {
    dmx_line_interrupt();
}

uint32_t board_millis(void) { return ticks; }
uint32_t board_ticks(void)  { return _CP0_GET_COUNT(); }

static const int switch_pins[8] = PIN_BOX_N;

void board_init(void) {
    ANSELA = 0;                                  // every pin digital
    ANSELB = 0;

    pin_output(PIN_LED_USB, false);
    pin_output(PIN_LED_DMX, false);
    pin_output(PIN_LED_RDM, false);
    for (int k = 0; k < 8; k++) pin_input_pulled_up(switch_pins[k]);   // a switch that is ON pulls to ground

    // Which UART pin goes where (peripheral pin select). The DMX line's pins
    // are dmx_line.c's; they are set here so the registers are unlocked once.
    SYSKEY = 0;
    SYSKEY = 0xAA996655;
    SYSKEY = 0x556699AA;
    CFGCONbits.IOLOCK = 0;
    RPB15R = 1;                                  // U1TX on RB15: DMX and RDM out
    U1RXR = 3;                                   // U1RX from RB13: RDM answers
    RPB0R = 2;                                   // U2TX on RB0: the log
    CFGCONbits.IOLOCK = 1;
    SYSKEY = 0;

    // The log: UART2, 8 data bits, 1 stop bit, sending only.
    pin_output(PIN_LOG_TX, true);
    U2MODE = 0;
    U2STA = 0;
    U2BRG = PBCLK_HZ / 4u / LOG_BAUD - 1u;       // BRGH = 1: PBCLK / 4 / (BRG + 1)
    U2MODE = _U2MODE_BRGH_MASK;
    U2MODESET = _U2MODE_ON_MASK;
    U2STASET = _U2STA_UTXEN_MASK;

    // Timer 1: one interrupt every millisecond (40 MHz / 8 / 5000).
    T1CON = 0;
    TMR1 = 0;
    PR1 = PBCLK_HZ / 8u / 1000u - 1u;
    T1CONbits.TCKPS = 1;                         // prescaler 1:8
    IPC1bits.T1IP = 2;
    IFS0CLR = _IFS0_T1IF_MASK;
    IEC0SET = _IEC0_T1IE_MASK;
    T1CONbits.ON = 1;

    // USB: priority 4 (TinyUSB switches its interrupt on itself). The DMX
    // line gets 5, see dmx_line.c: its bytes must not wait for USB.
    IPC7bits.USBIP = 4;

    INTCONSET = _INTCON_MVEC_MASK;               // one interrupt vector per source
    __builtin_enable_interrupts();

    uint32_t start = board_ticks();              // the pull-ups need a moment before the switch is read
    while (board_ticks() - start < 1000u * BOARD_TICKS_PER_US) {}
}

uint8_t board_box_number(void) {
    uint8_t n = 0;
    for (int k = 0; k < 8; k++)
        if (pin_is_low(switch_pins[k])) n |= (uint8_t)(1u << k);
    return n;
}

void board_leds(bool usb, bool dmx, bool rdm) {
    pin_set(PIN_LED_USB, usb);
    pin_set(PIN_LED_DMX, dmx);
    pin_set(PIN_LED_RDM, rdm);
}

static void log_put(char c) {
    while (U2STA & _U2STA_UTXBF_MASK) {}         // the UART's buffer is full
    U2TXREG = (uint8_t)c;
}

void board_log(const char *fmt, ...) {
    char line[160];
    va_list ap;
    va_start(ap, fmt);
    int n = vsnprintf(line, sizeof line, fmt, ap);
    va_end(ap);
    if (n > (int)sizeof line - 1) n = (int)sizeof line - 1;
    for (int i = 0; i < n; i++) {
        if (line[i] == '\n') log_put('\r');      // as the RP2040 boxes: lines end in CR LF
        log_put(line[i]);
    }
}
