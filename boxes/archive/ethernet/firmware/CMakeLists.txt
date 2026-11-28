cmake_minimum_required(VERSION 3.13)

set(PICO_BOARD pico CACHE STRING "Board type")
include(pico_sdk_import.cmake)

project(dmxbox C CXX ASM)
set(CMAKE_C_STANDARD 11)
pico_sdk_init()

add_executable(dmxbox
    src/main.c
    src/engine.c
    src/artnet.c
    src/ctrl.c
    src/presets.c
    src/enc28j60.c
    src/net.c
    src/dmx_out.c
    src/storage_flash.c
)

pico_generate_pio_header(dmxbox ${CMAKE_CURRENT_LIST_DIR}/src/dmx_tx.pio)
target_include_directories(dmxbox PRIVATE ${CMAKE_CURRENT_LIST_DIR}/src)
target_compile_options(dmxbox PRIVATE -Wall -Wextra -Wno-unused-parameter)

target_link_libraries(dmxbox
    pico_stdlib
    pico_multicore
    pico_flash
    pico_unique_id
    hardware_spi
    hardware_pio
    hardware_dma
    hardware_flash
)

# Log output over the Pico's USB port (115200, any terminal).
pico_enable_stdio_usb(dmxbox 1)
pico_enable_stdio_uart(dmxbox 0)
pico_add_extra_outputs(dmxbox)   # produces dmxbox.uf2
