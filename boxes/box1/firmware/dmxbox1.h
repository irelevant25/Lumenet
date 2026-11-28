// The dmxbox 1 board for the Pico SDK (PICO_BOARD=dmxbox1): a Raspberry Pi Pico,
// as the SDK's own boards/pico.h describes it, with the log on GPIO12. The pins
// are in box.h.
#ifndef _BOARDS_DMXBOX1_H
#define _BOARDS_DMXBOX1_H

pico_board_cmake_set(PICO_PLATFORM, rp2040)

// The log goes out of UART0 TX on GPIO12 (the service pins J7). GPIO13, the RX
// of the same UART, takes the RDM answers: the firmware sets it up itself.
#ifndef PICO_DEFAULT_UART
#define PICO_DEFAULT_UART 0
#endif
#ifndef PICO_DEFAULT_UART_TX_PIN
#define PICO_DEFAULT_UART_TX_PIN 12
#endif

// ---------------------------------------------------------------- flash: the Pico's 2 MB W25Q16JV
#define PICO_BOOT_STAGE2_CHOOSE_W25Q080 1

#ifndef PICO_FLASH_SPI_CLKDIV
#define PICO_FLASH_SPI_CLKDIV 2
#endif

pico_board_cmake_set_default(PICO_FLASH_SIZE_BYTES, (2 * 1024 * 1024))
#ifndef PICO_FLASH_SIZE_BYTES
#define PICO_FLASH_SIZE_BYTES (2 * 1024 * 1024)
#endif

// The Pico's own pins the firmware leaves alone: GPIO23 (the regulator's mode),
// GPIO24 (VBUS sense) and GPIO25 (the green LED on the Pico).
#ifndef PICO_SMPS_MODE_PIN
#define PICO_SMPS_MODE_PIN 23
#endif

// The RP2040 on the Pico is B1 or newer
#ifndef PICO_RP2040_B0_SUPPORTED
#define PICO_RP2040_B0_SUPPORTED 0
#endif

#endif
