#include "enc_model.h"
#include <string.h>

// register addresses used by the model (bank, address) - see datasheet table 3-1
enum { ERDPT = 0x00, EWRPT = 0x02, ETXST = 0x04, ETXND = 0x06, ERXST = 0x08, ERXND = 0x0A, ERXRDPT = 0x0C };
enum { EIE = 0x1B, EIR = 0x1C, ESTAT = 0x1D, ECON2 = 0x1E, ECON1 = 0x1F };
enum { B1_ERXFCON = 0x18, B1_EPKTCNT = 0x19 };
enum { B2_MICMD = 0x12, B2_MIREGADR = 0x14, B2_MIWRL = 0x16, B2_MIWRH = 0x17, B2_MIRDL = 0x18, B2_MIRDH = 0x19 };
enum { B3_MAADR5 = 0x00, B3_MISTAT = 0x0A, B3_EREVID = 0x12 };

#define MAX_SENT 16

static uint8_t  reg[4][0x20];      // EIE..ECON1 live in reg[0]
static uint8_t  mem[0x2000];
static uint16_t phy[0x20];
static bool     present, link_up;
static uint16_t rx_write;          // where the receiver stores the next frame
static int      busy_reads;        // MISTAT.BUSY stays set for this many reads

static struct { uint8_t data[1600]; int len; } sent[MAX_SENT];
static int sent_n;

static int violations;
static const char *last_violation = "";

// SPI transaction state
static bool    selected;
static int     byte_no;
static uint8_t op, arg;

static void violation(const char *what) { violations++; last_violation = what; }

static int cur_bank(void) { return reg[0][ECON1] & 0x03; }
static uint8_t *slot(int addr) { return addr >= 0x1B ? &reg[0][addr] : &reg[cur_bank()][addr]; }
static uint16_t ptr(int addr) { return (uint16_t)((reg[0][addr] | (reg[0][addr + 1] << 8)) & 0x1FFF); }
static void set_ptr(int addr, uint16_t v) { reg[0][addr] = (uint8_t)v; reg[0][addr + 1] = (uint8_t)((v >> 8) & 0x1F); }

// MAC and MII registers answer a read with a dummy byte first.
static bool is_mac_mii(int bank, int addr) {
    if (addr >= 0x1B) return false;
    if (bank == 2) return addr <= 0x19;
    if (bank == 3) return addr <= 0x05 || addr == B3_MISTAT;
    return false;
}

void model_power_on(bool chip_present) {
    memset(reg, 0, sizeof reg);
    memset(mem, 0xEE, sizeof mem);
    memset(phy, 0, sizeof phy);
    present = chip_present;
    link_up = true;
    sent_n = 0;
    violations = 0;
    last_violation = "";
    selected = false;
    busy_reads = 0;
    // reset values from the datasheet
    set_ptr(ERDPT, 0x05FA);
    set_ptr(ERXST, 0x05FA);
    set_ptr(ERXND, 0x1FFF);
    set_ptr(ERXRDPT, 0x05FA);
    reg[0][ECON2] = 0x80;
    reg[1][B1_ERXFCON] = 0xA1;
    reg[2][0x0B] = 0x06;           // MAMXFLH
    reg[3][B3_EREVID] = 0x06;      // silicon revision B7
    phy[0x10] = 0x0000;
    rx_write = 0x05FA;
}

void model_set_link(bool up) { link_up = up; }
uint8_t model_reg(int bank, int addr) { return addr >= 0x1B ? reg[0][addr] : reg[bank][addr]; }
uint16_t model_phy(int addr) { return phy[addr]; }
int model_rule_violations(void) { return violations; }
const char *model_last_violation(void) { return last_violation; }
int model_sent_count(void) { return sent_n; }
const uint8_t *model_sent(int i, int *len) { *len = sent[i].len; return sent[i].data; }
void model_clear_sent(void) { sent_n = 0; }

// ---------------------------------------------------------------- transmit
static void transmit(void) {
    uint16_t st = ptr(ETXST), nd = ptr(ETXND);
    if (nd <= st) { violation("ETXND not above ETXST"); return; }
    if (mem[st] != 0x00) violation("per-packet control byte is not 0");
    int len = nd - st;                               // bytes after the control byte
    if (sent_n < MAX_SENT && len <= (int)sizeof sent[0].data) {
        memcpy(sent[sent_n].data, &mem[st + 1], (size_t)len);
        sent[sent_n].len = len;
        sent_n++;
    }
    reg[0][ECON1] &= (uint8_t)~0x08;                 // TXRTS clears itself
    reg[0][EIR] |= 0x08;                             // TXIF
}

// ---------------------------------------------------------------- register writes
static void written(int bank, int addr, uint8_t before) {
    uint8_t v = *slot(addr);
    if (addr == ECON1) {
        if ((v & 0x08) && !(before & 0x08)) transmit();
        return;
    }
    if (addr == ECON2) {
        if (v & 0x40) {                              // PKTDEC: one packet handled
            if (reg[1][B1_EPKTCNT]) reg[1][B1_EPKTCNT]--;
            else violation("PKTDEC with EPKTCNT = 0");
            reg[0][ECON2] &= (uint8_t)~0x40;
        }
        return;
    }
    if (addr >= 0x1B) return;
    if (bank == 0 && addr == ERXRDPT + 1) {
        uint16_t p = ptr(ERXRDPT);
        if (!(p & 1)) violation("even value written to ERXRDPT (errata)");
        if (p < ptr(ERXST) || p > ptr(ERXND)) violation("ERXRDPT outside the receive buffer");
    }
    if (bank == 0 && (addr == ERXST + 1 || addr == ERXND + 1)) rx_write = ptr(ERXST);
    if (bank == 1 && addr == B1_EPKTCNT) { reg[1][B1_EPKTCNT] = before; violation("write to read-only EPKTCNT"); }
    if (bank == 2 && addr == B2_MIWRH) {
        phy[reg[2][B2_MIREGADR] & 0x1F] = (uint16_t)(reg[2][B2_MIWRL] | (reg[2][B2_MIWRH] << 8));
        busy_reads = 2;
    }
    if (bank == 2 && addr == B2_MICMD && (v & 0x01)) {
        uint16_t value = phy[reg[2][B2_MIREGADR] & 0x1F];
        if ((reg[2][B2_MIREGADR] & 0x1F) == 0x11) value = (uint16_t)(link_up ? 0x0400 : 0x0000);   // PHSTAT2.LSTAT
        reg[2][B2_MIRDL] = (uint8_t)value;
        reg[2][B2_MIRDH] = (uint8_t)(value >> 8);
        busy_reads = 2;
    }
}

static uint8_t read_reg(int bank, int addr) {
    if (bank == 3 && addr == B3_MISTAT) {
        if (busy_reads > 0) { busy_reads--; return 0x01; }
        return 0x00;
    }
    return *slot(addr);
}

// ---------------------------------------------------------------- SPI
static void hal_select(bool active) {
    if (active && selected) violation("CS asserted twice");
    selected = active;
    byte_no = 0;
}

static uint8_t spi_byte(uint8_t in) {
    if (!present) return 0xFF;
    if (!selected) { violation("SPI clocked without CS"); return 0xFF; }
    int n = byte_no++;
    if (n == 0) {
        op = in & 0xE0;
        arg = in & 0x1F;
        if (in == 0xFF) {                             // system reset
            bool l = link_up;
            int keep = violations;
            const char *kept = last_violation;
            model_power_on(true);
            link_up = l;
            violations = keep;
            last_violation = kept;
            selected = true;
        }
        return 0;
    }
    int bank = cur_bank();
    switch (op) {
    case 0x00:                                        // read control register
        if (is_mac_mii(bank, arg) && n == 1) return 0;            // dummy byte
        return read_reg(bank, arg);
    case 0x20: {                                      // read buffer memory (arg must be 0x1A)
        if (arg != 0x1A) violation("bad RBM opcode");
        uint16_t p = ptr(ERDPT);
        uint8_t v = mem[p];
        if (reg[0][ECON2] & 0x80) set_ptr(ERDPT, p == ptr(ERXND) ? ptr(ERXST) : (uint16_t)((p + 1) & 0x1FFF));
        return v;
    }
    case 0x40: {                                      // write control register
        if (n > 1) { violation("more than one byte in WCR"); return 0; }
        uint8_t before = *slot(arg);
        *slot(arg) = in;
        written(bank, arg, before);
        return 0;
    }
    case 0x60: {                                      // write buffer memory (arg must be 0x1A)
        if (arg != 0x1A) violation("bad WBM opcode");
        uint16_t p = ptr(EWRPT);
        mem[p] = in;
        if (reg[0][ECON2] & 0x80) set_ptr(EWRPT, (uint16_t)((p + 1) & 0x1FFF));
        return 0;
    }
    case 0x80:                                        // bit field set / clear: ETH registers only
    case 0xA0: {
        if (is_mac_mii(bank, arg)) { violation("BFS/BFC used on a MAC/MII register"); return 0; }
        uint8_t before = *slot(arg);
        *slot(arg) = op == 0x80 ? (uint8_t)(before | in) : (uint8_t)(before & ~in);
        written(bank, arg, before);
        return 0;
    }
    default:
        violation("unknown SPI opcode");
        return 0;
    }
}

static void hal_transfer(const uint8_t *tx, uint8_t *rx, size_t len) {
    for (size_t i = 0; i < len; i++) {
        uint8_t out = spi_byte(tx ? tx[i] : 0);
        if (rx) rx[i] = out;
    }
}

static void hal_delay_us(uint32_t us) { (void)us; }

const enc_hal_t MODEL_HAL = {hal_select, hal_transfer, hal_delay_us};

// ---------------------------------------------------------------- receive side
static void ring_put(uint8_t v) {
    mem[rx_write] = v;
    rx_write = rx_write == ptr(ERXND) ? ptr(ERXST) : (uint16_t)(rx_write + 1);
}

bool model_wire_in(const uint8_t *frame, int len) {
    static const uint8_t bcast[6] = {0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF};
    if (!present || !(reg[0][ECON1] & 0x04)) return false;        // receiver off
    uint8_t mac[6] = {reg[3][4], reg[3][5], reg[3][2], reg[3][3], reg[3][0], reg[3][1]};
    uint8_t filter = reg[1][B1_ERXFCON];
    bool for_us = (filter & 0x80) && memcmp(frame, mac, 6) == 0;
    bool broadcast = (filter & 0x01) && memcmp(frame, bcast, 6) == 0;
    if (!for_us && !broadcast) return false;
    if (len + 4 > (reg[2][0x0A] | (reg[2][0x0B] << 8)) && (reg[2][0x02] & 0x02)) return false;   // too long (FRMLNEN)

    // free space: the receiver never writes onto the byte ERXRDPT points at
    int size = ptr(ERXND) - ptr(ERXST) + 1;
    int used = (rx_write - ptr(ERXRDPT) - 1 + size) % size;
    int need = 6 + len + 4 + ((len + 4) & 1);
    if (need > size - used - 1) { reg[0][EIR] |= 0x01; return false; }           // RXERIF: buffer full

    uint16_t start = rx_write;
    int total = len + 4;                                                         // with CRC
    uint16_t next = (uint16_t)(ptr(ERXST) + (start - ptr(ERXST) + 6 + total + (total & 1)) % size);
    ring_put((uint8_t)next);
    ring_put((uint8_t)(next >> 8));
    ring_put((uint8_t)total);
    ring_put((uint8_t)(total >> 8));
    ring_put((uint8_t)(0x80 | (broadcast ? 0x02 : 0)));                          // received ok (+ broadcast)
    ring_put(0x00);
    for (int i = 0; i < len; i++) ring_put(frame[i]);
    for (int i = 0; i < 4; i++) ring_put(0xCC);                                  // stand-in for the CRC
    if (total & 1) ring_put(0x00);                                               // frames start on even addresses
    reg[1][B1_EPKTCNT]++;
    reg[0][EIR] |= 0x40;                                                         // PKTIF
    return true;
}
