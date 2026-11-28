# dmxbox_sim: dmxbox 4 (one output, lights with RDM on its line); dmxbox_sim2: the same with two outputs
# (no such box: for the tests).
COMMON = ../common
CFLAGS ?= -O2 -g
BOX = ../../boxes/box4/firmware
CFLAGS += -std=c11 -Wall -Wextra -Wno-unused-parameter -I$(BOX) -I$(COMMON)
CORE = sim.c rdm_lights.c $(COMMON)/engine.c $(COMMON)/artnet.c $(COMMON)/ctrl.c $(COMMON)/presets.c $(COMMON)/rdm.c $(COMMON)/dmx_in.c
HEADERS = $(wildcard $(COMMON)/*.h) rdm_lights.h $(BOX)/box.h

all: dmxbox_sim dmxbox_sim2

dmxbox_sim: $(CORE) $(HEADERS)
	$(CC) $(CFLAGS) -o $@ $(CORE)

dmxbox_sim2: $(CORE) $(HEADERS)
	$(CC) $(CFLAGS) -DDMX_OUTPUTS=2 -o $@ $(CORE)

clean:
	rm -f dmxbox_sim dmxbox_sim2 presets.bin

.PHONY: all clean
