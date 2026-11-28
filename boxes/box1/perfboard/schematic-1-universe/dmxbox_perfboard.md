# dmxbox on a perfboard: the lists

Written by `boxes/box1/perfboard/schematic-1-universe/perfboard.py`. Do not change it by hand: change the plan in the script and run it again.

A hole is written with a dash: `N-3` is column N, row 3. (Without the dash, names like C1, R3 or U4
would be both a hole and a part.)

## Parts and their holes

| Part | What | Holes |
|---|---|---|
| U1 | Raspberry Pi Pico on two 20-way socket strips | pins 1 to 20 in A-1 to A-20, pins 40 to 21 in H-1 to H-20 |
| U3 | 6N137 in a DIP-8 socket, notch towards row 1 | pins 1 to 4 in N-2 to N-5, pins 8 to 5 in Q-2 to Q-5 |
| U5 | SN75176BP or MAX485 in a DIP-8 socket, notch towards row 1 | pins 1 to 4 in U-2 to U-5, pins 8 to 5 in X-2 to X-5 |
| U4 | RKK-0505S/H in a 7-way socket strip | pin 1 in M-8, pin 2 in N-8, pin 5 in Q-8, pin 7 in S-8; the strip's other three contacts are in O-8, P-8, R-8 and join nothing |
| R2 | 270 Ω resistor | V-12 (+5V_ISO) and R-12 (GND2) |
| R3 | 180 Ω resistor | L-7 (3V3) and L-3 (LED_A) |
| R4 | 1 kΩ resistor | R-1 (+5V_ISO) and R-5 (DI) |
| C1 | 100 nF ceramic capacitor | T-4 (+5V_ISO) and T-6 (GND2) |
| C2 | 100 nF ceramic capacitor | V-7 (+5V_ISO) and X-7 (GND2) |
| C3 | 10 µF electrolytic capacitor | plus (the long leg) in W-11 (+5V_ISO) and X-11 (GND2) |
| C4 | 4.7 µF electrolytic capacitor | plus (the long leg) in M-11 (VSYS) and L-11 (GND) |
| D1 | P6KE6.8CA protection diode | Y-14 (A) and T-14 (GND2) |
| D3 | P6KE6.8CA protection diode | Z-16 (B) and U-16 (GND2) |
| J1 | screw terminal, 3 poles, to the XLR jack | Z-19 = XLR pin 2 (Data −), Z-21 = XLR pin 3 (Data +), Z-23 = XLR pin 1 (common) |
| SW1 | button, blackout | N-29 (BTN1) and I-31 (GND); the other two legs in I-29 and N-31 join nothing |
| SW2 | button, preset 1 | N-23 (BTN2) and I-25 (GND); the other two legs in I-23 and N-25 join nothing |

## Insulated wire

| Wire | From | To | What it does |
|---|---|---|---|
| W1 | B-1 | M-4 | Pico pin 1 (GP0, the DMX data) to U3 pin 3 |

## Tracks on the solder side

Each run goes in straight lines from hole to hole; every pad on the way belongs to it.

| Net | What it is | Runs |
|---|---|---|
| VSYS | 5 V from the USB cable (Pico pin 39) | H-2 → K-2 → K-6 → M-6 → M-11 |
| GND | ground of the Pico and the computer | H-13 → L-13 → L-12; H-18 → J-18 → J-25; I-25 → J-25; I-31 → J-31 → J-25; L-11 → L-12; L-12 → N-12 → N-8 |
| 3V3 | 3.3 V (Pico pin 36) to R3 | H-5 → J-5 → J-7 → L-7 |
| LED_A | R3 to U3 pin 2 | L-3 → N-3 |
| DMX_TX | the DMX data: Pico pin 1 to U3 pin 3 | A-1 → B-1; M-4 → N-4 |
| BTN1 | button SW1, blackout (Pico pin 24) | H-17 → M-17 → M-29 → N-29 |
| BTN2 | button SW2, preset 1 (Pico pin 25) | H-16 → N-16 → N-23 |
| +5V_ISO | isolated 5 V | R-1 → R-2 → Q-2 → Q-3 → U-3; S-8 → V-8; T-4 → U-4 → U-3; U-3 → V-3; V-3 → V-8; V-3 → W-3 → W-2 → X-2; V-8 → V-12 → W-12 → W-11 |
| GND2 | isolated ground, XLR pin 1 | Q-5 → Q-6; Q-6 → Q-12 → T-12 → T-13; Q-6 → T-6; T-13 → T-14 → U-14 → U-23 → Z-23; T-13 → X-13 → X-5 |
| DI | the DMX data: U3 pin 6 to U5 pin 4 | Q-4 → R-4 → R-5 → U-5 |
| A | Data +, XLR pin 3 | X-4 → Y-4 → Y-21 → Z-21 |
| B | Data −, XLR pin 2 | X-3 → Z-3 → Z-19 |
