"""Routes the board of dmxbox 3 with FreeRouting, then pours the ground planes (boxes/scripts/routing.py).

Run with KiCad's Python:  python route.py FREEROUTING_CLI [PASSES]
"""
import sys

import box  # noqa: F401  (boxes/scripts on the path)
import routing

if __name__ == "__main__":
    sys.exit(routing.main(*sys.argv[1:3]))
