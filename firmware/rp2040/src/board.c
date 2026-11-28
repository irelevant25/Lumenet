#include "board.h"
#include "pico/stdlib.h"
#include "hardware/adc.h"
#include "config.h"

// A device on OUT: CC between these (its 5.1k against our 33k gives about 0.44 V).
// Below 0.2 V is a cable's own resistor (Ra, 1k), above 1.6 V nothing.
#define CC_DEVICE_MIN_MV  200
#define CC_DEVICE_MAX_MV 1600
#define CC_SAMPLE_MS       10
#define CC_ON_SAMPLES      15     // seen for 150 ms: switch on (USB-C asks for 100 to 200 ms)
#define CC_OFF_SAMPLES      2     // gone for 20 ms: switch off

static bool     g_out_on;
#if BOX_CHAIN
static uint8_t  g_seen, g_missed;
static uint32_t g_next_sample;
#endif

void board_init(void) {
#if BOX_CHAIN
    gpio_init(PIN_OUT_EN);                   // R12 holds it low until here too
    gpio_set_dir(PIN_OUT_EN, GPIO_OUT);
    gpio_put(PIN_OUT_EN, 0);
#endif

    const uint leds[] = {PIN_LED_USB, PIN_LED_DMX, PIN_LED_MIDI};
    for (unsigned i = 0; i < 3; i++) {
        gpio_init(leds[i]);
        gpio_set_dir(leds[i], GPIO_OUT);
        gpio_put(leds[i], 0);
    }

    for (uint i = 0; i < 8; i++) {           // SW1: a switch that is ON pulls its pin to ground
        gpio_init(PIN_BOX_N1 + i);
        gpio_set_dir(PIN_BOX_N1 + i, GPIO_IN);
        gpio_pull_up(PIN_BOX_N1 + i);
    }

    adc_init();
#if BOX_CHAIN
    adc_gpio_init(PIN_CC1);
    adc_gpio_init(PIN_CC2);
#endif
    adc_gpio_init(PIN_VSENSE);
}

uint8_t board_box_number(void) {
    sleep_us(200);                           // the pull-ups need a moment
    uint8_t n = 0;
    for (uint i = 0; i < 8; i++)
        if (!gpio_get(PIN_BOX_N1 + i)) n |= (uint8_t)(1u << i);
    return n;
}

static uint16_t read_mv(uint pin) {
    adc_select_input(pin - 26);              // GPIO 26 to 29 are ADC inputs 0 to 3
    return (uint16_t)((adc_read() * 3300u) / 4095u);
}

uint16_t board_supply_mv(void) {
    return (uint16_t)(read_mv(PIN_VSENSE) * VSENSE_DIVIDER);   // box.h: how much the board divides it
}

#if BOX_CHAIN
static bool device_on(uint16_t mv) { return mv >= CC_DEVICE_MIN_MV && mv <= CC_DEVICE_MAX_MV; }

void board_poll(uint32_t now_ms) {
    if ((int32_t)(now_ms - g_next_sample) < 0) return;
    g_next_sample = now_ms + CC_SAMPLE_MS;
    bool device = device_on(read_mv(PIN_CC1)) || device_on(read_mv(PIN_CC2));
    if (device) {
        g_missed = 0;
        if (g_seen < CC_ON_SAMPLES) g_seen++;
    } else {
        g_seen = 0;
        if (g_missed < CC_OFF_SAMPLES) g_missed++;
    }
    bool on = g_out_on ? g_missed < CC_OFF_SAMPLES : g_seen >= CC_ON_SAMPLES;
    if (on != g_out_on) {
        g_out_on = on;
        gpio_put(PIN_OUT_EN, on);
    }
}

#else   // no OUT socket: nothing to switch
void board_poll(uint32_t now_ms) { (void)now_ms; }
#endif

bool board_out_powered(void) { return g_out_on; }

void board_leds(bool usb, bool dmx, bool midi) {
    gpio_put(PIN_LED_USB, usb);
    gpio_put(PIN_LED_DMX, dmx);
    gpio_put(PIN_LED_MIDI, midi);
}
