"""The drawing of dmxbox 4's case for JLCMC (make_case.py calls it): one A3 page, every face that gets holes seen
from outside at 1 : 1, and a table with each hole's size and its middle, measured from the face's left and bottom
edges as drawn. The coordinates come in as the case model's (x across, y up, z along the case); each view turns
them into its own (u to the right, v up)."""
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt                       # noqa: E402
from matplotlib.patches import Circle, FancyBboxPatch, Rectangle   # noqa: E402

PLATE_W, PLATE_H, PLATE_R, TUBE_L = 120.0, 63.0, 4.0, 130.0
PLATE_SCREWS = [(sx * 55.45, sy * 27.15) for sx in (-1, 1) for sy in (-1, 1)]   # the plates' own screw holes
PLATE_SINK = 5.9                                     # and their countersinks
LINE, THIN = 0.8, 0.4


def rounded(ax, u0, v0, w, h, r, **kw):
    ax.add_patch(FancyBboxPatch((u0 + r, v0 + r), w - 2 * r, h - 2 * r, boxstyle=f"round,pad={r}", **kw))


def slot(ax, uc, vc, length, width, **kw):
    """A slot with round ends, along u."""
    ax.add_patch(FancyBboxPatch((uc - length / 2 + width / 2, vc), length - width, 0,
                                boxstyle=f"round,pad={width / 2}", **kw))


def cross(ax, u, v, s=1.5):
    ax.plot([u - s, u + s], [v, v], color="0.4", lw=0.3)
    ax.plot([u, u], [v - s, v + s], color="0.4", lw=0.3)


class View:
    """One face, drawn with its bottom left corner at (ox, oy) on the page."""

    def __init__(self, ax, title, ox, oy, w, h, to_uv):
        self.ax, self.ox, self.oy, self.w, self.h, self.to_uv = ax, ox, oy, w, h, to_uv
        self.rows = []
        ax.text(ox, oy + h + 3, title, fontsize=7.5, fontweight="bold", va="bottom", linespacing=1.3)

    def uv(self, *p):
        """Case coordinates to the face's own, from its bottom left corner."""
        return self.to_uv(*p)

    def page(self, u, v):
        return self.ox + u, self.oy + v

    def hole(self, mark, desc, u, v, draw, mark_at=(1.2, 1.2)):
        draw(*self.page(u, v))
        cross(self.ax, *self.page(u, v))
        self.ax.text(*self.page(u + mark_at[0], v + mark_at[1]), mark, fontsize=6, color="tab:red")
        self.rows.append((mark, desc, u, v))


def write(path, h, sizes):
    fig = plt.figure(figsize=(420 / 25.4, 297 / 25.4))
    ax = fig.add_axes([0, 0, 1, 1])
    ax.set_xlim(0, 420)
    ax.set_ylim(0, 297)
    ax.set_aspect("equal")
    ax.axis("off")
    edge = dict(fill=False, lw=LINE, ec="black")
    cut = dict(fill=False, lw=LINE, ec="tab:red")
    old = dict(fill=False, lw=THIN, ec="0.55", ls="--")
    usb_w, usb_h, usb_r = sizes["usb"]
    xlr_d, xlr_screws = sizes["xlr"]
    din_d, din_screws = sizes["din"]
    screw = sizes["screw"]
    pocket_d, rib_h = sizes["pocket"]
    views = []

    def plate(view):
        rounded(ax, *view.page(0, 0), PLATE_W, PLATE_H, PLATE_R, **edge)
        for x, y in PLATE_SCREWS:
            ax.add_patch(Circle(view.page(*view.uv(x, y)), PLATE_SINK / 2, **old))

    # front plate, seen from the front: u = -x, v = y, from the plate's bottom left corner
    front = View(ax, "FRONT PLATE (1.5 mm): USB-C\nseen from outside", 20, 212, PLATE_W, PLATE_H,
                 lambda x, y: (PLATE_W / 2 - x, PLATE_H / 2 + y))
    plate(front)
    for i, (_, x, y, label) in enumerate(h["front"]):
        u, v = front.uv(x, y)
        front.hole(f"F{i + 1}", f"{usb_w} x {usb_h}, corners R{usb_r} ({label})", u, v,
                   lambda pu, pv: rounded(ax, pu - usb_w / 2, pv - usb_h / 2, usb_w, usb_h, usb_r, **cut))
    views.append(front)

    # back plate, seen from the back: u = x, v = y
    back = View(ax, "BACK PLATE (1.5 mm): DMX OUT, DMX IN, MIDI OUT, MIDI IN\nseen from outside, the XLRs upright",
                150, 212, PLATE_W, PLATE_H, lambda x, y: (PLATE_W / 2 + x, PLATE_H / 2 + y))
    plate(back)
    n = 1
    for kind, x, y, label in h["back"]:
        d, screws = (xlr_d, xlr_screws) if kind.startswith("xlr") else (din_d, din_screws)
        u, v = back.uv(x, y)
        back.hole(f"B{n}", f"dia. {d} ({label})", u, v,
                  lambda pu, pv, d=d: ax.add_patch(Circle((pu, pv), d / 2, **cut)))
        n += 1
        for dx, dy in screws:
            su, sv = back.uv(x + dx, y + dy)
            back.hole(f"B{n}", f"dia. {screw} (screw of {label})", su, sv,
                      lambda pu, pv: ax.add_patch(Circle((pu, pv), screw / 2, **cut)))
            n += 1
    views.append(back)

    # the SW1 side (x = +60), both halves, seen from outside: u = 130 - z (the front plate on the right), v = y
    side = View(ax, "SW1 SIDE: slot for the box-number switch, in the bottom half\nseen from outside, the front plate "
                "on the right", 280, 212, TUBE_L, PLATE_H, lambda z, y: (TUBE_L - z, PLATE_H / 2 + y))
    ax.add_patch(Rectangle(side.page(0, 0), TUBE_L, PLATE_H, **edge))
    ax.plot(*zip(side.page(0, PLATE_H / 2), side.page(TUBE_L, PLATE_H / 2)), color="0.4", lw=THIN)
    ax.text(*side.page(1.5, PLATE_H / 2 + 1.2), "top half", fontsize=5, color="0.4")
    ax.text(*side.page(1.5, PLATE_H / 2 - 3.2), "bottom half", fontsize=5, color="0.4")
    _, y, z0, z1, keys = h["side"][0]
    u0, v = side.uv(z1, y)
    u1, _ = side.uv(z0, y)
    side.hole("S1", f"slot {round(z1 - z0, 2)} x {sizes['slot']}, round ends, through the wall", (u0 + u1) / 2, v,
              lambda pu, pv: slot(ax, pu, pv, z1 - z0, sizes["slot"], **cut), mark_at=((z1 - z0) / 2 + 1.5, -1.0))
    for k, z in enumerate(keys):
        ku, kv = side.uv(z, y)
        ax.plot(*zip(side.page(ku, kv - 1), side.page(ku, kv + 1)), color="0.5", lw=0.3)
        ax.text(*side.page(ku, kv + 3.2), str(2 ** k), fontsize=4, ha="center", color="0.4")
    views.append(side)

    # the top half, seen from above: u = x + 60, v = 130 - z (the front plate at the top)
    top = View(ax, "TOP HALF: light pipes\nseen from above, the front plate at the top", 20, 62, PLATE_W, TUBE_L,
               lambda x, z: (PLATE_W / 2 + x, TUBE_L - z))
    ax.add_patch(Rectangle(top.page(0, 0), PLATE_W, TUBE_L, **edge))
    for i, (_, x, z, label) in enumerate(h["top"]):
        u, v = top.uv(x, z)
        top.hole(f"T{i + 1}", f"dia. {sizes['pipe']} (light pipe, {label} LED)", u, v,
                 lambda pu, pv: ax.add_patch(Circle((pu, pv), sizes["pipe"] / 2, **cut)))
    views.append(top)

    # the bottom half, seen from below: u = 60 - x, v = 130 - z (the front plate at the top)
    csk_d, csk_hole = sizes["csk"]
    bottom = View(ax, "BOTTOM HALF: screws of the board's spacers\nseen from below, the front plate at the top",
                  150, 62, PLATE_W, TUBE_L, lambda x, z: (PLATE_W / 2 - x, TUBE_L - z))
    ax.add_patch(Rectangle(bottom.page(0, 0), PLATE_W, TUBE_L, **edge))
    for i, (_, x, z) in enumerate(h["bottom"]):
        u, v = bottom.uv(x, z)

        def draw(pu, pv):
            ax.add_patch(Circle((pu, pv), csk_hole / 2, **cut))
            ax.add_patch(Circle((pu, pv), csk_d / 2, fill=False, lw=THIN, ec="tab:red"))
        bottom.hole(f"U{i + 1}", f"dia. {csk_hole} through, countersunk 90 deg. to dia. {csk_d} from outside (M3)",
                    u, v, draw)
    for i, (x, z) in enumerate(h["pockets"]):
        u, v = bottom.uv(x, z)
        bottom.hole(f"P{i + 1}", f"pocket dia. {pocket_d:g} from inside, {rib_h} deep: the ribs away, down to the "
                    "floor", u, v, lambda pu, pv: ax.add_patch(Circle((pu, pv), pocket_d / 2, fill=False, lw=THIN,
                                                                     ec="tab:red", ls="--")), mark_at=(4.0, -6.5))
    views.append(bottom)

    # the hole table
    tx, ty = 280, 192
    ax.text(tx, ty, "HOLES (mm): middle of each hole from the LEFT and BOTTOM edges of its view as drawn",
            fontsize=7, fontweight="bold")
    ty -= 5
    for view in views:
        for mark, desc, u, v in view.rows:
            ax.text(tx, ty, f"{mark:4s}", fontsize=6, family="monospace", color="tab:red")
            ax.text(tx + 8, ty, f"{u:7.2f}  {v:7.2f}   {desc}", fontsize=5.5, family="monospace")
            ty -= 3.6
    ty -= 3
    notes = [
        "Case: JLCMC K119-12063-H7-L130 (120 x 63 x 130 mm, two halves, end plates 1.5 mm).",
        "All holes go through unless stated. Tolerance +-0.1 mm unless stated. Dashed: the plates' own screw holes,",
        "as supplied, and the pockets inside the bottom half.",
        f"The board stands on four M3 nylon spacers of {sizes['spacer']:g} mm: M3 countersunk screws from below "
        "(U1 to U4).",
    ] + ([
        "Where a spacer stands on the floor's ribs, a pocket (P) takes them away, so it stands flat on the floor.",
    ] if h["pockets"] else []) + [
        "The 3D file dmxbox4_case.step has every hole too; where the two disagree, this drawing is meant.",
    ]
    for line in notes:
        ax.text(tx, ty, line, fontsize=5.5)
        ty -= 3.4
    ax.text(410, 8, "dmxbox 4 case  (made by boxes/box4/scripts/make_case.py)", fontsize=7, ha="right", color="0.3")
    fig.savefig(path)
    plt.close(fig)
