"""Plans dmxbox 2 on two breadboards: where every part goes and which wires join what.

Run from the repository root:

    uv run --no-project --python 3.13 --with resvg-py --with pillow python boxes/box2/perfboard/schematic-2-universes/breadboard.py

It writes dmxbox2_breadboard.svg/.png (the two boards), dmxbox2_breadboard_wires.svg/.png (the
list of wires), dmxbox2_breadboard.pdf (both) and dmxbox2_breadboard.md (the lists in text) next
to this file.

Two full-size boards of 830 holes (rows 1 to 63) lie one above the other, row 1 at the left:
board B (the two DMX outputs) at the top, board A (the microcontroller) under it, so that the
bottom power lines of B lie next to the top power lines of A.

The parts are placed by hand (below). The wires are found by the script: for every net of
circuit.py it joins the contacts that hold the net's legs, the shortest way, with a jumper to a
power line where the net has one on that board. Then a separate check starts again from the
holes and stops the script with a message if anything is wrong: every leg and wire end in a hole
of its own, none under a part, the nets exactly as in circuit.py (whichever way the buttons are
joined inside), and on board B the logic side and the isolated side apart.
"""
from __future__ import annotations

import math
import sys
from pathlib import Path

from circuit import BUTTONS, ISOLATED_NETS, NETS, PARTS, check_list, net_of
from svg import GREY, INK, Svg, esc, to_png_and_pdf

HERE = Path(__file__).resolve().parent
ROWS = 63
RAIL_ROWS = [row for row in range(3, 62) if (row - 3) % 6 != 5]   # power-line holes come in groups of five
LETTER_Y = {"j": 4, "i": 5, "h": 6, "g": 7, "f": 8, "e": 11, "d": 12, "c": 13, "b": 14, "a": 15}
RAIL_Y = {"T+": 0, "T-": 1, "B+": 18, "B-": 19}

# What each power line carries. On board B the top lines are the isolated side.
RAIL_NET = {
    ("A", "T+"): "+5V", ("A", "T-"): "GND", ("A", "B+"): "+3V3", ("A", "B-"): "GND",
    ("B", "T+"): "+5V_ISO", ("B", "T-"): "GND2", ("B", "B+"): "+5V", ("B", "B-"): "GND",
}
# Board B: the isolated side is the top half with its power lines, and the bottom half from this
# row on (the converter's output pins and the two line drivers). Its bottom power lines are only
# used left of this row.
ISO_FROM = 32


# =====================================================================================
# Holes and contacts
# =====================================================================================
def parse(hole):
    """'B:f12' -> ('B', 'f', 12); 'A:T+30' -> ('A', 'T+', 30)."""
    board, rest = hole.split(":")
    if rest[:2] in RAIL_Y:
        return board, rest[:2], int(rest[2:])
    return board, rest[0], int(rest[1:])


def strip_of(hole):
    """The contact a hole belongs to: a whole power line, or five holes in one row on one side of the gap."""
    board, where, row = parse(hole)
    if where in RAIL_Y:
        return (board, where)
    return (board, "top" if where in "fghij" else "bottom", row)


def holes_of(strip):
    if len(strip) == 2:
        # board B's bottom lines are the logic side: only left of the isolated part
        rows = [row for row in RAIL_ROWS if not (strip == ("B", "B+") or strip == ("B", "B-")) or row < ISO_FROM - 1]
        return [f"{strip[0]}:{strip[1]}{row}" for row in rows]
    order = "fghij" if strip[1] == "top" else "edcba"
    return [f"{strip[0]}:{letter}{strip[2]}" for letter in order]


def isolated(hole):
    """True for a hole of the isolated side (board B only)."""
    board, where, row = parse(hole)
    if board != "B":
        return False
    if where in ("T+", "T-") or where in "fghij":
        return True
    if where in ("B+", "B-"):
        return False                     # (and used left of the isolated part only, see holes_of)
    return row >= ISO_FROM


def position(hole):
    """(board, row, height in hole units) of a hole, for distances and drawing."""
    board, where, row = parse(hole)
    return board, row, RAIL_Y[where] if where in RAIL_Y else LETTER_Y[where]


# =====================================================================================
# Where the parts go
# =====================================================================================
LEGS = {}


def dip(ref, board, row, pins, turned=False):
    """A DIP across the middle gap. Notch to the left: pins 1 .. n/2 along e from `row` on, the
    others back along f. Turned round (notch to the right): pins 1 .. n/2 along f from the right
    end leftwards, the others along e from the left."""
    half = pins // 2
    for k in range(1, half + 1):
        if not turned:
            LEGS[f"{ref}.{k}"] = f"{board}:e{row + k - 1}"
            LEGS[f"{ref}.{pins + 1 - k}"] = f"{board}:f{row + k - 1}"
        else:
            LEGS[f"{ref}.{k}"] = f"{board}:f{row + half - k}"
            LEGS[f"{ref}.{half + k}"] = f"{board}:e{row + k - 1}"


def in_line(ref, board, letter, row, pins):
    for i, pin in enumerate(pins):
        if pin is not None:
            LEGS[f"{ref}.{pin}"] = f"{board}:{letter}{row + i}"


def part(ref, first, second):
    """A part with two legs: leg 1 (or A, +) in `first`, leg 2 (or K, -) in `second`."""
    names = ("A", "K") if ref == "D1" else ("1", "2")
    LEGS[f"{ref}.{names[0]}"], LEGS[f"{ref}.{names[1]}"] = first, second


def button(ref, board, row):
    """A 12 x 12 mm button across the gap: legs in d and g of `row` and of `row` + 2. Its net goes
    to one corner and GND to the opposite one: that works however it joins its legs inside."""
    LEGS[f"{ref}.net"] = f"{board}:d{row}"
    LEGS[f"{ref}.a"] = f"{board}:g{row}"
    LEGS[f"{ref}.b"] = f"{board}:d{row + 2}"
    LEGS[f"{ref}.gnd"] = f"{board}:g{row + 2}"


# ---- board A: the microcontroller and the logic side
dip("SW1", "A", 10, 16, turned=True)                                    # turned: GND side up, switch 1 at the right
part("C2", "A:B+5", "A:B-5")                                            # 10 µF on the 3.3 V
in_line("J2", "A", "a", 20, ["1", "2", "3", "4", "5", "6"])             # programming header, near pins 1, 4, 5
part("R3", "A:B+27", "A:c27")                                           # MCLR: pull-up ...
part("C9", "A:b27", "A:B-28")                                           # ... capacitor ...
part("R4", "A:d27", "A:d30")                                            # ... and to pin 1
dip("U1", "A", 30, 28)                                                  # pins 1-14 along e30-e43, 28-15 along f30-f43
part("C4", "A:g30", "A:g31")                                            # AVDD - AVSS
part("R9", "A:h30", "A:h34")                                            # RX1 pull-up, from AVDD's 3.3 V
part("R10", "A:i33", "A:i28")                                           # DIR1 to the LED of U6
part("C5", "A:h35", "A:T-35")                                           # VUSB3V3
part("R18", "A:i35", "A:i41")                                           # RX2 pull-up, from VUSB3V3's 3.3 V
part("Y1", "A:a38", "A:a39")                                            # the crystal: bend its legs to 2.54 mm
part("C7", "A:b38", "A:B-37")
part("C8", "A:b39", "A:B-39")
part("C6", "A:h38", "A:T-37")                                           # VCAP
part("C3", "A:c42", "A:B-43")                                           # VDD
part("R19", "A:i42", "A:i45")                                           # DIR2 to the LED of U10
in_line("J1", "A", "j", 46, ["VBUS", "D-", "D+", "CC1", "CC2", "GND"])  # the USB-C breakout, next to D- and D+
part("C1", "A:T+43", "A:T-43")                                          # 10 µF on the USB 5 V
part("R1", "A:i49", "A:T-53")                                           # CC1 to GND
part("R2", "A:g50", "A:T-54")                                           # CC2 to GND
in_line("U2", "A", "h", 52, ["1", "2", "3"])                            # MCP1700: GND, VIN, VOUT
part("R5", "A:c43", "A:c48")                                            # LED
part("D1", "A:a48", "A:B-49")
part("R6", "A:B+51", "A:b51")                                           # 3.3 V to the LED of U4
part("R15", "A:B+53", "A:b53")                                          # 3.3 V to the LED of U8
SWITCHES = {"SW2": ("A", 56), "SW3": ("A", 60)}
for _ref, (_board, _row) in SWITCHES.items():
    button(_ref, _board, _row)

# ---- board B: the two outputs. Optocouplers across the gap: logic below, isolated above.
OUT_ROWS = {1: 2, 2: 16}
for _n, _base in OUT_ROWS.items():
    _rx, _dr, _tx = {1: ("U5", "U6", "U4"), 2: ("U9", "U10", "U8")}[_n]
    dip(_rx, "B", _base, 8, turned=True)                # receive: its LED on the isolated side
    LEGS[f"{_dr}.1"], LEGS[f"{_dr}.2"] = f"B:e{_base + 5}", f"B:e{_base + 6}"   # direction: its LED below
    LEGS[f"{_dr}.4"], LEGS[f"{_dr}.3"] = f"B:f{_base + 5}", f"B:f{_base + 6}"
    dip(_tx, "B", _base + 8, 8)                         # send: its LED on the logic side
    _p = {1: dict(rled="R8", rup="R11", tup="R7", crx="C13", ctx="C12"),
          2: dict(rled="R17", rup="R20", tup="R16", crx="C16", ctx="C15")}[_n]
    part(_p["rled"], f"B:T+{_base + 2}", f"B:j{_base + 2}")
    part(_p["crx"], f"B:b{_base + 3}", f"B:b{_base}")
    part(_p["rup"], f"B:T+{_base + 5}", f"B:j{_base + 5}")
    part(_p["tup"], f"B:T+{_base + 10 if _base + 10 in RAIL_ROWS else _base + 9}", f"B:j{_base + 10}")
    part(_p["ctx"], f"B:h{_base + 8}", f"B:h{_base + 11}")
in_line("U3", "B", "e", 28, ["1", "2", None, None, "5", None, "7"])   # RKK-0505S/H, pins 1 and 2 at the left
part("C10", "B:B+27", "B:B-27")
part("C11", "B:T+37", "B:T-37")

# The line drivers, and around each the line: bias, termination, protection, the XLR socket.
for _n, _row in ((1, 40), (2, 52)):
    _p = {1: dict(drv="U7", c="C14", up="R12", term="R13", down="R14", da="D2", db="D3", xlr="J3"),
          2: dict(drv="U11", c="C17", up="R21", term="R22", down="R23", da="D4", db="D5", xlr="J4")}[_n]
    dip(_p["drv"], "B", _row, 8)                        # 1-4 (RO, /RE, DE, DI) along e, 8-5 (VCC, B, A, GND) along f
    part(_p["c"], f"B:h{_row}", f"B:h{_row + 3}")
    a, b = _row + 2, _row + 1                           # the strips of A (pin 6) and B (pin 7)
    part(_p["term"], f"B:g{a}", f"B:g{b}")               # 130 Ω standing between A and B
    part(_p["up"], f"B:T+{a}", f"B:h{a}")
    part(_p["da"], f"B:i{a}", f"B:T-{a + 1}")
    part(_p["down"], f"B:h{b}", f"B:T-{b}")
    part(_p["db"], f"B:i{b}", f"B:T-{b - 1}")
    LEGS[f"{_p['xlr']}.3"] = f"B:j{a}"                   # the three wires of the XLR socket
    LEGS[f"{_p['xlr']}.2"] = f"B:j{b}"
    LEGS[f"{_p['xlr']}.1"] = f"B:T-{a + 3}"

# Holes under a part: no wire may end there.
BLOCKED = {f"A:T{sign}{row}" for sign in "+-" for row in range(45, 53)}           # the USB-C breakout over the lines
for _ref, (_board, _row) in SWITCHES.items():
    BLOCKED |= {f"{_board}:{letter}{row}" for letter in "ef" for row in range(_row - 1, _row + 4)}

# Legs that belong to a net: circuit.py, plus the button corners.
PINS = {net: list(pins) for net, pins in NETS.items()}
for _ref, _net in BUTTONS.items():
    PINS[_net].append(f"{_ref}.net")
    PINS["GND"].append(f"{_ref}.gnd")
NET_OF = {pin: net for net, pins in PINS.items() for pin in pins}


# =====================================================================================
# Finding the wires
# =====================================================================================
def plan_wires():
    """For every net: the contacts holding its legs, joined the shortest way. Returns the wires
    as (net, hole, hole), or raises ValueError when a contact runs out of free holes."""
    taken = set(LEGS.values()) | BLOCKED
    wires = []

    def free_in(strip, near):
        """The free hole of a contact nearest to `near` (board, row, height)."""
        holes = [h for h in holes_of(strip) if h not in taken]
        if not holes:
            raise ValueError(f"no free hole left in {strip}")
        return min(holes, key=lambda h: distance(position(h), near))

    for net, pins in PINS.items():
        placed = [LEGS[pin] for pin in pins if pin in LEGS]
        members = {strip_of(hole) for hole in placed}
        # the net's power lines join it wherever it has a contact on that board
        for (board, rail), rail_net in RAIL_NET.items():
            if rail_net == net and any(strip[0] == board for strip in members):
                members.add((board, rail))
        if len(members) < 2:
            continue
        members = sorted(members, key=str)
        # Kruskal over all pairs, by the length of the wire it would take
        pairs = []
        for i, a in enumerate(members):
            for b in members[i + 1:]:
                pairs.append((cost(a, b), a, b))
        parent = {m: m for m in members}

        def find(m):
            while parent[m] != m:
                parent[m] = parent[parent[m]]
                m = parent[m]
            return m

        for _, a, b in sorted(pairs, key=lambda p: (p[0], str(p[1]), str(p[2]))):
            if find(a) == find(b):
                continue
            parent[find(a)] = find(b)
            ha = free_in(a, anchor(b, a))
            taken.add(ha)
            hb = free_in(b, position(ha))
            taken.add(hb)
            wires.append((net, ha, hb))
    return wires


def anchor(strip, other):
    """Where a contact is, as seen from another one: a power line counts at the other's row."""
    if len(strip) == 2:
        row = other[2] if len(other) == 3 else 32
        return strip[0], row, RAIL_Y[strip[1]]
    return strip[0], strip[2], 6 if strip[1] == "top" else 13


def distance(p, q):
    (board_p, row_p, y_p), (board_q, row_q, y_q) = p, q
    if board_p != board_q:
        # board B lies above board A: A's top lines are 4 units under B's bottom lines
        y_q = y_q + (23 if board_q == "A" else -23)
        y_p = y_p + 0
    return abs(row_p - row_q) + abs(y_p - y_q) * 1.2


def cost(a, b):
    # a power line on the same side of the gap is cheap: a short jumper; across the gap it is not
    if len(a) == 2 and len(b) == 3 and a[0] == b[0]:
        return 0.5 + abs(anchor(b, a)[2] - RAIL_Y[a[1]]) * 0.45
    if len(b) == 2 and len(a) == 3 and a[0] == b[0]:
        return cost(b, a)
    if len(a) == 2 and len(b) == 2:
        return 30 if a[0] == b[0] else 25
    return distance(anchor(a, b), anchor(b, a)) + (8 if a[0] != b[0] else 0)


# =====================================================================================
# The check. It does not trust the planner: it starts again from the holes.
# =====================================================================================
def check(wires, legs=None):
    legs = LEGS if legs is None else legs
    problems = []
    used = {}
    for who, hole in list(legs.items()) + [(f"wire {i + 1}", end) for i, w in enumerate(wires) for end in w[1:]]:
        board, where, row = parse(hole)
        if where in RAIL_Y:
            if row not in RAIL_ROWS:
                problems.append(f"{who}: the power line has no hole at row {row}")
        elif where not in LETTER_Y or not 1 <= row <= ROWS:
            problems.append(f"{who}: no such hole {hole}")
        if hole in used:
            problems.append(f"{who} and {used[hole]} share hole {hole}")
        if hole in BLOCKED:
            problems.append(f"{who}: {hole} is under a part")
        if board == "B" and where in ("B+", "B-") and row >= ISO_FROM - 1:
            problems.append(f"{who}: {hole} is the logic side's power line beside the isolated part")
        used[hole] = who

    for pin in NET_OF:
        if pin not in legs:
            problems.append(f"{pin} is in the net list but not on a board")

    for model in ("legs in the same row are joined", "legs on the same side of the gap are joined"):
        parent = {}

        def find(item):
            parent.setdefault(item, item)
            while parent[item] != item:
                parent[item] = parent[parent[item]]
                item = parent[item]
            return item

        def join(a, b):
            parent[find(a)] = find(b)

        for _, a, b in wires:
            join(strip_of(a), strip_of(b))
        for ref in SWITCHES:
            net, a, b, gnd = (strip_of(legs[f"{ref}.{leg}"]) for leg in ("net", "a", "b", "gnd"))
            if model.startswith("legs in the same row"):
                join(net, a)
                join(b, gnd)
            else:
                join(net, b)
                join(a, gnd)
        groups = {}
        for pin, hole in legs.items():
            if pin.split(".")[1] in ("a", "b") and pin.split(".")[0] in SWITCHES:
                continue
            groups.setdefault(find(strip_of(hole)), set()).add(pin)
        for net, pins in PINS.items():
            roots = {find(strip_of(legs[pin])) for pin in pins if pin in legs}
            if len(roots) != 1:
                problems.append(f"[{model}] net {net} is in {len(roots)} pieces")
            elif groups.get(next(iter(roots)), set()) != {p for p in pins if p in legs}:
                extra = sorted(groups.get(next(iter(roots)), set()) - set(pins))
                problems.append(f"[{model}] net {net} is joined to {extra}")
        for pins in groups.values():
            if len(pins) > 1 and not pins & set(NET_OF):
                problems.append(f"[{model}] pins that must stay free are joined: {sorted(pins)}")
        for ref, net in BUTTONS.items():
            signal = find(strip_of(legs[PINS[net][0]]))
            ground = find(strip_of(legs["U1.8"]))
            corners = {find(strip_of(legs[f"{ref}.{leg}"])) for leg in ("net", "a", "b", "gnd")}
            if signal == ground or corners != {signal, ground}:
                problems.append(f"[{model}] button {ref} does not sit between {net} and GND")

    # the isolation of board B: every hole carries a net of its own side
    for pin, hole in legs.items():
        net = NET_OF.get(pin)
        if net and isolated(hole) != (net in ISOLATED_NETS):
            problems.append(f"{pin} ({net}) sits in {hole}, on the other side of the isolation")
    for i, (net, a, b) in enumerate(wires, start=1):
        for hole in (a, b):
            if isolated(hole) != (net in ISOLATED_NETS):
                problems.append(f"wire {i} ({net}) ends in {hole}, on the other side of the isolation")
    for (board, rail), net in RAIL_NET.items():
        for pin, hole in legs.items():
            if strip_of(hole) == (board, rail) and NET_OF.get(pin) != net:
                problems.append(f"{pin} sits in the {rail} line of board {board}, which is {net}")
    return sorted(set(problems))


# =====================================================================================
# The wires, numbered: A1 ... on board A, B1 ... on board B, X1 ... from one board to the other
# =====================================================================================
def numbered(wires):
    groups = {"A": [], "B": [], "X": []}
    for net, a, b in wires:
        if a[0] != b[0]:
            a, b = (a, b) if a[0] == "A" else (b, a)       # from board A to board B
            groups["X"].append((net, a, b))
        else:
            (pa, pb) = sorted((a, b), key=lambda h: (position(h)[1], position(h)[2]))
            groups[a[0]].append((net, pa, pb))
    out = []
    for prefix, items in groups.items():
        items.sort(key=lambda w: (position(w[1])[1], position(w[1])[2], position(w[2])[1]))
        out += [(f"{prefix}{i}", net, a, b) for i, (net, a, b) in enumerate(items, start=1)]
    return out


NET_COLOR = {"+5V": "#dc2626", "GND": "#111827", "+3V3": "#f59e0b", "+5V_ISO": "#db2777", "GND2": "#2563eb"}
SIGNAL_COLORS = ["#7c3aed", "#0d9488", "#65a30d", "#a16207", "#0891b2", "#c026d3", "#16a34a", "#ea580c", "#4f46e5"]


def color_of(net):
    if net in NET_COLOR:
        return NET_COLOR[net]
    return SIGNAL_COLORS[sorted(NETS).index(net) % len(SIGNAL_COLORS)]


def hole_name(hole):
    board, where, row = parse(hole)
    if where in RAIL_Y:
        line = {"T+": "top red line", "T-": "top blue line", "B+": "bottom red line", "B-": "bottom blue line"}[where]
        return f"{board}: {line} ({row})"
    return f"{board}: {where}{row}"


# =====================================================================================
# The drawing of the two boards
# =====================================================================================
PITCH, X0 = 20, 70
BOARD_TOP = {"B": 190, "A": 690}                     # y of the top power line of each board


def X(row):
    return X0 + (row - 1) * PITCH


def Y(board, units):
    return BOARD_TOP[board] + units * PITCH


def at(hole):
    board, row, units = position(hole)
    return X(row), Y(board, units)


TWO_LEG_LOOK = {   # body length and thickness in pixels, fill, lettering
    "R": (34, 12, "#e3cfa4", "#3b2f12"), "C": (18, 12, "#e9a23b", "#3b2f12"), "D": (30, 14, "#2f2f2f", "#ffffff"),
    "Y": (24, 12, "#cbd5e1", "#1f2328"),
}


def draw_board(s, board, title, note, title_below=False):
    top = BOARD_TOP[board]
    s.rect(X(1) - 30, top - 18, X(ROWS) - X(1) + 60, 19 * PITCH + 36, fill="#f6f5ef", stroke="#c9c8bf", width=1.5, rx=10)
    title_y = top + 19 * PITCH + 46 if title_below else top - 30
    s.text(X(1) - 30, title_y, title, 15, weight="bold")
    s.text(X(1) + 260, title_y, note, 12, fill=GREY)
    # the red line runs along the + holes, the blue line along the - holes, on the outer side
    for units, rail, offset in ((0, "T+", -10), (1, "T-", 10), (18, "B+", -10), (19, "B-", 10)):
        net = RAIL_NET[(board, rail)]
        s.line(X(0.5), Y(board, units) + offset, X(63.5), Y(board, units) + offset,
               "#ef4444" if rail[1] == "+" else "#3b82f6", 2)
        s.text(X(ROWS) + 36, Y(board, units) + 4, net, 10.5, weight="bold", fill=color_of(net))
    for row in range(1, ROWS + 1):
        for letter, units in LETTER_Y.items():
            s.rect(X(row) - 2.5, Y(board, units) - 2.5, 5, 5, fill="#a8a79f", rx=1)
        if row in RAIL_ROWS:
            for units in (0, 1, 18, 19):
                s.rect(X(row) - 2.5, Y(board, units) - 2.5, 5, 5, fill="#a8a79f", rx=1)
        if row == 1 or row % 5 == 0:
            s.text(X(row), Y(board, 9.5) + 4, str(row), 10, "middle", fill="#8b8a82")
    for letter, units in LETTER_Y.items():
        s.text(X(ROWS) + 17, Y(board, units) + 4, letter, 11, "middle", fill="#8b8a82")
        s.text(X(1) - 17, Y(board, units) + 4, letter, 11, "middle", fill="#8b8a82")


def draw_iso_zone(s):
    """Board B's isolated side: the top half with its power lines, and the bottom half from ISO_FROM on."""
    left, right = X(0.3), X(63.7)
    mid = Y("B", 9.5)
    zone = [(left, Y("B", -0.8)), (right, Y("B", -0.8)), (right, Y("B", 16.2)), (X(ISO_FROM - 0.5), Y("B", 16.2)),
            (X(ISO_FROM - 0.5), mid), (left, mid)]
    s.path(zone, stroke="none", fill="#cfe1ff", opacity=0.55, close=True)
    s.path(zone[2:6], stroke="#1d4ed8", width=1.6, dash="7 5")


def draw_dip(s, ref, name, pins, turned=False):
    rows = sorted({position(LEGS[f"{ref}.{k}"])[1] for k in range(1, pins + 1)})
    board = LEGS[f"{ref}.1"][0]
    x1, x2 = X(rows[0] - 0.43), X(rows[-1] + 0.43)
    s.rect(x1, Y(board, 8.25), x2 - x1, Y(board, 10.75) - Y(board, 8.25), fill="#2a2a2a", rx=2)
    notch_x = x2 if turned else x1
    s.circle(notch_x, Y(board, 9.5), 6, fill="#f6f5ef")
    p1x, p1y = at(LEGS[f"{ref}.1"])
    s.circle(p1x + (-6 if turned else 6), p1y + (6 if not turned else -6) * -1 * (1 if not turned else -1) * -1,
             2.4, fill="#d1d5db")
    for k in range(1, pins + 1):
        px, py = at(LEGS[f"{ref}.{k}"])
        s.rect(px - 3, min(py, Y(board, 9.5)) if py < Y(board, 9.5) else Y(board, 10.75), 6,
               abs(py - Y(board, 8.25 if py < Y(board, 9.5) else 10.75)) + 3, fill="#c3c7cc", stroke="#6b7280",
               width=0.8)
    cx = (x1 + x2) / 2
    s.text(cx, Y(board, 9.5) - 2, ref, 11, "middle", "bold", "white")
    s.text(cx, Y(board, 9.5) + 11, name, 9, "middle", fill="#e5e7eb")


def draw_two(s, ref, kind, label):
    first, second = ("A", "K") if ref == "D1" else ("1", "2")
    (x1, y1), (x2, y2) = at(LEGS[f"{ref}.{first}"]), at(LEGS[f"{ref}.{second}"])
    s.line(x1, y1, x2, y2, "#8d8d8d", 2.2)
    length, thick, fill, ink = TWO_LEG_LOOK[kind]
    span = math.hypot(x2 - x1, y2 - y1)
    length = min(length, max(span - 8, 10))
    angle = math.degrees(math.atan2(y2 - y1, x2 - x1))
    if angle > 90 or angle <= -90:
        angle += 180
    mx, my = (x1 + x2) / 2, (y1 + y2) / 2
    if ref == "D1":
        s.circle(mx, my, 8, fill="#ef4444", stroke="#7f1d1d", width=1)
        s.text(mx + 12, my + 4, "D1", 9.5, weight="bold", halo=True)
    else:
        s.raw(f'<g transform="translate({mx:.1f} {my:.1f}) rotate({angle:.1f})">'
              f'<rect x="{-length / 2:.1f}" y="{-thick / 2}" width="{length:.1f}" height="{thick}" rx="{thick / 2.6:.1f}" '
              f'fill="{fill}" stroke="#222" stroke-width="1"/>'
              f'<text x="0" y="3.3" font-size="8.5" font-weight="bold" text-anchor="middle" fill="{ink}">'
              f'{esc(label)}</text></g>')
    for px, py in ((x1, y1), (x2, y2)):
        s.circle(px, py, 2.8, fill="#8d8d8d", stroke="#444", width=0.8)


def draw_parts(s):
    for ref, name, pins, turned in (("U1", "PIC32MX270F256B", 28, False), ("U4", "6N137", 8, False),
                                    ("U5", "6N137", 8, True), ("U8", "6N137", 8, False), ("U9", "6N137", 8, True),
                                    ("U7", "MAX485", 8, False), ("U11", "MAX485", 8, False)):
        draw_dip(s, ref, name, pins, turned)
    # the two PC817: pin 1 below, pin 4 above it
    for ref in ("U6", "U10"):
        board = "B"
        r1 = position(LEGS[f"{ref}.1"])[1]
        x1, x2 = X(r1 - 0.43), X(r1 + 1.43)
        s.rect(x1, Y(board, 8.25), x2 - x1, Y(board, 10.75) - Y(board, 8.25), fill="#ffffff", stroke="#111", rx=2)
        s.circle(X(r1 - 0.1), Y(board, 10.3), 2.4, fill="#111")
        s.text((x1 + x2) / 2, Y(board, 9.5) + 4, ref, 10, "middle", "bold")
    # SW1, the DIP switch, turned round: switch 1 at the right. A red body with eight white levers.
    rows = {k: position(LEGS[f"SW1.{k}"])[1] for k in range(1, 9)}
    x1, x2 = X(min(rows.values()) - 0.45), X(max(rows.values()) + 0.45)
    s.rect(x1, Y("A", 8.2), x2 - x1, Y("A", 10.8) - Y("A", 8.2), fill="#b91c1c", rx=2)
    for k, row in rows.items():
        s.rect(X(row) - 4, Y("A", 8.6), 8, 30, fill="#f3f4f6", rx=1)
        s.rect(X(row) - 4, Y("A", 8.6), 8, 12, fill="#9ca3af", rx=1)
        s.text(X(row), Y("A", 10.62), str(k), 8.5, "middle", "bold", "white")
    s.text((x1 + x2) / 2, Y("A", 8.2) - 6, "SW1, turned: switch 1 at the right", 10, "middle", "bold", halo=True)
    # the converter: seven positions, four pins
    first = position(LEGS["U3.1"])[1]
    s.rect(X(first - 0.4), Y("B", 10.2), X(first + 6.4) - X(first - 0.4), Y("B", 11.8) - Y("B", 10.2),
           fill="#1b1b1b", rx=2)
    s.text(X(first + 3), Y("B", 13.6), "U3  RKK-0505S/H", 10, "middle", "bold", halo=True)
    for pin in ("1", "2", "5", "7"):
        px, py = at(LEGS[f"U3.{pin}"])
        s.circle(px, py, 3.6, fill="#e2c15a", stroke="#8a6d1c", width=1)
        s.text(px, py - 7, pin, 8.5, "middle", "bold", "white")
    # the regulator: a TO-92, flat side towards the letters a to e
    px, py = at(LEGS["U2.1"])
    s.path([(X(52) - 9, py + 6), (X(54) + 9, py + 6), (X(54) + 9, py - 2), (X(53), py - 10), (X(52) - 9, py - 2)],
           fill="#111", stroke="#111", width=1, close=True)
    s.text(X(53), py + 18, "U2", 9.5, "middle", "bold", halo=True)
    # the USB-C breakout, hanging over the top power lines
    vx = X(position(LEGS["J1.VBUS"])[1])
    gx = X(position(LEGS["J1.GND"])[1])
    s.rect(vx - 14, Y("A", -1.6), gx - vx + 28, Y("A", 4.4) - Y("A", -1.6), fill="#1f4e79", stroke="#0b2440", rx=3)
    s.rect((vx + gx) / 2 - 22, Y("A", -1.6) - 7, 44, 12, fill="#c3c7cc", stroke="#6b7280", rx=4)
    s.text((vx + gx) / 2, Y("A", 1.2), "J1  USB-C", 10, "middle", "bold", "white")
    for pin, label in (("VBUS", "V"), ("D-", "D−"), ("D+", "D+"), ("CC1", "C1"), ("CC2", "C2"), ("GND", "G")):
        px, py = at(LEGS[f"J1.{pin}"])
        s.circle(px, py, 3.4, fill="#e2c15a", stroke="#8a6d1c", width=1)
        s.text(px, py - 8, label, 8, "middle", "bold", "white")
    # the programming header
    p1 = position(LEGS["J2.1"])[1]
    s.rect(X(p1 - 0.45), Y("A", 15) - 6, X(p1 + 5.45) - X(p1 - 0.45), 12, fill="#111", rx=2)
    for k in range(1, 7):
        px, py = at(LEGS[f"J2.{k}"])
        s.rect(px - 2.5, py - 2.5, 5, 5, fill="#e2c15a")
    s.text(X(p1 - 0.8), Y("A", 15) + 4, "J2 (PICkit, Snap): pin 1 here", 9.5, "end", "bold", halo=True)
    # buttons
    for ref, (board, row) in SWITCHES.items():
        cx, cy, half = X(row + 1), Y(board, 9.5), 2.36 * PITCH
        s.rect(cx - half, cy - half, 2 * half, 2 * half, fill="#2b2b2b", stroke="#111", width=1, rx=4)
        s.circle(cx, cy, 1.5 * PITCH, fill="#565656", stroke="#111", width=1)
        s.text(cx, cy + 4, ref, 11, "middle", "bold", "white")
    # parts with two legs
    for ref in ("R1", "R2", "R3", "R4", "R5", "R6", "R7", "R8", "R9", "R10", "R11", "R12", "R13", "R14", "R15",
                "R16", "R17", "R18", "R19", "R20", "R21", "R22", "R23"):
        draw_two(s, ref, "R", ref)
    for ref in [f"C{n}" for n in range(1, 18)]:
        draw_two(s, ref, "C", ref)
    for ref in ("D2", "D3", "D4", "D5"):
        draw_two(s, ref, "D", ref)
    draw_two(s, "Y1", "Y", "Y1")
    draw_two(s, "D1", "R", "D1")
    # the XLR wires end in the holes: mark them
    for xlr in ("J3", "J4"):
        for pin in ("1", "2", "3"):
            px, py = at(LEGS[f"{xlr}.{pin}"])
            s.circle(px, py, 4.5, fill="#16a34a" if pin == "3" else "#a16207" if pin == "2" else "#2563eb",
                     stroke="#111", width=1)
        ax, ay = at(LEGS[f"{xlr}.3"])
        s.text(ax + 8, Y("B", -1.6), f"{xlr}: to the XLR socket", 9.5, "start", "bold", halo=True)


def wire_path(a, b, index=0):
    """A wire as a curve, the way a jumper lies over the board: bowed to one side or the other
    (by its number), so that wires along one row of holes do not hide each other. Returns the SVG
    path and the place for its number."""
    (x1, y1), (x2, y2) = at(a), at(b)
    if a[0] != b[0]:                                      # from board A up to board B: through the space between them
        gap = (BOARD_TOP["B"] + 19 * PITCH + BOARD_TOP["A"]) / 2 + (index % 3 - 1) * 14
        d = f"M{x1:.1f},{y1:.1f} C{x1:.1f},{gap + 30:.1f} {x2:.1f},{gap - 30:.1f} {x2:.1f},{y2:.1f}"
        return d, ((x1 + x2) / 2, gap)
    length = math.hypot(x2 - x1, y2 - y1)
    side = 1 if index % 2 == 0 else -1
    bow = side * min(40.0, 8 + 0.10 * length + 6 * (index % 3)) if length > 3 * PITCH else 0.0
    nx, ny = (-(y2 - y1) / length, (x2 - x1) / length) if length else (0.0, 0.0)
    cx, cy = (x1 + x2) / 2 + nx * bow, (y1 + y2) / 2 + ny * bow
    d = f"M{x1:.1f},{y1:.1f} Q{cx:.1f},{cy:.1f} {x2:.1f},{y2:.1f}"
    return d, ((x1 + x2) / 2 + nx * bow / 2, (y1 + y2) / 2 + ny * bow / 2)


def draw_wires(s, wires):
    for index, (name, net, a, b) in enumerate(wires):
        d, _ = wire_path(a, b, index)
        s.raw(f'<path d="{d}" fill="none" stroke="#1f2328" stroke-width="3.6" stroke-linecap="round"/>')
        s.raw(f'<path d="{d}" fill="none" stroke="{color_of(net)}" stroke-width="2.2" stroke-linecap="round"/>')
    for index, (name, net, a, b) in enumerate(wires):
        color = color_of(net)
        for px, py in (at(a), at(b)):
            s.circle(px, py, 3.4, fill=color, stroke="#1f2328", width=1)
        _, (mx, my) = wire_path(a, b, index)
        s.rect(mx - 11, my - 6.5, 22, 13, fill="white", stroke=color, width=1.2, rx=6)
        s.text(mx, my + 3.4, name, 8.5, "middle", "bold")


def draw_breadboard(path, wires):
    width, height = 1460, 1270
    s = Svg(width, height)
    s.text(30, 42, "dmxbox 2 on two breadboards", 22, weight="bold")
    s.text(30, 64, "Two full-size boards (830 holes, rows 1 to 63), row 1 at the left: board B at the top, board A "
                   "under it. The five holes of one row on one side of the middle gap are one contact.", 13, fill=GREY)
    s.text(30, 82, "Wires: A1 ... on board A, B1 ... on board B, X1 ... from A to B; the list is on the second page. "
                   "Wire colors only tell the wires apart.", 13, fill=GREY)
    draw_board(s, "B", "Board B: the two DMX outputs",
               "Blue: the isolated side. Its power lines (top) are +5V_ISO and GND2; nothing from board A goes there.")
    draw_iso_zone(s)
    draw_board(s, "A", "Board A: the microcontroller",
               "Top lines: +5V (USB) and GND. Bottom lines: +3.3 V and GND.", title_below=True)
    draw_parts(s)
    draw_wires(s, wires)
    y = 1165
    s.text(30, y, "Before the USB cable goes in", 14, weight="bold")
    for i, line in enumerate((
        "Chips: the notch (and the dot of pin 1) as drawn. U5 and U9 stand the other way round from U4 and U8.",
        "U3 (RKK-0505S/H): pins 1 and 2 at the left. U2 (MCP1700): its flat side towards the letters a to e.",
        "The USB-C breakout: the plan takes its pins as VBUS, D−, D+, CC1, CC2, GND from the left. If yours differ, "
        "put each wire on the pin of that name. Without resistors on CC1 and CC2, fit R1 and R2.",
    )):
        s.text(38, y + 22 + i * 18, "•  " + line, 12)
    s.save(path)


def draw_wire_list(path, wires):
    lines = len(wires)
    per_column = math.ceil(lines / 3)
    parts_rows = math.ceil(sum(1 for ref in PARTS if f"{ref}.1" in LEGS or ref == "D1") / 3)
    width, height = 1460, 100 + per_column * 19 + 30 + 24 + parts_rows * 17 + 40
    s = Svg(width, height)
    s.text(30, 42, "dmxbox 2 on two breadboards: the wires", 22, weight="bold")
    s.text(30, 64, "Each wire joins two holes. A: board A, B: board B; a power line by its color and the row the "
                   "wire goes in at.", 13, fill=GREY)
    for i, (name, net, a, b) in enumerate(wires):
        col, row = divmod(i, per_column)
        x, y = 30 + col * 470, 100 + row * 19
        s.rect(x, y - 11, 30, 14, fill="white", stroke=color_of(net), width=1.4, rx=6)
        s.text(x + 15, y, name, 9.5, "middle", "bold")
        s.text(x + 38, y, f"{hole_name(a)}  →  {hole_name(b)}", 11)
        s.text(x + 440, y, net.replace("D-", "D−"), 10.5, "end", "bold", color_of(net))
    y = 100 + per_column * 19 + 30
    s.text(30, y, "The parts with two legs", 14, weight="bold")
    two = [ref for ref in sorted(PARTS, key=ref_order)
           if (ref[0] in "RCDY" and f"{ref}.1" in LEGS and ref[1:].isdigit()) or ref == "D1"]
    for i, ref in enumerate(two):
        first, second = ("A", "K") if ref == "D1" else ("1", "2")
        col, row = divmod(i, math.ceil(len(two) / 3))
        x, yy = 30 + col * 470, y + 24 + row * 17
        s.text(x, yy, f"{ref}  {PARTS[ref][1]}", 11, weight="bold")
        s.text(x + 120, yy, f"{hole_name(LEGS[ref + '.' + first])}  –  {hole_name(LEGS[ref + '.' + second])}", 10.5)
    s.save(path)


def ref_order(ref):
    """R2 before R10: the letters, then the number."""
    letters = "".join(ch for ch in ref if not ch.isdigit())
    digits = "".join(ch for ch in ref if ch.isdigit())
    return letters, int(digits or 0)


def lists_markdown(wires):
    rows = ["# dmxbox 2 on two breadboards: the lists", "",
            "Written by `boxes/box2/perfboard/schematic-2-universes/breadboard.py`. Do not change it by hand: change the plan in the "
            "script and run it again.", "",
            "Board B (the two DMX outputs) lies above board A (the microcontroller), row 1 of both at the left. "
            "`A: e30` is hole e30 of board A; a power line is named by its color and the row a leg or wire goes in.",
            "", "## Parts", "", "| Part | What | Holes |", "|---|---|---|"]
    by_ref = {}
    for pin, hole in LEGS.items():
        by_ref.setdefault(pin.split(".")[0], []).append((pin.split(".")[1], hole))
    for ref in sorted(by_ref, key=ref_order):
        what = PARTS[ref][1] if ref in PARTS else ""
        holes = ", ".join(f"{pin} {hole_name(hole)}" for pin, hole in sorted(by_ref[ref], key=lambda p: p[0]))
        rows.append(f"| {ref} | {what} | {holes} |")
    rows += ["", "## Wires", "", "| Wire | From | To | Net |", "|---|---|---|---|"]
    for name, net, a, b in wires:
        rows.append(f"| {name} | {hole_name(a)} | {hole_name(b)} | {net} |")
    return "\n".join(rows) + "\n"


def main():
    problems = check_list()
    if problems:
        print("The net list is wrong:", *problems, sep="\n  - ")
        return 1
    try:
        wires = plan_wires()
    except ValueError as error:
        print("No room for the wires:", error)
        return 1
    problems = check(wires)
    if problems:
        print("The breadboard plan is wrong:")
        for problem in problems:
            print("  -", problem)
        return 1
    listed = numbered(wires)
    print(f"breadboard plan: {len(LEGS)} legs, {len(listed)} wires, nets match the schematic")
    draw_breadboard(HERE / "dmxbox2_breadboard.svg", listed)
    draw_wire_list(HERE / "dmxbox2_breadboard_wires.svg", listed)
    (HERE / "dmxbox2_breadboard.md").write_text(lists_markdown(listed), encoding="utf-8")
    if to_png_and_pdf(HERE, ["dmxbox2_breadboard", "dmxbox2_breadboard_wires"], "dmxbox2_breadboard.pdf"):
        print("wrote the SVG, PNG, PDF and list files to", HERE)
    return 0


if __name__ == "__main__":
    sys.exit(main())
