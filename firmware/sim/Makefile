# dmxbox_sim: the Pico box (one output); dmxbox_sim2: the PIC32 box (two outputs).
COMMON = ../common
CFLAGS ?= -O2 -g
CFLAGS += -std=c11 -Wall -Wextra -Wno-unused-parameter
CORE = $(COMMON)/engine.c $(COMMON)/artnet.c $(COMMON)/ctrl.c $(COMMON)/presets.c

all: dmxbox_sim dmxbox_sim2

dmxbox_sim: sim.c $(CORE) $(wildcard $(COMMON)/*.h) ../pico/src/config.h
	$(CC) $(CFLAGS) -I../pico/src -I$(COMMON) -o $@ sim.c $(CORE)

dmxbox_sim2: sim.c $(CORE) $(wildcard $(COMMON)/*.h) ../pic32/src/config.h
	$(CC) $(CFLAGS) -I../pic32/src -I$(COMMON) -o $@ sim.c $(CORE)

clean:
	rm -f dmxbox_sim dmxbox_sim2 presets.bin

.PHONY: all clean
