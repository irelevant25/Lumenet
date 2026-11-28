"""Writes the drawings of dmxbox 4 (boxes/scripts/drawings.py): ../dmxbox4_schematic.pdf, ../dmxbox4_assembly.pdf
(where every part goes) and ../dmxbox4_copper.pdf (the two copper layers).

Run with any Python 3 once KiCad is installed:  python make_drawings.py
"""
import sys

import box  # noqa: F401  (boxes/scripts on the path)
import drawings

if __name__ == "__main__":
    sys.exit(drawings.main())
