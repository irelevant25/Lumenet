#include "enc28j60.h"

// ---- SPI commands (datasheet DS39662, table 4-1)
#define OP_RCR 0x00   // read control register
#define OP_RBM 0x3A   // read buffer memory
#define OP_WCR 0x40   // write control register
#define OP_WBM 0x7A   // write buffer memory
#define OP_BFS 0x80   // bit field set   (ETH registers only)
#define OP_BFC 0xA0   // bit field clear (ETH registers only)
#define OP_SRC 0xFF   // system reset

// ---- Registers. One byte encodes everything needed to reach a register:
//      bits 0-4 address, bits 5-6 bank, bit 7 = MAC/MII register (these send
//      one dummy byte before the value when read).
#define ETH(bank, addr) ((uint8_t)(((bank) << 5) | (addr)))
#define MAC(bank, addr) ((uint8_t)(0x80 | ((bank) << 5) | (addr)))
#define REG_ADDR(r) ((uint8_t)((r) & 0x1F))
#define REG_BANK(r) ((uint8_t)(((r) >> 5) & 0x03))
#define REG_HAS_DUMMY(r) ((r) & 0x80)

// present in all four banks
#define EIE   ETH(0, 0x1B)
#define EIR   ETH(0, 0x1C)
#define ESTAT ETH(0, 0x1D)
#define ECON2 ETH(0, 0x1E)
#define ECON1 ETH(0, 0x1F)
// bank 0: buffer pointers (each is a low/high pair)
#define ERDPTL   ETH(0, 0x00)
#define EWRPTL   ETH(0, 0x02)
#define ETXSTL   ETH(0, 0x04)
#define ETXNDL   ETH(0, 0x06)
#define ERXSTL   ETH(0, 0x08)
#define ERXNDL   ETH(0, 0x0A)
#define ERXRDPTL ETH(0, 0x0C)
// bank 1
#define ERXFCON  ETH(1, 0x18)
#define EPKTCNT  ETH(1, 0x19)
// bank 2: MAC and MII
#define MACON1   MAC(2, 0x00)
#define MACON3   MAC(2, 0x02)
#define MACON4   MAC(2, 0x03)
#define MABBIPG  MAC(2, 0x04)
#define MAIPGL   MAC(2, 0x06)
#define MAIPGH   MAC(2, 0x07)
#define MAMXFLL  MAC(2, 0x0A)
#define MICMD    MAC(2, 0x12)
#define MIREGADR MAC(2, 0x14)
#define MIWRL    MAC(2, 0x16)
#define MIWRH    MAC(2, 0x17)
#define MIRDL    MAC(2, 0x18)
#define MIRDH    MAC(2, 0x19)
// bank 3 (note the odd order of the MAC address bytes)
#define MAADR5   MAC(3, 0x00)
#define MAADR6   MAC(3, 0x01)
#define MAADR3   MAC(3, 0x02)
#define MAADR4   MAC(3, 0x03)
#define MAADR1   MAC(3, 0x04)
#define MAADR2   MAC(3, 0x05)
#define MISTAT   MAC(3, 0x0A)
#define EREVID   ETH(3, 0x12)

// bits
#define ECON1_TXRST 0x80
#define ECON1_RXRST 0x40
#define ECON1_TXRTS 0x08
#define ECON1_RXEN  0x04
#define ECON1_BSEL  0x03
#define ECON2_AUTOINC 0x80
#define ECON2_PKTDEC  0x40
#define EIR_TXIF    0x08
#define EIR_TXERIF  0x02
#define ERXFCON_UCEN  0x80   // accept frames addressed to our MAC
#define ERXFCON_CRCEN 0x20   // ...only with a valid CRC
#define ERXFCON_BCEN  0x01   // accept broadcast frames
#define MACON1_MARXEN  0x01
#define MACON3_PADCFG0 0x20  // pad short frames to 60 bytes, then add the CRC
#define MACON3_TXCRCEN 0x10
#define MACON3_FRMLNEN 0x02
#define MACON4_DEFER   0x40
#define MICMD_MIIRD    0x01
#define MISTAT_BUSY    0x01
// PHY registers (reached through the MII registers)
#define PHCON1  0x00
#define PHCON2  0x10
#define PHSTAT2 0x11
#define PHCON2_HDLDIS 0x0100   // do not loop our own transmissions back
#define PHSTAT2_LSTAT 0x0400   // link is up

// ---- The chip's 8 KB buffer: receive ring at the bottom (it must start at 0,
//      errata), room for one transmit frame at the top.
#define RX_START 0x0000
#define RX_END   0x19FF   // odd on purpose (errata: ERXRDPT must hold an odd value)
#define TX_START 0x1A00
#define TX_END   0x1FFF
#define MAX_FRAME_WITH_CRC 1518

static const enc_hal_t *hal;
static uint8_t  bank;          // bank currently selected in the chip
static uint16_t next_packet;   // buffer address of the next unread frame
static uint8_t  revision;

// ---------------------------------------------------------------- SPI access
static void write_op(uint8_t op, uint8_t reg, uint8_t value) {
    uint8_t tx[2] = {(uint8_t)(op | REG_ADDR(reg)), value};
    hal->select(true);
    hal->transfer(tx, NULL, 2);
    hal->select(false);
}

static void select_bank(uint8_t reg) {
    if (REG_ADDR(reg) >= 0x1B) return;            // EIE..ECON1 exist in every bank
    if (REG_BANK(reg) == bank) return;
    bank = REG_BANK(reg);
    write_op(OP_BFC, ECON1, ECON1_BSEL);
    write_op(OP_BFS, ECON1, bank);
}

static uint8_t rd(uint8_t reg) {
    uint8_t tx[3] = {(uint8_t)(OP_RCR | REG_ADDR(reg)), 0, 0}, rx[3];
    size_t n = REG_HAS_DUMMY(reg) ? 3 : 2;
    select_bank(reg);
    hal->select(true);
    hal->transfer(tx, rx, n);
    hal->select(false);
    return rx[n - 1];
}

static void wr(uint8_t reg, uint8_t value) { select_bank(reg); write_op(OP_WCR, reg, value); }
static void set_bits(uint8_t reg, uint8_t mask) { select_bank(reg); write_op(OP_BFS, reg, mask); }
static void clear_bits(uint8_t reg, uint8_t mask) { select_bank(reg); write_op(OP_BFC, reg, mask); }

// 16-bit registers are a low/high pair; the low byte goes first.
static void wr16(uint8_t reg_low, uint16_t value) {
    wr(reg_low, (uint8_t)value);
    wr((uint8_t)(reg_low + 1), (uint8_t)(value >> 8));
}
static uint16_t rd16(uint8_t reg_low) {
    uint16_t lo = rd(reg_low);
    return (uint16_t)(lo | (rd((uint8_t)(reg_low + 1)) << 8));
}

static void buffer_read(uint8_t *data, size_t len) {
    uint8_t op = OP_RBM;
    hal->select(true);
    hal->transfer(&op, NULL, 1);
    hal->transfer(NULL, data, len);
    hal->select(false);
}

static void buffer_write(const uint8_t *data, size_t len) {
    uint8_t op = OP_WBM;
    hal->select(true);
    hal->transfer(&op, NULL, 1);
    hal->transfer(data, NULL, len);
    hal->select(false);
}

// ---------------------------------------------------------------- PHY access
static void phy_wait(void) {
    for (int i = 0; i < 100 && (rd(MISTAT) & MISTAT_BUSY); i++) hal->delay_us(15);
}

static void phy_write(uint8_t reg, uint16_t value) {
    wr(MIREGADR, reg);
    wr(MIWRL, (uint8_t)value);
    wr(MIWRH, (uint8_t)(value >> 8));   // writing the high byte starts the transfer
    hal->delay_us(15);
    phy_wait();
}

static uint16_t phy_read(uint8_t reg) {
    wr(MIREGADR, reg);
    wr(MICMD, MICMD_MIIRD);
    hal->delay_us(15);
    phy_wait();
    wr(MICMD, 0);
    return rd16(MIRDL);
}

// ---------------------------------------------------------------- setup
static void rx_pointers(void) {
    next_packet = RX_START;
    wr16(ERXSTL, RX_START);
    wr16(ERXRDPTL, RX_END);
    wr16(ERXNDL, RX_END);
}

bool enc_init(const enc_hal_t *h, const uint8_t mac[6]) {
    hal = h;
    uint8_t op = OP_SRC;
    hal->select(true);
    hal->transfer(&op, NULL, 1);
    hal->select(false);
    hal->delay_us(2000);     // errata: after a reset wait at least 1 ms instead of polling CLKRDY
    bank = 0;

    rx_pointers();
    wr16(ETXSTL, TX_START);
    wr16(ETXNDL, TX_END);
    // A missing chip reads back 0x00 or 0xFF everywhere.
    if (rd16(ERXNDL) != RX_END || rd16(ETXSTL) != TX_START) return false;
    revision = (uint8_t)(rd(EREVID) & 0x1F);

    wr(ERXFCON, ERXFCON_UCEN | ERXFCON_CRCEN | ERXFCON_BCEN);
    wr(MACON1, MACON1_MARXEN);
    wr(MACON3, MACON3_PADCFG0 | MACON3_TXCRCEN | MACON3_FRMLNEN);   // half duplex
    wr(MACON4, MACON4_DEFER);
    wr16(MAMXFLL, MAX_FRAME_WITH_CRC);
    wr(MABBIPG, 0x12);       // inter-packet gaps for half duplex (datasheet 6.5)
    wr(MAIPGL, 0x12);
    wr(MAIPGH, 0x0C);
    wr(MAADR1, mac[0]);
    wr(MAADR2, mac[1]);
    wr(MAADR3, mac[2]);
    wr(MAADR4, mac[3]);
    wr(MAADR5, mac[4]);
    wr(MAADR6, mac[5]);
    phy_write(PHCON1, 0x0000);            // PHY half duplex too (must match MACON3)
    phy_write(PHCON2, PHCON2_HDLDIS);

    wr(EIE, 0);                           // no interrupts: the main loop polls
    set_bits(ECON2, ECON2_AUTOINC);
    set_bits(ECON1, ECON1_RXEN);
    return true;
}

uint8_t enc_revision(void) { return revision; }

bool enc_link_up(void) { return (phy_read(PHSTAT2) & PHSTAT2_LSTAT) != 0; }

// ---------------------------------------------------------------- receive
int enc_receive(uint8_t *frame, int cap) {
    if (rd(EPKTCNT) == 0) return 0;

    // Every frame in the ring is preceded by 6 bytes: address of the next
    // frame (2), length including CRC (2), status (2).
    uint8_t head[6];
    wr16(ERDPTL, next_packet);
    buffer_read(head, sizeof head);
    uint16_t next = (uint16_t)(head[0] | (head[1] << 8));
    int      len  = (head[2] | (head[3] << 8)) - 4;
    bool     ok   = (head[4] & 0x80) != 0;            // "received ok"

    if (next > RX_END || (next & 1) || len > MAX_FRAME_WITH_CRC) {
        // The ring no longer makes sense (should not happen): restart the receiver.
        clear_bits(ECON1, ECON1_RXEN);
        set_bits(ECON1, ECON1_RXRST);
        clear_bits(ECON1, ECON1_RXRST);
        rx_pointers();
        while (rd(EPKTCNT)) set_bits(ECON2, ECON2_PKTDEC);
        set_bits(ECON1, ECON1_RXEN);
        return -1;
    }

    int result = -1;
    if (ok && len >= 14 && len <= cap) {
        buffer_read(frame, (size_t)len);              // the chip wraps around the ring by itself
        result = len;
    }

    // Give the space back. ERXRDPT must never be even (errata), so it trails
    // the next frame by one byte.
    next_packet = next;
    wr16(ERXRDPTL, next == RX_START ? RX_END : (uint16_t)(next - 1));
    set_bits(ECON2, ECON2_PKTDEC);
    return result;
}

// ---------------------------------------------------------------- transmit
bool enc_send(const uint8_t *frame, int len) {
    if (len < 14 || len > ENC_MAX_FRAME) return false;

    // The previous frame is normally long gone; with collisions it can take a while.
    for (int i = 0; i < 2000 && (rd(ECON1) & ECON1_TXRTS); i++) hal->delay_us(10);
    if ((rd(ECON1) & ECON1_TXRTS) || (rd(EIR) & EIR_TXERIF)) {
        // Errata: after an aborted transmission the transmit logic can stay stuck.
        set_bits(ECON1, ECON1_TXRST);
        clear_bits(ECON1, ECON1_TXRST);
    }
    clear_bits(EIR, EIR_TXIF | EIR_TXERIF);

    uint8_t control = 0x00;                           // per-packet control byte: use the MACON3 settings
    wr16(EWRPTL, TX_START);
    buffer_write(&control, 1);
    buffer_write(frame, (size_t)len);
    wr16(ETXSTL, TX_START);
    wr16(ETXNDL, (uint16_t)(TX_START + len));         // address of the last byte of the frame
    set_bits(ECON1, ECON1_TXRTS);                     // send it
    return true;
}
