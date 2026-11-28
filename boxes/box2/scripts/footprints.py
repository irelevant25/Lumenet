"""Writes the footprints of dmxbox 2 that KiCad's library does not have into ../dmxbox2.pretty, with the project's
footprint library table (boxes/scripts/footprintlib.py says how each is drawn).

Run with KiCad's Python (it has pcbnew):  python footprints.py
make_board.py runs it too, before it places the parts.
"""
import box  # noqa: F401  (boxes/scripts on the path)
import footprintlib
import make_board

if __name__ == "__main__":
    footprintlib.main(make_board.FOOTPRINTS)
