"""Asks JLCPCB's parts library about every part of dmxbox 4 (boxes/scripts/partscheck.py): in stock, basic or
extended, what an order costs.

Run with any Python 3:  python check_parts.py [BOARDS]
"""
import sys

import box  # noqa: F401  (boxes/scripts on the path)
import partscheck

if __name__ == "__main__":
    sys.exit(partscheck.main(int(sys.argv[1]) if len(sys.argv) > 1 else 5))
