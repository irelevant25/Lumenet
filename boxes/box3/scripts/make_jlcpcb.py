"""Writes what JLCPCB needs to make and assemble dmxbox 3, into ../jlcpcb/ (boxes/scripts/jlcpcb.py): the Gerber
ZIP, the BOM and the placement file.

Run with KiCad's Python:  python make_jlcpcb.py
"""
import sys

import box  # noqa: F401  (boxes/scripts on the path)
import jlcpcb

if __name__ == "__main__":
    sys.exit(jlcpcb.main())
