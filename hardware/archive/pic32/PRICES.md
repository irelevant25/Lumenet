# dmxbox 2: prices

What it costs to build one dmxbox 2: the box with one DMX output ([schematic-1-universe/](schematic-1-universe/))
and the box with two ([schematic-2-universes/](schematic-2-universes/)), on a printed board from JLCPCB in a metal
box. Designators (U1, R3, ...) match [BOM.md](BOM.md) and the schematics.

The prices are from 24 November 2026, in euros without VAT (Slovak VAT is 23 %). Most are from TME: tme.eu pages
found through a web search, many of them shown in USD or GBP and converted at the ECB rates of November 2026
(1 € = 1.1127 USD = 0.83305 GBP), and some read on tme.sk itself. The board and the box are priced at JLCPCB and
JLCMC. Prices and stock change: check the basket before ordering.

- **Per box** counts only the pieces that go into one box, at the price a piece of the pack bought.
- **First order** is what the first order costs. TME sells some parts only in packs (resistors by 100, others by
  5, 10 or 17) and JLCPCB makes at least 5 boards; the rest of each pack is for the next boxes.

## Totals

| | 1 output | 2 outputs |
|---|---|---|
| **Per box** | **22.83 €** | **28.21 €** |
| Per box, with 23 % VAT | 28.09 € | 34.70 € |
| First order | 40.28 € | 45.16 € |
| First order, with 23 % VAT | 49.55 € | 55.55 € |

Not included: the programmer (see below), shipping from TME, JLCPCB and JLCMC, solder, wire and the USB-C cable.

## Parts

Refs in brackets are only in the box with two outputs. Both boxes have one U3: it feeds both outputs.

| Ref | Part (where the price is from) | € a piece | Sold by | 1 output | € | 2 outputs | € |
|---|---|---|---|---|---|---|---|
| U1 | [PIC32MX270F256B-I/SP](https://www.tme.eu/gb/details/32mx270f256b-i_sp/32-bit-pic32-family/microchip-technology/pic32mx270f256b-i-sp/) | 5.09 | 1 | 1 | 5.09 | 1 | 5.09 |
| U2 | [MCP1700-3302E/TO](https://www.tme.eu/eg/en/details/mcp1700-3302e_to/ldo-fixed-voltage-regulators/microchip-technology/) | 0.44 | 1 | 1 | 0.44 | 1 | 0.44 |
| U3 | [TRACO TMA 0505S](https://www.tme.eu/no/en/details/tma0505s/dc-dc-converters/traco-power/tma-0505s/) | 3.59 | 1 | 1 | 3.59 | 1 | 3.59 |
| U4, U5 (U8, U9) | [Liteon 6N137-L](https://www.tme.eu/gb/details/6n137-l/optocouplers-digital-output/liteon/) | 0.37 | 1 | 2 | 0.74 | 4 | 1.49 |
| U6 (U10) | [Sharp PC817X1NSZ1B](https://www.tme.eu/en/katalog/optocouplers_112291/p,sharp_124/) | 0.51 | 1 | 1 | 0.51 | 2 | 1.02 |
| U7 (U11) | [ST485BDR, SO-8](https://www.tme.eu/en/katalog/rs232-rs422-rs485-integr-circ_112854/) | 0.96 | 1 | 1 | 0.96 | 2 | 1.92 |
| Y1 | [YIC 8.00M-HC49-S, 8 MHz](https://www.tme.eu/en/details/8.00m-hc49-s/tht-quartz-crystals/yic/) | 0.22 | 5 | 1 | 0.22 | 1 | 0.22 |
| D1 | [Kingbright L-7104LGD, LED 3 mm](https://www.tme.eu/en/details/l-7104lgd/tht-leds-round/kingbright-electronic/) | 0.13 | 10 | 1 | 0.13 | 1 | 0.13 |
| D2, D3 (D4, D5) | [DC Components P6KE6.8CA](https://www.tme.eu/sk/details/p6ke6.8ca-dc/diody-obojsmerne-tvs-tht/dc-components/p6ke6-8ca/) | 0.146 | 5 | 2 | 0.29 | 4 | 0.58 |
| R1, R2 | [CF1/4W-5K1, 5.1 kΩ](https://www.tme.eu/en/details/cf1_4w-5k1/1-4w-carbon-tht-resistors/sr-passives/) | 0.009 | 100 | 2 | 0.02 | 2 | 0.02 |
| R3, R11 (R20) | [CF1/4W-10K, 10 kΩ](https://www.tme.eu/en/details/cf1_4w-10k/tht-resistors/sr-passives/) | 0.009 | 100 | 2 | 0.02 | 3 | 0.03 |
| R4, R7, R9 (R16, R18) | [CF1/4W-1K, 1 kΩ](https://www.tme.eu/en/details/cf1_4w-1k/tht-resistors/sr-passives/) | 0.009 | 100 | 3 | 0.03 | 5 | 0.04 |
| R5, R10 (R19) | [CF1/4W-470R, 470 Ω](https://www.tme.eu/en/details/cf1_4w-470r/14w-carbon-tht-resistors/sr-passives/) | 0.009 | 100 | 2 | 0.02 | 3 | 0.03 |
| R6 (R15) | [CF1/4W-180R, 180 Ω](https://www.tme.eu/en/details/cf1_4w-180r/tht-resistors/sr-passives/) | 0.009 | 100 | 1 | 0.009 | 2 | 0.02 |
| R8 (R17) | [CF1/4WS-390R, 390 Ω](https://www.tme.eu/en/details/cf1_4ws-390r/tht-resistors/sr-passives/) | 0.009 | 100 | 1 | 0.009 | 2 | 0.02 |
| R12, R14 (R21, R23) | [CF1/4W-560R, 560 Ω](https://www.tme.eu/gb/details/cf1_4w-560r/tht-resistors/sr-passives/) | 0.009 | 100 | 2 | 0.02 | 4 | 0.04 |
| R13 (R22) | [CF1/4W-130R, 130 Ω](https://www.tme.eu/en/details/cf1_4w-130r/tht-resistors/sr-passives/) | 0.009 | 100 | 1 | 0.009 | 2 | 0.02 |
| C1, C2, C6, C10, C11 | [TDK FG28X5R1E106MRT06, 10 µF 25 V X5R](https://www.tme.eu/cz/details/fg28x5r1e106mrt06/kondenzatory-mlcc-tht/tdk/) | 0.36 | 1 | 5 | 1.80 | 5 | 1.80 |
| C3, C4, C5, C9, C12–C14 (C15–C17) | [TDK FG28X7R1H104KNT06, 100 nF 50 V X7R](https://www.tme.eu/sk/details/fg28x7r1h104knt06/kondenzatory-mlcc-tht/tdk/) | 0.08 | 1 | 7 | 0.56 | 10 | 0.80 |
| C7, C8 | [SR Passives CCT-22P/50V, 22 pF C0G](https://www.tme.eu/en/katalog/ceramic-capacitors_100003/?params=118%3A1437236_capacitance%3A22pf) | 0.08 | 10 | 2 | 0.16 | 2 | 0.16 |
| SW1 | [ONPOW DPL08RGT](https://www.tme.eu/sk/details/dpl08rgt/prepinace-dip-switch/onpow/) | 0.80 | 1 | 1 | 0.80 | 1 | 0.80 |
| SW2, SW3 | [E-Switch TL1100FF160Q, 12 × 12 mm](https://www.tme.eu/en/details/tl1100ff160q/microswitches-tact/e-switch/) | 0.35 | 1 | 2 | 0.70 | 2 | 0.70 |
| J1 | [GCT USB4900-00-A, USB-C socket](https://www.tme.eu/no/en/details/usb4900-00-a/usb-ieee1394-connectors/gct/) | 0.66 | 1 | 1 | 0.66 | 1 | 0.66 |
| J2 | [TE 640456-6, pin header 1 × 6](https://www.tme.eu/en/details/640456-6/raster-signal-connectors-2-54mm/te-connectivity/) | 0.30 | 1 | 1 | 0.30 | 1 | 0.30 |
| J3 (J4) | [Neutrik NC3FPP, XLR 3-pin female](https://www.tme.eu/gb/details/ntr-nc3fpp/xlr-connectors/neutrik/nc3fpp/) | 1.91 | 1 | 1 | 1.91 | 2 | 3.82 |
| screw terminal for J3 (J4) | [Degson DG301-5.0-03P-12-00Z(H), 3 poles](https://www.tme.eu/sk/details/dg301-5.0-3p12/svorkovnice-do-dps/degson-electronics/dg301-5-0-03p-12-00z-h/) | 0.15 | 1 | 1 | 0.15 | 2 | 0.30 |
| socket for U1 | [Connfly DS1009-28AT1NS, DIP-28](https://www.tme.eu/en/details/icvt-28p-w/standard-dip-sockets/connfly/ds1009-28at1ns/) | 0.07 | 17 | 1 | 0.07 | 1 | 0.07 |
| sockets for U4, U5 (U8, U9) | [TE 1-2199298-2, DIP-8](https://www.tme.eu/en/details/1-2199298-2/standard-dip-sockets/te-connectivity/) | 0.18 | 1 | 2 | 0.36 | 4 | 0.72 |
| socket for U6 (U10) | [Connfly STS-20P, turned-pin strip of 20 (4 contacts a socket)](https://www.tme.eu/en/details/sts-20p/precision-sockets/connfly/ds1002-01-1-20v13-jk/) | 0.64 a strip | 2 strips | 4 contacts | 0.13 | 8 contacts | 0.26 |
| socket for U3 | [Connfly ZL262-7SG, socket strip 1 × 7](https://www.tme.eu/gb/details/zl262-7sg/pin-headers/connfly/ds1023-1-7s21/) | 0.08 | 10 | 1 | 0.08 | 1 | 0.08 |
| board | JLCPCB, 100 × 100 mm, 2 layers (0.50 € with VAT) | 0.41 | 5 boards | 1 | 0.41 | 1 | 0.41 |
| box | JLCMC, metal, 100 × 50 (3.11 € with VAT) | 2.53 | 1 | 1 | 2.53 | 1 | 2.53 |
| screws | [Kraftberg M3X6/D7985, M3 × 6](https://www.tme.eu/en/details/m3x6_d7985/bolts/kraftberg/) | 0.03 | 100 | 4 | 0.11 | 4 | 0.11 |

## Where this differs from BOM.md

The prices are for the box as it will be built on a printed board, which differs from [BOM.md](BOM.md) in these
parts:

| Ref | Here | In BOM.md | Why |
|---|---|---|---|
| U3 | TRACO TMA 0505S | Recom RKK-0505S/H | TME lists the RKK without a price. The TMA is also a 1 W converter from 5 V to an isolated 5 V. Its input is on the same pins as the RKK's (1 plus, 2 minus), but its output is on pins 4 (minus) and 6 (plus) instead of 5 and 7 ([datasheet](https://tracopower.com/tma-datasheet)). |
| U7 (U11) | ST485BDR, SO-8 (surface mount), soldered onto the board | MAX485, DIP-8 in a socket | At TME the MAX485 costs about 6 € and is hard to get. The ST485B has the same pins and draws as little (about 0.3 mA), so two of them fit the 1 W converter too. For a box without surface-mount parts, see [Through-hole only](#through-hole-only). |
| C1, C2, C6, C10, C11 | TDK FG28X5R1E106MRT06, X5R | X7R | X5R is rated to 85 °C instead of 125 °C, which is plenty inside the box. It is 25 V, with the 5 mm lead spacing BOM.md asks for. |
| J1 | USB-C socket GCT USB4900-00-A on the board | USB-C breakout board | R1 and R2 are needed now. D+ goes to A6 and B6, D− to A7 and B7, so that the plug works both ways round. |
| J3 (J4) | Neutrik NC3FPP, an XLR socket for the board | XLR panel socket on wires | The screw terminals are only needed if the XLR sockets stay on wires. |
| board | JLCPCB, 100 × 100 mm, 2 layers | perfboard 90 × 150 mm | The present layouts are 90 × 150 mm: the board has to be drawn again to fit. |
| box | JLCMC, metal, 100 × 50 | — | Check that the board fits inside it. |

Two more things to watch:

- C1, C2, C6, C10 and C11 must be 10 µF. SR Passives' CM-10N looks similar and is cheaper still, but it is 10 nF.
- The resistors are SR Passives carbon film, ±5 %. TME has 390 Ω only as the smaller CF1/4WS.

## Through-hole only

The list above has two surface-mount parts: U7 (U11), the ST485BDR, and J1, whose signal pins are surface-mount. A
box with through-hole parts only takes the [GCT USB4085-GF-A](https://www.tme.com/us/en-us/details/usb4085-gf-a/usb-ieee1394-connectors/gct/)
as J1 (0.75 €) and a DIP-8 chip in a socket as U7 (U11), with the isolation kept:

| | 1 output | 2 outputs, MAX487CPA+ | 2 outputs, SN75176BP |
|---|---|---|---|
| U7 (U11) | [SN75176BP](https://www.tme.com/us/en-us/details/sn75176bp/rs232-rs422-rs485-integr-circ/texas-instruments/), 1.80 € | [MAX487CPA+](https://www.tme.eu/en/details/max487cpa+/rs232-rs422-rs485-integr-circ/analog-devices-maxim-integrated/), 5.78 € each | SN75176BP, 1.80 € each |
| U3 | TRACO TMA 0505S, 1 W | TRACO TMA 0505S, 1 W | [AIMTEC AM2D-0505SZ](https://www.tme.eu/en/details/am2d-0505sz/dc-dc-converters/aimtec/), 2 W, 4.13 € |
| Current from USB, roughly | 0.22 A | 0.25 A | 0.35 A |
| **Per box** | **23.94 €** | **38.29 €** | **30.86 €** |
| Per box, with 23 % VAT | 29.44 € | 47.10 € | 37.96 € |
| First order | 41.38 € | 55.24 € | 47.81 € |
| First order, with 23 % VAT | 50.90 € | 67.95 € | 58.80 € |

- The SN75176BP is the transceiver of the Pico box: the same pins as the MAX485, and cheap, but it draws about
  40 mA more per output. With one output the 1 W converter covers that; with two it does not, which is why
  BOM.md says "not the SN75176BP here".
- Two outputs therefore need either a chip that draws little, the MAX487CPA+ (made for 250 kbit/s, about 0.12 mA;
  TME marks it hard to get), or a 2 W converter for two SN75176BPs. The 2 W converter is the cheaper way, but the
  box then draws about 0.35 A from USB, close to the 0.4 A it asks the computer for. The AM2D-0505SZ is SIP-7 like
  the TMA: check its pins before drawing the board.
- TME's EU site showed no SN75176BP in stock when the prices were collected. Its price and the USB4085-GF-A's are
  from TME's US site (tme.com), whose prices include US customs; tme.sk may differ.

## Without the isolation

U3 and the optocouplers keep the DMX lines electrically apart from the computer. DMX works without that, and many
cheap USB-DMX interfaces leave it out, but then the computer's ground is joined to pin 1 of the DMX cables: with
long cables, fixtures on other mains circuits or a faulty fixture, the data can be disturbed and a fault can reach
the computer's USB port. For a stage, keep the isolation.

Without it, the box would leave out U3, U4 to U6 (U8 to U10) with their sockets, R6, R8, R10 (R15, R17, R19) and
C10 to C13 (C15, C16). U7 (U11) would be a 3.3 V transceiver, the
[ST3485EBDR](https://www.tme.eu/en/details/st3485ebd/rs232-rs422-rs485-integr-circ/stmicroelectronics/st3485ebdr/)
(1.19 €), fed from +3V3 and wired straight to the PIC: TX to DI, RO to RX, the direction pin to DE and /RE. It has
to be a 3.3 V part: pin 24 (RB13, the RX of output 1) is also an analog input and does not take 5 V. U2 then feeds
the line drivers too, about 100 mA with one output and 130 mA with two, within its 250 mA. The firmware would need
one change: the direction pins would be high while the box sends (today they are low, because U6 turns the signal
around; see [board.c](../../firmware/pic32/src/board.c)). Such a box needs a changed schematic first. It would cost:

| | 1 output | 2 outputs |
|---|---|---|
| Per box | 16.73 € | 20.40 € |
| Per box, with 23 % VAT | 20.58 € | 25.09 € |
| First order | 30.50 € | 33.83 € |
| First order, with 23 % VAT | 37.52 € | 41.61 € |

## The programmer

The PIC gets its firmware once, through J2, from a programmer (see [BOM.md](BOM.md)). One programmer does any number
of boxes.

| Programmer | Price | Notes |
|---|---|---|
| [Microchip MPLAB Snap PG164100](https://www.tme.eu/de/details/mplab-snap/programmiergeraete/microchip-technology/pg164100/?brutto=1) at TME | 10.66 € (13.11 € with VAT) | The lowest price found: [RS in the UK](https://uk.rs-online.com/web/p/debuggers-in-circuit-emulators/1774116) asks £34.65. Works with the free MPLAB IPE. |
| PICkit 2 clone from AliExpress or Alibaba | about 6 to 7 € ([example](https://electronics.alibaba.com/product/pickit-2)) | Programs PIC32 chips with the free [pic32prog](https://github.com/sergev/pic32prog), which finds out the chip by itself. Not tried with this box yet. |
| PICkit 3, or a clone of it | about 20 € and more | Not worth it: dearer than the Snap, and MPLAB X and MPLAB IPE after version 6.20 no longer support it ([Microchip](https://www.microchip.com/en-us/about/media-center/blog/2024/discontinued-ide-support-for-gen3-tools)). |
