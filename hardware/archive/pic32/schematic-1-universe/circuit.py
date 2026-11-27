"""The circuit of dmxbox 2 with one DMX output: its parts and its nets. The same box as the one with two
outputs (../schematic-2-universes), with output 2 left out: U1 pins 16, 17 and 18 stay free.

The schematic, the breadboard plan and the perfboard plan are drawn from this one list, and each
drawing is checked against it before it is written. The pin numbers of the chips were checked in
their datasheets: PIC32MX270F256B in DS60001168L (table 4, 28-pin SPDIP; table 11-2 and 11-1 for
which pins can carry U1TX, U2TX, U1RX and U2RX), MCP1700 in DS20001826E (TO-92: 1 GND, 2 VIN,
3 VOUT), 6N137 and MAX485 as on the Pico box, PC817 (1 anode, 2 cathode, 3 emitter, 4 collector),
Recom RKK (1 +Vin, 2 -Vin, 5 -Vout, 7 +Vout).
"""

# What every part is. ref -> (what, value or type, case)
PARTS = {
    "U1": ("microcontroller", "PIC32MX270F256B-I/SP", "DIP-28, 7.62 mm wide, in a socket"),
    "U2": ("3.3 V regulator", "MCP1700-3302E/TO", "TO-92"),
    "U3": ("isolated DC-DC converter, 5 V to 5 V, 1 W", "RKK-0505S/H", "SIP-7, in a socket strip"),
    "U4": ("optocoupler, output 1 send", "6N137", "DIP-8, in a socket"),
    "U5": ("optocoupler, output 1 receive (RDM)", "6N137", "DIP-8, in a socket"),
    "U6": ("optocoupler, output 1 direction", "PC817", "DIP-4, in a socket"),
    "U7": ("RS-485 transceiver, output 1", "MAX485", "DIP-8, in a socket"),
    "U8": ("optocoupler, output 2 send", "6N137", "DIP-8, in a socket"),
    "U9": ("optocoupler, output 2 receive (RDM)", "6N137", "DIP-8, in a socket"),
    "U10": ("optocoupler, output 2 direction", "PC817", "DIP-4, in a socket"),
    "U11": ("RS-485 transceiver, output 2", "MAX485", "DIP-8, in a socket"),
    "Y1": ("crystal", "8 MHz", "HC-49/US"),
    "J1": ("USB-C socket on a small breakout board", "USB 2.0 Type-C breakout", "6 pins, 2.54 mm"),
    "J2": ("programming header (PICkit or MPLAB Snap)", "pin header 1x6", "2.54 mm"),
    "J3": ("DMX output 1", "XLR 3-pin female, panel", "on wires"),
    "J4": ("DMX output 2", "XLR 3-pin female, panel", "on wires"),
    "SW1": ("box number: switches 1 to 6", "piano DIP switch, 8 positions (DPL08RGT)", "DIP-16"),
    "SW2": ("button: blackout", "tact switch 12 x 12 mm", "4 legs"),
    "SW3": ("button: preset 1", "tact switch 12 x 12 mm", "4 legs"),
    "D1": ("LED: the box's state", "LED 3 mm", "2 legs"),
    "D2": ("TVS diode, output 1 Data +", "P6KE6.8CA", "DO-15"),
    "D3": ("TVS diode, output 1 Data -", "P6KE6.8CA", "DO-15"),
    "D4": ("TVS diode, output 2 Data +", "P6KE6.8CA", "DO-15"),
    "D5": ("TVS diode, output 2 Data -", "P6KE6.8CA", "DO-15"),
    "R1": ("USB-C: CC1 to ground (if the breakout has none)", "5.1 kΩ", "0.25 W"),
    "R2": ("USB-C: CC2 to ground (if the breakout has none)", "5.1 kΩ", "0.25 W"),
    "R3": ("MCLR pull-up", "10 kΩ", "0.25 W"),
    "R4": ("MCLR, keeps C9 away from the programmer", "1 kΩ", "0.25 W"),
    "R5": ("LED", "470 Ω", "0.25 W"),
    "R6": ("output 1: LED of U4", "180 Ω", "0.25 W"),
    "R7": ("output 1: pull-up of U4's output", "1 kΩ", "0.25 W"),
    "R8": ("output 1: LED of U5", "390 Ω", "0.25 W"),
    "R9": ("output 1: pull-up of U5's output", "1 kΩ", "0.25 W"),
    "R10": ("output 1: LED of U6", "470 Ω", "0.25 W"),
    "R11": ("output 1: DE and /RE pull-up (send)", "10 kΩ", "0.25 W"),
    "R12": ("output 1: line bias, Data + up", "560 Ω", "0.25 W"),
    "R13": ("output 1: line termination", "130 Ω", "0.25 W"),
    "R14": ("output 1: line bias, Data - down", "560 Ω", "0.25 W"),
    "R15": ("output 2: LED of U8", "180 Ω", "0.25 W"),
    "R16": ("output 2: pull-up of U8's output", "1 kΩ", "0.25 W"),
    "R17": ("output 2: LED of U9", "390 Ω", "0.25 W"),
    "R18": ("output 2: pull-up of U9's output", "1 kΩ", "0.25 W"),
    "R19": ("output 2: LED of U10", "470 Ω", "0.25 W"),
    "R20": ("output 2: DE and /RE pull-up (send)", "10 kΩ", "0.25 W"),
    "R21": ("output 2: line bias, Data + up", "560 Ω", "0.25 W"),
    "R22": ("output 2: line termination", "130 Ω", "0.25 W"),
    "R23": ("output 2: line bias, Data - down", "560 Ω", "0.25 W"),
    "C1": ("5 V from USB", "10 µF", "ceramic X7R, 16 V or more"),
    "C2": ("3.3 V", "10 µF", "ceramic X7R, 16 V or more"),
    "C3": ("U1 VDD", "100 nF", "ceramic X7R"),
    "C4": ("U1 AVDD", "100 nF", "ceramic X7R"),
    "C5": ("U1 VUSB3V3", "100 nF", "ceramic X7R"),
    "C6": ("U1 VCAP: the chip's own core supply", "10 µF", "ceramic X7R, 16 V or more"),
    "C7": ("crystal", "22 pF", "ceramic C0G"),
    "C8": ("crystal", "22 pF", "ceramic C0G"),
    "C9": ("MCLR", "100 nF", "ceramic X7R"),
    "C10": ("U3 input", "10 µF", "ceramic X7R, 16 V or more"),
    "C11": ("U3 output: isolated 5 V", "10 µF", "ceramic X7R, 16 V or more"),
    "C12": ("U4", "100 nF", "ceramic X7R"),
    "C13": ("U5", "100 nF", "ceramic X7R"),
    "C14": ("U7", "100 nF", "ceramic X7R"),
    "C15": ("U8", "100 nF", "ceramic X7R"),
    "C16": ("U9", "100 nF", "ceramic X7R"),
    "C17": ("U11", "100 nF", "ceramic X7R"),
}

# The two outputs are built alike. Their parts, by job.
OUTPUTS = {
    1: {"send": "U4", "receive": "U5", "direction": "U6", "driver": "U7", "send_led": "R6", "send_up": "R7",
        "receive_led": "R8", "receive_up": "R9", "direction_led": "R10", "direction_up": "R11", "bias_up": "R12",
        "termination": "R13", "bias_down": "R14", "c_send": "C12", "c_receive": "C13", "c_driver": "C14",
        "tvs_a": "D2", "tvs_b": "D3", "xlr": "J3", "tx": "U1.26", "dir": "U1.25", "rx": "U1.24"},
    2: {"send": "U8", "receive": "U9", "direction": "U10", "driver": "U11", "send_led": "R15", "send_up": "R16",
        "receive_led": "R17", "receive_up": "R18", "direction_led": "R19", "direction_up": "R20", "bias_up": "R21",
        "termination": "R22", "bias_down": "R23", "c_send": "C15", "c_receive": "C16", "c_driver": "C17",
        "tvs_a": "D4", "tvs_b": "D5", "xlr": "J4", "tx": "U1.18", "dir": "U1.16", "rx": "U1.17"},
}

# This box has output 1 only: the parts of output 2 are left out, and its three U1 pins stay free.
for _job, _ref in OUTPUTS[2].items():
    if _job not in ("tx", "dir", "rx"):
        del PARTS[_ref]
del OUTPUTS[2]

# The pins of U1 with their names in the 28-pin DIP case (datasheet table 4).
PIC_PINS = {
    1: "MCLR", 2: "RA0", 3: "RA1", 4: "RB0 PGED1", 5: "RB1 PGEC1", 6: "RB2", 7: "RB3", 8: "VSS", 9: "OSC1",
    10: "OSC2", 11: "RB4", 12: "RA4", 13: "VDD", 14: "RB5", 15: "VBUS", 16: "RB7", 17: "RB8 U2RX",
    18: "RB9 U2TX", 19: "VSS", 20: "VCAP", 21: "D+", 22: "D-", 23: "VUSB3V3", 24: "RB13 U1RX",
    25: "RB14", 26: "RB15 U1TX", 27: "AVSS", 28: "AVDD",
}


def _outputs():
    nets = {}
    for n, p in OUTPUTS.items():
        send, receive, direction, driver = p["send"], p["receive"], p["direction"], p["driver"]
        nets.update({
            # the logic side: the PIC's three pins of this output
            f"TX{n}": [p["tx"], f"{send}.3"],
            f"TX{n}_LED": [f"{p['send_led']}.2", f"{send}.2"],
            f"RX{n}": [p["rx"], f"{receive}.6", f"{p['receive_up']}.2"],
            f"DIR{n}": [p["dir"], f"{p['direction_led']}.1"],
            f"DIR{n}_LED": [f"{p['direction_led']}.2", f"{direction}.1"],
            # the isolated side
            f"DI{n}": [f"{send}.6", f"{p['send_up']}.2", f"{driver}.4"],
            f"RO{n}": [f"{driver}.1", f"{receive}.3"],
            f"RO{n}_LED": [f"{p['receive_led']}.2", f"{receive}.2"],
            f"DE{n}": [f"{direction}.4", f"{p['direction_up']}.2", f"{driver}.2", f"{driver}.3"],
            f"A{n}": [f"{driver}.6", f"{p['bias_up']}.2", f"{p['termination']}.1", f"{p['tvs_a']}.1", f"{p['xlr']}.3"],
            f"B{n}": [f"{driver}.7", f"{p['termination']}.2", f"{p['bias_down']}.1", f"{p['tvs_b']}.1", f"{p['xlr']}.2"],
        })
    return nets


def _supply(net):
    """The pins of the four supply nets that the outputs add."""
    pins = []
    for p in OUTPUTS.values():
        send, receive, direction, driver = p["send"], p["receive"], p["direction"], p["driver"]
        pins += {
            "+3V3": [f"{p['send_led']}.1", f"{p['receive_up']}.1"],
            "+5V": [f"{receive}.8", f"{receive}.7", f"{p['c_receive']}.1"],
            "GND": [f"{direction}.2", f"{receive}.5", f"{p['c_receive']}.2"],
            "+5V_ISO": [f"{send}.8", f"{send}.7", f"{p['c_send']}.1", f"{p['send_up']}.1", f"{p['receive_led']}.1",
                        f"{p['direction_up']}.1", f"{driver}.8", f"{p['c_driver']}.1", f"{p['bias_up']}.1"],
            "GND2": [f"{send}.5", f"{p['c_send']}.2", f"{direction}.3", f"{driver}.5", f"{p['c_driver']}.2",
                     f"{p['bias_down']}.2", f"{p['tvs_a']}.2", f"{p['tvs_b']}.2", f"{p['xlr']}.1"],
        }[net]
    return pins


NETS = {
    # USB and the supplies of the logic side
    "+5V": ["J1.VBUS", "C1.1", "U2.2", "U1.15", "U3.1", "C10.1"] + _supply("+5V"),
    "GND": ["J1.GND", "R1.2", "R2.2", "C1.2", "U2.1", "C2.2", "U1.8", "U1.19", "U1.27", "C3.2", "C4.2", "C5.2",
            "C6.2", "C7.2", "C8.2", "C9.2", "J2.3", "U3.2", "C10.2", "D1.K",
            "SW1.1", "SW1.2", "SW1.3", "SW1.4", "SW1.5", "SW1.6"] + _supply("GND"),
    "+3V3": ["U2.3", "C2.1", "U1.13", "U1.23", "U1.28", "C3.1", "C5.1", "C4.1", "R3.1", "J2.2"] + _supply("+3V3"),
    "CC1": ["J1.CC1", "R1.1"],
    "CC2": ["J1.CC2", "R2.1"],
    "D+": ["J1.D+", "U1.21"],
    "D-": ["J1.D-", "U1.22"],
    "VCAP": ["U1.20", "C6.1"],
    "OSC1": ["U1.9", "Y1.1", "C7.1"],
    "OSC2": ["U1.10", "Y1.2", "C8.1"],
    "MCLR_RC": ["R3.2", "R4.1", "C9.1"],
    "MCLR": ["R4.2", "U1.1", "J2.1"],
    "BLACKOUT": ["U1.4", "J2.4"],          # and button SW2, see BUTTONS
    "PRESET": ["U1.5", "J2.5"],            # and button SW3
    "N1": ["U1.2", "SW1.16"],              # switch 1: counts 1
    "N2": ["U1.3", "SW1.15"],              # switch 2: counts 2
    "N4": ["U1.6", "SW1.14"],
    "N8": ["U1.7", "SW1.13"],
    "N16": ["U1.11", "SW1.12"],
    "N32": ["U1.12", "SW1.11"],            # switch 6: counts 32
    "LED": ["U1.14", "R5.1"],
    "LED_A": ["R5.2", "D1.A"],
    # the isolated side
    "+5V_ISO": ["U3.7", "C11.1"] + _supply("+5V_ISO"),
    "GND2": ["U3.5", "C11.2"] + _supply("GND2"),
    **_outputs(),
}

# Each button joins its net to GND while it is pressed.
BUTTONS = {"SW2": "BLACKOUT", "SW3": "PRESET"}

# Pins that are soldered but join nothing.
FREE_PINS = (["U4.1", "U4.4", "U5.1", "U5.4", "J2.6", "U1.16", "U1.17", "U1.18"]
             + [f"SW1.{n}" for n in (7, 8, 9, 10)])

LOGIC_NETS = {"+5V", "GND", "+3V3", "CC1", "CC2", "D+", "D-", "VCAP", "OSC1", "OSC2", "MCLR_RC", "MCLR",
              "BLACKOUT", "PRESET", "N1", "N2", "N4", "N8", "N16", "N32", "LED", "LED_A",
              "TX1", "TX1_LED", "RX1", "DIR1", "DIR1_LED"}
ISOLATED_NETS = set(NETS) - LOGIC_NETS


def net_of(pin):
    return next((net for net, pins in NETS.items() if pin in pins), None)


def check_list():
    """The net list itself: no pin in two nets, every part has its pins somewhere."""
    problems = []
    seen = {}
    for net, pins in NETS.items():
        for pin in pins:
            if pin in seen:
                problems.append(f"{pin} is in {seen[pin]} and in {net}")
            seen[pin] = net
    for ref in PARTS:
        if ref not in BUTTONS and not any(pin.split(".")[0] == ref for pin in seen):
            problems.append(f"{ref} has no pin in any net")
    for pin in FREE_PINS:
        if pin in seen:
            problems.append(f"{pin} is listed as free but is in {seen[pin]}")
    pic = {int(pin.split(".")[1]) for pin in list(seen) + FREE_PINS if pin.startswith("U1.")}
    if pic != set(range(1, 29)):
        problems.append(f"U1 pins not in any net: {sorted(set(range(1, 29)) - pic)}")
    return problems


if __name__ == "__main__":
    found = check_list()
    print("\n".join(found) if found else f"net list: {len(NETS)} nets, {sum(map(len, NETS.values()))} pins, fine")
