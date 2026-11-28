"""Checks dmxbox 2's KiCad files against circuit.py (boxes/scripts/checks.py).

  python check.py sch NETLIST     KiCad's net list of the schematic has exactly the nets of circuit.py
  python check.py erc REPORT      the ERC report (kicad-cli sch erc --format json) has nothing to report
  python check.py drc REPORT      the same for the board (kicad-cli pcb drc --format json --schematic-parity)
"""
import sys

import box  # noqa: F401  (boxes/scripts on the path)
import checks

if __name__ == "__main__":
    sys.exit(checks.main(sys.argv[1], sys.argv[2]))
