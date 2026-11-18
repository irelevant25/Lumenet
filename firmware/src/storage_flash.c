// Preset bank persistence in the last 12 KB of the Pico's 2 MB flash.
#include <string.h>
#include "hardware/flash.h"
#include "pico/flash.h"
#include "storage.h"

#define BANK_PROG_SIZE   ((sizeof(preset_bank_t) + FLASH_PAGE_SIZE - 1) / FLASH_PAGE_SIZE * FLASH_PAGE_SIZE)
#define BANK_ERASE_SIZE  ((BANK_PROG_SIZE + FLASH_SECTOR_SIZE - 1) / FLASH_SECTOR_SIZE * FLASH_SECTOR_SIZE)
#define BANK_FLASH_OFFSET (PICO_FLASH_SIZE_BYTES - BANK_ERASE_SIZE)

static uint8_t staging[BANK_PROG_SIZE];

void storage_load(preset_bank_t *b) {
    memcpy(b, (const void *)(XIP_BASE + BANK_FLASH_OFFSET), sizeof *b);
    presets_validate(b);   // fresh/garbage flash -> empty bank
}

static void do_write(void *param) {
    (void)param;
    flash_range_erase(BANK_FLASH_OFFSET, BANK_ERASE_SIZE);
    flash_range_program(BANK_FLASH_OFFSET, staging, BANK_PROG_SIZE);
}

bool storage_save(const preset_bank_t *b) {
    memset(staging, 0xFF, sizeof staging);
    memcpy(staging, b, sizeof *b);
    // Pauses the other core (it must have called flash_safe_execute_core_init)
    // and disables interrupts while flash is unavailable. PIO + DMA keep
    // shifting out the current DMX frame from RAM; the next frame simply
    // starts a bit later, which DMX allows (inter-packet mark up to 1 s).
    return flash_safe_execute(do_write, NULL, 500) == PICO_OK;
}
