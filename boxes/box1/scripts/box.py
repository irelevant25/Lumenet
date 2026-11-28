"""Box 1, dmxbox 1: what the shared scripts in boxes/scripts need to know of it. Every script of this folder
imports it first; it also puts boxes/scripts on Python's path. Plain Python, so Blender and CadQuery use it too."""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.normpath(os.path.join(HERE, ".."))      # boxes/box1: the KiCad project and what is made from it
FOLDER = "box1"
NAME = "dmxbox1"                                       # the files' names, and the project's footprint library
TITLE = "dmxbox 1"
BOARD = os.path.join(OUT, NAME + ".kicad_pcb")
WORK = os.path.join(HERE, "work")                      # in-between files (not kept by git)

SHARED = os.path.normpath(os.path.join(HERE, "..", "..", "scripts"))
if SHARED not in sys.path:
    sys.path.insert(1, SHARED)                         # after this folder: a box's own scripts come first
