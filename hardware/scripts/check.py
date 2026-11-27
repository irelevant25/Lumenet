"""Checks the KiCad files against circuit.py.

  check.py sch NETLIST     KiCad's net list of the schematic (kicad-cli sch export netlist) must have exactly
                           the nets of circuit.py: the same pins joined, nothing else joined.
  check.py erc REPORT      the ERC report (kicad-cli sch erc --format json) has no errors and no warnings
                           except the missing global library tables of a KiCad that was never opened.
  check.py drc REPORT      the same for the board (kicad-cli pcb drc --format json --schematic-parity).
"""
import json
import sys

import circuit
from sexpr import parse, find, find_all

# KiCad that was installed but never started has no global library tables: kicad-cli then warns about every
# symbol and footprint. The files themselves are complete (the schematic carries its symbols).
IGNORED = {"lib_symbol_issues", "footprint_link_issues", "lib_footprint_issues"}


def netlist_nets(path):
    with open(path, encoding="utf-8") as f:
        root = parse(f.read())
    nets = {}
    for n in find_all(find(root, "nets"), "net"):
        name = str(find(n, "name")[1])
        pins = sorted(f"{find(p, 'ref')[1]}.{find(p, 'pin')[1]}" for p in find_all(n, "node"))
        nets[name.lstrip("/")] = pins
    return nets


def check_netlist(path):
    got = netlist_nets(path)
    problems = []
    want = {net: sorted(pins) for net, pins in circuit.NETS.items()}
    # KiCad names a net after a label or power symbol; unconnected pins become "unconnected-(...)" nets
    by_pin = {}
    for net, pins in got.items():
        for p in pins:
            by_pin[p] = net
    for net, pins in want.items():
        names = {by_pin.get(p) for p in pins}
        if len(names) != 1:
            problems.append(f"{net}: its pins are in {sorted(str(n) for n in names)}")
            continue
        name = names.pop()
        if name is None:
            problems.append(f"{net}: not in KiCad's list")
            continue
        extra = sorted(set(got[name]) - set(pins))
        if extra:
            problems.append(f"{net} (KiCad: {name}) also has {extra}")
        # KiCad's names: sheet path + label for names used on one sheet, a pin name for unnamed wires; only
        # the pins that are joined matter here
        short = name.split("/")[-1]
        if short != net and not name.startswith("Net-(") and short not in circuit.NETS:
            problems.append(f"{net} is called {name} by KiCad")
    free = set(circuit.NO_CONNECT)
    for net, pins in got.items():
        if len(pins) == 1 and pins[0] not in free and pins[0].split(".")[0] in circuit.PARTS:
            problems.append(f"{pins[0]} is alone in {net}")
    return problems


def check_report(path):
    with open(path, encoding="utf-8") as f:
        d = json.load(f)
    problems = []
    items = []
    for sh in d.get("sheets", []):
        items += sh.get("violations", [])
    items += d.get("violations", []) + d.get("unconnected_items", []) + d.get("schematic_parity", [])
    for v in items:
        if v["type"] in IGNORED:
            continue
        where = "; ".join(i["description"] for i in v.get("items", []))
        problems.append(f"{v['severity']} {v['type']}: {v['description']} [{where}]")
    return problems


if __name__ == "__main__":
    kind, path = sys.argv[1], sys.argv[2]
    found = check_netlist(path) if kind == "sch" else check_report(path)
    print("\n".join(found) if found else f"{kind}: fine")
    sys.exit(1 if found else 0)
