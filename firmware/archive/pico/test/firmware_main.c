// The real main.c of the firmware, compiled for the PC test.
//
// Its main() gets another name here, because the test program has a main() of
// its own. The Pico SDK headers it includes are the stand-ins in fake_pico/.
#define main firmware_main
#include "../src/main.c"
