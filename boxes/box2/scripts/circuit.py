"""The circuit of dmxbox 2: every part and every net. The schematic and the board are made from this list.

dmxbox 2 is a Microchip PIC32MX270F256B on a board of through-hole parts that you solder yourself: one DMX output
with RDM (box 1's DMX side, part for part) and the switch for its number, a through-hole USB-C socket (GCT
USB4085), the PIC's 3.3 V from an MCP1700 and its clock from an 8 MHz crystal. The PIC takes its first firmware
from a programmer (PICkit or MPLAB Snap) on J2.

Who fits each part (PARTS[ref]["by"]):
  user  you solder it (every part: the board comes bare from JLCPCB)
  none  nothing to fit (holes)

Pins: the PIC32MX270F256B's from its datasheet (DS60001168L, table 4, 28-pin SPDIP), as the first dmxbox 2 design
(boxes/box2/perfboard) has them; which pins can carry U1TX, U1RX and U2TX from its tables 11-1 and 11-2. MCP1700 in
TO-92: 1 GND, 2 VIN, 3 VOUT. The rest as on box 1.
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
    # the PIC32, its clock, its reset and its programming header
    "U1": part("dmxbox2:PIC32MX270F256B_SP", "Package_DIP:DIP-28_W7.62mm_Socket", "PIC32MX270F256B-I/SP",
               "microcontroller with USB, DIP-28 in a socket"),
    "Y1": part("Device:Crystal", "Crystal:Crystal_HC49-U_Vertical", "8MHz", "the PIC's clock (HC-49/US)"),
    "C7": C("22p", "crystal (C0G)"),
    "C8": C("22p", "crystal (C0G)"),
    "R3": R("10k", "MCLR pull-up"),
    "R4": R("1k", "MCLR: keeps C9 away from the programmer"),
    "C9": C("100n", "MCLR"),
    "C3": C("100n", "VDD"),
    "C4": C("100n", "AVDD"),
    "C5": C("100n", "VUSB3V3"),
    "C6": C("10u", "VCAP: the PIC's own core supply (ceramic, X5R or X7R)"),
    "J2": part("Connector:Conn_PIC_ICSP_ICD", "Connector_PinHeader_2.54mm:PinHeader_1x06_P2.54mm_Vertical",
               "ICSP", "programmer (PICkit, MPLAB Snap): MCLR, VDD, GND, PGED (also the log), PGEC, free"),
    # USB and 3.3 V
    "J1": part("Connector:USB_C_Receptacle_USB2.0_16P", "Connector_USB:USB_C_Receptacle_GCT_USB4085",
               "USB4085-GF-A", "USB-C socket, through-hole: to the computer"),
    "R1": R("5.1k", "CC1 to ground (the box asks for 5 V)"),
    "R2": R("5.1k", "CC2 to ground"),
    "U2": part("Regulator_Linear:MCP1700x-330xxTO", "Package_TO_SOT_THT:TO-92_Inline", "MCP1700-3302E/TO",
               "3.3 V regulator, 250 mA"),
    "C1": C("10u", "5 V (ceramic)"),
    "C2": C("10u", "3.3 V (ceramic)"),
    # LEDs
    "D4": part("Device:LED_Small", "LED_THT:LED_D3.0mm", "green", "USB: on = computer"),
    "D5": part("Device:LED_Small", "LED_THT:LED_D3.0mm", "yellow", "DMX"),
    "D6": part("Device:LED_Small", "LED_THT:LED_D3.0mm", "red", "RDM"),
    "R21": R("330", "green LED"),
    "R22": R("330", "yellow LED"),
    "R23": R("1.5k", "red LED: on PGEC, so it draws little from the programmer"),
    # the box number
    "SW1": part("Switch:SW_DIP_x08", "Button_Switch_THT:SW_DIP_SPSTx08_Piano_10.8x21.88mm_W7.62mm_P2.54mm",
                "DPL08", "box number 1 to 255 (piano DIP switch with 8 keys at the board edge)"),
    # DMX with RDM, isolated: box 1's
    "U5": part("dmxbox2:DCDC_RKK", "dmxbox2:Converter_DCDC_RECOM_RKK_SIP7", "RKK-0505S/H",
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
    "R27": R("1k", "U7 output pull-up: sends when the PIC is off"),
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


def _usb_c(ref, vbus, cc1, cc2, dp, dm):
    """The 16 pins of a USB-C socket: ground, VBUS, CC, D+ and D-."""
    return {"GND": [f"{ref}.{p}" for p in ("A1", "A12", "B1", "B12", "SH")],
            vbus: [f"{ref}.{p}" for p in ("A4", "A9", "B4", "B9")],
            cc1: [f"{ref}.A5"], cc2: [f"{ref}.B5"],
            dp: [f"{ref}.A6", f"{ref}.B6"], dm: [f"{ref}.A7", f"{ref}.B7"]}


# The PIC's pins by job (the firmware's boxes/box2/firmware/box.h uses the same ports). Every one of its 15 port
# pins is used; the log shares PGED1 with the programmer (it holds the PIC in reset while it writes) and the red
# LED shares PGEC1.
NETS = _merge(
    _usb_c("J1", "+5V", "CC1", "CC2", "USB_DP", "USB_DM"),
    {
        "GND": ["R1.2", "R2.2", "U2.1", "C1.2", "C2.2", "U1.8", "U1.19", "U1.27", "C3.2", "C4.2", "C5.2", "C6.2",
                "C7.2", "C8.2", "C9.2", "J2.3",
                "D4.1", "D5.1", "D6.1",
                "SW1.1", "SW1.2", "SW1.3", "SW1.4", "SW1.5", "SW1.6", "SW1.7", "SW1.8",
                "U5.2", "C26.2", "U8.5", "C32.2"],
        "CC1": ["R1.1"], "CC2": ["R2.1"],
        "+5V": ["U2.2", "C1.1", "U1.15", "U5.1", "C26.1", "U8.8", "U8.7", "C32.1"],
        "+3V3": ["U2.3", "C2.1", "U1.13", "U1.23", "U1.28", "C3.1", "C4.1", "C5.1", "R3.1", "J2.2",
                 "R24.1", "R26.1", "R29.1"],
        "VCAP": ["U1.20", "C6.1"],
        "OSC1": ["U1.9", "Y1.1", "C7.1"],
        "OSC2": ["U1.10", "Y1.2", "C8.1"],
        "MCLR": ["U1.1", "R3.2", "R4.1", "J2.1"],
        "MCLR_C": ["R4.2", "C9.1"],
        "USB_DP": ["U1.21"], "USB_DM": ["U1.22"],
        "PGED_LOG": ["U1.4", "J2.4"],
        "PGEC_LED_RDM": ["U1.5", "J2.5", "R23.1"],
        # LEDs
        "LED_USB": ["U1.17", "R21.1"], "LED_USB_A": ["R21.2", "D4.2"],
        "LED_DMX": ["U1.18", "R22.1"], "LED_DMX_A": ["R22.2", "D5.2"],
        "LED_RDM_A": ["R23.2", "D6.2"],
        # box number: the key between pins k and 17-k joins pin 17-k to ground; the leftmost key adds 1 (box 1's)
        "N1": ["U1.2", "SW1.9"], "N2": ["U1.3", "SW1.10"], "N4": ["U1.6", "SW1.11"], "N8": ["U1.7", "SW1.12"],
        "N16": ["U1.11", "SW1.13"], "N32": ["U1.12", "SW1.14"], "N64": ["U1.14", "SW1.15"],
        "N128": ["U1.16", "SW1.16"],
        # DMX, the PIC's side
        "DMX_TX": ["U1.26", "U6.3"], "DMX_TX_LED": ["R24.2", "U6.2"],
        "DMX_DE": ["U1.25", "U7.3"], "DMX_DE_LED": ["R26.2", "U7.2"],
        "DMX_RX": ["U1.24", "U8.6", "R29.2"],
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
)

# Pins that join nothing (they get a no-connect mark in the schematic)
NO_CONNECT = ["J1.A8", "J1.B8", "J2.6", "U6.1", "U6.4", "U7.1", "U7.4", "U8.1", "U8.4", "J4.4", "J4.5"]

# Nets of the isolated DMX side: they stay right of the isolation gap on the board
ISOLATED = {"+5V_ISO", "GND_ISO", "DMX_DI", "DMX_DE_ISO", "DMX_RO_LED", "DMX_RO", "DMX_A", "DMX_B"}

# The symbols KiCad does not have: the DC-DC module (Recom's RKK-0505S/H pins) and the PIC in its 28-pin DIP
DCDC_NAME = "DCDC_RKK"
DCDC_PINS = [("1", "+VIN", "power_in"), ("2", "-VIN", "power_in"), ("5", "-VO", "passive"),
             ("7", "+VO", "power_out")]
DCDC_DESCRIPTION = "Isolated DC-DC 5 V to 5 V, 1 W, 4 kV, SIP7 with 4 pins: Recom RKK-0505S/H"

PIC_PINS = [
    ("1", "MCLR", "input"), ("2", "RA0", "bidirectional"), ("3", "RA1", "bidirectional"),
    ("4", "RB0/PGED1", "bidirectional"), ("5", "RB1/PGEC1", "bidirectional"), ("6", "RB2", "bidirectional"),
    ("7", "RB3", "bidirectional"), ("8", "VSS", "power_in"), ("9", "OSC1", "passive"), ("10", "OSC2", "passive"),
    ("11", "RB4", "bidirectional"), ("12", "RA4", "bidirectional"), ("13", "VDD", "power_in"),
    ("14", "RB5", "bidirectional"), ("15", "VBUS", "input"), ("16", "RB7", "bidirectional"),
    ("17", "RB8", "bidirectional"), ("18", "RB9/U2TX", "bidirectional"), ("19", "VSS", "power_in"),
    ("20", "VCAP", "passive"), ("21", "D+", "bidirectional"), ("22", "D-", "bidirectional"),
    ("23", "VUSB3V3", "power_in"), ("24", "RB13/U1RX", "bidirectional"), ("25", "RB14", "bidirectional"),
    ("26", "RB15/U1TX", "bidirectional"), ("27", "AVSS", "power_in"), ("28", "AVDD", "power_in"),
]
SYMBOLS = {
    "PIC32MX270F256B_SP": {
        "pins": PIC_PINS,
        # by job: the supplies on top, the grounds (one place for the three) and VCAP below, reset, clock,
        # programming, USB and DMX on the left, the switch and the LEDs on the right
        "at": {"1": (-17.78, 15.24, 0), "9": (-17.78, 10.16, 0), "10": (-17.78, 7.62, 0),
               "4": (-17.78, 2.54, 0), "5": (-17.78, 0.0, 0), "21": (-17.78, -5.08, 0), "22": (-17.78, -7.62, 0),
               "24": (-17.78, -12.7, 0), "25": (-17.78, -15.24, 0), "26": (-17.78, -17.78, 0),
               "2": (17.78, 15.24, 180), "3": (17.78, 12.7, 180), "6": (17.78, 10.16, 180), "7": (17.78, 7.62, 180),
               "11": (17.78, 5.08, 180), "12": (17.78, 2.54, 180), "14": (17.78, 0.0, 180), "16": (17.78, -2.54, 180),
               "17": (17.78, -7.62, 180), "18": (17.78, -10.16, 180),
               "13": (-5.08, 22.86, 270), "28": (-2.54, 22.86, 270), "23": (0.0, 22.86, 270), "15": (7.62, 22.86, 270),
               "8": (0.0, -22.86, 90), "19": (0.0, -22.86, 90), "27": (0.0, -22.86, 90), "20": (7.62, -22.86, 90)},
        "body": (-15.24, 20.32, 15.24, -20.32),
        "description": "Microchip PIC32MX270F256B, 32-bit microcontroller with USB, 28-pin SPDIP",
    },
}


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
    pic = {p.split(".")[1] for net in NETS.values() for p in net if p.startswith("U1.")}
    missing = [n for n, _, _ in PIC_PINS if n not in pic]
    if missing:
        problems.append(f"U1 pins in no net: {missing}")
    return problems


if __name__ == "__main__":
    found = check()
    print("\n".join(found) if found else f"{len(PARTS)} parts, {len(NETS)} nets, "
          f"{sum(map(len, NETS.values()))} pins, fine")
