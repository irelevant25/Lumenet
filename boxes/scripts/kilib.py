"""Reads symbols and footprints from the installed KiCad libraries, and from the box's own footprint library."""
import os

import box
from sexpr import parse, find, find_all, Q

KICAD = os.path.join(os.environ["LOCALAPPDATA"], "Programs", "KiCad", "10.0", "share", "kicad")
SYM_DIR = os.path.join(KICAD, "symbols")
FP_DIR = os.path.join(KICAD, "footprints")
# The box's own footprints (its footprints.py writes them): the library named like the box ("dmxbox4")
PROJECT_FP_DIR = os.path.join(box.OUT, box.NAME + ".pretty")


def fp_dir(lib):
    """The folder of a footprint library: KiCad's own, or the box's own."""
    return PROJECT_FP_DIR if lib == box.NAME else os.path.join(FP_DIR, lib + ".pretty")

_libs = {}


def sym_lib(lib):
    if lib not in _libs:
        with open(os.path.join(SYM_DIR, lib + ".kicad_sym"), encoding="utf-8") as f:
            _libs[lib] = parse(f.read())
    return _libs[lib]


def symbol(lib_id):
    """The symbol's s-expression with 'extends' resolved (a deep copy, named Lib:Name)."""
    lib, name = lib_id.split(":")
    s = _resolved(lib, name)
    s[1] = Q(lib_id)
    return s


def _resolved(lib, name):
    import copy
    root = sym_lib(lib)
    syms = {s[1]: s for s in find_all(root, "symbol")}
    s = copy.deepcopy(syms[name])
    ext = find(s, "extends")
    if ext:
        parent = _resolved(lib, ext[1])
        # the child's properties win; the parent's drawings and pins stay, renamed
        child_props = {p[1]: p for p in find_all(s, "property")}
        out = [parent[0], Q(name)]
        for x in parent[2:]:
            if isinstance(x, list) and x[0] == "property" and x[1] in child_props:
                out.append(child_props.pop(x[1]))
            elif isinstance(x, list) and x[0] == "symbol":
                sub = list(x)
                sub[1] = Q(sub[1].replace(ext[1], name, 1))
                out.append(sub)
            else:
                out.append(x)
        for p in child_props.values():
            out.append(p)
        s = out
    s[1] = Q(name)
    return s


def pins(lib_id, unit=None):
    """[(number, name, type, x, y, angle, length, unit)] of a symbol (y up, as in the library)."""
    s = symbol(lib_id)
    out = []
    for sub in find_all(s, "symbol"):
        # sub name: Name_<unit>_<style>
        parts = str(sub[1]).rsplit("_", 2)
        u = int(parts[1]) if len(parts) == 3 else 0
        if parts[-1] not in ("0", "1"):
            continue
        if parts[-1] == "0" and len(parts) == 3:
            pass
        for p in find_all(sub, "pin"):
            at = find(p, "at")
            length = find(p, "length")
            name = find(p, "name")
            num = find(p, "number")
            out.append((str(num[1]), str(name[1]), p[1], float(at[1]), float(at[2]),
                        float(at[3]) if len(at) > 3 else 0.0, float(length[1]), u))
    if unit is not None:
        out = [p for p in out if p[7] in (0, unit)]
    return out


def footprint(lib_fp):
    lib, name = lib_fp.split(":")
    with open(os.path.join(fp_dir(lib), name + ".kicad_mod"), encoding="utf-8") as f:
        return parse(f.read())


def pads(lib_fp):
    fp = footprint(lib_fp)
    out = []
    for p in find_all(fp, "pad"):
        at = find(p, "at")
        size = find(p, "size")
        out.append((str(p[1]), p[2], p[3], float(at[1]), float(at[2]), float(size[1]), float(size[2])))
    return out


if __name__ == "__main__":
    import sys
    for a in sys.argv[1:]:
        if a.startswith("fp="):
            for p in pads(a[3:]):
                print("  pad", p)
        else:
            print("==", a)
            for p in sorted(pins(a), key=lambda t: (t[7], int(t[0]) if t[0].isdigit() else 999, t[0])):
                print("  ", p)
