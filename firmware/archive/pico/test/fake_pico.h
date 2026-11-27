// The test's remote control for the pretend hardware in fake_pico.c.
//
// fake_pico.c plays three roles: the Pico itself (clock, pins, flash, serial
// number), the DMX output of core 1, and TinyUSB together with the computer at
// the other end of the USB cable. Its USB part is a model of TinyUSB's NCM
// driver, written from the source of TinyUSB 0.18.0 (src/class/net/ncm_device.c).
#pragma once
#include <stdbool.h>
#include <stdint.h>

// ---------------------------------------------------------------- a scenario
// A scenario is a list of things that happen to the box, each at a set time
// (milliseconds after power-on). Add them in order of time.
void fake_reset(bool keep_flash);            // start a new scenario; keep_flash: the saved presets survive
void fake_at(uint32_t ms);                   // what is added next happens at this time
void fake_connect(void);                     // a computer sets the adapter up and switches it on
void fake_disconnect(void);                  // cable pulled
void fake_suspend(bool asleep);              // the computer goes to sleep / wakes up
void fake_host_fetch(bool on);               // the computer stops / resumes fetching what the box sends
void fake_frame(const uint8_t *frame, int len);   // one Ethernet frame from the computer, in a USB transfer of its own
void fake_block_begin(void);                 // the frames added until fake_block_end() arrive in ONE USB transfer
void fake_block_end(void);
void fake_button(unsigned pin, bool pressed);
void fake_sample_dmx(int slot);              // remember what the DMX output sends at that moment (slot 0..7)

// ---------------------------------------------------------------- running
void     fake_run_firmware(uint32_t until_ms);   // power on: runs the real main() of the firmware until that time
void     fake_time_set(uint32_t ms);             // for tests that call usb_net_* themselves
uint32_t fake_now(void);

// ---------------------------------------------------------------- what came out
int            fake_host_count(void);            // frames the computer has received
const uint8_t *fake_host_frame(int i, int *len);
uint32_t       fake_host_frame_ms(int i);        // when frame i arrived
void           fake_host_clear(void);
int            fake_host_waiting(void);          // USB transfers the box has not taken yet
bool           fake_led(uint32_t ms);            // state of the on-board LED at that time
const uint8_t *fake_dmx(int slot, int *len);     // a DMX packet remembered by fake_sample_dmx()
const char    *fake_log(void);                   // everything the firmware printed
int            fake_saves(void);                 // how often the presets were written to flash
bool           fake_pin_is_output(unsigned pin);
bool           fake_pin_has_pullup(unsigned pin);
int            fake_usb_starts(void);            // how often tud_init() was called
int            fake_dmx_pin(void);               // pin given to dmx_out_init()
bool           fake_core1_flash_ready(void);     // core 1 called flash_safe_execute_core_init()
const uint8_t *fake_control_reply(int *len);     // what the firmware last handed to tud_control_xfer()
int            fake_library_control_requests(void);   // how many requests the firmware handed on to TinyUSB's own driver
int            fake_violations(void);            // uses of TinyUSB against its rules
const char    *fake_last_violation(void);
