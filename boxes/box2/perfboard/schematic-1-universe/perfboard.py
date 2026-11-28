"""Plans dmxbox 2 with one output on a perfboard: where every part goes and which pads are joined.
The places are those of the box with two outputs (../schematic-2-universes), without output 2.

Run from the repository root:

    uv run --no-project --python 3.13 --with resvg-py --with pillow python boxes/box2/perfboard/schematic-1-universe/perfboard.py

It writes dmxbox2_1_universe_perfboard.svg/.png (the parts side), dmxbox2_1_universe_perfboard_solder.svg/.png
(the solder side, as you see it with the board turned over), dmxbox2_1_universe_perfboard.pdf (both) and
dmxbox2_1_universe_perfboard.md (the lists: parts, insulated wires, tracks) next to this file.

The board is 90 x 150 mm with 33 x 57 holes, every hole with a pad of its own: columns A to Z
and AA to AG, rows 1 to 57. A hole is written with a dash ("AB-12"), because names like C1 or
R13 are also parts. The logic side is at the left, the isolated DMX side at the right, and the
two columns S and T between them stay empty: only the optocouplers and the converter reach across.
The left edge is cut off 2 mm left of column A, so that the keys of SW1, the box number, reach the
wall of the box; screws hold the board in holes drilled out at B-1 and B-57 and in its right corners.

How the plan is made: the places of the parts are chosen by hand (below). The tracks are found by
the script: for every net of circuit.py it looks for the shortest way from pad to pad that
touches nothing else, and the nets negotiate for pads they both want. Where a net cannot get
through on the solder side, it goes over the top in an insulated wire, which costs as much as a
long track, so there are as few wires as the plan allows. Then a separate check goes over the
result, and a mistake stops the script with a message: every leg in a hole of its own, the nets
exactly as in circuit.py (whichever way the buttons are joined inside), nothing joined to a pin
that must stay free, the two sides apart, no two parts in each other's way, no wire over a leg or
under a socket. --selftest breaks the plan on purpose and confirms that the check notices.
"""
from __future__ import annotations

import functools
import heapq
import itertools
import random
import sys
from collections import Counter
from dataclasses import dataclass
from pathlib import Path

from circuit import BUTTONS, FREE_PINS, ISOLATED_NETS, NETS, PARTS, check_list
from svg import GREY, INK, Svg, to_png_and_pdf

HERE = Path(__file__).resolve().parent
TITLE = "dmxbox 2 with one output"
FILE = "dmxbox2_1_universe_perfboard"
SCRIPT = "boxes/box2/perfboard/schematic-1-universe/perfboard.py"
COLUMNS = [chr(ord("A") + i) for i in range(26)] + ["A" + chr(ord("A") + i) for i in range(7)]   # A .. Z, AA .. AG
ROWS = 57
GAP = ("S", "T")                     # the two empty columns between the logic side and the isolated side
GAP_FIRST = COLUMNS.index(GAP[0])


def at(hole):
    """'AB12' -> (27, 12): the column as a number from 0, and the row."""
    letters = "".join(ch for ch in hole if ch.isalpha())
    return COLUMNS.index(letters), int(hole[len(letters):])


def name(cell):
    return f"{COLUMNS[cell[0]]}{cell[1]}"


def shown(hole):
    """How a hole is written for people: AB-12."""
    letters = "".join(ch for ch in hole if ch.isalpha())
    return f"{letters}-{hole[len(letters):]}"


def col(letters):
    return COLUMNS.index(letters)


# =====================================================================================
# Where the parts go. Every leg: the hole it is soldered into.
# =====================================================================================
LEGS = {}


def vdip(ref, left, top, pins, notch="up"):
    """A DIP standing upright: its two rows of legs in the columns `left` and `left` + 3.
    Notch up: pins 1 .. n/2 down the left column, the others back up the right one.
    Notch down (turned round): pins 1 .. n/2 up the right column, the others down the left one."""
    half = pins // 2
    c = col(left)
    for k in range(1, half + 1):
        if notch == "up":
            LEGS[f"{ref}.{k}"] = name((c, top + k - 1))
            LEGS[f"{ref}.{pins + 1 - k}"] = name((c + 3, top + k - 1))
        else:
            LEGS[f"{ref}.{k}"] = name((c + 3, top + half - k))
            LEGS[f"{ref}.{half + k}"] = name((c, top + k - 1))


def part(ref, first, second):
    names = ("A", "K") if ref == "D1" else ("1", "2")
    LEGS[f"{ref}.{names[0]}"], LEGS[f"{ref}.{names[1]}"] = first, second


def column_of(ref, letter, top, pins, step=1):
    for i, pin in enumerate(pins):
        if pin is not None:
            LEGS[f"{ref}.{pin}"] = name((col(letter), top + i * step))


# ---- across the gap: the converter and the six optocouplers, logic pins in R, isolated pins in U
# U3 is a SIP-7 with its positions 1 to 7 in Q to W: positions 3 and 4 have no pin and sit over the gap.
LEGS.update({"U3.1": "Q24", "U3.2": "R24", "U3.5": "U24", "U3.7": "W24"})
EMPTY_CONTACTS = {"U3 position 3": "S24", "U3 position 4": "T24", "U3 position 6": "V24"}
vdip("U4", "R", 8, 8, "up")           # send 1: LED (pins 2, 3) on the logic side
vdip("U6", "R", 13, 4, "up")          # direction 1
vdip("U5", "R", 16, 8, "down")        # receive 1: LED on the isolated side
# (the box with two outputs has U8, U9 and U10 of output 2 below the converter)

# ---- the microcontroller, upright, its outputs towards the gap
vdip("U1", "H", 12, 28, "up")         # pins 1-14 down column H, 28-15 down column K

# ---- the logic side
# The box number switch is a piano DIP switch at the left edge: its keys stick out sideways towards the
# edge and a slot in the box, so the number can be set from outside. Row 19 stays free below it, for
# the lines to the PIC's pins 11 and 12 to get past the crystal and its capacitors.
vdip("SW1", "A", 11, 16, "up")        # pins 1-8 down A (to GND), 16-9 down D (the box number)
# The USB-C breakout at the bottom edge, straight below the PIC: D- and D+ come down under the chip
# in the columns I and J and go on straight to it.
LEGS.update({f"J1.{pin}": f"{letter}55" for pin, letter in
             zip(["VBUS", "D-", "D+", "CC1", "CC2", "GND"], ["H", "I", "J", "K", "L", "M"])})
column_of("U2", "G", 47, ["1", "2", "3"])                             # MCP1700, flat side to the left
column_of("J2", "B", 3, ["1", "2", "3", "4", "5", "6"])               # programming header at the top left
part("R1", "K53", "K49")
part("R2", "L53", "L49")
part("C1", "F44", "F46")
part("C2", "F49", "F51")
part("R3", "F4", "F8")
part("C9", "G8", "G10")
part("R4", "F9", "H9")
part("C3", "F25", "F27")
part("C4", "M10", "M12")
part("C5", "M17", "M19")
part("C6", "M20", "M22")
part("Y1", "F20", "F22")
part("C7", "E20", "C20")              # the crystal's capacitors to the left of it: the PIC's pins
part("C8", "E22", "C22")              # 11 to 14 (G-22 to G-25) stay free to leave to the left
part("R5", "H27", "H31")
part("D1", "H32", "H33")
part("R6", "O8", "O12")
part("R9", "P19", "P23")
part("R10", "N14", "Q14")
part("C10", "P26", "R26")
part("C13", "Q18", "Q16")             # each leg beside the pin of U5 with the same net
SWITCHES = {"SW2": ("B", "G", 30), "SW3": ("B", "G", 36)}   # left column, right column, upper row
for _ref, (_left, _right, _row) in SWITCHES.items():
    LEGS[f"{_ref}.a"] = f"{_left}{_row}"
    LEGS[f"{_ref}.net"] = f"{_right}{_row}"
    LEGS[f"{_ref}.gnd"] = f"{_left}{_row + 2}"
    LEGS[f"{_ref}.b"] = f"{_right}{_row + 2}"

# ---- the isolated side: the line driver, the line, the screw terminal of the XLR wires
vdip("U7", "Y", 12, 8, "up")
part("C11", "U26", "W26")
part("C12", "V9", "V11")
part("C14", "AC10", "AC12")
part("R7", "W8", "W10")
part("R8", "V22", "V18")
part("R11", "W13", "W17")
for _n, _top in ((1, 12),):
    _p = {1: dict(up="R12", term="R13", down="R14", da="D2", db="D3", xlr="J3"),
          2: dict(up="R21", term="R22", down="R23", da="D4", db="D5", xlr="J4")}[_n]
    part(_p["term"], f"AD{_top + 3}", f"AD{_top + 7}")
    part(_p["up"], f"AE{_top}", f"AE{_top + 4}")
    part(_p["down"], f"AE{_top + 6}", f"AE{_top + 10}")
    part(_p["da"], f"AD{_top + 1}", f"AD{_top - 4}")   # Data + below, GND2 above: towards the terminal
    part(_p["db"], f"AD{_top + 9}", f"AD{_top + 14}")
    # The screw terminal, above the driver: its body reaches 5.2 mm behind the pins, over the columns
    # AE and AF, so it stands where those are free. XLR pins 1, 3, 2 from the top.
    LEGS[f"{_p['xlr']}.1"] = f"AG{_top - 8}"
    LEGS[f"{_p['xlr']}.3"] = f"AG{_top - 6}"
    LEGS[f"{_p['xlr']}.2"] = f"AG{_top - 4}"

# The board's left edge is cut off 2 mm left of the middle of column A's holes, so that SW1's keys
# reach the wall of the box; its left corners go with it. Four screws hold the board: two in holes
# drilled out at B-1 and B-57, two in the right corners. The holes round each screw stay empty: the
# drill takes the pads beside it, and the screw's head covers the ones on the corners.
CUT_LEFT = 2.0                        # mm, from the middle of column A's holes
DRILLED = ("B1", "B57")                # drilled out to 3.2 mm for the two screws at the left
KEEP_FREE = {name((c, r)) for screw in DRILLED + ("AG1", "AG57")
             for c in range(at(screw)[0] - 1, at(screw)[0] + 2) for r in range(at(screw)[1] - 1, at(screw)[1] + 2)
             if 0 <= c < len(COLUMNS) and 1 <= r <= ROWS}

# Legs that belong to a net: circuit.py, plus the button corners.
PINS = {net: list(pins) for net, pins in NETS.items()}
for _ref, _net in BUTTONS.items():
    PINS[_net].append(f"{_ref}.net")
    PINS["GND"].append(f"{_ref}.gnd")
NET_OF = {pin: net for net, pins in PINS.items() for pin in pins}


# =====================================================================================
# The rules of the board
# =====================================================================================
STEPS = ((1, 0), (-1, 0), (0, 1), (0, -1))
SOCKETS = ("U1", "U4", "U5", "U6", "U7", "U8", "U9", "U10", "U11", "SW1")
TWO_LEGS = {"R": (1, 2, 3, 4), "C": (2,), "Y": (2,), "D": (5,)}   # lead spacings, in holes, by kind
LED_SPAN = 1


def side_of(net):
    return "isolated" if net in ISOLATED_NETS else "logic"


def allowed(cell, net):
    column, row = cell
    if not (0 <= column < len(COLUMNS) and 1 <= row <= ROWS):
        return False
    if COLUMNS[column] in GAP:
        return False
    return (column < GAP_FIRST) == (side_of(net) == "logic")


def two_leg_ends(ref, legs):
    first, second = ("A", "K") if ref == "D1" else ("1", "2")
    return at(legs[f"{ref}.{first}"]), at(legs[f"{ref}.{second}"])


def standing(ref, a, b):
    """Resistors with their legs 1 or 2 holes apart stand upright, the body over their first leg."""
    return ref[0] == "R" and abs(a[0] - b[0]) + abs(a[1] - b[1]) <= 2


@functools.lru_cache(maxsize=None)
def between(a, b):
    """The holes on the straight line from a to b, without a and b."""
    step = ((b[0] > a[0]) - (b[0] < a[0]), (b[1] > a[1]) - (b[1] < a[1]))
    length = max(abs(b[0] - a[0]), abs(b[1] - a[1]))
    return tuple((a[0] + step[0] * k, a[1] + step[1] * k) for k in range(1, length))


def body_cells(legs):
    """The holes each part covers on the parts side, close to the board, where no other part may
    stand: a socket its whole rectangle, a button its 12 mm square, a screw terminal the two columns
    behind its pins as well, the USB-C breakout everything from its header to the edge, the regulator
    the column of its round back. Lying resistors, diodes, capacitors and the crystal cover the holes
    from leg to leg; a standing resistor only its two holes."""
    by_ref = {}
    for pin, hole in legs.items():
        by_ref.setdefault(pin.split(".")[0], []).append(at(hole))
    cells = {}
    for ref, spots in by_ref.items():
        c0, c1 = min(c for c, _ in spots), max(c for c, _ in spots)
        r0, r1 = min(r for _, r in spots), max(r for _, r in spots)
        if ref in SWITCHES:
            r0, r1 = r0 - 1, r1 + 1
        elif ref == "J1" and r0 == r1:              # the breakout from its row of pins to the bottom edge
            c0, c1, r0, r1 = c0 - 1, c1 + 1, r0 - 1, ROWS
        elif ref == "J1":                           # ... or from its column of pins to the left edge
            c0, r0, r1 = 0, r0 - 1, r1 + 1
        elif ref in ("J3", "J4"):
            c0, r0, r1 = c0 - 2, r0 - 1, r1 + 1
        elif ref == "U2":
            c1 = c1 + 1
        elif ref in PARTS and ref[0] in "RCDY" and ref[1:].isdigit():
            a, b = two_leg_ends(ref, legs)
            cells[ref] = {a, b} if standing(ref, a, b) else {a, b} | set(between(a, b))
            continue
        cells[ref] = {(c, r) for c in range(c0, c1 + 1) for r in range(r0, r1 + 1)}
    return cells


def wire_block(legs):
    """Holes an insulated wire may neither be laid over nor end in: every hole with a leg (the leg
    stands in the way) and every hole under a part that sits flat on the board: sockets, the
    converter's strip, buttons, terminals, the USB-C breakout. Wires may pass under lying resistors,
    diodes and capacitors: those go in after the wires."""
    blocked = {at(hole) for hole in legs.values()} | {at(hole) for hole in EMPTY_CONTACTS.values()}
    blocked |= {at(hole) for hole in KEEP_FREE}
    for ref, cells in body_cells(legs).items():
        if not (ref in PARTS and ref[0] in "RCDY" and ref[1:].isdigit()):
            blocked |= cells
    return blocked


# =====================================================================================
# Finding the tracks, on two layers
# =====================================================================================
# The bottom layer: tracks of joined pads on the solder side. The top layer on the perfboard:
# insulated wires on the parts side, laid from hole to hole and bent where they have to; on a
# printed board (pcb.py) the copper of the top side. Where a net changes layer there is a "via":
# on the perfboard the end of a wire, soldered into a free hole whose pad the track goes on from;
# on a printed board a plated hole.
BOTTOM, TOP = 0, 1
BEND = 0.4
INF = float("inf")


@dataclass(frozen=True)
class Mode:
    """How the two layers may be used."""
    name: str
    via_cost: float             # one change of layer
    top_step: float             # one hole on the top layer, where a hole on the bottom costs 1
    pins_on_top: bool           # the legs are pads on the top layer too (a printed board's plated holes)
    top_branches: bool          # the top layer may branch anywhere (copper) or not at all (a wire)
    blocked_top: frozenset      # holes the top layer may not use and no via may be in


def perfboard_mode(legs):
    # A wire has two ends: soldering them costs about as much as 30 holes of track.
    return Mode("perfboard", via_cost=15.0, top_step=1.2, pins_on_top=False, top_branches=False,
                blocked_top=frozenset(wire_block(legs)))


def merge(pieces):
    merged = []
    for piece in pieces:
        piece = set(piece)
        for other in [m for m in merged if m & piece]:
            piece |= other
            merged.remove(other)
        merged.append(piece)
    return merged


def find_way(starts, goals, price, net, mode, fixed, avoid=frozenset()):
    """The cheapest way from a state of `starts` to one of `goals` (A*, with the distance to the box
    around the goals as the estimate). A state is (hole, layer). A step goes to a neighbouring hole
    on the same layer, a via to the other layer in the same hole. `price(hole, layer)` is what
    taking that hole on that layer costs, or None. `avoid`: top holes this net uses already."""
    goal_cells = [cell for cell, _ in goals]
    x0, x1 = min(c for c, _ in goal_cells), max(c for c, _ in goal_cells)
    y0, y1 = min(r for _, r in goal_cells), max(r for _, r in goal_cells)

    def estimate(cell):                         # never more than the real price: a step costs 1 or more
        return max(0, x0 - cell[0], cell[0] - x1) + max(0, y0 - cell[1], cell[1] - y1)

    order = itertools.count()
    queue = [(estimate(cell), next(order), 0.0, cell, layer, None) for cell, layer in starts]
    best = {(cell, layer, None): 0.0 for cell, layer in starts}
    came = {}
    while queue:
        _, _, cost, cell, layer, heading = heapq.heappop(queue)
        if best.get((cell, layer, heading), INF) < cost:
            continue
        if (cell, layer) in goals and (cell, layer) not in starts:
            way = [(cell, layer)]
            state = (cell, layer, heading)
            while state in came:
                state = came[state]
                way.append(state[:2])
            return way[::-1]

        def offer(nxt, nxt_layer, nxt_heading, new):
            key = (nxt, nxt_layer, nxt_heading)
            if new < best.get(key, INF):
                best[key] = new
                came[key] = (cell, layer, heading)
                heapq.heappush(queue, (new + estimate(nxt), next(order), new, nxt, nxt_layer, nxt_heading))

        for step in STEPS:
            nxt = (cell[0] + step[0], cell[1] + step[1])
            if not allowed(nxt, net):
                continue
            if layer == TOP and (nxt in mode.blocked_top or nxt in avoid):
                continue
            taking = price(nxt, layer)
            if taking is None:
                continue
            factor = mode.top_step if layer == TOP else 1.0
            offer(nxt, layer, step, cost + taking * factor + (BEND if heading not in (None, step) else 0.0))

        other = TOP if layer == BOTTOM else BOTTOM
        if fixed.get(cell) == net and mode.pins_on_top:   # a plated hole joins both layers already
            extra = 0.0
        elif cell in fixed or cell in mode.blocked_top or cell in avoid:
            continue                                       # no via in a hole with a leg, or under a part
        else:
            extra = mode.via_cost
        taking = price(cell, other)
        if taking is not None:
            offer(cell, other, None, cost + extra + taking)
    return None


def join_net(net, price, legs, fixed, mode):
    """Joins all pins of one net, growing from its first pin: each time the cheapest way from what
    is joined to the nearest other piece. Returns (bottom pairs, top pairs, vias), or None when
    there is no way. On the perfboard a new way never starts from a wire or joins one in its middle:
    a wire has its two ends and nothing else."""
    layers = (BOTTOM, TOP) if mode.pins_on_top else (BOTTOM,)
    pieces = merge([{(at(legs[pin]), layer) for layer in layers} for pin in PINS[net]])
    tree, others = pieces[0], pieces[1:]
    bottom, top, vias = set(), set(), set()
    own_top = set()
    while others:
        goals = set().union(*others)
        if mode.top_branches:
            starts, ends, avoid = tree, goals, frozenset()
        else:
            starts = {state for state in tree if state[1] == BOTTOM}
            ends = {state for state in goals if state[1] == BOTTOM}
            avoid = frozenset(own_top)
        way = find_way(starts, ends, price, net, mode, fixed, avoid)
        if way is None:
            return None
        for (a, la), (b, lb) in zip(way, way[1:]):
            if a == b:
                vias.add(a)
            elif la == BOTTOM:
                bottom.add(frozenset((a, b)))
            else:
                top.add(frozenset((a, b)))
        own_top |= {cell for cell, layer in way if layer == TOP}
        reached = [piece for piece in others if way[-1] in piece]
        tree = tree | set(way) | set().union(*reached)
        others = [piece for piece in others if piece not in reached]
    return bottom, top, vias


def slots_of(route):
    """The (hole, layer) places a route takes."""
    bottom, top, vias = route
    slots = {(pad, BOTTOM) for pair in bottom for pad in pair} | {(pad, TOP) for pair in top for pad in pair}
    return slots | {(cell, layer) for cell in vias for layer in (BOTTOM, TOP)}


def wires_in(route):
    return len(route[2]) // 2 if route[2] else 0


def find_tracks(legs=None, mode=None, rounds=80, report=None, orders=(0, "reversed", 7, 19)):
    """Tracks for all nets, or None. The nets negotiate (see negotiate); when they cannot agree
    within the rounds, they start again in another order: the list of nets the other way round, or
    turned round by a few places. Which net chooses first changes where the negotiation goes."""
    for order in orders:
        routes = negotiate(legs, mode, rounds, report, order)
        if routes is not None:
            return routes
    return None


def negotiate(legs, mode, rounds, report, order=0):
    """Tracks for all nets, or None. The nets negotiate: a place wanted by two nets gets dearer every
    round, until no place is shared. Then every net with a via is laid again around the others,
    first on the bottom alone, then with as few vias as it can."""
    legs = LEGS if legs is None else legs
    mode = perfboard_mode(legs) if mode is None else mode
    fixed = {}
    for pin, hole in legs.items():
        fixed[at(hole)] = NET_OF.get(pin) or f"free:{pin}"
    for what, hole in EMPTY_CONTACTS.items():
        fixed[at(hole)] = f"free:{what}"
    for hole in KEEP_FREE:
        fixed[at(hole)] = "free:screw"
    nets = list(PINS)[::-1] if order == "reversed" else list(PINS)[order:] + list(PINS)[:order]
    routes = {net: (set(), set(), set()) for net in nets}
    memory = {}
    pressure = 0.5

    def price_for(net, hard, top=True):
        taken = Counter()
        for other in nets:
            if other != net:
                taken.update(slots_of(routes[other]))
        now = pressure

        def price(cell, layer):
            if layer == TOP and not top:
                return None
            owner = fixed.get(cell)
            if owner is not None:
                if owner != net or (layer == TOP and not mode.pins_on_top):
                    return None
                return 1.0
            key = (cell, layer)
            if hard:
                return None if key in taken else 1.0
            return (1.0 + memory.get(key, 0.0)) * (1.0 + now * taken.get(key, 0))
        return price

    def shared():
        claims = Counter()
        for net in nets:
            claims.update(slots_of(routes[net]))
        return [slot for slot, count in claims.items() if count > 1]

    for round_number in range(rounds):
        for net in nets:
            route = join_net(net, price_for(net, hard=False), legs, fixed, mode)
            if route is None:
                if report is not None:
                    report.append(f"no way at all for {net}")
                return None
            routes[net] = route
        clashes = shared()
        if report is not None and (round_number % 5 == 0 or not clashes):
            report.append(f"round {round_number}: {len(clashes)} places shared, "
                          f"{sum(len(r[2]) for r in routes.values())} vias")
        if not clashes:
            break
        for slot in clashes:
            memory[slot] = memory.get(slot, 0.0) + 1.0
        pressure *= 1.3
    else:
        if report is not None:
            report.append(f"still shared after {rounds} rounds: {len(shared())} places")
        return None

    def size(route):
        return len(route[2]), len(route[0]) + len(route[1])

    rng = random.Random(7)
    for pass_number in range(6):                  # fewer vias: each net laid again around all the others
        changed = False
        for net in rng.sample(nets, len(nets)):
            if not routes[net][2]:
                continue
            best = routes[net]
            for top in (False, True):             # first on the bottom alone, then with the top as well
                route = join_net(net, price_for(net, hard=True, top=top), legs, fixed, mode)
                if route is not None and size(route) < size(best):
                    best = route
                    break
            changed |= best is not routes[net]
            routes[net] = best
            if not routes[net][2]:
                continue
            # the net and one other one lifted together: this one laid first, if it can be, on the
            # bottom alone, then the other one around it; kept if the two need fewer vias than before
            for other in rng.sample(nets, len(nets)):
                if other == net or not routes[net][2]:
                    continue
                saved = routes[net], routes[other]
                routes[net] = routes[other] = (set(), set(), set())
                first = join_net(net, price_for(net, hard=True, top=False), legs, fixed, mode)
                second = None
                if first is not None:
                    routes[net] = first
                    second = join_net(other, price_for(other, hard=True), legs, fixed, mode)
                if second is not None and len(first[2]) + len(second[2]) < len(saved[0][2]) + len(saved[1][2]):
                    routes[other] = second
                    changed = True
                else:
                    routes[net], routes[other] = saved
        if report is not None:
            report.append(f"fewer vias, pass {pass_number}: {sum(len(r[2]) for r in routes.values())} vias")
        if not changed:
            break
    return routes


def boxed_in(legs=None):
    """Pins whose pad has no neighbour a track of their net could use: something to move."""
    legs = LEGS if legs is None else legs
    owner = {at(hole): NET_OF.get(pin, "free") for pin, hole in legs.items()}
    owner.update({at(hole): "free" for hole in EMPTY_CONTACTS.values()})
    owner.update({at(hole): "free" for hole in KEEP_FREE})
    found = []
    for pin, hole in legs.items():
        net = NET_OF.get(pin)
        if net is None or len(PINS[net]) < 2:
            continue
        c, r = at(hole)
        ways = [(c + dc, r + dr) for dc, dr in STEPS]
        if not any(allowed(w, net) and owner.get(w, net) == net for w in ways):
            found.append(f"{pin} ({net}) in {shown(hole)} is boxed in")
    return found


# =====================================================================================
# The check. It does not trust the track finder: it starts again from the pads.
# =====================================================================================
def wires_of(route):
    """The insulated wires of a perfboard route: each a list of holes from one via to the other."""
    _, top, vias = route
    neighbours = {}
    for pair in top:
        a, b = tuple(pair)
        neighbours.setdefault(a, set()).add(b)
        neighbours.setdefault(b, set()).add(a)
    wires, seen = [], set()
    for start in sorted(vias):
        if start in seen or start not in neighbours:
            continue
        path, previous, cell = [start], None, start
        while True:
            onward = sorted(neighbours[cell] - {previous})
            if not onward or (cell in vias and cell != start):
                break
            previous, cell = cell, onward[0]
            path.append(cell)
        seen |= {path[0], path[-1]}
        wires.append(path)
    return wires


def check(routes, legs=None, mode=None):
    legs = LEGS if legs is None else legs
    mode = perfboard_mode(legs) if mode is None else mode
    problems = []
    used = {}
    for who, hole in list(legs.items()) + list(EMPTY_CONTACTS.items()):
        cell = at(hole)
        if not (0 <= cell[0] < len(COLUMNS) and 1 <= cell[1] <= ROWS):
            problems.append(f"{who}: there is no hole {shown(hole)}")
        if hole in used:
            problems.append(f"{who} and {used[hole]} share hole {shown(hole)}")
        used[hole] = who
    for hole in sorted(KEEP_FREE & set(used)):
        problems.append(f"{used[hole]} stands where a screw goes, at {shown(hole)}")
    for ref in PARTS:
        if ref[0] in "RCDY" and ref[1:].isdigit():
            a, b = two_leg_ends(ref, legs)
            span = abs(a[0] - b[0]) + abs(a[1] - b[1])
            if (a[0] != b[0] and a[1] != b[1]) or span not in ((LED_SPAN,) if ref == "D1" else TWO_LEGS[ref[0]]):
                problems.append(f"{ref}: its legs in {shown(name(a))} and {shown(name(b))} do not suit the part")
    covered = {}
    for ref, cells in sorted(body_cells(legs).items()):
        for cell in sorted(cells):
            if cell in covered:
                problems.append(f"{ref} and {covered[cell]} stand in each other's way at {shown(name(cell))}")
            covered[cell] = ref
    keep = {at(hole) for hole in KEEP_FREE}
    for ref, cells in sorted(body_cells(legs).items()):
        for cell in sorted(cells & keep):
            problems.append(f"{ref} stands where a screw goes, at {shown(name(cell))}")

    listed = set(NET_OF)
    for pin in listed - set(legs):
        problems.append(f"{pin} is in the net list but not on the board")
    leg_cells = {at(hole): NET_OF.get(pin) for pin, hole in legs.items()}
    leg_cells.update({at(hole): None for hole in EMPTY_CONTACTS.values()})
    places = {}
    for net, route in routes.items():
        bottom, top, vias = route
        for layer, pairs in ((BOTTOM, bottom), (TOP, top)):
            for pair in pairs:
                a, b = sorted(pair)
                if abs(a[0] - b[0]) + abs(a[1] - b[1]) != 1:
                    problems.append(f"{net}: {shown(name(a))} and {shown(name(b))} are no neighbours")
        for slot in slots_of(route):
            cell, layer = slot
            if not allowed(cell, net):
                problems.append(f"{net} uses {shown(name(cell))}, which is off the board, in the gap or on the other side")
            if slot in places and places[slot] != net:
                problems.append(f"{shown(name(cell))} carries both {places[slot]} and {net}"
                                f"{' on the top' if layer == TOP else ''}")
            places[slot] = net
            if cell in keep:
                problems.append(f"{net} uses {shown(name(cell))}, which stays empty for a screw")
            if cell in leg_cells and leg_cells[cell] != net:
                problems.append(f"a track of {net} runs into {'a free leg' if leg_cells[cell] is None else 'the leg of ' + leg_cells[cell]}"
                                f" at {shown(name(cell))}")
            if layer == TOP and not mode.pins_on_top and cell in mode.blocked_top:
                problems.append(f"a wire of {net} lies over {shown(name(cell))}, where a leg or a part is")
        for cell in vias:
            if cell in leg_cells and not mode.pins_on_top:
                problems.append(f"a wire of {net} ends in the hole of a leg at {shown(name(cell))}")
        if not mode.top_branches:
            degree = Counter()
            for pair in top:
                degree.update(pair)
            for cell, count in degree.items():
                if count > 2 or (count == 1) != (cell in vias):
                    problems.append(f"a wire of {net} branches or ends without a solder joint at {shown(name(cell))}")

    for model in ("legs in the same row are joined", "legs in the same column are joined"):
        parent = {}

        def find(item):
            parent.setdefault(item, item)
            while parent[item] != item:
                parent[item] = parent[parent[item]]
                item = parent[item]
            return item

        def join(a, b):
            parent[find(a)] = find(b)

        for net, (bottom, top, vias) in routes.items():
            for pair in bottom:
                a, b = tuple(pair)
                join((a, BOTTOM), (b, BOTTOM))
            for pair in top:
                a, b = tuple(pair)
                join((a, TOP), (b, TOP))
            for cell in vias:
                join((cell, BOTTOM), (cell, TOP))
        if mode.pins_on_top:
            for hole in legs.values():
                join((at(hole), BOTTOM), (at(hole), TOP))
        for ref in SWITCHES:
            a, net, gnd, b = ((at(legs[f"{ref}.{leg}"]), BOTTOM) for leg in ("a", "net", "gnd", "b"))
            if model.startswith("legs in the same row"):
                join(a, net)
                join(gnd, b)
            else:
                join(a, gnd)
                join(net, b)
        spares = {f"{ref}.{leg}" for ref in SWITCHES for leg in "ab"}
        groups = {}
        for pin, hole in legs.items():
            if pin not in spares:
                groups.setdefault(find((at(hole), BOTTOM)), set()).add(pin)
        for net, pins in PINS.items():
            roots = {find((at(legs[pin]), BOTTOM)) for pin in pins if pin in legs}
            if len(roots) != 1:
                problems.append(f"[{model}] net {net} is in {len(roots)} pieces")
            elif groups[roots.pop()] != set(pins):
                problems.append(f"[{model}] net {net} is joined to something else")
        for pins in groups.values():
            if len(pins) > 1 and not pins & listed:
                problems.append(f"[{model}] pins that must stay free are joined: {sorted(pins)}")
        for ref, net in BUTTONS.items():
            signal, ground = find((at(legs[PINS[net][0]]), BOTTOM)), find((at(legs["U1.8"]), BOTTOM))
            corners = {find((at(legs[f"{ref}.{leg}"]), BOTTOM)) for leg in ("a", "net", "gnd", "b")}
            if signal == ground or corners != {signal, ground}:
                problems.append(f"[{model}] button {ref} does not sit between {net} and GND")
        side = {}
        nodes = {slot for route in routes.values() for slot in slots_of(route)}
        nodes |= {(at(hole), BOTTOM) for pin, hole in legs.items() if pin in listed}
        for node in nodes:
            side.setdefault(find(node), set()).add("logic" if node[0][0] < GAP_FIRST else "isolated")
        for root, sides in side.items():
            if len(sides) > 1:
                problems.append(f"[{model}] something joins the logic side to the isolated side at {shown(name(root[0]))}")
    return sorted(set(problems))


# =====================================================================================
# The drawings
# =====================================================================================
PITCH = 21.0
BOARD_W, BOARD_H = 90 / 2.54, 150 / 2.54                  # the board, in hole distances
EDGE_X, EDGE_Y = (BOARD_W - (len(COLUMNS) - 1)) / 2, (BOARD_H - (ROWS - 1)) / 2
PAPER, BOARD, SOFT = "#ffffff", "#f6f3e7", "#9ca3af"
SUPPLY_COLORS = {"+5V": "#dc2626", "GND": "#111827", "+3V3": "#f59e0b", "+5V_ISO": "#db2777", "GND2": "#2563eb"}
PALETTE = ["#7c3aed", "#0d9488", "#65a30d", "#a16207", "#0891b2", "#c026d3", "#16a34a", "#ea580c", "#4f46e5",
           "#be123c", "#0f766e", "#854d0e"]


def color_of(net):
    return SUPPLY_COLORS.get(net) or PALETTE[sorted(NETS).index(net) % len(PALETTE)]


def numbered_wires(routes):
    """All wires of the plan, numbered W1, W2 ... from the top left: (number, net, holes)."""
    wires = sorted(((path[0], net, path) for net, route in routes.items() for path in wires_of(route)),
                   key=lambda item: (item[0][1], item[0][0]))
    return [(i, net, path) for i, (_, net, path) in enumerate(wires, start=1)]


class View:
    """Turns holes into places on the drawing. Mirrored for the solder side: column A at the right."""

    def __init__(self, svg, x0, y0, mirrored):
        self.svg, self.x0, self.y0, self.mirrored = svg, x0, y0, mirrored

    def x(self, column):
        return self.x0 + (EDGE_X + ((len(COLUMNS) - 1) - column if self.mirrored else column)) * PITCH

    def y(self, row):
        return self.y0 + (EDGE_Y + row - 1) * PITCH

    def xy(self, cell):
        return self.x(cell[0]), self.y(cell[1])


def draw_board(view, title, note):
    s = view.svg
    s.text(view.x0, view.y0 - 50, title, 22, weight="bold")
    s.text(view.x0, view.y0 - 28, note, 13, fill=GREY)
    s.rect(view.x0, view.y0, BOARD_W * PITCH, BOARD_H * PITCH, fill=BOARD, stroke=INK, width=1.5, rx=6)
    # The strip that is cut off at the left edge (at the right on the solder side).
    cut = view.x(0) + (1 if view.mirrored else -1) * CUT_LEFT / 2.54 * PITCH
    edge = view.x0 + (BOARD_W * PITCH if view.mirrored else 0)
    s.rect(min(cut, edge), view.y0 + 1, abs(edge - cut), BOARD_H * PITCH - 2, fill="#fca5a5", opacity=0.55)
    s.line(cut, view.y0 - 12, cut, view.y0 + BOARD_H * PITCH + 12, "#b91c1c", 1.8, dash="7 4")
    s.text((cut + edge) / 2 + 4, view.y(40), "cut off", 10, "middle", "bold", "#991b1b", rotate=-90)
    x1, x2 = sorted((view.x(col(GAP[0])), view.x(col(GAP[1]))))
    s.rect(x1 - PITCH / 2, view.y0 + 1, x2 - x1 + PITCH, BOARD_H * PITCH - 2, fill="#fca5a5", opacity=0.33)
    s.text((x1 + x2) / 2 + 4, view.y(48), "isolation gap: nothing is soldered in these two columns", 12, "middle",
           fill="#991b1b", rotate=-90)
    for c in range(len(COLUMNS)):
        for y in (view.y0 - 6, view.y0 + BOARD_H * PITCH + 16):
            s.text(view.x(c), y, COLUMNS[c], 10, "middle", "bold", GREY)
        for r in range(1, ROWS + 1):
            s.circle(view.x(c), view.y(r), 3.0, fill=PAPER, stroke=SOFT, width=1)
    for r in range(1, ROWS + 1):
        s.text(view.x0 - 12, view.y(r) + 4, r, 10, "end", fill=GREY)
        s.text(view.x0 + BOARD_W * PITCH + 12, view.y(r) + 4, r, 10, fill=GREY)


def draw_tracks(view, routes, faint):
    for net, (bottom, _, _) in routes.items():
        for pair in bottom:
            a, b = tuple(pair)
            view.svg.line(*view.xy(a), *view.xy(b), stroke="#cbd5e1" if faint else color_of(net), width=3 if faint else 7)


def draw_wires(view, routes, parts_side):
    """The insulated wires: on the parts side the wires themselves, on the solder side their ends."""
    s = view.svg
    wires = numbered_wires(routes)
    for number, net, path in wires:
        points = [view.xy(cell) for cell in path]
        if parts_side:
            s.path(points, "white", 8.0)
            s.path(points, color_of(net), 4.5)
            mid = points[len(points) // 2] if len(points) > 2 else ((points[0][0] + points[-1][0]) / 2,
                                                                    (points[0][1] + points[-1][1]) / 2)
            s.rect(mid[0] - 13, mid[1] - 7, 26, 14, fill="white", stroke=color_of(net), width=1.3, rx=6)
            s.text(mid[0], mid[1] + 4, f"W{number}", 9.5, "middle", "bold")
        for x, y in (points[0], points[-1]):
            s.circle(x, y, 7.5, stroke=color_of(net), width=2.4, fill=color_of(net) if parts_side else "none")
            if not parts_side:
                s.text(x, y - 10, f"W{number}", 9, "middle", "bold", color_of(net), halo=True)
    return wires


def draw_pads(view, routes, colored):
    s = view.svg
    if colored:
        for net, (bottom, _, vias) in routes.items():
            for pad in {pad for pair in bottom for pad in pair} | set(vias):
                s.circle(*view.xy(pad), 5.0, fill=color_of(net))
    for pin, hole in LEGS.items():
        net = NET_OF.get(pin)
        cell = at(hole)
        if net is None:
            s.circle(*view.xy(cell), 4.0, fill=PAPER, stroke=INK, width=1.5)
        else:
            s.circle(*view.xy(cell), 6.0 if colored else 4.0, fill=color_of(net) if colored else INK)
            if colored:
                s.circle(*view.xy(cell), 2.0, fill=PAPER)
    for hole in EMPTY_CONTACTS.values():
        s.circle(*view.xy(at(hole)), 4.0, fill=PAPER, stroke=INK, width=1.5)
    for hole in DRILLED:                     # drilled out to 3.2 mm for a screw
        x, y = view.xy(at(hole))
        s.circle(x, y, 1.6 / 2.54 * PITCH, fill="#fee2e2", stroke="#b91c1c", width=1.8)
        s.line(x - 6, y - 6, x + 6, y + 6, "#b91c1c", 1.6)
        s.line(x - 6, y + 6, x + 6, y - 6, "#b91c1c", 1.6)
        s.text(x + (-20 if view.mirrored else 20), y + 4, "drill 3.2 mm", 9.5, "end" if view.mirrored else "start",
               "bold", "#991b1b", halo=True)


def outline(view, cells, pad=0.45, fill="#e5e7eb", stroke=INK, dash=None, rx=3):
    xs = [view.x(c[0]) for c in cells]
    ys = [view.y(c[1]) for c in cells]
    x1, x2, y1, y2 = min(xs) - pad * PITCH, max(xs) + pad * PITCH, min(ys) - pad * PITCH, max(ys) + pad * PITCH
    view.svg.rect(x1, y1, x2 - x1, y2 - y1, fill=fill, stroke=stroke, width=1.5, rx=rx, dash=dash)
    return x1, y1, x2 - x1, y2 - y1


def draw_parts(view, outline_only):
    s = view.svg
    stroke, dash = (SOFT, "5 4") if outline_only else (INK, None)

    def body(ref, cells, fill, label=None, sub=None, pad=0.45):
        x, y, w, h = outline(view, cells, pad, "none" if outline_only else fill, stroke, dash)
        if not outline_only and label:
            s.text(x + w / 2, y + h / 2 + (0 if sub else 4), label, 11, "middle", "bold", rotate=-90 if h > 2.5 * w else None)
            if sub:
                s.text(x + w / 2 + (14 if h > 2.5 * w else 0), y + h / 2 + (0 if h > 2.5 * w else 15), sub, 9.5, "middle",
                       rotate=-90 if h > 2.5 * w else None)
        return x, y, w, h

    chips = {"U1": ("U1", "PIC32MX270F256B"), "U4": ("U4", "6N137"), "U5": ("U5", "6N137"), "U8": ("U8", "6N137"),
             "U9": ("U9", "6N137"), "U6": ("U6", "PC817"), "U10": ("U10", "PC817"), "U7": ("U7", "MAX485"),
             "U11": ("U11", "MAX485"), "SW1": ("SW1", "box number")}
    for ref, (label, sub) in chips.items():
        if f"{ref}.1" not in LEGS:
            continue
        cells = [at(hole) for pin, hole in LEGS.items() if pin.split(".")[0] == ref]
        fill = "#b91c1c" if ref == "SW1" else "#d1d5db"
        x, y, w, h = body(ref, cells, fill, label, sub)
        if not outline_only:
            p1 = view.xy(at(LEGS[f"{ref}.1"]))
            s.text(p1[0] + (9 if p1[0] < x + w / 2 else -9), p1[1] + 4, "1", 9, "start" if p1[0] < x + w / 2 else "end",
                   "bold", "#991b1b")
        if ref == "SW1":                          # the piano keys, sideways towards the left edge of the board
            out = 1 if view.mirrored else -1
            for k in range(1, 9):
                kx, ky = view.xy(at(LEGS[f"SW1.{k}"]))
                near, tip = kx + out * 0.45 * PITCH, kx + out * 1.32 * PITCH    # the key ends 3.36 mm out
                s.rect(min(near, tip), ky - 0.27 * PITCH, abs(tip - near), 0.54 * PITCH,
                       fill="none" if outline_only else "white", stroke=stroke, width=1.2, rx=1.5, dash=dash)
    body("U3", [at(LEGS["U3.1"]), at(LEGS["U3.7"])], "#fde68a", "U3 RKK", None, pad=0.6)
    body("U2", [at(LEGS[f"U2.{k}"]) for k in "123"], "#374151", "U2", None)
    shapes = body_cells(LEGS)
    x, y, w, h = body("J1", shapes["J1"], "#bfdbfe", None, pad=0.45)
    if not outline_only:
        s.text(x + w / 2, y - 6, "J1 USB-C breakout, its socket over the edge", 10, "middle", "bold", halo=True)
        for pin in ("VBUS", "D-", "D+", "CC1", "CC2", "GND"):
            px, py = view.xy(at(LEGS[f"J1.{pin}"]))
            s.text(px + 4, py + 16, pin.replace("D-", "D−"), 8, weight="bold", rotate=90)
    body("J2", [at(LEGS[f"J2.{k}"]) for k in "16"], "#111827", None)
    if not outline_only:
        jx, jy = view.xy(at(LEGS["J2.1"]))
        s.text(jx + 10, jy + 4, "J2: pin 1 (MCLR) here", 9, weight="bold", halo=True)
    for ref in SWITCHES:
        cells = [at(LEGS[f"{ref}.{leg}"]) for leg in ("a", "net", "gnd", "b")]
        x, y, w, h = body(ref, cells, "#e5e7eb", None, pad=0.9)
        if not outline_only:
            s.circle(x + w / 2, y + h / 2, 0.9 * PITCH, fill="#f9fafb", stroke=INK, width=1.3)
            s.text(x + w / 2, y + h / 2 + 4, ref, 11, "middle", "bold")
    for xlr, output in (("J3", 1), ("J4", 2)):
        if xlr not in shapes:
            continue
        x, y, w, h = body(xlr, shapes[xlr], "#bbf7d0", None, pad=0.5)
        if not outline_only:
            s.text(x + w / 2, y - 5, f"{xlr}: XLR of output {output}", 9.5, "middle", "bold", halo=True)
            for pin, what in (("1", "1"), ("3", "3 +"), ("2", "2 −")):
                px, py = view.xy(at(LEGS[f"{xlr}.{pin}"]))
                s.text(px - 9, py + 4, what, 9, "end", "bold")
    for ref in PARTS:
        if ref[0] not in "RCDY" or not ref[1:].isdigit() or ref == "D1":
            continue
        two_legs(view, ref, outline_only)
    two_legs(view, "D1", outline_only)


def two_legs(view, ref, outline_only):
    s = view.svg
    a, b = two_leg_ends(ref, LEGS)
    (x1, y1), (x2, y2) = view.xy(a), view.xy(b)
    kind = {"R": "#e7d3b0", "C": "#f2c94c", "D": "#4b5563", "Y": "#cbd5e1"}[ref[0]]
    stroke = SOFT if outline_only else INK
    if standing(ref, a, b):                       # the body stands over the first leg, the second is bent down
        if not outline_only:
            s.line(x1, y1, x2, y2, "#6b7280", 2)
        s.circle(x1, y1, 0.6 * PITCH, fill="none" if outline_only else kind, stroke=stroke, width=1.3)
        if not outline_only:
            s.text(x1, y1 + 3.3, ref, 8, "middle", "bold")
        return
    if ref == "D1":                               # a 3 mm LED: the longer leg (A) is the plus leg
        cx, cy = (x1 + x2) / 2, (y1 + y2) / 2
        s.circle(cx, cy, 0.72 * PITCH, fill="none" if outline_only else "#fca5a5", stroke=stroke, width=1.3)
        if not outline_only:
            s.text(cx, cy + 3.3, "D1", 8.5, "middle", "bold")
            s.text(x1 - 0.85 * PITCH, y1 + 3.5, "A", 9, "middle", "bold", "#991b1b")
        return
    upright = abs(x1 - x2) < 1
    length = max(abs(x2 - x1), abs(y2 - y1)) - 0.7 * PITCH
    w, h = (0.8 * PITCH, length) if upright else (length, 0.8 * PITCH)
    cx, cy = (x1 + x2) / 2, (y1 + y2) / 2
    if not outline_only:
        s.line(x1, y1, x2, y2, "#6b7280", 2)
    s.rect(cx - w / 2, cy - h / 2, w, h, fill="none" if outline_only else kind, stroke=SOFT if outline_only else INK,
           width=1.3, rx=4)
    if not outline_only:
        ink = "white" if ref[0] == "D" and ref != "D1" else INK
        s.text(cx + (3 if upright else 0), cy + (0 if upright else 4), ref, 9.5, "middle", "bold", ink,
               rotate=-90 if upright else None)


def draw_legend(s, x, y, solder_side, wires):
    s.text(x, y, "Tracks on the solder side" if solder_side else "What is where", 14, weight="bold")
    lines = ([
        "Each color is one net; a dot with a white middle",
        "is a pad with a leg in it, a plain dot a pad the",
        "track only passes, a ring a leg that joins nothing.",
        "Neighbouring pads of two colors must not touch:",
        "look for solder bridges before the first power-up.",
        "",
        "W1, W2 ...: the two ends of an insulated wire",
        "(it runs on the other side of the board).",
    ] if solder_side else [
        "First cut the left edge off along the red line,",
        "2 mm from the middle of column A's holes, and",
        "drill B-1 and B-57 out to 3.2 mm for two screws.",
        "The holes around the screws stay empty.",
        "",
        "Seen from above. Black dots: holes with a leg.",
        "Rings: legs that are soldered but join nothing.",
        "Pale lines: the tracks on the other side.",
        (f"W1 to W{len(wires)}: insulated wires on this side," if len(wires) > 1
         else "W1: an insulated wire on this side," if wires else "No wires are needed."),
        "laid as drawn and soldered into the holes at" if wires else "",
        "their two ends. Lay them before the parts." if wires else "",
        "",
        "Chips: pin 1 is marked; the notch is at its end.",
        "A circle with one line: a resistor standing",
        "upright, its body over the circle, the other",
        "leg bent down to the end of the line.",
        "D1: the longer leg (A, plus) in the marked hole.",
        "U3 (RKK) in a 7-way socket strip: pull out the",
        "contacts in the gap (S, T) before soldering it.",
        "U2 (MCP1700): flat side to the left.",
        "SW1: keys over the left edge, switch 1 at the top.",
        "J3, J4: the wires enter from the right edge;",
        "from the top XLR pin 1, pin 3 (+), pin 2 (−).",
    ])
    for i, line in enumerate(lines):
        s.text(x, y + 24 + i * 16, line, 11)
    yy = y + 24 + len(lines) * 16 + 18
    s.text(x, yy, "Supplies", 12, weight="bold")
    for i, (net, color) in enumerate(SUPPLY_COLORS.items()):
        s.line(x, yy + 18 + i * 18, x + 30, yy + 18 + i * 18, color, 7)
        s.text(x + 40, yy + 22 + i * 18, net, 11, weight="bold")


def draw(path, routes, solder_side):
    width, height = 1240, int(BOARD_H * PITCH) + 170
    svg = Svg(width, height)
    view = View(svg, 60, 100, mirrored=solder_side)
    if solder_side:
        draw_board(view, f"{TITLE} on a perfboard: the solder side",
                   "The board turned over sideways: column A is at the right now. 90 × 150 mm, 33 × 57 holes.")
        draw_parts(view, outline_only=True)
        draw_tracks(view, routes, faint=False)
        draw_pads(view, routes, colored=True)
        wires = draw_wires(view, routes, parts_side=False)
    else:
        draw_board(view, f"{TITLE} on a perfboard: the parts side",
                   "Seen from above: the logic side at the left, USB-C at the bottom edge, the XLR wires at the right.")
        draw_tracks(view, routes, faint=True)
        draw_parts(view, outline_only=False)
        draw_pads(view, routes, colored=False)
        wires = draw_wires(view, routes, parts_side=True)
    draw_legend(svg, 60 + BOARD_W * PITCH + 40, 130, solder_side, wires)
    svg.save(path)


# =====================================================================================
# The lists
# =====================================================================================
def runs_of(pairs):
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
            runs.append(corners(run))
    return runs


def corners(run):
    """The holes where a run starts, turns and ends."""
    return [run[0]] + [b for a, b, c in zip(run, run[1:], run[2:])
                       if (b[0] - a[0], b[1] - a[1]) != (c[0] - b[0], c[1] - b[1])] + [run[-1]]


def lists_markdown(routes):
    by_ref = {}
    for pin, hole in LEGS.items():
        by_ref.setdefault(pin.split(".")[0], []).append((pin.split(".")[1], hole))
    lines = [f"# {TITLE} on a perfboard: the lists", "",
             f"Written by `{SCRIPT}`. Do not change it by hand: change the plan in the script and run it again.", "",
             "The board is 90 x 150 mm with 33 x 57 holes: columns A to Z and AA to AG, rows 1 to 57. A hole is "
             "written with a dash: `AB-12` is column AB, row 12.", "",
             "## Parts and their holes", "", "| Part | What | Holes |", "|---|---|---|"]

    def order(ref):
        letters = "".join(ch for ch in ref if not ch.isdigit())
        return letters, int("".join(ch for ch in ref if ch.isdigit()) or 0)

    for ref in sorted(by_ref, key=order):
        what = PARTS[ref][1] if ref in PARTS else ""
        if ref in PARTS and ref[0] == "R" and standing(ref, *two_leg_ends(ref, LEGS)):
            what += f", standing: the body over {shown(LEGS[ref + '.1'])}"
        holes = ", ".join(f"{pin}: {shown(hole)}" for pin, hole in sorted(by_ref[ref]))
        lines.append(f"| {ref} | {what} | {holes} |")
    wires = numbered_wires(routes)
    lines += ["", "## Insulated wires (on the parts side)", "",
              "Each wire is soldered into the hole at each of its ends; it lies along the holes given between them, "
              "turning where the list turns.", "", "| Wire | Net | From | Along | To |", "|---|---|---|---|---|"]
    for number, net, path in wires:
        bends = corners(path)[1:-1]
        along = ", ".join(shown(name(cell)) for cell in bends) or "straight"
        lines.append(f"| W{number} | {net} | {shown(name(path[0]))} | {along} | {shown(name(path[-1]))} |")
    lines += ["", "## Tracks on the solder side", "",
              "Each run goes in straight lines from hole to hole; every pad on the way belongs to it.", "",
              "| Net | Runs |", "|---|---|"]
    for net in sorted(routes):
        bottom = routes[net][0]
        if bottom:
            runs = "; ".join(" → ".join(shown(name(pad)) for pad in run) for run in runs_of(bottom))
            lines.append(f"| {net} | {runs} |")
    return "\n".join(lines) + "\n"


def selftest(routes):
    """Breaks the good plan on purpose, one way at a time: the check must notice every one."""
    def changed(net, remove=(), add=(), top=False):
        copy = {n: (set(r[0]), set(r[1]), set(r[2])) for n, r in routes.items()}
        layer = copy[net][1 if top else 0]
        layer -= {frozenset(pair) for pair in remove}
        layer |= {frozenset(pair) for pair in add}
        return copy

    net_with_track = "TX1" if routes["TX1"][0] else next(net for net in sorted(routes) if routes[net][0])
    some_step = sorted(tuple(sorted(pair)) for pair in routes[net_with_track][0])[0]
    gnd_leg, tx_leg = at(LEGS["U1.8"]), at(LEGS[PINS["TX1"][0]])
    free_pin = next(pin for pin in LEGS if pin not in NET_OF and pin.startswith("U1.")) if any(
        pin not in NET_OF and pin.startswith("U1.") for pin in LEGS) else next(pin for pin in LEGS if pin not in NET_OF)
    first, second = sorted(pin for pin in LEGS if pin.startswith("U1."))[:2]
    moved = dict(LEGS)
    moved[first], moved[second] = LEGS[second], LEGS[first]
    shared = dict(LEGS)
    shared["C3.1"] = LEGS["C3.2"]
    gnd_pad = sorted(pad for pair in routes["GND"][0] for pad in pair if pad[0] < GAP_FIRST - 1)[-1]
    edge = (GAP_FIRST - 1, gnd_pad[1])
    leg_over = at(LEGS["U1.2"])
    cases = {
        "a track step missing": (changed(net_with_track, remove=[some_step]), LEGS),
        "a track joining two nets": (changed("TX1", add=[(tx_leg, (tx_leg[0], tx_leg[1] - 1))]), LEGS),
        "a track touching a free pin": (changed("GND", add=[(gnd_leg, at(LEGS[free_pin]))]), LEGS),
        "two legs swapped": (routes, moved),
        "two legs in one hole": (routes, shared),
        "a track in the gap": (changed("GND", add=[(gnd_pad, edge), (edge, (GAP_FIRST, gnd_pad[1]))]), LEGS),
        "a wire over a leg": (changed("TX1", add=[((leg_over[0], leg_over[1] - 1), leg_over),
                                                 (leg_over, (leg_over[0], leg_over[1] + 1))], top=True), LEGS),
        "a resistor under a screw terminal": (routes, {**LEGS, "R12.1": "AF3", "R12.2": "AF7"}),
        "a capacitor with its legs too far apart": (routes, {**LEGS, "C12.1": "V8"}),
        "a track where a screw goes": (changed("GND", add=[(at("A1"), at("A2"))]), LEGS),
    }
    missed = []
    for what, (broken, legs) in cases.items():
        found = check(broken, legs)
        print(f"  {what}: {'noticed' if found else 'NOT NOTICED'}" + (f" ({found[0]})" if found else ""))
        if not found:
            missed.append(what)
    return 1 if missed else 0


def main():
    problems = check_list()
    if problems:
        print("The net list is wrong:", *problems, sep="\n  - ")
        return 1
    boxed = boxed_in()
    if boxed:
        print("Pins that no track can leave:", *boxed, sep="\n  - ")
        return 1
    notes = []
    routes = find_tracks(report=notes)
    if routes is None:
        print("No way found for the tracks:", *notes[-12:], sep="\n  ")
        return 1
    problems = check(routes)
    if problems:
        print("The perfboard plan is wrong:", *problems, sep="\n  - ")
        return 1
    steps = sum(len(r[0]) for r in routes.values())
    print(f"perfboard plan: {len(LEGS)} legs, {steps} track steps, {len(numbered_wires(routes))} insulated wires, "
          "nets match the schematic")
    if "--selftest" in sys.argv:
        return selftest(routes)
    names = (f"{FILE}", f"{FILE}_solder")
    draw(HERE / f"{names[0]}.svg", routes, solder_side=False)
    draw(HERE / f"{names[1]}.svg", routes, solder_side=True)
    (HERE / f"{FILE}.md").write_text(lists_markdown(routes), encoding="utf-8")
    if to_png_and_pdf(HERE, names, f"{FILE}.pdf"):
        print("wrote the SVG, PNG, PDF and list files to", HERE)
    return 0


if __name__ == "__main__":
    sys.exit(main())
