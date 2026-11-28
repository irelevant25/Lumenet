// The dmxbox 4 board for the Pico SDK (PICO_BOARD=dmxbox4): an RP2040 with a
// 16 MB W25Q128JV flash and a 12 MHz ABM8 crystal, like Raspberry Pi's
// "minimal design". The pins are in box.h.
#ifndef _BOARDS_DMXBOX4_H
#define _BOARDS_DMXBOX4_H

pico_board_cmake_set(PICO_PLATFORM, rp2040)

// The log goes out of UART0 TX on GPIO12 (the service pads J7). GPIO13, the RX
// of the same UART, takes the RDM answers: the firmware sets it up itself.
#ifndef PICO_DEFAULT_UART
#define PICO_DEFAULT_UART 0
#endif
#ifndef PICO_DEFAULT_UART_TX_PIN
#define PICO_DEFAULT_UART_TX_PIN 12
#endif

// No LED for the SDK: the three LEDs of the board are the firmware's.

// ---------------------------------------------------------------- flash
#define PICO_BOOT_STAGE2_CHOOSE_W25Q080 1   // the W25Q128JV takes the same commands

#ifndef PICO_FLASH_SPI_CLKDIV
#define PICO_FLASH_SPI_CLKDIV 2
#endif

pico_board_cmake_set_default(PICO_FLASH_SIZE_BYTES, (16 * 1024 * 1024))
#ifndef PICO_FLASH_SIZE_BYTES
#define PICO_FLASH_SIZE_BYTES (16 * 1024 * 1024)
#endif

// ---------------------------------------------------------------- crystal
// Raspberry Pi's advice for the ABM8-272-T3: give it more time to start.
#ifndef PICO_XOSC_STARTUP_DELAY_MULTIPLIER
#define PICO_XOSC_STARTUP_DELAY_MULTIPLIER 64
#endif

// The RP2040 here is B2 or newer
#ifndef PICO_RP2040_B0_SUPPORTED
#define PICO_RP2040_B0_SUPPORTED 0
#endif

#endif
