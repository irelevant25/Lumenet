cmake_minimum_required(VERSION 3.13)

# The dmxbox 3 board: src/boards/dmxbox3.h (16 MB flash, the crystal of the minimal design).
set(PICO_BOARD dmxbox3 CACHE STRING "Board type")
set(PICO_BOARD_HEADER_DIRS ${CMAKE_CURRENT_LIST_DIR}/src/boards)
include(pico_sdk_import.cmake)

project(dmxbox C CXX ASM)
set(CMAKE_C_STANDARD 11)
pico_sdk_init()

# The code the box shares with the simulator and the tests (engine, protocols,
# RDM, MIDI bytes, network, USB) is in common/.
set(COMMON ${CMAKE_CURRENT_LIST_DIR}/common)

add_executable(dmxbox
    src/main.c
    src/board.c
    src/dmx_line.c
    src/midi.c
    src/storage_flash.c
    ${COMMON}/engine.c
    ${COMMON}/artnet.c
    ${COMMON}/ctrl.c
    ${COMMON}/rdm.c
    ${COMMON}/presets.c
    ${COMMON}/midi_parse.c
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
    pico_bootrom
    tinyusb_device      # the USB library: network adapter (common/usb_net.c) and MIDI (src/midi.c)
    hardware_pio
    hardware_dma
    hardware_flash
    hardware_uart
    hardware_adc
)

# The USB port is the network adapter and the MIDI port, so the log text cannot
# use it. It goes out of UART0 TX on GPIO12 (the service pads J7) at 250000 baud,
# the speed of the RDM answers that come in on the same UART (main.c sets it up).
pico_enable_stdio_usb(dmxbox 0)
pico_enable_stdio_uart(dmxbox 1)

pico_add_extra_outputs(dmxbox)   # produces dmxbox.uf2
