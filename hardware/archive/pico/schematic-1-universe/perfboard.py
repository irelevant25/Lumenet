"""Plans the dmxbox on a perfboard: where every part goes and which pads are joined.

Run from the repository root:

    uv run --no-project --python 3.13 --with resvg-py --with pillow python hardware/pico/schematic-1-universe/perfboard.py

It writes dmxbox_perfboard.svg/.png (the parts side), dmxbox_perfboard_solder.svg/.png (the
solder side, as you see it with the board turned over), dmxbox_perfboard.pdf (both, for
printing) and dmxbox_perfboard.md (the lists: parts, wires, tracks) next to this file.

The board is the 70 x 90 mm kind with 26 x 31 holes, lettered A to Z and numbered 1 to 31,
every hole with a solder pad of its own. A hole is written with a dash here ("N-3"), because
names like C1, R3 or U4 are also parts.

How the plan is made: the places of the parts and the wire links are chosen by hand (PLACES
and LINKS below). The tracks are found by the script: for every net of the schematic (NETS in
draw.py) it looks for the shortest way from pad to pad that touches nothing else. Then a
separate check goes over the result, and a mistake stops the script with a message:

* every leg has a hole of its own, on the board;
* the pads joined by the tracks and the wire links give exactly the nets of the schematic,
  whichever way the buttons are joined inside;
* nothing is connected to a pin that must stay free;
* the logic side and the isolated side stay three holes apart, with two empty columns between;
* no two parts stand in each other's way.

--selftest breaks the plan on purpose in several ways and confirms that the check notices each.
"""
from __future__ import annotations

import heapq
import sys

from draw import BUTTONS, GREY, HERE, INK, NETS, Svg

COLUMNS = "ABCDEFGHIJKLMNOPQRSTUVWXYZ"
ROWS = 31
GAP = "OP"                       # the two empty columns between the logic side and the isolated side
LOGIC_NETS = {"VSYS", "GND", "3V3", "LED_A", "DMX_TX", "BTN1", "BTN2"}


def at(hole):
    """'N3' -> (13, 3): the column as a number from 0, and the row."""
    return COLUMNS.index(hole[0]), int(hole[1:])


def name(cell):
    return f"{COLUMNS[cell[0]]}{cell[1]}"


def shown(hole):
    """How a hole is written for people: N-3."""
    return f"{hole[0]}-{hole[1:]}"


# =====================================================================================
# Where the parts go. Every leg: the hole it is soldered into.
# =====================================================================================
LEGS = {}
for _n in range(1, 41):                                  # the Pico, USB socket at row 1
    LEGS[f"U1.{_n}"] = f"A{_n}" if _n <= 20 else f"H{41 - _n}"


def dip8(ref, left, right, top):
    """A DIP-8 with its notch towards row 1: pins 1-4 down the left column, 8-5 down the right."""
    for n in range(1, 5):
        LEGS[f"{ref}.{n}"] = f"{left}{top + n - 1}"
    for n in range(5, 9):
        LEGS[f"{ref}.{n}"] = f"{right}{top + 8 - n}"


dip8("U3", "N", "Q", 2)                                  # the optocoupler bridges the gap
dip8("U5", "U", "X", 2)
LEGS.update({
    "U4.1": "M8", "U4.2": "N8", "U4.5": "Q8", "U4.7": "S8",   # the converter bridges the gap too
    "C4.+": "M11", "C4.-": "L11",                             # close to the converter's two input pins
    "R3.1": "L7", "R3.2": "L3",
    "R4.1": "R1", "R4.2": "R5",
    "C1.1": "T4", "C1.2": "T6",
    "C2.1": "V7", "C2.2": "X7",
    "C3.+": "W11", "C3.-": "X11",
    "R2.1": "V12", "R2.2": "R12",
    "D1.1": "Y14", "D1.2": "T14",
    "D3.1": "Z16", "D3.2": "U16",
    "J1.2": "Z19", "J1.3": "Z21", "J1.1": "Z23",              # screw terminal: XLR pins 2, 3, 1 from the top
})
# A button has four legs. Its net goes to one corner and GND to the opposite corner: that works
# whichever pairs of legs the button joins inside. The other two legs are soldered but join nothing.
SWITCHES = {"SW2": ("I", "N", 23), "SW1": ("I", "N", 29)}      # left column, right column, upper row
for _ref, (_left, _right, _row) in SWITCHES.items():
    LEGS[f"{_ref}.a"] = f"{_left}{_row}"                 # upper left
    LEGS[f"{_ref}.net"] = f"{_right}{_row}"              # upper right: the button's net
    LEGS[f"{_ref}.gnd"] = f"{_left}{_row + 2}"           # lower left: GND
    LEGS[f"{_ref}.b"] = f"{_right}{_row + 2}"            # lower right
# The socket strip of U4 has seven contacts, but the converter has no pin at 3, 4 and 6.
EMPTY_CONTACTS = {"U4 position 3": "O8", "U4 position 4": "P8", "U4 position 6": "R8"}

# Insulated wires on the parts side: net, from, to, color, what it is.
LINKS = [
    ("DMX_TX", "B1", "M4", "#7c3aed", "Pico pin 1 (GP0, the DMX data) to U3 pin 3"),
]

# Legs that belong to a net: the schematic's list, plus the button corners.
PINS = {net: list(pins) for net, pins in NETS.items()}
for _ref, _net in BUTTONS.items():
    PINS[_net].append(f"{_ref}.net")
    PINS["GND"].append(f"{_ref}.gnd")
PICO_GROUND = [pin for pin in NETS["GND"] if pin.startswith("U1.")]   # joined inside the Pico
# Pin 33 is the Pico's analog ground. It is joined to the others inside the Pico, but the supply
# current of the converter and the buttons has no business there, so no track goes to it.
NO_TRACK = {"U1.33"}

# What stands on the board, for the check that parts are not in each other's way:
# name -> (left, top, right, bottom) in hole units, measured between hole centres.
BODIES = {
    "U1 Pico": (-0.63, 0.46, 7.63, 20.54),
    "U3": (12.5, 1.5, 16.5, 5.5),
    "U5": (19.5, 1.5, 23.5, 5.5),
    "U4": (11.14, 6.6, 18.86, 9.4),
    "C4": (10.52, 10.02, 12.48, 11.98),
    "C3": (21.52, 10.02, 23.48, 11.98),
    "C1": (18.51, 3.6, 19.49, 6.4),
    "C2": (20.6, 6.51, 23.4, 7.49),
    "R3": (10.55, 3.7, 11.45, 6.3),
    "R4": (16.55, 1.7, 17.45, 4.3),
    "R2": (17.7, 11.55, 20.3, 12.45),
    "D1": (20.0, 13.3, 23.0, 14.7),
    "D3": (21.0, 15.3, 24.0, 16.7),
    "J1": (23.45, 17.9, 26.6, 24.1),
    "SW2": (8.14, 21.64, 12.86, 26.36),
    "SW1": (8.14, 27.64, 12.86, 32.36),
}


# =====================================================================================
# Finding the tracks
# =====================================================================================
def side_of(net):
    return "logic" if net in LOGIC_NETS else "isolated"


def allowed(cell, net):
    column, row = cell
    if not (0 <= column < 26 and 1 <= row <= ROWS):
        return False
    if COLUMNS[column] in GAP:
        return False
    return (column < COLUMNS.index(GAP[0])) == (side_of(net) == "logic")


def find_way(starts, goals, price_of, net):
    """The cheapest way from one of `starts` to one of `goals`. `price_of(pad)` is what a step onto
    a pad costs, or None where this net may not go. A corner costs a little extra."""
    queue = [(0.0, cell, None) for cell in starts]
    best = {(cell, None): 0.0 for cell in starts}
    came = {}
    while queue:
        cost, cell, heading = heapq.heappop(queue)
        if best.get((cell, heading), 1e9) < cost:
            continue
        if cell in goals and cell not in starts:
            way = [cell]
            state = (cell, heading)
            while state in came:
                state = came[state]
                way.append(state[0])
            return way[::-1]
        for step in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            nxt = (cell[0] + step[0], cell[1] + step[1])
            if not allowed(nxt, net):
                continue
            price = price_of(nxt)
            if price is None:
                continue
            new = cost + price + (0.4 if heading not in (None, step) else 0.0)
            if new < best.get((nxt, step), 1e9):
                best[(nxt, step)] = new
                came[(nxt, step)] = (cell, heading)
                heapq.heappush(queue, (new, nxt, step))
    return None


def join_net(net, price_of, legs, links):
    """Joins all pads of one net. Returns the joined pad pairs, or None when there is no way."""
    # the pieces that are joined already: every leg, a wire link, the ground pins inside the Pico
    pieces = [{at(legs[pin])} for pin in PINS[net] if pin not in NO_TRACK]
    pieces += [{at(a), at(b)} for link_net, a, b, *_ in links if link_net == net]
    if net == "GND":
        pieces.append({at(legs[pin]) for pin in PICO_GROUND if pin not in NO_TRACK})
    pieces = merge(pieces)
    pairs = set()
    while len(pieces) > 1:
        found = None                                      # join the two pieces that are closest
        for i, piece in enumerate(pieces):
            others = set().union(*(p for j, p in enumerate(pieces) if j != i))
            way = find_way(piece, others, price_of, net)
            if way and (found is None or len(way) < len(found)):
                found = way
        if found is None:
            return None
        pairs |= {frozenset(pair) for pair in zip(found, found[1:])}
        pieces = merge(pieces + [set(found)])
    return pairs


def pads_of(pairs):
    return {pad for pair in pairs for pad in pair}


def find_tracks(legs=None, links=None, rounds=150):
    """Finds tracks for all nets, or returns None.

    The nets negotiate: at first two nets may run over the same pad. Every round a shared pad
    gets dearer, so the nets that have another way take it, until no pad is shared. Then every
    net is pulled straight once more with the others fixed in place.
    """
    legs = LEGS if legs is None else legs
    links = LINKS if links is None else links
    fixed = {}                                            # pads that belong to one net for good
    for pin, hole in legs.items():
        net = None if pin in NO_TRACK else next((net for net, pins in PINS.items() if pin in pins), None)
        fixed[at(hole)] = net or f"free:{pin}"
    for what, hole in EMPTY_CONTACTS.items():
        fixed[at(hole)] = f"free:{what}"
    for net, a, b, *_ in links:
        for hole in (a, b):
            if fixed.setdefault(at(hole), net) != net:
                return None

    nets = list(PINS)
    tracks = {net: set() for net in nets}
    memory = {}                                           # how often a pad has been fought over
    pressure = 0.5

    def price_for(net, hard):
        taken = {}
        for other in nets:
            if other != net:
                for pad in pads_of(tracks[other]):
                    taken[pad] = taken.get(pad, 0) + 1

        def price_of(pad):
            owner = fixed.get(pad)
            if owner is not None:
                return 1.0 if owner == net else None
            if hard:
                return None if pad in taken else 1.0
            return (1.0 + memory.get(pad, 0.0)) * (1.0 + pressure * taken.get(pad, 0))
        return price_of

    for _ in range(rounds):
        for net in nets:
            pairs = join_net(net, price_for(net, hard=False), legs, links)
            if pairs is None:
                return None
            tracks[net] = pairs
        claims = {}
        for net in nets:
            for pad in pads_of(tracks[net]):
                if pad not in fixed:
                    claims[pad] = claims.get(pad, 0) + 1
        shared = [pad for pad, count in claims.items() if count > 1]
        if not shared:
            break
        for pad in shared:
            memory[pad] = memory.get(pad, 0.0) + 1.0
        pressure *= 1.3
    else:
        return None

    for _ in range(3):                                    # pull every net straight, the others fixed
        for net in sorted(nets, key=lambda n: -len(tracks[n])):
            pairs = join_net(net, price_for(net, hard=True), legs, links)
            if pairs is not None and len(pairs) <= len(tracks[net]):
                tracks[net] = pairs
    return tracks


def merge(pieces):
    """Joins sets that share a pad."""
    merged = []
    for piece in pieces:
        piece = set(piece)
        for other in [m for m in merged if m & piece]:
            piece |= other
            merged.remove(other)
        merged.append(piece)
    return merged


# =====================================================================================
# The check. It does not trust the track finder: it starts again from the pads.
# =====================================================================================
def check(tracks, legs=None, links=None):
    legs = LEGS if legs is None else legs
    links = LINKS if links is None else links
    problems = []

    used = {}
    for who, hole in list(legs.items()) + list(EMPTY_CONTACTS.items()) + [
            (f"wire link {i + 1}", end) for i, link in enumerate(links) for end in link[1:3]]:
        column, row = hole[0], int(hole[1:])
        if column not in COLUMNS or not 1 <= row <= ROWS:
            problems.append(f"{who}: there is no hole {shown(hole)}")
        if hole in used:
            problems.append(f"{who} and {used[hole]} share hole {shown(hole)}")
        used[hole] = who

    parent = {}

    def find(item):
        parent.setdefault(item, item)
        while parent[item] != item:
            parent[item] = parent[parent[item]]
            item = parent[item]
        return item

    def join(a, b):
        parent[find(a)] = find(b)

    track_pads = set()
    for net, pairs in tracks.items():
        for pair in pairs:
            a, b = tuple(pair)
            if abs(a[0] - b[0]) + abs(a[1] - b[1]) != 1:
                problems.append(f"track of {net}: {name(a)} and {name(b)} are not neighbours")
            track_pads |= {a, b}
    for pad in track_pads:
        if COLUMNS[pad[0]] in GAP:
            problems.append(f"a track uses {shown(name(pad))}, in the gap between the two sides")

    listed = {pin for pins in PINS.values() for pin in pins}
    for pin in listed - set(legs):
        problems.append(f"{pin} is in the net list but not on the board")
    free_holes = {at(hole) for pin, hole in legs.items() if pin not in listed}
    free_holes |= {at(hole) for hole in EMPTY_CONTACTS.values()}
    for pad in track_pads & free_holes:
        problems.append(f"a track touches {shown(name(pad))}, which must stay unconnected")

    # Two ways a 4-leg button can be joined inside. The plan has to work with both.
    for model in ("legs in the same row are joined", "legs in the same column are joined"):
        parent.clear()
        for pairs in tracks.values():
            for pair in pairs:
                join(*tuple(pair))
        for _, a, b, *_ in links:
            join(at(a), at(b))
        for pin in PICO_GROUND:                           # the Pico joins its ground pins itself
            join(at(legs[pin]), at(legs[PICO_GROUND[0]]))
        for ref in SWITCHES:
            a, net, gnd, b = (at(legs[f"{ref}.{leg}"]) for leg in ("a", "net", "gnd", "b"))
            if model.startswith("legs in the same row"):
                join(a, net)
                join(gnd, b)
            else:
                join(a, gnd)
                join(net, b)

        button_spares = {f"{ref}.{leg}" for ref in SWITCHES for leg in "ab"}
        groups = {}
        for pin, hole in legs.items():
            if pin not in button_spares:
                groups.setdefault(find(at(hole)), set()).add(pin)
        for net, pins in PINS.items():
            roots = {find(at(legs[pin])) for pin in pins if pin in legs}
            if len(roots) != 1:
                problems.append(f"[{model}] net {net} is in {len(roots)} pieces")
            elif groups[roots.pop()] != set(pins):
                problems.append(f"[{model}] net {net} is joined to something else")
        for pins in groups.values():
            if len(pins) > 1 and not pins & listed:
                problems.append(f"[{model}] pins that must stay free are joined: {sorted(pins)}")
        for ref, net in BUTTONS.items():
            signal, ground = find(at(legs[PINS[net][0]])), find(at(legs[PICO_GROUND[0]]))
            corners = {find(at(legs[f"{ref}.{leg}"])) for leg in ("a", "net", "gnd", "b")}
            if signal == ground or corners != {signal, ground}:
                problems.append(f"[{model}] button {ref} does not sit between {net} and GND")

        # the isolation: no piece of copper may reach across the gap, or come near the other side
        side = {}
        for pad in track_pads | {at(hole) for hole in legs.values()}:
            if COLUMNS[pad[0]] in GAP and pad not in free_holes:
                problems.append(f"{shown(name(pad))} is in the gap and carries a net")
            side.setdefault(find(pad), set()).add("logic" if pad[0] < COLUMNS.index(GAP[0]) else "isolated")
        for root, sides in side.items():
            if len(sides) > 1:
                problems.append(f"[{model}] something joins the logic side to the isolated side at {shown(name(root))}")

    for i, (a, box_a) in enumerate(BODIES.items()):
        for b, box_b in list(BODIES.items())[i + 1:]:
            if box_a[0] < box_b[2] and box_b[0] < box_a[2] and box_a[1] < box_b[3] and box_b[1] < box_a[3]:
                problems.append(f"{a} and {b} stand in each other's way")
    return sorted(set(problems))


def selftest(tracks):
    """Breaks the plan in ways a real mistake would, and reports any the check does not notice."""
    missed = []

    def expect(what, new_tracks=None, legs=None, links=None):
        if not check(tracks if new_tracks is None else new_tracks, legs, links):
            missed.append(what)

    for net in PINS:                                      # a track that was forgotten
        if tracks[net]:
            less = dict(tracks)
            less[net] = set(list(tracks[net])[1:])
            expect(f"one track pair of {net} removed", less)
    swapped = dict(LEGS)                                  # Data + and Data - swapped at the terminal
    swapped["J1.2"], swapped["J1.3"] = LEGS["J1.3"], LEGS["J1.2"]
    expect("J1 pins 2 and 3 swapped", legs=swapped)
    turned = dict(LEGS)                                   # U5 put in the other way round
    for n in range(1, 9):
        turned[f"U5.{n}"] = LEGS[f"U5.{9 - n}"]
    expect("U5 turned round", legs=turned)
    moved = dict(LEGS)
    moved["R3.2"] = "K3"
    expect("R3 moved by one hole", legs=moved)
    bridge = dict(tracks)                                 # a solder bridge between two nets
    bridge["GND"] = tracks["GND"] | {frozenset((at(LEGS["C4.+"]), at(LEGS["C4.-"])))}
    expect("VSYS bridged to GND", bridge)
    across = dict(tracks)                                 # a track across the isolation gap
    across["GND2"] = tracks["GND2"] | {frozenset((at(f"{c}8"), at(f"{d}8"))) for c, d in ("NO", "OP", "PQ")}
    expect("GND joined to GND2 across the gap", across)
    expect("the wire link left out", links=[])
    corner = dict(LEGS)                                   # button wired to the neighbouring corner
    corner["SW1.gnd"], corner["SW1.b"] = LEGS["SW1.b"], LEGS["SW1.gnd"]
    expect("button SW1 wired to two corners of one side", legs=corner)
    return missed


# =====================================================================================
# The drawings
# =====================================================================================
PITCH = 30.0                                              # pixels from hole to hole
BOARD_W, BOARD_H = 70 / 2.54, 90 / 2.54                   # the board, in hole distances
EDGE_X, EDGE_Y = (BOARD_W - 25) / 2, (BOARD_H - 30) / 2   # from the board's edge to the first hole

NET_INFO = {                                              # color, what the net is
    "VSYS": ("#dc2626", "5 V from the USB cable (Pico pin 39)"),
    "GND": ("#111827", "ground of the Pico and the computer"),
    "3V3": ("#f59e0b", "3.3 V (Pico pin 36) to R3"),
    "LED_A": ("#ea580c", "R3 to U3 pin 2"),
    "DMX_TX": ("#7c3aed", "the DMX data: Pico pin 1 to U3 pin 3"),
    "BTN1": ("#0d9488", "button SW1, blackout (Pico pin 24)"),
    "BTN2": ("#65a30d", "button SW2, preset 1 (Pico pin 25)"),
    "+5V_ISO": ("#db2777", "isolated 5 V"),
    "GND2": ("#2563eb", "isolated ground, XLR pin 1"),
    "DI": ("#a21caf", "the DMX data: U3 pin 6 to U5 pin 4"),
    "A": ("#16a34a", "Data +, XLR pin 3"),
    "B": ("#a16207", "Data −, XLR pin 2"),
}
TWO_LEGS = {                                              # kind, value, body length and thickness in hole distances
    "R2": ("resistor", "270 Ω", 2.5, 0.9), "R3": ("resistor", "180 Ω", 2.5, 0.9), "R4": ("resistor", "1 kΩ", 2.5, 0.9),
    "C1": ("ceramic", "100 nF", 2.7, 0.9), "C2": ("ceramic", "100 nF", 2.7, 0.9),
    "C3": ("electrolytic", "10 µF", 0, 0), "C4": ("electrolytic", "4.7 µF", 0, 0),
    "D1": ("diode", "P6KE6.8CA", 3.0, 1.4), "D3": ("diode", "P6KE6.8CA", 3.0, 1.4),
}
BUTTON_JOB = {"SW1": "blackout", "SW2": "preset 1"}
PAPER, BOARD, SOFT = "#ffffff", "#f6f3e7", "#9ca3af"


def net_of(pin):
    return next((net for net, pins in PINS.items() if pin in pins), None)


class View:
    """Turns holes into places on the drawing. Mirrored for the solder side: column A at the right."""

    def __init__(self, svg, x0, y0, mirrored):
        self.svg, self.x0, self.y0, self.mirrored = svg, x0, y0, mirrored

    def x(self, column):
        return self.x0 + (EDGE_X + (25 - column if self.mirrored else column)) * PITCH

    def y(self, row):
        return self.y0 + (EDGE_Y + row - 1) * PITCH

    def xy(self, cell):
        return self.x(cell[0]), self.y(cell[1])

    def box(self, body):
        left, top, right, bottom = body
        x1, x2 = sorted((self.x(left), self.x(right)))
        return x1, self.y(top), x2 - x1, self.y(bottom) - self.y(top)


def draw_board(view, title, note):
    s = view.svg
    s.text(view.x0, view.y0 - 62, title, size=22, weight="bold")
    s.text(view.x0, view.y0 - 38, note, size=13, fill=GREY)
    s.rect(view.x0, view.y0, BOARD_W * PITCH, BOARD_H * PITCH, fill=BOARD, stroke=INK, width=1.5, rx=6)
    # the two columns that stay empty
    x1, x2 = sorted((view.x(COLUMNS.index(GAP[0])), view.x(COLUMNS.index(GAP[-1]))))
    s.rect(x1 - PITCH / 2, view.y0 + 1, x2 - x1 + PITCH, BOARD_H * PITCH - 2, fill="#fca5a5", opacity=0.33)
    s.text((x1 + x2) / 2 + 4, view.y(27.5), "isolation gap: nothing is soldered in these two columns",
           size=12, anchor="middle", fill="#991b1b", rotate=-90)
    for column in range(26):
        for y in (view.y0 - 8, view.y0 + BOARD_H * PITCH + 18):
            s.text(view.x(column), y, COLUMNS[column], size=12, anchor="middle", weight="bold", fill=GREY)
        for row in range(1, ROWS + 1):
            s.circle(view.x(column), view.y(row), 3.2, fill=PAPER, stroke=SOFT, width=1)
    for row in range(1, ROWS + 1):
        s.text(view.x0 - 16, view.y(row) + 4, row, size=11, anchor="end", fill=GREY)
        s.text(view.x0 + BOARD_W * PITCH + 16, view.y(row) + 4, row, size=11, fill=GREY)


def draw_tracks(view, tracks, faint):
    for net, pairs in tracks.items():
        for pair in pairs:
            a, b = tuple(pair)
            view.svg.line(*view.xy(a), *view.xy(b), stroke="#cbd5e1" if faint else NET_INFO[net][0],
                          width=3 if faint else 7)


def draw_pads(view, tracks, colored):
    """A dot in every hole that holds a leg. On the solder side the dot has the color of its net."""
    s = view.svg
    if colored:
        for net, pairs in tracks.items():
            for pad in pads_of(pairs):
                s.circle(*view.xy(pad), 5.5, fill=NET_INFO[net][0])
    for pin, hole in LEGS.items():
        net = net_of(pin)
        if net is None:
            s.circle(*view.xy(at(hole)), 4.2, fill=PAPER, stroke=INK, width=1.6)       # a leg that joins nothing
        else:
            s.circle(*view.xy(at(hole)), 6.5 if colored else 4.4, fill=NET_INFO[net][0] if colored else INK)
            if colored:
                s.circle(*view.xy(at(hole)), 2.2, fill=PAPER)
    for hole in EMPTY_CONTACTS.values():
        s.circle(*view.xy(at(hole)), 4.2, fill=PAPER, stroke=INK, width=1.6)


def two_leg_part(view, ref, outline_only):
    s = view.svg
    kind, value, length, thickness = TWO_LEGS[ref]
    first, second = (".+", ".-") if kind == "electrolytic" else (".1", ".2")
    (x1, y1), (x2, y2) = view.xy(at(LEGS[ref + first])), view.xy(at(LEGS[ref + second]))
    cx, cy = (x1 + x2) / 2, (y1 + y2) / 2
    upright = abs(x1 - x2) < 1
    stroke = SOFT if outline_only else INK
    fill = "none" if outline_only else {"resistor": "#e7d3b0", "ceramic": "#f2c94c", "electrolytic": "#c7d2fe",
                                        "diode": "#d1d5db"}[kind]
    if kind == "electrolytic":
        radius = 0.98 * PITCH
        s.circle(cx, cy, radius, fill=fill, stroke=stroke, width=1.5)
        if not outline_only:
            # the name and the value stand clear of the two legs: outside the can when the legs are above each other
            s.text(cx, cy - (radius + 5 if upright else 14), ref, size=11, anchor="middle", weight="bold", halo=upright)
            s.text(cx, cy + (radius + 14 if upright else 22), value, size=10, anchor="middle", halo=upright)
            s.text(x1 + (-13 if upright or x1 < x2 else 13), y1 + 5, "+", size=15, anchor="middle", weight="bold",
                   fill="#b91c1c")
        return
    if not outline_only:
        s.line(x1, y1, x2, y2, stroke="#6b7280", width=2)
    w, h = (thickness * PITCH, length * PITCH) if upright else (length * PITCH, thickness * PITCH)
    s.rect(cx - w / 2, cy - h / 2, w, h, fill=fill, stroke=stroke, width=1.5, rx=5)
    if outline_only:
        return
    if kind == "diode":
        s.text(cx, cy - 2, ref, size=11, anchor="middle", weight="bold")
        s.text(cx, cy + 12, value, size=10, anchor="middle")
    else:
        # a ceramic capacitor has its legs inside the body, so its lettering is shorter
        label = f"{ref} {value.replace(' nF', 'n')}" if kind == "ceramic" else f"{ref} {value}"
        s.text(cx + (4 if upright else 0), cy + (0 if upright else 4), label, size=10 if kind == "ceramic" else 11,
               anchor="middle", weight="bold", rotate=-90 if upright else None)


def draw_parts(view, outline_only):
    """The parts as seen from above. With outline_only they are only hinted, for the solder side."""
    s = view.svg
    stroke, dash = (SOFT, "5 4") if outline_only else (INK, None)

    def outline(ref, fill, rx=4):
        x, y, w, h = view.box(BODIES[ref])
        s.path([(x, y), (x + w, y), (x + w, y + h), (x, y + h)], stroke=stroke, width=1.5, dash=dash, close=True,
               fill="none" if outline_only else fill, opacity=None if outline_only else 0.9)
        return x, y, w, h

    # the Pico: only its outline, because the tracks under it have to stay visible
    x, y, w, h = view.box(BODIES["U1 Pico"])
    s.path([(x, y), (x + w, y), (x + w, y + h), (x, y + h)], stroke="#15803d" if not outline_only else SOFT,
           width=2 if not outline_only else 1.5, dash=dash, close=True, fill="none")
    usb = w * 0.36
    s.rect(x + (w - usb) / 2, y - 12, usb, 22, fill="none" if outline_only else "#d1d5db",
           stroke="#15803d" if not outline_only else SOFT, width=1.5, rx=2)
    if not outline_only:
        s.text(x + w / 2, y + 4, "USB", size=10, anchor="middle")
        s.text(x + w / 2, y + h / 2, "U1  Raspberry Pi Pico, on two socket strips", size=13, anchor="middle",
               weight="bold", fill="#15803d", rotate=-90)
        for pin, dx in ((1, -1), (20, -1), (21, 1), (40, 1)):      # the numbers stand outside the Pico
            px, py = view.xy(at(LEGS[f"U1.{pin}"]))
            s.text(px + dx * 11, py + 4, pin, size=10, anchor="start" if dx > 0 else "end", weight="bold",
                   fill="#15803d", halo=True)

    for ref, label in (("U3", ["U3", "6N137"]), ("U5", ["U5", "SN75176", "or MAX485"])):
        x, y, w, h = outline(ref, "#e5e7eb")
        if outline_only:
            continue
        s.path([(x + w / 2 - 9, y), (x + w / 2 - 9, y + 7), (x + w / 2 + 9, y + 7), (x + w / 2 + 9, y)], width=1.5)
        for i, line in enumerate(label):
            s.text(x + w / 2, y + h / 2 - 2 + i * 14 - (len(label) - 2) * 7, line, size=11 if i else 13,
                   anchor="middle", weight="bold" if i == 0 else "normal")
        px, py = view.xy(at(LEGS[f"{ref}.1"]))
        s.text(px + 10, py + 4, "1", size=10)

    x, y, w, h = outline("U4", "#fde68a")
    if not outline_only:
        s.text(x + w / 2, y + 16, "U4  RKK-0505S/H, in a 7-way socket strip", size=11, anchor="middle", weight="bold")
        for pin in (1, 2, 5, 7):
            px, py = view.xy(at(LEGS[f"U4.{pin}"]))
            s.text(px, py + 22, pin, size=10, anchor="middle")

    x, y, w, h = outline("J1", "#bbf7d0")
    if not outline_only:
        s.text(x + w / 2, y + 14, "J1", size=12, anchor="middle", weight="bold")
        for pin, what in ((2, "2 Data −"), (3, "3 Data +"), (1, "1 common")):
            px, py = view.xy(at(LEGS[f"J1.{pin}"]))
            s.text(px - 10, py + 4, what, size=9.5, anchor="end")

    for ref in SWITCHES:
        x, y, w, h = outline(ref, "#e5e7eb")
        if outline_only:
            continue
        s.circle(x + w / 2, y + h / 2, 1.3 * PITCH, fill="#f9fafb", stroke=INK, width=1.5)
        s.text(x + w / 2, y + h / 2 - 2, ref, size=13, anchor="middle", weight="bold")
        s.text(x + w / 2, y + h / 2 + 13, BUTTON_JOB[ref], size=11, anchor="middle")

    for ref in TWO_LEGS:
        two_leg_part(view, ref, outline_only)


def draw_names(view):
    """The names of the parts on the solder side, written over the tracks so they stay readable."""
    s = view.svg
    for ref, body in BODIES.items():
        if ref == "U1 Pico":
            continue
        x, y, w, h = view.box(body)
        cx, cy = x + w / 2, y + h / 2
        if ref in ("U4", "J1"):                           # their middle is a pad: the name goes to the top edge
            cy = y + 10
        elif ref in TWO_LEGS:                             # between two holes, never on one
            first, second = [view.xy(at(hole)) for pin, hole in LEGS.items() if pin.startswith(ref + ".")]
            cx, cy = (first[0] + second[0]) / 2, (first[1] + second[1]) / 2
            holes_apart = round((abs(first[0] - second[0]) + abs(first[1] - second[1])) / PITCH)
            if holes_apart % 2 == 0:
                cx, cy = (cx, cy + PITCH / 2) if abs(first[0] - second[0]) < 1 else (cx + PITCH / 2, cy)
        s.text(cx, cy + 4, ref, size=11, anchor="middle", weight="bold", fill="#4b5563", halo=True)
    x, y, w, h = view.box(BODIES["U1 Pico"])
    s.text(x + w / 2, y + h / 2, "U1  Raspberry Pi Pico", size=13, anchor="middle", weight="bold", fill="#4b5563",
           rotate=-90, halo=True)
    s.text(x + w / 2, y + 4, "USB", size=10, anchor="middle", fill="#4b5563", halo=True)
    for pin, dx in ((1, 1), (20, 1), (21, -1), (40, -1)):          # mirrored: pin 1 is at the right edge now
        px, py = view.xy(at(LEGS[f"U1.{pin}"]))
        s.text(px + dx * 11, py + 4, pin, size=10, anchor="start" if dx > 0 else "end", weight="bold",
               fill="#4b5563", halo=True)


def link_way(a, b):
    """Where a wire link is drawn: round the top end of the Pico's socket strip, then down to its hole."""
    (ac, ar), (bc, br) = at(a), at(b)
    return [(ac, ar), (ac, 0.1), (bc, 0.1), (bc, br)]


def draw_links(view, parts_side):
    s = view.svg
    for i, (net, a, b, color, _) in enumerate(LINKS, start=1):
        if parts_side:
            points = [view.xy(cell) for cell in link_way(a, b)]
            s.path(points, stroke="white", width=7)
            s.path(points, stroke=color, width=4)
            lx, ly = view.xy((at(a)[0] + 7.6, 0.1))
            s.text(lx, ly - 7, f"W{i}: insulated wire", size=11, weight="bold", fill=color, halo=True)
        for hole in (a, b):
            x, y = view.xy(at(hole))
            s.circle(x, y, 8.5, stroke=color, width=2.5, fill="none" if not parts_side else color)
            if parts_side:
                s.circle(x, y, 2.2, fill=PAPER)
            else:
                s.text(x, y - 12, f"W{i}", size=10, anchor="middle", weight="bold", fill=color, halo=True)


def draw_legend(view, x, y, solder_side):
    s = view.svg
    s.text(x, y, "Tracks on the solder side" if solder_side else "What is connected", size=14, weight="bold")
    for i, (net, (color, what)) in enumerate(NET_INFO.items()):
        yy = y + 26 + i * 24
        s.line(x, yy - 4, x + 34, yy - 4, stroke=color, width=7)
        s.text(x + 44, yy, net, size=12, weight="bold")
        s.text(x + 44, yy + 12, what, size=10, fill=GREY)
    yy = y + 26 + len(NET_INFO) * 24 + 16
    lines = ([
        "A dot with a white middle is a pad with a leg in it.",
        "A plain dot is a pad the track only passes.",
        "A ring is a leg that is soldered but joins nothing.",
        "W1 marks the two ends of the insulated wire.",
        "Dashed outlines: where the parts are, seen",
        "through the board.",
        "",
        "Neighbouring pads of two colors must not touch:",
        "look for solder bridges before the first power-up.",
    ] if solder_side else [
        "A black dot is a hole with a leg in it.",
        "A ring is a leg that is soldered but joins nothing.",
        "The pale lines are the tracks on the other side.",
        "The small numbers are pin numbers.",
        "",
        "U3, U5: the notch points to row 1.",
        "U4: pin 1 (marked on the part) goes to M-8.",
        "C3, C4: the long leg goes into the hole marked +.",
        "D1, D3 and the resistors fit either way round.",
        "",
        "U4's socket strip: pull the contacts 3 and 4 out",
        "before soldering it in. They would stand in the",
        "isolation gap (O-8 and P-8).",
    ])
    for i, line in enumerate(lines):
        s.text(x, yy + i * 16, line, size=11, fill=INK)


def draw(path, tracks, solder_side):
    width, height = 1250, 1230
    svg = Svg(width, height)
    view = View(svg, 60, 100, mirrored=solder_side)
    if solder_side:
        draw_board(view, "dmxbox on a perfboard: the solder side",
                   "The board turned over sideways: column A is at the right now. 70 × 90 mm, 26 × 31 holes.")
        draw_parts(view, outline_only=True)
        draw_tracks(view, tracks, faint=False)
        draw_pads(view, tracks, colored=True)
        draw_names(view)
        draw_links(view, parts_side=False)
    else:
        draw_board(view, "dmxbox on a perfboard: the parts side",
                   "Seen from above, the USB socket of the Pico at the top. 70 × 90 mm, 26 × 31 holes.")
        draw_tracks(view, tracks, faint=True)
        draw_links(view, parts_side=True)                 # under the parts: the wire lies on the board
        draw_parts(view, outline_only=False)
        draw_pads(view, tracks, colored=False)
    draw_legend(view, 60 + BOARD_W * PITCH + 50, 130, solder_side)
    svg.save(path)


# =====================================================================================
# The lists
# =====================================================================================
def runs_of(pairs):
    """A net's tracks as straight runs: lists of the pads where a run starts, turns and ends."""
    neighbours = {}
    for pair in pairs:
        a, b = tuple(pair)
        neighbours.setdefault(a, set()).add(b)
        neighbours.setdefault(b, set()).add(a)
    ends = sorted(pad for pad, others in neighbours.items() if len(others) != 2) or sorted(neighbours)[:1]
    seen, runs = set(), []
    for start in ends:
        for nxt in sorted(neighbours[start]):
            if frozenset((start, nxt)) in seen:
                continue
            run, previous, pad = [start], start, nxt
            while True:
                seen.add(frozenset((previous, pad)))
                run.append(pad)
                onward = [other for other in neighbours[pad] if other != previous]
                if len(neighbours[pad]) != 2 or frozenset((pad, onward[0])) in seen:
                    break
                previous, pad = pad, onward[0]
            corners = [run[0]] + [b for a, b, c in zip(run, run[1:], run[2:])
                                  if (b[0] - a[0], b[1] - a[1]) != (c[0] - b[0], c[1] - b[1])] + [run[-1]]
            runs.append(corners)
    return runs


def lists_markdown(tracks):
    hole_of = lambda pin: shown(LEGS[pin])                # noqa: E731
    lines = [
        "# dmxbox on a perfboard: the lists",
        "",
        "Written by `hardware/pico/schematic-1-universe/perfboard.py`. Do not change it by hand: change the plan in the script and run it again.",
        "",
        "A hole is written with a dash: `N-3` is column N, row 3. (Without the dash, names like C1, R3 or U4",
        "would be both a hole and a part.)",
        "",
        "## Parts and their holes",
        "",
        "| Part | What | Holes |",
        "|---|---|---|",
        f"| U1 | Raspberry Pi Pico on two 20-way socket strips | pins 1 to 20 in {hole_of('U1.1')} to {hole_of('U1.20')}, "
        f"pins 40 to 21 in {hole_of('U1.40')} to {hole_of('U1.21')} |",
        f"| U3 | 6N137 in a DIP-8 socket, notch towards row 1 | pins 1 to 4 in {hole_of('U3.1')} to {hole_of('U3.4')}, "
        f"pins 8 to 5 in {hole_of('U3.8')} to {hole_of('U3.5')} |",
        f"| U5 | SN75176BP or MAX485 in a DIP-8 socket, notch towards row 1 | pins 1 to 4 in {hole_of('U5.1')} to "
        f"{hole_of('U5.4')}, pins 8 to 5 in {hole_of('U5.8')} to {hole_of('U5.5')} |",
        "| U4 | RKK-0505S/H in a 7-way socket strip | "
        + ", ".join(f"pin {n} in {hole_of(f'U4.{n}')}" for n in (1, 2, 5, 7))
        + "; the strip's other three contacts are in "
        + ", ".join(shown(hole) for hole in EMPTY_CONTACTS.values()) + " and join nothing |",
    ]
    for ref, (kind, value, _, _) in TWO_LEGS.items():
        first, second = (".+", ".-") if kind == "electrolytic" else (".1", ".2")
        what = {"resistor": "resistor", "ceramic": "ceramic capacitor", "electrolytic": "electrolytic capacitor",
                "diode": "protection diode"}[kind]
        plus = "plus (the long leg) in " if kind == "electrolytic" else ""
        lines.append(f"| {ref} | {value} {what} | {plus}{hole_of(ref + first)} ({net_of(ref + first)}) and "
                     f"{hole_of(ref + second)} ({net_of(ref + second)}) |")
    lines.append(f"| J1 | screw terminal, 3 poles, to the XLR jack | {hole_of('J1.2')} = XLR pin 2 (Data −), "
                 f"{hole_of('J1.3')} = XLR pin 3 (Data +), {hole_of('J1.1')} = XLR pin 1 (common) |")
    for ref, net in BUTTONS.items():
        lines.append(f"| {ref} | button, {BUTTON_JOB[ref]} | {hole_of(ref + '.net')} ({net}) and {hole_of(ref + '.gnd')} (GND); "
                     f"the other two legs in {hole_of(ref + '.a')} and {hole_of(ref + '.b')} join nothing |")
    lines += ["", "## Insulated wire", "", "| Wire | From | To | What it does |", "|---|---|---|---|"]
    for i, (net, a, b, _, what) in enumerate(LINKS, start=1):
        lines.append(f"| W{i} | {shown(a)} | {shown(b)} | {what} |")
    lines += ["", "## Tracks on the solder side", "",
              "Each run goes in straight lines from hole to hole; every pad on the way belongs to it.", "",
              "| Net | What it is | Runs |", "|---|---|---|"]
    for net, (_, what) in NET_INFO.items():
        runs = "; ".join(" → ".join(shown(name(pad)) for pad in run) for run in runs_of(tracks[net]))
        lines.append(f"| {net} | {what} | {runs} |")
    return "\n".join(lines) + "\n"


def main():
    tracks = find_tracks()
    if tracks is None:
        print("No way found for the tracks: the parts have to be placed differently.")
        return 1
    problems = check(tracks)
    if problems:
        print("The perfboard plan is wrong:")
        for problem in problems:
            print("  -", problem)
        return 1
    pads = sum(len(pairs) for pairs in tracks.values())
    print(f"perfboard plan: {len(LEGS)} legs, {len(LINKS)} wire link(s), {pads} track steps, nets match the schematic")
    if "--selftest" in sys.argv:
        missed = selftest(tracks)
        print("selftest:", "every planted mistake was noticed" if not missed else f"NOT noticed: {missed}")
        return 1 if missed else 0
    if "--text" in sys.argv:
        print(as_text(tracks))

    names = ("dmxbox_perfboard", "dmxbox_perfboard_solder")
    draw(HERE / f"{names[0]}.svg", tracks, solder_side=False)
    draw(HERE / f"{names[1]}.svg", tracks, solder_side=True)
    (HERE / "dmxbox_perfboard.md").write_text(lists_markdown(tracks), encoding="utf-8")
    try:
        import resvg_py
        from PIL import Image
    except ImportError:
        print("SVG and list written. For the PNG and PDF files run it with: --with resvg-py --with pillow")
        return 0
    pages = []
    for file in names:
        png = HERE / f"{file}.png"
        png.write_bytes(bytes(resvg_py.svg_to_bytes(svg_path=str(HERE / f"{file}.svg"), zoom=2.0)))
        pages.append(Image.open(png).convert("RGB"))
    pages[0].save(HERE / "dmxbox_perfboard.pdf", save_all=True, append_images=pages[1:], resolution=200.0)
    print("wrote the SVG, PNG, PDF and list files to", HERE)
    return 0


def as_text(tracks):
    """The board as letters, for a quick look: one letter per net, capital where a leg sits."""
    letters = {net: letter for net, letter in zip(PINS, "vg3ldxy5ziab")}
    cells = {}
    for net, pairs in tracks.items():
        for pair in pairs:
            for pad in pair:
                cells[pad] = letters[net]
    for pin, hole in LEGS.items():
        net = next((n for n, pins in PINS.items() if pin in pins), None)
        cells[at(hole)] = letters[net].upper() if net else "#"
    for hole in EMPTY_CONTACTS.values():
        cells[at(hole)] = "#"
    for net, a, b, *_ in LINKS:
        cells[at(a)] = cells[at(b)] = "@"
    lines = ["    " + " ".join(COLUMNS)]
    for row in range(1, ROWS + 1):
        lines.append(f"{row:3d} " + " ".join(cells.get((column, row), ".") for column in range(26)))
    lines.append("  " + ", ".join(f"{letter}={net}" for net, letter in letters.items()) + ", #=free leg, @=wire link end")
    return "\n".join(lines)


if __name__ == "__main__":
    sys.exit(main())
