"""Asks JLCPCB's parts library about every LCSC number in circuit.py: still there, how many in stock, basic or
extended, what it costs. Prints the parts that need attention and an estimate for an order of 5 boards.

Run with any Python 3:  python check_parts.py [BOARDS]
"""
import json
import ssl
import sys
import urllib.request

import circuit

URL = "https://jlcpcb.com/api/overseas-pcb-order/v1/shoppingCart/smtGood/selectSmtComponentList"
SETUP, EXTENDED_FEE, STENCIL = 8.18, 3.07, 1.53   # USD, JLCPCB economic assembly (their price page, 2026)


def query(code):
    body = json.dumps({"currentPage": 1, "pageSize": 5, "keyword": code, "searchSource": "search"}).encode()
    req = urllib.request.Request(URL, data=body, headers={"Content-Type": "application/json"})
    with urllib.request.urlopen(req, context=ssl.create_default_context(), timeout=30) as r:
        d = json.load(r)
    for e in (d.get("data") or {}).get("componentPageInfo", {}).get("list") or []:
        if e.get("componentCode") == code:
            return e
    return None


def main():
    boards = int(sys.argv[1]) if len(sys.argv) > 1 else 5
    count = {}
    for ref, d in circuit.PARTS.items():
        if d["by"] == "jlc":
            count[d["lcsc"]] = count.get(d["lcsc"], 0) + 1
    total, extended, warn = 0.0, [], []
    for code, n in sorted(count.items()):
        e = query(code)
        if not e:
            warn.append(f"{code}: not found")
            continue
        kind = "basic" if e["componentLibraryType"] == "base" else ("preferred" if e.get("preferredComponentFlag")
                                                                    else "extended")
        price = e.get("initialPrice") or 0
        need = n * boards
        line = f"{code:10} {kind:9} stock {e['stockCount']:>9}  ${price:<8} x{need:<4} {e['componentModelEn']}"
        print(line)
        total += price * need
        if kind == "extended":
            extended.append(code)
        if e["stockCount"] < need * 4:
            warn.append(f"{code} ({e['componentModelEn']}): only {e['stockCount']} in stock")
    fees = SETUP + STENCIL + EXTENDED_FEE * len(extended)
    print(f"\n{boards} boards: parts about ${total:.2f}, fees about ${fees:.2f} "
          f"(setup {SETUP}, stencil {STENCIL}, {len(extended)} extended parts x {EXTENDED_FEE})")
    print("\n".join(warn) if warn else "every part in stock")


if __name__ == "__main__":
    main()
