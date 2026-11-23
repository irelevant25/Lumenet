cmake_minimum_required(VERSION 3.13)

# Which board to build for: pico (default), pico_w or pico2.
set(PICO_BOARD pico CACHE STRING "Board type")
include(pico_sdk_import.cmake)

project(dmxbox C CXX ASM)
set(CMAKE_C_STANDARD 11)
pico_sdk_init()

# The code both boxes share (engine, protocols, network, USB) is in ../common.
set(COMMON ${CMAKE_CURRENT_LIST_DIR}/../common)

add_executable(dmxbox
    src/main.c
    src/dmx_out.c
    src/storage_flash.c
    ${COMMON}/engine.c
    ${COMMON}/artnet.c
    ${COMMON}/ctrl.c
    ${COMMON}/presets.c
    ${COMMON}/net.c
    ${COMMON}/dhcp_server.c
    ${COMMON}/usb_net.c
    ${COMMON}/usb_descriptors.c
)

pico_generate_pio_header(dmxbox ${CMAKE_CURRENT_LIST_DIR}/src/dmx_tx.pio)
# src/ holds config.h and tusb_config.h, which the shared code and the USB
# library (TinyUSB) look for on the include path.
target_include_directories(dmxbox PRIVATE ${CMAKE_CURRENT_LIST_DIR}/src ${COMMON})
target_compile_options(dmxbox PRIVATE -Wall -Wextra -Wno-unused-parameter)

target_link_libraries(dmxbox
    pico_stdlib
    pico_multicore
    pico_flash
    pico_unique_id
    tinyusb_device      # the USB library; the box is a USB network adapter (common/usb_net.c)
    hardware_pio
    hardware_dma
    hardware_flash
)

# The USB port is the network adapter, so the log text cannot use it.
# It goes to a serial pin instead: UART0, TX = GP16 (Pico pin 21), RX = GP17 (pin 22), 115200 baud.
pico_enable_stdio_usb(dmxbox 0)
pico_enable_stdio_uart(dmxbox 1)
target_compile_definitions(dmxbox PRIVATE
    PICO_DEFAULT_UART=0
    PICO_DEFAULT_UART_TX_PIN=16
    PICO_DEFAULT_UART_RX_PIN=17
)

pico_add_extra_outputs(dmxbox)   # produces dmxbox.uf2
