"""The footprints of the boxes that KiCad's library does not have. A box's footprints.py writes the ones it uses
into its own library, a folder named like the box (boxes/box4/dmxbox4.pretty), with the project's footprint library
table (fp-lib-table) that names that folder; its make_board.py runs it too, before it places the parts. Needs
KiCad's Python (pcbnew).

  Converter_DCDC_Mornsun_B05_XT-1WR3
      The isolated DC-DC module B0505XT-1WR3: drawn from Mornsun's datasheet of the B05_XT-1WR3 series
      (2022.01.08-A/5, page 4, "Dimensions and Recommended Layout"): body 13.20 x 8.50 mm, leads to 11.40 mm,
      pads 1.00 x 2.10 mm, 12.20 mm across the two rows of pads, pins 1, 2, 4 in one row and 8, 5 in the other
      at 2.54 mm and 7.62 mm. Pin 1 GND, 2 Vin, 4 0V, 5 +Vo, 8 NC (to be left alone). Input on one half, output
      on the other: the module bridges the isolation gap along its length. EVISUN's datasheet of its
      B0505XT-1WR3 (V1.0, the part JLCPCB fits) gives the same pins, pads and size.
  SW_DIP_SPSTx08_Piano_Kangshen_DSPV08LSGET_P2.54mm
      The box-number switch, kangshen DSPV08LSGET, drawn from its drawing KE-DSPV-008-S-A: body 21.72 x 9.00 mm,
      7.00 mm high, its keys 1.10 mm wide and reaching 1.10 mm beyond one long side; pads 2.20 x 1.10 mm, 2.54 mm
      apart, 6.60 mm between the two rows. With the keys up the page, switch k is the k-th key from the left: it
      joins pin k in the row on the keys' side and pin 17 - k across from it, the pins of KiCad's symbol
      SW_DIP_x08. (Which way the switch's own print counts is not sure: kangshen's drawing has 1 on the left with
      the keys up, JLCPCB's library drawing 1 on the right. The board's labels follow the wiring.)
  Converter_DCDC_RECOM_RKK_SIP7
      The isolated DC-DC module Recom RKK-0505S/H (box 1), drawn from its datasheet (Rev. 5-2026, page 4): SIP7
      with pins only at 1, 2, 5 and 7 (2.54 mm apart, holes 1.02 mm), body 19.6 x 6.0 mm with pin 1 2.22 mm from
      its end and the pins 1.24 mm from one long side. Input on pins 1 and 2, output on 5 and 7: the module
      bridges the isolation gap between pins 2 and 5.
  PinHeader_1x05_P2.54mm_Vertical_SMD_BothSides
  PinSocket_1x06_P2.54mm_Vertical_SMD_BothSides
      KiCad's SMD pin header and socket strip, whose legs alternate left and right, with a pad on BOTH sides of
      every pin. The makers do not agree on which side pin 1's leg is (hanxia's "TP-YQ" and "TP-ZQ"), and a strip
      of six turned round does not fit the other kind: with pads on both sides every kind fits, turned either way.
"""
import os

import pcbnew

import box

LIB = os.path.join(box.OUT, box.NAME + ".pretty")
KICAD_FP = os.path.join(os.environ["LOCALAPPDATA"], "Programs", "KiCad", "10.0", "share", "kicad", "footprints")

DCDC = "Converter_DCDC_Mornsun_B05_XT-1WR3"
SWITCH = "SW_DIP_SPSTx08_Piano_Kangshen_DSPV08LSGET_P2.54mm"
HEADER5 = "PinHeader_1x05_P2.54mm_Vertical_SMD_BothSides"
SOCKET6 = "PinSocket_1x06_P2.54mm_Vertical_SMD_BothSides"
RKK = "Converter_DCDC_RECOM_RKK_SIP7"


def mm(v):
    return pcbnew.FromMM(v)


def pt(x, y):
    return pcbnew.VECTOR2I(mm(x), mm(y))


def line(fp, x1, y1, x2, y2, layer, width):
    s = pcbnew.PCB_SHAPE(fp)
    s.SetShape(pcbnew.SHAPE_T_SEGMENT)
    s.SetStart(pt(x1, y1))
    s.SetEnd(pt(x2, y2))
    s.SetLayer(layer)
    s.SetWidth(mm(width))
    fp.Add(s)


def rect(fp, x1, y1, x2, y2, layer, width):
    for a, b, c, d in ((x1, y1, x2, y1), (x2, y1, x2, y2), (x2, y2, x1, y2), (x1, y2, x1, y1)):
        line(fp, a, b, c, d, layer, width)


def dcdc():
    fp = pcbnew.FOOTPRINT(None)
    fp.SetFPID(pcbnew.LIB_ID(box.NAME, DCDC))
    fp.SetLibDescription("Mornsun B05_XT-1WR3 isolated DC-DC converter, 1 W, SMD 13.2x8.5 mm, 1.5 kV "
                         "(https://www.mornsun-power.com/html/products-detail/B05_XT-1WR3.html)")
    fp.SetKeywords("DC-DC isolated Mornsun B0505XT-1WR3 SMD")
    fp.SetAttributes(pcbnew.FP_SMD)
    for num, x, y in (("1", -3.81, 5.05), ("2", -1.27, 5.05), ("4", 3.81, 5.05), ("5", 3.81, -5.05),
                      ("8", -3.81, -5.05)):
        pad = pcbnew.PAD(fp)
        pad.SetNumber(num)
        pad.SetAttribute(pcbnew.PAD_ATTRIB_SMD)
        pad.SetShape(pcbnew.PAD_SHAPE_ROUNDRECT)
        pad.SetRoundRectRadiusRatio(0.25)
        pad.SetSize(pt(1.0, 2.1))
        pad.SetPosition(pt(x, y))
        pad.SetLayerSet(pad.SMDMask())
        fp.Add(pad)
    # the body on the fab layer, pin 1's corner cut; on the silkscreen the two ends and pin 1's mark
    fab = pcbnew.F_Fab
    line(fp, -5.6, 4.25, 6.6, 4.25, fab, 0.1)
    line(fp, 6.6, 4.25, 6.6, -4.25, fab, 0.1)
    line(fp, 6.6, -4.25, -6.6, -4.25, fab, 0.1)
    line(fp, -6.6, -4.25, -6.6, 3.25, fab, 0.1)
    line(fp, -6.6, 3.25, -5.6, 4.25, fab, 0.1)
    silk = pcbnew.F_SilkS
    line(fp, -6.71, -4.36, -6.71, 4.36, silk, 0.12)
    line(fp, 6.71, -4.36, 6.71, 4.36, silk, 0.12)
    line(fp, -6.71, 4.36, -4.6, 4.36, silk, 0.12)
    line(fp, -4.6, 4.36, -4.6, 6.1, silk, 0.12)                 # pin 1's mark: beside its pad
    rect(fp, -6.95, -6.4, 6.95, 6.4, pcbnew.F_CrtYd, 0.05)
    fp.Reference().SetPosition(pt(0, -7.3))
    fp.Reference().SetLayer(silk)
    fp.Value().SetPosition(pt(0, 7.3))
    fp.Value().SetLayer(fab)
    fp.Value().SetText(DCDC)
    return fp


def piano_switch():
    fp = pcbnew.FOOTPRINT(None)
    fp.SetFPID(pcbnew.LIB_ID(box.NAME, SWITCH))
    fp.SetLibDescription("8-way piano DIP switch, SMD, the keys on one long side: kangshen DSPV08LSGET (LCSC C3293170)")
    fp.SetKeywords("DIP switch piano SMD kangshen DSPV08LSGET")
    fp.SetAttributes(pcbnew.FP_SMD)
    keys = [-8.89 + 2.54 * k for k in range(8)]
    for k, x in enumerate(keys):
        for num, y in ((k + 1, -4.4), (16 - k, 4.4)):
            pad = pcbnew.PAD(fp)
            pad.SetNumber(str(num))
            pad.SetAttribute(pcbnew.PAD_ATTRIB_SMD)
            pad.SetShape(pcbnew.PAD_SHAPE_ROUNDRECT)
            pad.SetRoundRectRadiusRatio(0.25)
            pad.SetSize(pt(1.1, 2.2))
            pad.SetPosition(pt(x, y))
            pad.SetLayerSet(pad.SMDMask())
            fp.Add(pad)
    # the body and its keys on the fab layer; on the silkscreen the two ends of the body
    rect(fp, -10.86, -4.5, 10.86, 4.5, pcbnew.F_Fab, 0.1)
    for x in keys:
        rect(fp, x - 0.55, -5.6, x + 0.55, -4.5, pcbnew.F_Fab, 0.1)
    silk = pcbnew.F_SilkS
    for side in (-1, 1):
        line(fp, side * 10.97, -4.61, side * 10.97, 4.61, silk, 0.12)
        for row in (-1, 1):
            line(fp, side * 10.97, row * 4.61, side * 9.75, row * 4.61, silk, 0.12)
    rect(fp, -11.11, -5.85, 11.11, 5.75, pcbnew.F_CrtYd, 0.05)
    fp.Reference().SetPosition(pt(12.0, 0))
    fp.Reference().SetTextAngleDegrees(90)
    fp.Reference().SetLayer(silk)
    fp.Value().SetPosition(pt(0, 0))
    fp.Value().SetLayer(pcbnew.F_Fab)
    fp.Value().SetText(SWITCH)
    return fp


def rkk():
    fp = pcbnew.FOOTPRINT(None)
    fp.SetFPID(pcbnew.LIB_ID(box.NAME, RKK))
    fp.SetLibDescription("Recom RKK-xxxxS/H isolated DC-DC converter, 1 W, SIP7 with pins 1, 2, 5, 7 "
                         "(https://recom-power.com/pdf/Econoline/RKK.pdf)")
    fp.SetKeywords("DC-DC isolated Recom RKK SIP7")
    fp.SetAttributes(pcbnew.FP_THROUGH_HOLE)
    for num, x in (("1", 0.0), ("2", 2.54), ("5", 10.16), ("7", 15.24)):
        pad = pcbnew.PAD(fp)
        pad.SetNumber(num)
        pad.SetAttribute(pcbnew.PAD_ATTRIB_PTH)
        pad.SetShape(pcbnew.PAD_SHAPE_RECT if num == "1" else pcbnew.PAD_SHAPE_CIRCLE)
        pad.SetSize(pt(1.8, 1.8))
        pad.SetDrillSize(pt(1.1, 1.1))
        pad.SetPosition(pt(x, 0))
        pad.SetLayerSet(pad.PTHMask())
        fp.Add(pad)
    rect(fp, -2.22, -1.24, 17.38, 4.76, pcbnew.F_Fab, 0.1)
    rect(fp, -2.33, -1.35, 17.49, 4.87, pcbnew.F_SilkS, 0.12)
    line(fp, -2.33, -1.9, -1.0, -1.9, pcbnew.F_SilkS, 0.12)             # pin 1's mark
    rect(fp, -2.54, -2.54, 17.68, 5.06, pcbnew.F_CrtYd, 0.05)
    fp.Reference().SetPosition(pt(7.62, -3.4))
    fp.Reference().SetLayer(pcbnew.F_SilkS)
    fp.Value().SetPosition(pt(7.62, 2.4))
    fp.Value().SetLayer(pcbnew.F_Fab)
    fp.Value().SetText(RKK)
    return fp


def both_sides(source_lib, source_name, name):
    """A KiCad SMD strip with legs to alternate sides, given a pad on the other side of every pin as well."""
    fp = pcbnew.FootprintLoad(os.path.join(KICAD_FP, source_lib + ".pretty"), source_name)
    fp.SetFPID(pcbnew.LIB_ID(box.NAME, name))
    fp.SetLibDescription(fp.GetLibDescription() + ", with a pad on both sides of every pin (fits either kind)")
    fp.Value().SetText(name)
    for pad in list(fp.Pads()):
        twin = pcbnew.PAD(pad)
        pos = pad.GetPosition()
        twin.SetPosition(pcbnew.VECTOR2I(-pos.x, pos.y))
        fp.Add(twin)
    return fp


MAKERS = {
    DCDC: dcdc,
    RKK: rkk,
    SWITCH: piano_switch,
    HEADER5: lambda: both_sides("Connector_PinHeader_2.54mm", "PinHeader_1x05_P2.54mm_Vertical_SMD_Pin1Left",
                                HEADER5),
    SOCKET6: lambda: both_sides("Connector_PinSocket_2.54mm", "PinSocket_1x06_P2.54mm_Vertical_SMD_Pin1Left",
                                SOCKET6),
}


def main(names):
    """Writes the footprints NAMES into the box's library, and its fp-lib-table."""
    os.makedirs(LIB, exist_ok=True)
    made = [MAKERS[name]() for name in names]
    # KiCad's own format by name: guessed from the path, an empty folder is taken for another kind
    io = pcbnew.PCB_IO_MGR.FindPlugin(pcbnew.PCB_IO_MGR.KICAD_SEXP)
    for fp in made:
        io.FootprintSave(LIB, fp)
    with open(os.path.join(box.OUT, "fp-lib-table"), "w", encoding="utf-8", newline="\n") as f:
        f.write(f'(fp_lib_table\n\t(version 7)\n\t(lib (name "{box.NAME}")(type "KiCad")'
                f'(uri "${{KIPRJMOD}}/{box.NAME}.pretty")'
                f'(options "")(descr "{box.TITLE} footprints that KiCad does not have"))\n)\n')
    print("wrote", ", ".join(fp.GetFPID().GetLibItemName().wx_str() for fp in made), "into", LIB)
