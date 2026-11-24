# dmxbox_sim: the Pico box (one output); dmxbox_sim_pico2: a Pico box with two outputs (no such
# hardware exists); dmxbox_sim2: the PIC32 box (two outputs); dmxbox_sim2_1: the PIC32 box with one.
COMMON = ../common
CFLAGS ?= -O2 -g
CFLAGS += -std=c11 -Wall -Wextra -Wno-unused-parameter
CORE = $(COMMON)/engine.c $(COMMON)/artnet.c $(COMMON)/ctrl.c $(COMMON)/presets.c
HEADERS = $(wildcard $(COMMON)/*.h)

all: dmxbox_sim dmxbox_sim_pico2 dmxbox_sim2 dmxbox_sim2_1

dmxbox_sim: sim.c $(CORE) $(HEADERS) ../pico/src/config.h
	$(CC) $(CFLAGS) -I../pico/src -I$(COMMON) -o $@ sim.c $(CORE)

dmxbox_sim_pico2: sim.c $(CORE) $(HEADERS) ../pico/src/config.h
	$(CC) $(CFLAGS) -DDMX_OUTPUTS=2 -I../pico/src -I$(COMMON) -o $@ sim.c $(CORE)

dmxbox_sim2: sim.c $(CORE) $(HEADERS) ../pic32/src/config.h
	$(CC) $(CFLAGS) -I../pic32/src -I$(COMMON) -o $@ sim.c $(CORE)

dmxbox_sim2_1: sim.c $(CORE) $(HEADERS) ../pic32/src/config.h
	$(CC) $(CFLAGS) -DDMX_OUTPUTS=1 -I../pic32/src -I$(COMMON) -o $@ sim.c $(CORE)

clean:
	rm -f dmxbox_sim dmxbox_sim_pico2 dmxbox_sim2 dmxbox_sim2_1 presets.bin

.PHONY: all clean
