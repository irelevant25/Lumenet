SRC_DIR = ../firmware/src
CFLAGS ?= -O2 -g
CFLAGS += -std=c11 -Wall -Wextra -Wno-unused-parameter -I$(SRC_DIR)
CORE = $(SRC_DIR)/engine.c $(SRC_DIR)/artnet.c $(SRC_DIR)/ctrl.c $(SRC_DIR)/presets.c

dmxbox_sim: sim.c $(CORE) $(wildcard $(SRC_DIR)/*.h)
	$(CC) $(CFLAGS) -o $@ sim.c $(CORE)

clean:
	rm -f dmxbox_sim presets.bin

.PHONY: clean
