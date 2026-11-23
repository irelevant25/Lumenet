#include "board.h"
#include <xc.h>
#include <sys/attribs.h>
#include "config.h"          // DMX_OUTPUTS
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
#pragma config JTAGEN = OFF         // RB5, RB7, RB8 and RB9 are pins of the board, not JTAG
#pragma config ICESEL = ICS_PGx1    // the programmer is on PGED1/PGEC1 (pins 4 and 5)
#pragma config PWP = OFF
#pragma config BWP = OFF
#pragma config CP = OFF
#pragma config FUSBIDIO = OFF       // RB5 is the LED, not USBID
#pragma config FVBUSONIO = OFF      // RB14 is a direction pin, not VBUSON
#pragma config IOL1WAY = OFF
#pragma config PMDL1WAY = OFF

#define SYSCLK_HZ 40000000u
#define PBCLK_HZ  40000000u

// Port bits (RAx, RBx)
#define SW1 (1u << 0)        // RA0, pin 2
#define SW2 (1u << 1)        // RA1, pin 3
#define SW3 (1u << 2)        // RB2, pin 6
#define SW4 (1u << 3)        // RB3, pin 7
#define SW5 (1u << 4)        // RB4, pin 11
#define SW6 (1u << 4)        // RA4, pin 12
#define BTN_BLACKOUT (1u << 0)   // RB0, pin 4 (PGED1)
#define BTN_PRESET   (1u << 1)   // RB1, pin 5 (PGEC1)
#define LED          (1u << 5)   // RB5, pin 14
#define TX1  (1u << 15)          // RB15, pin 26
#define DIR1 (1u << 14)          // RB14, pin 25
#define RX1  (1u << 13)          // RB13, pin 24
#define TX2  (1u << 9)           // RB9, pin 18
#define RX2  (1u << 8)           // RB8, pin 17
#define DIR2 (1u << 7)           // RB7, pin 16

static volatile uint32_t ticks;   // milliseconds, counted by timer 1

void __ISR(_TIMER_1_VECTOR, IPL2SOFT) timer1_isr(void) {
    ticks++;
    IFS0CLR = _IFS0_T1IF_MASK;
}

// The USB port: TinyUSB's driver does the work (and clears the flag).
void __ISR(_USB_1_VECTOR, IPL4SOFT) usb_isr(void) {
    tud_int_handler(0);
}

uint32_t board_millis(void) { return ticks; }

// Waits a number of microseconds on the core timer (it counts at half the system clock).
static void wait_us(uint32_t us) {
    uint32_t start = _CP0_GET_COUNT();
    while (_CP0_GET_COUNT() - start < us * (SYSCLK_HZ / 2000000u)) {}
}

void board_init(void) {
    ANSELA = 0;                                  // every pin digital
    ANSELB = 0;

    // Outputs: the TX lines idle high (no light in the optocoupler: the DMX
    // line rests) until the UARTs take them; direction low (the box sends);
    // the LED off.
    LATBSET = TX1 | TX2;
    LATBCLR = DIR1 | DIR2 | LED;
    TRISBCLR = TX1 | TX2 | DIR1 | DIR2 | LED;

    // Inputs with the chip's own pull-ups: the switch and the buttons pull to
    // ground. The RX lines have pull-ups on the board as well.
    CNPUASET = SW1 | SW2 | SW6;
    CNPUBSET = SW3 | SW4 | SW5 | BTN_BLACKOUT | BTN_PRESET | RX1 | RX2;

    // Which UART pin goes where (peripheral pin select).
    SYSKEY = 0;
    SYSKEY = 0xAA996655;
    SYSKEY = 0x556699AA;
    CFGCONbits.IOLOCK = 0;
    RPB15R = 1;                                  // U1TX on RB15
    U1RXR = 3;                                   // U1RX from RB13 (for RDM, later)
#if DMX_OUTPUTS == 2                             // the box with one output leaves RB7 to RB9 free
    RPB9R = 2;                                   // U2TX on RB9
    U2RXR = 4;                                   // U2RX from RB8 (for RDM, later)
#endif
    CFGCONbits.IOLOCK = 1;
    SYSKEY = 0;

    // Timer 1: one interrupt every millisecond (40 MHz / 8 / 5000).
    T1CON = 0;
    TMR1 = 0;
    PR1 = PBCLK_HZ / 8u / 1000u - 1u;
    T1CONbits.TCKPS = 1;                         // prescaler 1:8
    IPC1bits.T1IP = 2;
    IFS0CLR = _IFS0_T1IF_MASK;
    IEC0SET = _IEC0_T1IE_MASK;
    T1CONbits.ON = 1;

    // USB: priority 4 (TinyUSB switches the interrupt on itself). The DMX
    // outputs get 5, see dmx_out.c: their bytes must not wait for USB.
    IPC7bits.USBIP = 4;

    INTCONSET = _INTCON_MVEC_MASK;               // one interrupt vector per source
    __builtin_enable_interrupts();

    wait_us(1000);                               // the pull-ups need a moment before the switch is read
}

uint8_t board_box_number(void) {
    uint32_t a = PORTA, b = PORTB;               // a switch that is ON pulls its pin to ground
    uint8_t number = 0;
    if (!(a & SW1)) number |= 1;
    if (!(a & SW2)) number |= 2;
    if (!(b & SW3)) number |= 4;
    if (!(b & SW4)) number |= 8;
    if (!(b & SW5)) number |= 16;
    if (!(a & SW6)) number |= 32;
    return number;
}

bool board_button_blackout(void) { return !(PORTB & BTN_BLACKOUT); }
bool board_button_preset(void)   { return !(PORTB & BTN_PRESET); }

void board_led(bool on) {
    if (on) LATBSET = LED;
    else    LATBCLR = LED;
}
