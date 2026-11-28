"""The circuit of dmxbox 3: every part and every net. The schematic and the board are made from this list.

dmxbox 3 is a USB network adapter with one DMX output (with RDM) and the switch for its number: box 4 without the
hub, the OUT and POWER sockets and MIDI. Its parts keep box 4's names (U1 the RP2040, R14 and R15 the supply
reading ...), so the shared firmware's comments fit both boxes.

Who fits each part (PARTS[ref]["by"]):
  jlc   JLCPCB solders it (surface mount, from their parts library: LCSC number in "lcsc")
  none  nothing to fit (holes)

Pin numbers as in box 4's circuit.py, which was checked against the KiCad symbols and the datasheets.
"""

R0603 = "Resistor_SMD:R_0603_1608Metric"
C0603 = "Capacitor_SMD:C_0603_1608Metric"
C0805 = "Capacitor_SMD:C_0805_2012Metric"
USBC = "Connector_USB:USB_C_Receptacle_HRO_TYPE-C-31-M-12"


def R(value, lcsc, desc, fp=R0603):
    return {"sym": "Device:R_Small", "fp": fp, "value": value, "lcsc": lcsc, "by": "jlc", "desc": desc}


def C(value, lcsc, desc, fp=C0603):
    return {"sym": "Device:C_Small", "fp": fp, "value": value, "lcsc": lcsc, "by": "jlc", "desc": desc}


def part(sym, fp, value, lcsc, by, desc, **more):
    d = {"sym": sym, "fp": fp, "value": value, "lcsc": lcsc, "by": by, "desc": desc}
    d.update(more)
    return d


# LCSC numbers of the parts used more than once
R5K1, R1K, R10K = "C23186", "C21190", "C25804"
C100N, C1U, C4U7, C10U_0603, C10U_0805, C15P = "C14663", "C15849", "C19666", "C19702", "C15850", "C1644"

PARTS = {
    # USB: the computer
    "J1": part("Connector:USB_C_Receptacle_USB2.0_16P", USBC, "USB-C", "C165948", "jlc",
               "USB-C socket: to the computer"),
    "R1": R("5.1k", R5K1, "CC1 to ground (the box asks for 5 V)"),
    "R2": R("5.1k", R5K1, "CC2 to ground"),
    "C1": C("4.7u", C4U7, "5 V at the socket"),
    "C3": C("10u", C10U_0805, "+5V", fp=C0805),
    "C4": C("100n", C100N, "+5V"),
    "R14": R("10k", R10K, "supply voltage to the RP2040, upper half"),
    "R15": R("10k", R10K, "supply voltage to the RP2040, lower half"),
    "C6": C("100n", C100N, "supply voltage reading"),
    # 3.3 V
    "U4": part("Regulator_Linear:XC6206PxxxMR", "Package_TO_SOT_SMD:SOT-23-3", "XC6206P332MR", "C5446", "jlc",
               "3.3 V regulator, 200 mA"),
    "C7": C("1u", C1U, "regulator input"),
    "C8": C("10u", C10U_0603, "+3V3"),
    # the RP2040
    "U1": part("MCU_RaspberryPi:RP2040", "Package_DFN_QFN:QFN-56-1EP_7x7mm_P0.4mm_EP3.2x3.2mm",
               "RP2040", "C2040", "jlc", "microcontroller"),
    "Y1": part("Device:Crystal_GND24", "Crystal:Crystal_SMD_3225-4Pin_3.2x2.5mm", "12MHz ABM8-272-T3",
               "C20625731", "jlc", "RP2040 clock (the crystal Raspberry Pi names)"),
    "C24": C("15p", C15P, "crystal"),
    "C25": C("15p", C15P, "crystal"),
    "R16": R("1k", R1K, "crystal drive (as in Raspberry Pi's minimal design)"),
    "R17": R("10k", R10K, "RUN pull-up"),
    "R18": R("1k", R1K, "BOOT: short the BOOT pad to GND while plugging in, for the USB boot loader"),
    "R19": R("27", "C25190", "USB D-"),
    "R20": R("27", "C25190", "USB D+"),
    "C12": C("100n", C100N, "IOVDD"),
    "C13": C("100n", C100N, "IOVDD"),
    "C14": C("100n", C100N, "IOVDD"),
    "C15": C("100n", C100N, "IOVDD"),
    "C16": C("100n", C100N, "IOVDD"),
    "C17": C("100n", C100N, "IOVDD"),
    "C18": C("100n", C100N, "USB_VDD"),
    "C19": C("100n", C100N, "ADC_AVDD"),
    "C20": C("1u", C1U, "VREG_VIN"),
    "C21": C("1u", C1U, "VREG_VOUT, 1.1 V core"),
    "C22": C("100n", C100N, "DVDD"),
    "C23": C("100n", C100N, "DVDD"),
    "U3": part("Memory_Flash:W25Q128JVS", "Package_SO:SOIC-8_5.3x5.3mm_P1.27mm", "W25Q128JVSIQ", "C97521",
               "jlc", "16 MB flash for the firmware (a JLCPCB basic part)"),
    "C27": C("100n", C100N, "flash"),
    "J7": part("Connector_Generic:Conn_01x06", "dmxbox3:PinSocket_1x06_P2.54mm_Vertical_SMD_BothSides",
               "SERVICE", "C42380984", "jlc", "service pins, an SMD 1 x 6 socket strip: GND, BOOT, RUN, SWCLK, SWDIO, LOG"),
    # LEDs
    "D4": part("Device:LED_Small", "LED_SMD:LED_0805_2012Metric", "green", "C2297", "jlc", "USB: on = computer"),
    "D5": part("Device:LED_Small", "LED_SMD:LED_0805_2012Metric", "yellow", "C2296", "jlc", "DMX"),
    "D6": part("Device:LED_Small", "LED_SMD:LED_0805_2012Metric", "red", "C84256", "jlc", "RDM"),
    "R21": R("100", "C22775", "green LED"),
    "R22": R("330", "C23138", "yellow LED"),
    "R23": R("330", "C23138", "red LED"),
    # the box number
    "SW1": part("Switch:SW_DIP_x08", "dmxbox3:SW_DIP_SPSTx08_Piano_Kangshen_DSPV08LSGET_P2.54mm",
                "DSPV08LSGET", "C3293170", "jlc",
                "box number 1 to 255 (SMD piano DIP switch, kangshen; its keys at the board edge)"),
    # DMX with RDM, isolated
    "U5": part("dmxbox3:DCDC_B05XT", "dmxbox3:Converter_DCDC_Mornsun_B05_XT-1WR3", "B0505XT-1WR3", "C19188495",
               "jlc", "isolated 5 V, 1 W, 1.5 kV, SMD (Mornsun B05_XT-1WR3 package)"),
    "C28": C("10u", C10U_0805, "DC-DC input", fp=C0805),
    "C29": C("10u", C10U_0805, "isolated 5 V", fp=C0805),
    "U6": part("Isolator:6N137", "Package_DIP:SMDIP-8_W9.53mm", "6N137S", "C92651", "jlc",
               "DMX send (Lite-On 6N137S-TA1-L, gull wing)"),
    "U7": part("Isolator:6N137", "Package_DIP:SMDIP-8_W9.53mm", "6N137S", "C92651", "jlc",
               "DMX direction: send or listen for RDM (6N137S)"),
    "U8": part("Isolator:6N137", "Package_DIP:SMDIP-8_W9.53mm", "6N137S", "C92651", "jlc",
               "DMX receive: RDM answers (6N137S)"),
    "R24": R("180", "C22828", "U6 LED"),
    "R25": R("1k", R1K, "U6 output pull-up"),
    "R26": R("180", "C22828", "U7 LED"),
    "R27": R("1k", R1K, "U7 output pull-up: sends when the RP2040 is off"),
    "R28": R("390", "C23151", "U8 LED"),
    "R29": R("1k", R1K, "U8 output pull-up"),
    "C30": C("100n", C100N, "U6"),
    "C31": C("100n", C100N, "U7"),
    "C32": C("100n", C100N, "U8"),
    "U9": part("Interface_UART:MAX485E", "Package_SO:SOIC-8_3.9x4.9mm_P1.27mm", "ST485BDR", "C91197", "jlc",
               "RS-485 transceiver (ST485B: MAX485 pins, 0.3 mA)"),
    "C33": C("100n", C100N, "U9"),
    "R30": R("470", "C23179", "RDM line bias: Data+ up"),
    "R31": R("120", "C17909", "line termination at the box", fp="Resistor_SMD:R_1206_3216Metric"),
    "R32": R("470", "C23179", "RDM line bias: Data- down"),
    "D2": part("Diode:SM712_SOT23", "Package_TO_SOT_SMD:SOT-23", "PSM712", "C32677", "jlc",
               "protection of the DMX line (made for RS-485)"),
    "J4": part("Connector_Generic:Conn_01x05", "dmxbox3:PinHeader_1x05_P2.54mm_Vertical_SMD_BothSides",
               "DMX OUT", "C41417362", "jlc", "SMD pin header to the DMX OUT socket (XLR 5-pin female) in the box "
               "wall: pin n = XLR pin n (1 common, 2 Data-, 3 Data+, 4 and 5 free)"),
    # holes for M3 screws
    "H1": part("Mechanical:MountingHole", "MountingHole:MountingHole_3.2mm_M3", "M3", "", "none", "screw"),
    "H2": part("Mechanical:MountingHole", "MountingHole:MountingHole_3.2mm_M3", "M3", "", "none", "screw"),
    "H3": part("Mechanical:MountingHole", "MountingHole:MountingHole_3.2mm_M3", "M3", "", "none", "screw"),
    "H4": part("Mechanical:MountingHole", "MountingHole:MountingHole_3.2mm_M3", "M3", "", "none", "screw"),
}


def _usb_c(ref, vbus, cc1, cc2, dp=None, dm=None):
    """The 16 pins of a USB-C socket: ground, VBUS, CC; D+ and D- if used."""
    nets = {"GND": [f"{ref}.{p}" for p in ("A1", "A12", "B1", "B12", "SH")],
            vbus: [f"{ref}.{p}" for p in ("A4", "A9", "B4", "B9")],
            cc1: [f"{ref}.A5"], cc2: [f"{ref}.B5"]}
    if dp:
        nets[dp] = [f"{ref}.A6", f"{ref}.B6"]
        nets[dm] = [f"{ref}.A7", f"{ref}.B7"]
    return nets


def _merge(*groups):
    out = {}
    for g in groups:
        for net, pins in g.items():
            out.setdefault(net, []).extend(pins)
    return out


# The RP2040's pins by job (GPIO numbers; the firmware's boxes/box3/firmware/box.h uses the same)
GPIO = {
    1: "LED_USB",
    2: "LED_DMX",
    3: "LED_RDM",
    12: "LOG_TX",       # UART0 TX: service log, 250000 baud
    13: "DMX_RX",       # UART0 RX: RDM answers
    14: "DMX_TX",       # PIO: DMX and RDM out
    15: "DMX_DE",       # low = listen (RDM), high = send
    18: "N1", 19: "N2", 20: "N4", 21: "N8", 22: "N16", 23: "N32", 24: "N64", 25: "N128",   # box number
    28: "VSENSE",       # ADC2: half the supply voltage
}
RP2040_GPIO_PIN = {0: 2, 1: 3, 2: 4, 3: 5, 4: 6, 5: 7, 6: 8, 7: 9, 8: 11, 9: 12, 10: 13, 11: 14, 12: 15, 13: 16,
                   14: 17, 15: 18, 16: 27, 17: 28, 18: 29, 19: 30, 20: 31, 21: 32, 22: 34, 23: 35, 24: 36, 25: 37,
                   26: 38, 27: 39, 28: 40, 29: 41}

NETS = _merge(
    _usb_c("J1", "+5V", "CC1_IN", "CC2_IN", "USB_DP", "USB_DM"),
    {
        "GND": ["R1.2", "R2.2", "C1.2", "C3.2", "C4.2", "R15.2", "C6.2", "U4.1", "C7.2", "C8.2",
                "U1.57", "U1.19", "Y1.2", "Y1.4", "C24.2", "C25.2", "C12.2", "C13.2", "C14.2", "C15.2", "C16.2",
                "C17.2", "C18.2", "C19.2", "C20.2", "C21.2", "C22.2", "C23.2", "U3.4", "C27.2", "J7.1",
                "D4.1", "D5.1", "D6.1",
                "SW1.1", "SW1.2", "SW1.3", "SW1.4", "SW1.5", "SW1.6", "SW1.7", "SW1.8",
                "U5.1", "C28.2", "U8.5", "C32.2"],
        "CC1_IN": ["R1.1"], "CC2_IN": ["R2.1"],
        "+5V": ["C1.1", "C3.1", "C4.1", "R14.1", "U4.3", "C7.1",
                "U5.2", "C28.1", "U8.8", "U8.7", "C32.1"],
        "VSENSE": ["R14.2", "R15.1", "C6.1"],
        "+3V3": ["U4.2", "C8.1",
                 "U1.1", "U1.10", "U1.22", "U1.33", "U1.42", "U1.49", "U1.48", "U1.43", "U1.44",
                 "C12.1", "C13.1", "C14.1", "C15.1", "C16.1", "C17.1", "C18.1", "C19.1", "C20.1",
                 "R17.1", "U3.8", "C27.1",
                 "R24.1", "R26.1", "R29.1"],
        # RP2040
        "+1V1": ["U1.45", "U1.23", "U1.50", "C21.1", "C22.1", "C23.1"],
        "RP_XIN": ["U1.20", "Y1.1", "C24.1"],
        "RP_XOUT": ["U1.21", "R16.1"],
        "RP_XOUT_R": ["R16.2", "Y1.3", "C25.1"],
        "RUN": ["U1.26", "R17.2", "J7.3"],
        "SWCLK": ["U1.24", "J7.4"],
        "SWDIO": ["U1.25", "J7.5"],
        "BOOT": ["R18.2", "J7.2"],
        "QSPI_SS": ["U1.56", "U3.1", "R18.1"],
        "QSPI_SCLK": ["U1.52", "U3.6"],
        "QSPI_SD0": ["U1.53", "U3.5"],
        "QSPI_SD1": ["U1.55", "U3.2"],
        "QSPI_SD2": ["U1.54", "U3.3"],
        "QSPI_SD3": ["U1.51", "U3.7"],
        "USB_DM": ["R19.2"], "USB_DP": ["R20.2"],
        "RP_USB_DM": ["U1.46", "R19.1"],
        "RP_USB_DP": ["U1.47", "R20.1"],
        "LOG_TX": ["J7.6"],
        # LEDs
        "LED_USB": ["R21.1"], "LED_USB_A": ["R21.2", "D4.2"],
        "LED_DMX": ["R22.1"], "LED_DMX_A": ["R22.2", "D5.2"],
        "LED_RDM": ["R23.1"], "LED_RDM_A": ["R23.2", "D6.2"],
        # box number: switch k joins pin 17-k to ground
        "N1": ["SW1.16"], "N2": ["SW1.15"], "N4": ["SW1.14"], "N8": ["SW1.13"],
        "N16": ["SW1.12"], "N32": ["SW1.11"], "N64": ["SW1.10"], "N128": ["SW1.9"],
        # DMX, the RP2040's side
        "DMX_TX": ["U6.3"], "DMX_TX_LED": ["R24.2", "U6.2"],
        "DMX_DE": ["U7.3"], "DMX_DE_LED": ["R26.2", "U7.2"],
        "DMX_RX": ["U8.6", "R29.2"],
        # DMX, the isolated side
        "+5V_ISO": ["U5.5", "C29.1", "U6.8", "U6.7", "C30.1", "R25.1", "U7.8", "U7.7", "C31.1", "R27.1",
                    "R28.1", "U9.8", "C33.1", "R30.1"],
        "GND_ISO": ["U5.4", "C29.2", "U6.5", "C30.2", "U7.5", "C31.2", "U9.5", "C33.2", "R32.2", "D2.3",
                    "J4.1"],
        "DMX_DI": ["U6.6", "R25.2", "U9.4"],
        "DMX_DE_ISO": ["U7.6", "R27.2", "U9.2", "U9.3"],
        "DMX_RO_LED": ["R28.2", "U8.2"],
        "DMX_RO": ["U8.3", "U9.1"],
        "DMX_A": ["U9.6", "R30.2", "R31.1", "D2.1", "J4.3"],
        "DMX_B": ["U9.7", "R31.2", "R32.1", "D2.2", "J4.2"],
    },
    # the RP2040's GPIOs
    {net: [f"U1.{RP2040_GPIO_PIN[g]}"] for g, net in GPIO.items()},
)

# Pins that join nothing (they get a no-connect mark in the schematic)
NO_CONNECT = (
    ["J1.A8", "J1.B8"]
    + [f"U1.{RP2040_GPIO_PIN[g]}" for g in range(30) if g not in GPIO]
    + ["U5.8", "U6.1", "U6.4", "U7.1", "U7.4", "U8.1", "U8.4", "J4.4", "J4.5"]
)

# Nets of the isolated DMX side: they stay right of the isolation gap on the board
ISOLATED = {"+5V_ISO", "GND_ISO", "DMX_DI", "DMX_DE_ISO", "DMX_RO_LED", "DMX_RO", "DMX_A", "DMX_B"}

# The custom symbol of the DC-DC module (not in KiCad's libraries): Mornsun's B05_XT-1WR3 pins
DCDC_PINS = [("1", "GND", "power_in"), ("2", "VIN", "power_in"), ("4", "0V", "passive"),
             ("5", "+VO", "power_out"), ("8", "NC", "no_connect")]


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
