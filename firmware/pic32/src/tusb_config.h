// Settings for TinyUSB, the USB library (the copy in the Pico SDK, see build.cmd).
// TinyUSB looks for a file with exactly this name.
//
// The box is one USB network adapter of the kind "CDC-NCM", as the Pico box.
// Windows 11, macOS and Linux bring a driver for it. Nothing else is switched on.
#ifndef TUSB_CONFIG_H_
#define TUSB_CONFIG_H_

#ifdef __cplusplus
extern "C" {
#endif

// ---------------------------------------------------------------- general
#ifndef CFG_TUSB_MCU
#define CFG_TUSB_MCU OPT_MCU_PIC32MX       // TinyUSB's driver for this chip: portable/microchip/pic/dcd_pic.c
#endif

#define CFG_TUSB_OS    OPT_OS_NONE         // no operating system: the main loop calls tud_task()
#define CFG_TUSB_DEBUG 0

#define BOARD_TUD_RHPORT  0                // the PIC32MX270 has one USB port
#define CFG_TUD_ENABLED   1                // it is a USB device (not a host)
#define CFG_TUD_MAX_SPEED OPT_MODE_FULL_SPEED

#define CFG_TUSB_MEM_SECTION
#define CFG_TUSB_MEM_ALIGN __attribute__((aligned(4)))
#define CFG_TUD_ENDPOINT0_SIZE 64

// ---------------------------------------------------------------- which USB functions exist
#define CFG_TUD_NCM       1                // network adapter, "CDC-NCM" flavour
#define CFG_TUD_ECM_RNDIS 0
#define CFG_TUD_CDC       0
#define CFG_TUD_MSC       0
#define CFG_TUD_HID       0
#define CFG_TUD_MIDI      0
#define CFG_TUD_VENDOR    0

// ---------------------------------------------------------------- network adapter buffers
// As on the Pico box (tried with Windows 11 there): a block clearly larger than one frame of 1514 bytes.
#define CFG_TUD_NCM_IN_NTB_MAX_SIZE  3200  // box -> computer
#define CFG_TUD_NCM_OUT_NTB_MAX_SIZE 3200  // computer -> box
#define CFG_TUD_NCM_OUT_NTB_N        1     // blocks kept for receiving
#define CFG_TUD_NCM_IN_NTB_N         2     // two for sending: one on its way, the next one filling
#define CFG_TUD_NCM_OUT_MAX_DATAGRAMS_PER_NTB 6

#ifdef __cplusplus
}
#endif

#endif /* TUSB_CONFIG_H_ */
