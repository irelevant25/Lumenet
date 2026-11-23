// Settings for TinyUSB, the USB library that comes with the Pico SDK.
// TinyUSB looks for a file with exactly this name.
//
// The box is one USB network adapter of the kind "CDC-NCM". Windows 11, macOS
// and Linux bring a driver for it. Nothing else is switched on.
#ifndef TUSB_CONFIG_H_
#define TUSB_CONFIG_H_

#ifdef __cplusplus
extern "C" {
#endif

// ---------------------------------------------------------------- general
// The Pico SDK passes CFG_TUSB_MCU and CFG_TUSB_OS on the compiler command line.
#ifndef CFG_TUSB_MCU
#error CFG_TUSB_MCU must be defined
#endif

#ifndef CFG_TUSB_OS
#define CFG_TUSB_OS OPT_OS_NONE
#endif

#ifndef CFG_TUSB_DEBUG
#define CFG_TUSB_DEBUG 0
#endif

#ifndef BOARD_TUD_RHPORT
#define BOARD_TUD_RHPORT 0                 // the Pico has one USB port: number 0
#endif

#ifndef BOARD_TUD_MAX_SPEED
#define BOARD_TUD_MAX_SPEED OPT_MODE_DEFAULT_SPEED
#endif

#define CFG_TUD_ENABLED   1                // the Pico is a USB device (not a host)
#define CFG_TUD_MAX_SPEED BOARD_TUD_MAX_SPEED

#ifndef CFG_TUSB_MEM_SECTION
#define CFG_TUSB_MEM_SECTION
#endif

#ifndef CFG_TUSB_MEM_ALIGN
#define CFG_TUSB_MEM_ALIGN __attribute__((aligned(4)))
#endif

#ifndef CFG_TUD_ENDPOINT0_SIZE
#define CFG_TUD_ENDPOINT0_SIZE 64
#endif

// ---------------------------------------------------------------- which USB functions exist
#define CFG_TUD_NCM       1                // network adapter, "CDC-NCM" flavour
#define CFG_TUD_ECM_RNDIS 0                // the two older network flavours (they would need more code)
#define CFG_TUD_CDC       0                // no serial port: the log goes to pins GP16/GP17
#define CFG_TUD_MSC       0
#define CFG_TUD_HID       0
#define CFG_TUD_MIDI      0
#define CFG_TUD_VENDOR    0

// ---------------------------------------------------------------- network adapter buffers
// Frames travel in blocks; a block must be clearly larger than one frame of 1514 bytes.
#define CFG_TUD_NCM_IN_NTB_MAX_SIZE  3200  // box -> computer
#define CFG_TUD_NCM_OUT_NTB_MAX_SIZE 3200  // computer -> box
#define CFG_TUD_NCM_OUT_NTB_N        1     // blocks kept for receiving
// Two blocks for sending: while one is on its way to the computer, the next
// answers are collected in the other, so nothing has to wait.
#define CFG_TUD_NCM_IN_NTB_N         2
// Most frames the computer may put into one block (TinyUSB's usual value). The
// box tells the computer this number itself, see the end of usb_net.c.
#define CFG_TUD_NCM_OUT_MAX_DATAGRAMS_PER_NTB 6

#ifdef __cplusplus
}
#endif

#endif /* TUSB_CONFIG_H_ */
