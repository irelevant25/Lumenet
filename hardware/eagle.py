"""Writes a design as an EAGLE schematic (.sch) and board (.brd): EAGLE's XML format (version 9),
which EAGLE, Fusion Electronics and KiCad (File, Import) open. The hardware scripts of both boxes
use it; it knows nothing about either of them.

A design is a list of parts and a list of nets. Each part has its pads where they are on the board
(millimetres, the board's middle or any other point as the origin), and a symbol pin for every pad;
each net names the pins it joins. The board can carry tracks and vias; a connection without a
track shows as an air wire in EAGLE.

The schematic is a working one, not a pretty one: every part is a box with its pins, the parts
stand in rows, and every pin that belongs to a net has a short wire with the net's name on it.
EAGLE joins all wires with the same name, so the nets are exactly those of the design, and
EAGLE's own check (ERC) and its board/schematic consistency hold.
"""
from __future__ import annotations

import math
from dataclasses import dataclass, field
from pathlib import Path
from xml.sax.saxutils import escape, quoteattr

GRID = 2.54
LIBRARY = "lumenet"
LAYERS = [  # number, name, color, fill
    (1, "Top", 4, 1), (16, "Bottom", 1, 1), (17, "Pads", 2, 1), (18, "Vias", 2, 1), (19, "Unrouted", 6, 1),
    (20, "Dimension", 15, 1), (21, "tPlace", 7, 1), (22, "bPlace", 7, 1), (25, "tNames", 7, 1),
    (26, "bNames", 7, 1), (27, "tValues", 7, 1), (28, "bValues", 7, 1), (29, "tStop", 7, 3), (30, "bStop", 7, 6),
    (35, "tGlue", 7, 4), (36, "bGlue", 7, 5), (39, "tKeepout", 4, 11), (40, "bKeepout", 1, 11),
    (41, "tRestrict", 4, 10), (42, "bRestrict", 1, 10), (43, "vRestrict", 2, 10), (44, "Drills", 7, 1),
    (45, "Holes", 7, 1), (46, "Milling", 3, 1), (47, "Measures", 7, 1), (48, "Document", 7, 1),
    (49, "Reference", 7, 1), (51, "tDocu", 7, 1), (52, "bDocu", 7, 1), (91, "Nets", 2, 1), (92, "Busses", 1, 1),
    (93, "Pins", 2, 1), (94, "Symbols", 4, 1), (95, "Names", 7, 1), (96, "Values", 7, 1), (97, "Info", 7, 1),
    (98, "Guide", 6, 1),
]


@dataclass
class Part:
    """One part. "pads": pad name -> (x, y) on the board in mm. "pins": symbol pin name -> pad name,
    in the order the symbol shows them. "kind" groups parts that share a package and a symbol
    (parts of one kind must have the same pads and pins). "drill" and "diameter" in mm."""
    ref: str
    value: str
    kind: str
    pads: dict[str, tuple[float, float]]
    pins: dict[str, str]
    drill: float = 0.9
    diameter: float = 1.8
    description: str = ""
    outline: list[tuple[float, float, float, float]] = field(default_factory=list)   # silk lines, board mm


@dataclass
class Design:
    name: str
    parts: list[Part]
    nets: dict[str, list[tuple[str, str]]]          # net -> (part ref, pin name)
    outline: list[tuple[float, float]] = field(default_factory=list)        # board corners, in order
    holes: list[tuple[float, float, float]] = field(default_factory=list)   # x, y, drill
    tracks: list[tuple[str, int, float, float, float, float, float]] = field(default_factory=list)  # net, layer, x1, y1, x2, y2, width
    vias: list[tuple[str, float, float, float, float]] = field(default_factory=list)  # net, x, y, drill, diameter
    texts: list[tuple[float, float, float, int, str]] = field(default_factory=list)   # x, y, size, layer, text
    note: str = ""


def _f(value: float) -> str:
    text = f"{value:.4f}".rstrip("0").rstrip(".")
    return "0" if text in ("-0", "") else text


def _rotate(x: float, y: float, degrees: int) -> tuple[float, float]:
    a = math.radians(degrees)
    return (x * math.cos(a) - y * math.sin(a), x * math.sin(a) + y * math.cos(a))


@dataclass
class _Kind:
    """What the parts of one kind share: the package (pads with the first pad at 0, 0 and the
    second, where there is one, along a fixed direction), the symbol and the device."""
    name: str
    pads: dict[str, tuple[float, float]]
    pins: dict[str, str]
    drill: float
    diameter: float
    outline: list[tuple[float, float, float, float]]


def _placement(part: Part) -> tuple[tuple[float, float], int, dict[str, tuple[float, float]]]:
    """The part's origin (its first pad), its rotation (0, 90, 180 or 270, chosen so that its
    second pad lies to the right of or below its first one in the package) and its pads in the
    package, relative to the origin and turned back by the rotation."""
    names = list(part.pads)
    ox, oy = part.pads[names[0]]
    rotation = 0
    if len(names) == 2:                           # in the package: the second pad to the right of the first
        dx, dy = part.pads[names[1]][0] - ox, part.pads[names[1]][1] - oy
        if abs(dx) >= abs(dy):
            rotation = 0 if dx > 0 else 180
        else:
            rotation = 90 if dy > 0 else 270
    elif len(names) > 2:                          # in the package: the second pad below the first, as on a DIP
        dx, dy = part.pads[names[1]][0] - ox, part.pads[names[1]][1] - oy
        if abs(dy) >= abs(dx):
            rotation = 0 if dy < 0 else 180
        else:
            rotation = 90 if dx > 0 else 270
    local = {}
    for name, (x, y) in part.pads.items():
        lx, ly = _rotate(x - ox, y - oy, -rotation)
        local[name] = (round(lx, 3), round(ly, 3))
    return (ox, oy), rotation, local


def _kinds(parts: list[Part]) -> tuple[dict[str, _Kind], dict[str, tuple[str, tuple[float, float], int]]]:
    kinds: dict[str, _Kind] = {}
    placed: dict[str, tuple[str, tuple[float, float], int]] = {}
    for part in parts:
        origin, rotation, local = _placement(part)
        name = part.kind
        kind = kinds.get(name)
        if kind is not None and (kind.pads != local or kind.pins != part.pins):
            number = 2                            # same name, other shape: a kind of its own
            while f"{part.kind}_{number}" in kinds and (kinds[f"{part.kind}_{number}"].pads != local
                                                       or kinds[f"{part.kind}_{number}"].pins != part.pins):
                number += 1
            name = f"{part.kind}_{number}"
            kind = kinds.get(name)
        if kind is None:
            outline = []
            for x1, y1, x2, y2 in part.outline:
                a = _rotate(x1 - origin[0], y1 - origin[1], -rotation)
                b = _rotate(x2 - origin[0], y2 - origin[1], -rotation)
                outline.append((a[0], a[1], b[0], b[1]))
            kinds[name] = _Kind(name, local, dict(part.pins), part.drill, part.diameter, outline)
        placed[part.ref] = (name, origin, rotation)
    return kinds, placed


def _package(kind: _Kind) -> str:
    lines = [f'<package name={quoteattr(kind.name)}>']
    first = next(iter(kind.pads))
    for name, (x, y) in kind.pads.items():
        shape = "square" if name == first and len(kind.pads) > 1 else "round"
        lines.append(f'<pad name={quoteattr(name)} x="{_f(x)}" y="{_f(y)}" drill="{_f(kind.drill)}" '
                     f'diameter="{_f(kind.diameter)}" shape="{shape}"/>')
    if kind.outline:
        for x1, y1, x2, y2 in kind.outline:
            lines.append(f'<wire x1="{_f(x1)}" y1="{_f(y1)}" x2="{_f(x2)}" y2="{_f(y2)}" width="0.2032" layer="21"/>')
    else:                                          # a frame around the pads
        xs = [x for x, _ in kind.pads.values()]
        ys = [y for _, y in kind.pads.values()]
        x0, x1, y0, y1 = min(xs) - 1.5, max(xs) + 1.5, min(ys) - 1.5, max(ys) + 1.5
        for a, b, c, d in ((x0, y0, x1, y0), (x1, y0, x1, y1), (x1, y1, x0, y1), (x0, y1, x0, y0)):
            lines.append(f'<wire x1="{_f(a)}" y1="{_f(b)}" x2="{_f(c)}" y2="{_f(d)}" width="0.2032" layer="21"/>')
    xs = [x for x, _ in kind.pads.values()]
    ys = [y for _, y in kind.pads.values()]
    lines.append(f'<text x="{_f(min(xs) - 1.5)}" y="{_f(max(ys) + 2.2)}" size="1.27" layer="25">&gt;NAME</text>')
    lines.append(f'<text x="{_f(min(xs) - 1.5)}" y="{_f(min(ys) - 3.4)}" size="1.27" layer="27">&gt;VALUE</text>')
    lines.append("</package>")
    return "\n".join(lines)


def _symbol_pins(kind: _Kind) -> list[tuple[str, float, float, str]]:
    """Where each pin of the symbol is: name, x, y, rotation. Two pins: one at each end. More: the
    first half down the left side, the rest up the right side, like a DIP seen from above."""
    names = list(kind.pins)
    if len(names) <= 2:
        return [(names[0], -2 * GRID, 0.0, "R0")] + ([(names[1], 2 * GRID, 0.0, "R180")] if len(names) > 1 else [])
    half = (len(names) + 1) // 2
    width = 6 * GRID if max(len(n.split("@")[0]) for n in names) > 4 else 4 * GRID
    pins = []
    for index, name in enumerate(names):
        if index < half:
            pins.append((name, -width / 2 - GRID, -index * GRID, "R0"))
        else:
            pins.append((name, width / 2 + GRID, -(len(names) - 1 - index) * GRID, "R180"))
    return pins


def _symbol(kind: _Kind) -> str:
    pins = _symbol_pins(kind)
    lines = [f'<symbol name={quoteattr(kind.name)}>']
    if len(pins) <= 2:
        x0, x1, y0, y1 = -GRID, GRID, -GRID / 2, GRID / 2
    else:
        xs = [x for _, x, _, _ in pins]
        ys = [y for _, _, y, _ in pins]
        x0, x1 = min(xs) + GRID, max(xs) - GRID
        y0, y1 = min(ys) - GRID, max(ys) + GRID
    for a, b, c, d in ((x0, y0, x1, y0), (x1, y0, x1, y1), (x1, y1, x0, y1), (x0, y1, x0, y0)):
        lines.append(f'<wire x1="{_f(a)}" y1="{_f(b)}" x2="{_f(c)}" y2="{_f(d)}" width="0.254" layer="94"/>')
    for name, x, y, rotation in pins:
        visible = "pad" if len(pins) <= 2 else "both"
        lines.append(f'<pin name={quoteattr(name)} x="{_f(x)}" y="{_f(y)}" visible="{visible}" length="short" '
                     f'direction="pas" rot="{rotation}"/>')
    lines.append(f'<text x="{_f(x0)}" y="{_f(y1 + 1.0)}" size="1.778" layer="95">&gt;NAME</text>')
    lines.append(f'<text x="{_f(x0)}" y="{_f(y0 - 2.8)}" size="1.778" layer="96">&gt;VALUE</text>')
    lines.append("</symbol>")
    return "\n".join(lines)


def _deviceset(kind: _Kind, prefix: str) -> str:
    connects = "".join(f'<connect gate="G$1" pin={quoteattr(pin)} pad={quoteattr(pad)}/>' for pin, pad in kind.pins.items())
    return (f'<deviceset name={quoteattr(kind.name)} prefix={quoteattr(prefix)}>'
            f'<gates><gate name="G$1" symbol={quoteattr(kind.name)} x="0" y="0"/></gates>'
            f'<devices><device name="" package={quoteattr(kind.name)}><connects>{connects}</connects>'
            f'<technologies><technology name=""/></technologies></device></devices></deviceset>')


def _library(kinds: dict[str, _Kind], placed, parts: list[Part], with_symbols: bool) -> str:
    prefixes = {}
    for part in parts:
        prefixes.setdefault(placed[part.ref][0], "".join(ch for ch in part.ref if ch.isalpha()))
    packages = "\n".join(_package(kind) for kind in kinds.values())
    out = [f'<library name="{LIBRARY}">', "<description>Parts of the Lumenet dmxbox, written by hardware/eagle.py.</description>",
           f"<packages>\n{packages}\n</packages>"]
    if with_symbols:
        out.append("<symbols>\n" + "\n".join(_symbol(kind) for kind in kinds.values()) + "\n</symbols>")
        out.append("<devicesets>\n" + "\n".join(_deviceset(kind, prefixes.get(kind.name, "X")) for kind in kinds.values())
                   + "\n</devicesets>")
    out.append("</library>")
    return "\n".join(out)


def _head() -> str:
    layers = "\n".join(f'<layer number="{n}" name="{name}" color="{color}" fill="{fill}" visible="yes" active="yes"/>'
                       for n, name, color, fill in LAYERS)
    return ('<?xml version="1.0" encoding="utf-8"?>\n<!DOCTYPE eagle SYSTEM "eagle.dtd">\n<eagle version="9.6.2">\n<drawing>\n'
            '<settings><setting alwaysvectorfont="no"/><setting verticaltext="up"/></settings>\n'
            '<grid distance="0.1" unitdist="inch" unit="inch" style="lines" multiple="1" display="no" '
            'altdistance="0.01" altunitdist="inch" altunit="inch"/>\n'
            f"<layers>\n{layers}\n</layers>\n")


def _resolve(design: Design, kinds: dict[str, _Kind], placed) -> None:
    by_ref = {part.ref: part for part in design.parts}
    for net, pins in design.nets.items():
        for ref, pin in pins:
            if ref not in by_ref:
                raise ValueError(f"net {net}: there is no part {ref}")
            if pin not in by_ref[ref].pins:
                raise ValueError(f"net {net}: part {ref} has no pin {pin}")


def write_sch(design: Design, path: Path) -> None:
    kinds, placed = _kinds(design.parts)
    _resolve(design, kinds, placed)
    # the parts in rows, widest symbols first, on the 0.1 inch grid
    spots: dict[str, tuple[float, float]] = {}
    x, y, row_height, column = 0.0, 0.0, 0.0, 0
    for part in design.parts:
        pins = _symbol_pins(kinds[placed[part.ref][0]])
        width = (max(p[1] for p in pins) - min(p[1] for p in pins)) + 14 * GRID
        height = (max(p[2] for p in pins) - min(p[2] for p in pins)) + 6 * GRID
        if column == 6:
            x, y, column = 0.0, y - row_height, 0
            row_height = 0.0
        spots[part.ref] = (round(x / GRID) * GRID, round(y / GRID) * GRID)
        x += width
        row_height = max(row_height, height)
        column += 1

    parts = "\n".join(f'<part name={quoteattr(p.ref)} library="{LIBRARY}" deviceset={quoteattr(placed[p.ref][0])} '
                      f'device="" value={quoteattr(p.value)}/>' for p in design.parts)
    instances = "\n".join(f'<instance part={quoteattr(ref)} gate="G$1" x="{_f(sx)}" y="{_f(sy)}"/>'
                          for ref, (sx, sy) in spots.items())
    nets = []
    for net, pins in design.nets.items():
        segments = []
        for ref, pin in pins:
            kind = kinds[placed[ref][0]]
            px, py, rotation = next((x, y, r) for name, x, y, r in _symbol_pins(kind) if name == pin)
            sx, sy = spots[ref]
            ax, ay = sx + px, sy + py
            step = -2 * GRID if rotation == "R0" else 2 * GRID
            bx = ax + step
            label_x = bx - (len(net) * 1.6 + 0.5 if step < 0 else -0.5)
            segments.append(f'<segment><pinref part={quoteattr(ref)} gate="G$1" pin={quoteattr(pin)}/>'
                            f'<wire x1="{_f(ax)}" y1="{_f(ay)}" x2="{_f(bx)}" y2="{_f(ay)}" width="0.1524" layer="91"/>'
                            f'<label x="{_f(label_x if step < 0 else bx + 0.5)}" y="{_f(ay + 0.4)}" size="1.27" layer="95"/>'
                            f"</segment>")
        nets.append(f'<net name={quoteattr(net)} class="0">' + "".join(segments) + "</net>")
    note = (f'<text x="0" y="{_f(10 * GRID)}" size="2.54" layer="97">{escape(design.name)}</text>'
            + (f'<text x="0" y="{_f(8 * GRID)}" size="1.778" layer="97">{escape(design.note)}</text>' if design.note else ""))
    text = (_head() + '<schematic xreflabel="%F%N/%S.%C%R" xrefpart="/%S.%C%R">\n<libraries>\n'
            + _library(kinds, placed, design.parts, with_symbols=True)
            + '\n</libraries>\n<attributes/>\n<variantdefs/>\n<classes><class number="0" name="default" width="0" drill="0"/></classes>\n'
            + f"<parts>\n{parts}\n</parts>\n<sheets>\n<sheet>\n<plain>{note}</plain>\n<instances>\n{instances}\n</instances>\n"
            + "<busses/>\n<nets>\n" + "\n".join(nets) + "\n</nets>\n</sheet>\n</sheets>\n</schematic>\n</drawing>\n</eagle>\n")
    Path(path).write_text(text, encoding="utf-8")


def write_brd(design: Design, path: Path) -> None:
    kinds, placed = _kinds(design.parts)
    _resolve(design, kinds, placed)
    plain = []
    corners = design.outline
    for (x1, y1), (x2, y2) in zip(corners, corners[1:] + corners[:1]):
        plain.append(f'<wire x1="{_f(x1)}" y1="{_f(y1)}" x2="{_f(x2)}" y2="{_f(y2)}" width="0" layer="20"/>')
    for x, y, drill in design.holes:
        plain.append(f'<hole x="{_f(x)}" y="{_f(y)}" drill="{_f(drill)}"/>')
    for x, y, size, layer, body in design.texts:
        plain.append(f'<text x="{_f(x)}" y="{_f(y)}" size="{_f(size)}" layer="{layer}">{escape(body)}</text>')
    elements = []
    for part in design.parts:
        kind_name, (ox, oy), rotation = placed[part.ref]
        elements.append(f'<element name={quoteattr(part.ref)} library="{LIBRARY}" package={quoteattr(kind_name)} '
                        f'value={quoteattr(part.value)} x="{_f(ox)}" y="{_f(oy)}" rot="R{rotation}"/>')
    pad_of = {(part.ref, pin): pad for part in design.parts for pin, pad in part.pins.items()}
    signals = []
    for net, pins in design.nets.items():
        body = [f'<contactref element={quoteattr(ref)} pad={quoteattr(pad_of[(ref, pin)])}/>' for ref, pin in pins]
        for t_net, layer, x1, y1, x2, y2, width in design.tracks:
            if t_net == net:
                body.append(f'<wire x1="{_f(x1)}" y1="{_f(y1)}" x2="{_f(x2)}" y2="{_f(y2)}" width="{_f(width)}" layer="{layer}"/>')
        for v_net, x, y, drill, diameter in design.vias:
            if v_net == net:
                body.append(f'<via x="{_f(x)}" y="{_f(y)}" extent="1-16" drill="{_f(drill)}" diameter="{_f(diameter)}"/>')
        signals.append(f'<signal name={quoteattr(net)}>' + "".join(body) + "</signal>")
    text = (_head() + "<board>\n<plain>\n" + "\n".join(plain) + "\n</plain>\n<libraries>\n"
            + _library(kinds, placed, design.parts, with_symbols=False)
            + '\n</libraries>\n<attributes/>\n<variantdefs/>\n<classes><class number="0" name="default" width="0" drill="0"/></classes>\n'
            + "<elements>\n" + "\n".join(elements) + "\n</elements>\n<signals>\n" + "\n".join(signals)
            + "\n</signals>\n</board>\n</drawing>\n</eagle>\n")
    Path(path).write_text(text, encoding="utf-8")


def check_files(sch: Path, brd: Path, design: Design) -> list[str]:
    """Reads both files back and confirms what EAGLE will see: every net of the design in the
    schematic with exactly its pins, every pin in at most one net, the same parts and nets on the
    board, and every board connection on a pad the package has."""
    # Only the two files this module has just written are read here, never a file from elsewhere;
    # the standard parser does not fetch the DTD that they name.
    import xml.etree.ElementTree as ET

    problems = []
    s = ET.parse(sch).getroot()
    b = ET.parse(brd).getroot()
    sch_nets = {}
    for net in s.iter("net"):
        sch_nets[net.get("name")] = sorted((p.get("part"), p.get("pin")) for p in net.iter("pinref"))
    want = {net: sorted(pins) for net, pins in design.nets.items()}
    if sch_nets != want:
        problems.append("the schematic's nets differ from the design")
    seen = {}
    for net, pins in sch_nets.items():
        for pin in pins:
            if pin in seen:
                problems.append(f"{pin} is in {seen[pin]} and in {net}")
            seen[pin] = net
    sch_parts = {p.get("name") for p in s.iter("part")}
    brd_parts = {e.get("name") for e in b.iter("element")}
    if sch_parts != brd_parts:
        problems.append(f"parts differ: {sorted(sch_parts ^ brd_parts)}")
    brd_nets = {sig.get("name"): sorted((c.get("element"), c.get("pad")) for c in sig.iter("contactref"))
                for sig in b.iter("signal")}
    pad_of = {(part.ref, pin): pad for part in design.parts for pin, pad in part.pins.items()}
    want_brd = {net: sorted((ref, pad_of[(ref, pin)]) for ref, pin in pins) for net, pins in design.nets.items()}
    if brd_nets != want_brd:
        problems.append("the board's signals differ from the design")
    packages = {p.get("name"): {pad.get("name") for pad in p.iter("pad")} for p in b.iter("package")}
    element_package = {e.get("name"): e.get("package") for e in b.iter("element")}
    for net, contacts in brd_nets.items():
        for element, pad in contacts:
            if pad not in packages.get(element_package.get(element), set()):
                problems.append(f"{net}: {element} has no pad {pad}")
    return problems
