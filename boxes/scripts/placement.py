"""Where a box's make_board.py puts its parts, read without running it (that needs KiCad's pcbnew): for the cases
and the pictures, which run in CadQuery's and Blender's Python. Plain Python."""
import ast
import os

import box


def board_layout():
    """W, H and PLACE of the box's make_board.py: the board's size and every part's (x, y, rotation), in mm from
    the board's top left corner."""
    tree = ast.parse(open(os.path.join(box.HERE, "make_board.py"), encoding="utf-8").read())
    found = {}
    for node in tree.body:
        if isinstance(node, ast.Assign):
            t = node.targets[0]
            if isinstance(t, ast.Name) and t.id == "PLACE":
                found["PLACE"] = ast.literal_eval(node.value)
            elif isinstance(t, ast.Tuple) and [e.id for e in t.elts] == ["W", "H"]:
                found["W"], found["H"] = ast.literal_eval(node.value)
    return found["W"], found["H"], found["PLACE"]
