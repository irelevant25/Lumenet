cmake_minimum_required(VERSION 3.13)

# The RP2040 firmware of the dmxboxes 1, 3 and 4. BOX says which one (build.cmd BOX): its settings are in
# boxes/boxN/firmware, box.h (name, what it has, pins) and dmxboxN.h, the board file for the Pico SDK.
set(BOX 4 CACHE STRING "Which box: 1, 3 or 4")
get_filename_component(BOX_DIR ${CMAKE_CURRENT_LIST_DIR}/../../boxes/box${BOX}/firmware ABSOLUTE)
if(NOT EXISTS ${BOX_DIR}/box.h OR NOT EXISTS ${BOX_DIR}/dmxbox${BOX}.h)
    message(FATAL_ERROR "No RP2040 box ${BOX}: box.h and dmxbox${BOX}.h are missing in ${BOX_DIR}")
endif()
set(PICO_BOARD dmxbox${BOX})
set(PICO_BOARD_HEADER_DIRS ${BOX_DIR})
include(pico_sdk_import.cmake)

project(dmxbox C CXX ASM)
set(CMAKE_C_STANDARD 11)
pico_sdk_init()

# The code the box shares with the simulator and the tests (engine, protocols,
# RDM, MIDI bytes, network, USB) is in firmware/common.
get_filename_component(COMMON ${CMAKE_CURRENT_LIST_DIR}/../common ABSOLUTE)

add_executable(dmxbox
    src/main.c
    src/board.c
    src/dmx_line.c
    src/midi.c
    src/dmx_in_rx.c
    src/storage_flash.c
    ${COMMON}/engine.c
    ${COMMON}/artnet.c
    ${COMMON}/ctrl.c
    ${COMMON}/rdm.c
    ${COMMON}/dmx_in.c
    ${COMMON}/presets.c
    ${COMMON}/midi_parse.c
    ${COMMON}/net.c
    ${COMMON}/dhcp_server.c
    ${COMMON}/usb_net.c
    ${COMMON}/usb_descriptors.c
)

pico_generate_pio_header(dmxbox ${CMAKE_CURRENT_LIST_DIR}/src/dmx_tx.pio)
pico_generate_pio_header(dmxbox ${CMAKE_CURRENT_LIST_DIR}/src/dmx_rx.pio)
# box.h (in the box's folder), config.h (in common/) and tusb_config.h (in
# src/): the shared code and the USB library (TinyUSB) look for them on the
# include path.
target_include_directories(dmxbox PRIVATE ${BOX_DIR} ${CMAKE_CURRENT_LIST_DIR}/src ${COMMON})
target_compile_options(dmxbox PRIVATE -Wall -Wextra -Wno-unused-parameter)

target_link_libraries(dmxbox
    pico_stdlib
    pico_multicore
    pico_flash
    pico_unique_id
    pico_bootrom
    tinyusb_device      # the USB library: network adapter (common/usb_net.c) and MIDI (src/midi.c)
    hardware_pio
    hardware_irq
    hardware_dma
    hardware_flash
    hardware_uart
    hardware_adc
)

# The USB port is the network adapter and the MIDI port, so the log text cannot
# use it. It goes out of UART0 TX (PIN_LOG_TX of box.h) at 250000 baud, the
# speed of the RDM answers that come in on the same UART (main.c sets it up).
pico_enable_stdio_usb(dmxbox 0)
pico_enable_stdio_uart(dmxbox 1)

pico_add_extra_outputs(dmxbox)   # produces dmxbox.uf2
