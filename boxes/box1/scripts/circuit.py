"""The circuit of dmxbox 1: every part and every net. The schematic and the board are made from this list.

dmxbox 1 is a Raspberry Pi Pico on a board of through-hole parts that you solder yourself: one DMX output with RDM
(box 4's DMX side, in through-hole parts) and the switch for its number. The Pico's own micro-USB socket is the
box's USB socket, and the Pico's own regulator makes the 3.3 V. The parts that do what box 4's do keep box 4's
names (U6 to U9, R24 to R32 ...).

Who fits each part (PARTS[ref]["by"]):
  user  you solder it (every part: the board comes bare from JLCPCB)
  none  nothing to fit (holes)

Pins: the Pico's from KiCad's symbol (MCU_Module:RaspberryPi_Pico), the 6N137 and the MAX485 as on box 4, the
Recom RKK-0505S/H from its datasheet (1 +Vin, 2 -Vin, 5 -Vout, 7 +Vout; positions 3, 4 and 6 have no pin).
"""

RES = "Resistor_THT:R_Axial_DIN0207_L6.3mm_D2.5mm_P7.62mm_Horizontal"
CER = "Capacitor_THT:C_Disc_D5.0mm_W2.5mm_P5.00mm"
ELKO = "Capacitor_THT:CP_Radial_D5.0mm_P2.00mm"
DIP8 = "Package_DIP:DIP-8_W7.62mm_Socket"


def R(value, desc):
    return {"sym": "Device:R_Small", "fp": RES, "value": value, "lcsc": "", "by": "user", "desc": desc}


def C(value, desc, fp=CER, sym="Device:C_Small"):
    return {"sym": sym, "fp": fp, "value": value, "lcsc": "", "by": "user", "desc": desc}


def part(sym, fp, value, desc, by="user"):
    return {"sym": sym, "fp": fp, "value": value, "lcsc": "", "by": by, "desc": desc}


PARTS = {
    # the Pico, in two 1 x 20 socket strips
    "U1": part("MCU_Module:RaspberryPi_Pico", "Module:RaspberryPi_Pico_Common_THT", "Raspberry Pi Pico",
               "Raspberry Pi Pico in two 1 x 20 socket strips: its micro-USB is the box's USB socket"),
    "J7": part("Connector_Generic:Conn_01x03", "Connector_PinHeader_2.54mm:PinHeader_1x03_P2.54mm_Vertical",
               "SERVICE", "service pins: GND, RUN, LOG"),
    # LEDs
    "D4": part("Device:LED_Small", "LED_THT:LED_D3.0mm", "green", "USB: on = computer"),
    "D5": part("Device:LED_Small", "LED_THT:LED_D3.0mm", "yellow", "DMX"),
    "D6": part("Device:LED_Small", "LED_THT:LED_D3.0mm", "red", "RDM"),
    "R21": R("100", "green LED"),
    "R22": R("330", "yellow LED"),
    "R23": R("330", "red LED"),
    # the box number
    "SW1": part("Switch:SW_DIP_x08", "Button_Switch_THT:SW_DIP_SPSTx08_Piano_10.8x21.88mm_W7.62mm_P2.54mm",
                "DPL08", "box number 1 to 255 (piano DIP switch with 8 keys at the board edge)"),
    # DMX with RDM, isolated
    "U5": part("dmxbox1:DCDC_RKK", "dmxbox1:Converter_DCDC_RECOM_RKK_SIP7", "RKK-0505S/H",
               "isolated 5 V, 1 W, 4 kV (Recom RKK-0505S/H, SIP7 with 4 pins)"),
    "C26": C("100n", "DC-DC input (the module has its own input filter)"),
    "C29": C("10u", "isolated 5 V", fp=ELKO, sym="Device:C_Polarized_Small"),
    "R33": R("270", "base load of the DC-DC module: its output stays near 5 V when the line rests"),
    "U6": part("Isolator:6N137", DIP8, "6N137", "DMX send"),
    "U7": part("Isolator:6N137", DIP8, "6N137", "DMX direction: send or listen for RDM"),
    "U8": part("Isolator:6N137", DIP8, "6N137", "DMX receive: RDM answers"),
    "R24": R("180", "U6 LED"),
    "R25": R("1k", "U6 output pull-up"),
    "R26": R("180", "U7 LED"),
    "R27": R("1k", "U7 output pull-up: sends when the Pico is off"),
    "R28": R("390", "U8 LED"),
    "R29": R("1k", "U8 output pull-up"),
    "C30": C("100n", "U6"),
    "C31": C("100n", "U7"),
    "C32": C("100n", "U8"),
    "U9": part("Interface_UART:MAX485E", DIP8, "MAX485",
               "RS-485 transceiver (MAX485 or SN75176BP: the same pins)"),
    "C33": C("100n", "U9"),
    "R30": R("470", "RDM line bias: Data+ up"),
    "R31": R("120", "line termination at the box"),
    "R32": R("470", "RDM line bias: Data- down"),
    "D1": part("Device:D_TVS", "Diode_THT:D_DO-15_P10.16mm_Horizontal", "P6KE6.8CA",
               "protection of Data+ (both ways)"),
    "D2": part("Device:D_TVS", "Diode_THT:D_DO-15_P10.16mm_Horizontal", "P6KE6.8CA",
               "protection of Data- (both ways)"),
    "J4": part("Connector_Generic:Conn_01x05", "Connector_PinHeader_2.54mm:PinHeader_1x05_P2.54mm_Vertical",
               "DMX OUT", "pin header to the DMX OUT socket (XLR 5-pin female) in the box wall: pin n = XLR pin n "
               "(1 common, 2 Data-, 3 Data+, 4 and 5 free)"),
    # holes for M3 screws
    "H1": part("Mechanical:MountingHole", "MountingHole:MountingHole_3.2mm_M3", "M3", "screw", by="none"),
    "H2": part("Mechanical:MountingHole", "MountingHole:MountingHole_3.2mm_M3", "M3", "screw", by="none"),
    "H3": part("Mechanical:MountingHole", "MountingHole:MountingHole_3.2mm_M3", "M3", "screw", by="none"),
    "H4": part("Mechanical:MountingHole", "MountingHole:MountingHole_3.2mm_M3", "M3", "screw", by="none"),
}


def _merge(*groups):
    out = {}
    for g in groups:
        for net, pins in g.items():
            out.setdefault(net, []).extend(pins)
    return out


# The Pico's GPIOs by job (GPIO numbers; the firmware's boxes/box1/firmware/box.h uses the same). The supply
# reading needs no pin of the board: the Pico has VSYS / 3 on GPIO 29 itself.
GPIO = {
    1: "LED_USB",
    2: "LED_DMX",
    3: "LED_RDM",
    4: "N1", 5: "N2", 6: "N4", 7: "N8", 8: "N16", 9: "N32", 10: "N64", 11: "N128",   # box number
    12: "LOG_TX",       # UART0 TX: service log, 250000 baud
    13: "DMX_RX",       # UART0 RX: RDM answers
    14: "DMX_TX",       # PIO: DMX and RDM out
    15: "DMX_DE",       # low = listen (RDM), high = send
}
PICO_GPIO_PIN = {0: 1, 1: 2, 2: 4, 3: 5, 4: 6, 5: 7, 6: 9, 7: 10, 8: 11, 9: 12, 10: 14, 11: 15, 12: 16, 13: 17,
                 14: 19, 15: 20, 16: 21, 17: 22, 18: 24, 19: 25, 20: 26, 21: 27, 22: 29, 26: 31, 27: 32, 28: 34}

NETS = _merge(
    {
        "GND": ["U1.3", "U1.8", "U1.13", "U1.18", "U1.23", "U1.28", "U1.38", "J7.1",
                "D4.1", "D5.1", "D6.1",
                "SW1.1", "SW1.2", "SW1.3", "SW1.4", "SW1.5", "SW1.6", "SW1.7", "SW1.8",
                "U5.2", "C26.2", "U8.5", "C32.2"],
        "+5V": ["U1.39", "U5.1", "C26.1", "U8.8", "U8.7", "C32.1"],
        "+3V3": ["U1.36", "R24.1", "R26.1", "R29.1"],
        "RUN": ["U1.30", "J7.2"],
        # LEDs
        "LED_USB": ["R21.1"], "LED_USB_A": ["R21.2", "D4.2"],
        "LED_DMX": ["R22.1"], "LED_DMX_A": ["R22.2", "D5.2"],
        "LED_RDM": ["R23.1"], "LED_RDM_A": ["R23.2", "D6.2"],
        "LOG_TX": ["J7.3"],
        # box number: the key between pins k and 17-k joins pin 17-k to ground. The switch lies with its keys
        # at the board's top edge and its pin 1 at the right, so the leftmost key (pins 8 and 9) adds 1, as the
        # first key from the left does on the other boxes
        "N1": ["SW1.9"], "N2": ["SW1.10"], "N4": ["SW1.11"], "N8": ["SW1.12"],
        "N16": ["SW1.13"], "N32": ["SW1.14"], "N64": ["SW1.15"], "N128": ["SW1.16"],
        # DMX, the Pico's side
        "DMX_TX": ["U6.3"], "DMX_TX_LED": ["R24.2", "U6.2"],
        "DMX_DE": ["U7.3"], "DMX_DE_LED": ["R26.2", "U7.2"],
        "DMX_RX": ["U8.6", "R29.2"],
        # DMX, the isolated side
        "+5V_ISO": ["U5.7", "C29.1", "R33.1", "U6.8", "U6.7", "C30.1", "R25.1", "U7.8", "U7.7", "C31.1", "R27.1",
                    "R28.1", "U9.8", "C33.1", "R30.1"],
        "GND_ISO": ["U5.5", "C29.2", "R33.2", "U6.5", "C30.2", "U7.5", "C31.2", "U9.5", "C33.2", "R32.2",
                    "D1.2", "D2.2", "J4.1"],
        "DMX_DI": ["U6.6", "R25.2", "U9.4"],
        "DMX_DE_ISO": ["U7.6", "R27.2", "U9.2", "U9.3"],
        "DMX_RO_LED": ["R28.2", "U8.2"],
        "DMX_RO": ["U8.3", "U9.1"],
        "DMX_A": ["U9.6", "R30.2", "R31.1", "D1.1", "J4.3"],
        "DMX_B": ["U9.7", "R31.2", "R32.1", "D2.1", "J4.2"],
    },
    # the Pico's GPIOs
    {net: [f"U1.{PICO_GPIO_PIN[g]}"] for g, net in GPIO.items()},
)

# Pins that join nothing (they get a no-connect mark in the schematic)
NO_CONNECT = (
    [f"U1.{PICO_GPIO_PIN[g]}" for g in PICO_GPIO_PIN if g not in GPIO]
    + ["U1.33", "U1.35", "U1.37", "U1.40"]        # AGND (the Pico joins it to GND), ADC_VREF, 3V3_EN, VBUS
    + ["U6.1", "U6.4", "U7.1", "U7.4", "U8.1", "U8.4", "J4.4", "J4.5"]
)

# Nets of the isolated DMX side: they stay right of the isolation gap on the board
ISOLATED = {"+5V_ISO", "GND_ISO", "DMX_DI", "DMX_DE_ISO", "DMX_RO_LED", "DMX_RO", "DMX_A", "DMX_B"}

# The custom symbol of the DC-DC module (not in KiCad's libraries): Recom's RKK-0505S/H pins
DCDC_NAME = "DCDC_RKK"
DCDC_PINS = [("1", "+VIN", "power_in"), ("2", "-VIN", "power_in"), ("5", "-VO", "passive"),
             ("7", "+VO", "power_out")]
DCDC_DESCRIPTION = "Isolated DC-DC 5 V to 5 V, 1 W, 4 kV, SIP7 with 4 pins: Recom RKK-0505S/H"


def check():
    """The list itself: no pin twice, every net has two pins or more, every part is used."""
    problems = []
    seen = {}
    for net, pins in NETS.items():
        for p in pins:
            if p in seen:
                problems.append(f"{p} is in {seen[p]} and in {net}")
            seen[p] = net
        if len(pins) < 2:
            problems.append(f"net {net} has only {pins}")
    for p in NO_CONNECT:
        if p in seen:
            problems.append(f"{p} is marked free but is in {seen[p]}")
    for ref, d in PARTS.items():
        if d["sym"].startswith("Mechanical:"):
            continue
        if not any(p.split(".")[0] == ref for p in seen):
            problems.append(f"{ref} has no pin in any net")
    for net, pins in NETS.items():
        for p in pins:
            if p.split(".")[0] not in PARTS:
                problems.append(f"{p} in {net}: no such part")
    return problems


if __name__ == "__main__":
    found = check()
    print("\n".join(found) if found else f"{len(PARTS)} parts, {len(NETS)} nets, "
          f"{sum(map(len, NETS.values()))} pins, fine")
