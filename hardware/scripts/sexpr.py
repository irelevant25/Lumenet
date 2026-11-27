"""A small reader and writer for KiCad's s-expression files.

parse(text) -> nested lists of str (atoms keep their quotes off; a Quoted subclass marks quoted strings)
dump(node) -> text
"""
import re


class Q(str):
    """A string that was quoted in the file (and is written quoted)."""


_TOKEN = re.compile(r'\s*(?:(\()|(\))|"((?:[^"\\]|\\.)*)"|([^\s()"]+))', re.S)


def parse(text):
    stack, cur = [], []
    pos = 0
    n = len(text)
    while pos < n:
        m = _TOKEN.match(text, pos)
        if not m:
            if text[pos:].strip() == "":
                break
            raise ValueError(f"cannot read at {pos}: {text[pos:pos + 40]!r}")
        pos = m.end()
        if m.group(1):
            stack.append(cur)
            cur = []
        elif m.group(2):
            done = cur
            cur = stack.pop()
            cur.append(done)
        elif m.group(3) is not None:
            cur.append(Q(m.group(3).replace('\\"', '"').replace("\\\\", "\\")))
        else:
            cur.append(m.group(4))
    if stack:
        raise ValueError("unbalanced parentheses")
    return cur[0] if len(cur) == 1 else cur


def _atom(a):
    if isinstance(a, Q):
        return '"' + a.replace("\\", "\\\\").replace('"', '\\"') + '"'
    if isinstance(a, bool):
        return "yes" if a else "no"
    if isinstance(a, float):
        s = f"{a:.6f}".rstrip("0").rstrip(".")
        return "0" if s in ("-0", "") else s
    return str(a)


def dump(node, indent=0):
    if not isinstance(node, list):
        return _atom(node)
    # short lists of atoms on one line
    if all(not isinstance(x, list) for x in node):
        return "(" + " ".join(_atom(x) for x in node) + ")"
    pad = "\t" * (indent + 1)
    head = []
    rest = []
    for x in node:
        if not rest and not isinstance(x, list):
            head.append(_atom(x))
        else:
            rest.append(x)
    out = "(" + " ".join(head)
    for x in rest:
        out += "\n" + pad + dump(x, indent + 1)
    out += "\n" + "\t" * indent + ")"
    return out


def find(node, key):
    """First child list whose head is key."""
    for x in node:
        if isinstance(x, list) and x and x[0] == key:
            return x
    return None


def find_all(node, key):
    return [x for x in node if isinstance(x, list) and x and x[0] == key]
