// The presets in the PIC32's program flash: a block of whole flash pages that
// the firmware keeps for them. Programming the chip writes it empty (0xFF), so
// a new firmware starts without presets.
//
// While a page is erased or a row written, the chip stops for some
// milliseconds: a save takes about a fifth of a second, and the DMX frame on
// its way just ends a little later (DMX allows that).
#include <string.h>
#include <xc.h>
#include "storage.h"

#define PAGE_SIZE  1024u     // erased at once (datasheet 5.0: 1 KB pages, 128-byte rows)
#define ROW_SIZE   128u      // written at once
#define BANK_PAGES ((sizeof(preset_bank_t) + PAGE_SIZE - 1u) / PAGE_SIZE)

#define NVMOP_ROW_PROGRAM  0x3u
#define NVMOP_PAGE_ERASE   0x4u

static const uint8_t flash_bank[BANK_PAGES * PAGE_SIZE] __attribute__((aligned(PAGE_SIZE))) = {
    [0 ... BANK_PAGES * PAGE_SIZE - 1u] = 0xFF,
};

static uint32_t row_buffer[ROW_SIZE / 4u];   // a row is written from RAM

static uint32_t physical(const volatile void *p) { return (uint32_t)p & 0x1FFFFFFFu; }

// The flash as it is now, past every cache: through the uncached address range (KSEG1).
static const volatile uint8_t *flash_now(void) {
    return (const volatile uint8_t *)(physical(flash_bank) | 0xA0000000u);
}

// One erase or write: Microchip's unlock sequence, with no interrupt in between.
static bool nvm(uint32_t operation) {
    unsigned int interrupts = __builtin_get_isr_state();
    __builtin_disable_interrupts();
    NVMCON = _NVMCON_WREN_MASK | operation;
    uint32_t start = _CP0_GET_COUNT();               // the voltage watcher needs 6 us: wait 7
    while (_CP0_GET_COUNT() - start < 7u * 20u) {}   // the core timer counts 20 per microsecond
    NVMKEY = 0xAA996655u;
    NVMKEY = 0x556699AAu;
    NVMCONSET = _NVMCON_WR_MASK;
    while (NVMCON & _NVMCON_WR_MASK) {}
    NVMCONCLR = _NVMCON_WREN_MASK;
    __builtin_set_isr_state(interrupts);
    return (NVMCON & (_NVMCON_WRERR_MASK | _NVMCON_LVDERR_MASK)) == 0;
}

void storage_load(preset_bank_t *b) {
    const volatile uint8_t *from = flash_now();
    uint8_t *to = (uint8_t *)b;
    for (uint32_t i = 0; i < sizeof *b; i++) to[i] = from[i];
    presets_validate(b);   // empty flash or garbage -> an empty bank
}

bool storage_save(const preset_bank_t *b) {
    const uint8_t *data = (const uint8_t *)b;
    for (uint32_t page = 0; page < BANK_PAGES; page++) {
        NVMADDR = physical(flash_bank + page * PAGE_SIZE);
        if (!nvm(NVMOP_PAGE_ERASE)) return false;
    }
    for (uint32_t at = 0; at < sizeof *b; at += ROW_SIZE) {
        uint32_t count = sizeof *b - at < ROW_SIZE ? sizeof *b - at : ROW_SIZE;
        memset(row_buffer, 0xFF, sizeof row_buffer);
        memcpy(row_buffer, data + at, count);
        NVMADDR = physical(flash_bank + at);
        NVMSRCADDR = physical(row_buffer);
        if (!nvm(NVMOP_ROW_PROGRAM)) return false;
    }
    const volatile uint8_t *written = flash_now();   // read it back
    for (uint32_t i = 0; i < sizeof *b; i++)
        if (written[i] != data[i]) return false;
    return true;
}
