// dmxbox simulator: runs the exact same engine / Art-Net / control-protocol / RDM
// code as the box, but on your PC over normal UDP sockets, with lights on its
// DMX line that exist only in memory and answer RDM. Develop your PC software
// against this before the hardware exists. There are two builds:
//   dmxbox_sim   dmxbox 3 (one output, 512 channels)
//   dmxbox_sim2  the same with two outputs, which does not exist as hardware (for tests)
// sim.cmd at the root of the repository starts one as if the box were plugged in.
//
//   dmxbox_sim [--host 127.0.0.1] [--artnet-port 6454] [--ctrl-port 7000] [--drop 0.2]
//              [--presets presets.bin] [--show 16] [--quiet] [--parent 1234]
//              [--rdm-lights 3] [--box 0]
//
// --host binds both sockets to one address of the PC (every address without it;
// 127.0.0.1 keeps the simulator off the network, and Windows does not ask about
// its firewall). --drop P randomly drops a fraction P of control packets in both
// directions, to prove the retransmit/dedupe logic works. --show N prints the
// first N channels of every output when they change. --parent PID ends the
// simulator when that process ends (sim.cmd starts it so). --rdm-lights N puts
// N lights with RDM on the line (3 if not given, 0 to 64). --box N is the number
// of the box switch: the Art-Net universe is ARTNET_UNIVERSE + N.
//
// Builds on Windows (build.cmd, Visual Studio C compiler) and on Linux/macOS (make).
#ifdef _WIN32
#define WIN32_LEAN_AND_MEAN
#include <winsock2.h>
#include <ws2tcpip.h>
#include <windows.h>
#include <mswsock.h>
typedef SOCKET sock_t;
#else
#define _POSIX_C_SOURCE 200809L
#include <arpa/inet.h>
#include <netinet/in.h>
#include <sys/select.h>
#include <sys/socket.h>
#include <unistd.h>
typedef int sock_t;
#endif
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <time.h>

#include "artnet.h"
#include "ctrl.h"
#include "engine.h"
#include "platform.h"
#include "presets.h"
#include "rdm.h"
#include "rdm_lights.h"

static engine_t      g_engine;
static rdm_t         g_rdm;
static preset_bank_t g_bank;
static ctrl_t        g_ctrl;
static artnet_node_t g_artnet;
static const char   *g_preset_file = "presets.bin";

void plat_lock(void) {}
void plat_unlock(void) {}

// The RDM line: the lights in memory hear the packet at once.
static uint8_t line_answer[RDM_MAX_PKT];
static int     line_len;
static bool    line_garbled, line_waiting;

void plat_rdm_send(const uint8_t *pkt, int len, uint8_t expect) {
    (void)expect;
    line_len = lights_hear(pkt, len, line_answer, (int)sizeof line_answer, &line_garbled);
    line_waiting = true;
}

int plat_rdm_poll(uint8_t *raw, int cap, int *n, bool *garbled) {
    if (!line_waiting) return 0;
    line_waiting = false;
    *n = line_len < cap ? line_len : cap;
    memcpy(raw, line_answer, (size_t)*n);
    *garbled = line_garbled;
    return 1;
}

uint16_t plat_supply_mv(void) { return 5000; }

void plat_request_bootloader(void) { printf("the desk asked for the USB boot loader (a real box restarts now)\n"); }

bool plat_save_presets(const preset_bank_t *b) {
    FILE *f = fopen(g_preset_file, "wb");
    if (!f) return false;
    bool ok = fwrite(b, sizeof *b, 1, f) == 1;
    return fclose(f) == 0 && ok;
}

static void load_presets(void) {
    FILE *f = fopen(g_preset_file, "rb");
    if (!f || fread(&g_bank, sizeof g_bank, 1, f) != 1) presets_clear(&g_bank);
    else presets_validate(&g_bank);
    if (f) fclose(f);
}

// Monotonic milliseconds, like the Pico's time since boot.
static uint32_t now_ms(void) {
#ifdef _WIN32
    static LARGE_INTEGER freq;
    LARGE_INTEGER t;
    if (!freq.QuadPart) QueryPerformanceFrequency(&freq);
    QueryPerformanceCounter(&t);
    return (uint32_t)(t.QuadPart * 1000 / freq.QuadPart);
#else
    struct timespec ts;
    clock_gettime(CLOCK_MONOTONIC, &ts);
    return (uint32_t)(ts.tv_sec * 1000u + ts.tv_nsec / 1000000u);
#endif
}

static void die(const char *what) {
#ifdef _WIN32
    fprintf(stderr, "%s: winsock error %d\n", what, WSAGetLastError());
#else
    perror(what);
#endif
    exit(1);
}

static uint32_t g_host = INADDR_ANY;   // the address to bind to, in host order

static sock_t udp_bind(uint16_t port) {
    sock_t s = socket(AF_INET, SOCK_DGRAM, 0);
    int one = 1;
#ifdef _WIN32
    if (s == INVALID_SOCKET) die("socket");
    // Windows reports "port unreachable" from an earlier sendto() as an error
    // on the next recvfrom(). A UDP server does not care: switch that off.
    BOOL off = FALSE;
    DWORD ret = 0;
    WSAIoctl(s, SIO_UDP_CONNRESET, &off, sizeof off, NULL, 0, &ret, NULL, NULL);
#else
    if (s < 0) die("socket");
    setsockopt(s, SOL_SOCKET, SO_REUSEADDR, (const char *)&one, sizeof one);
#endif
    setsockopt(s, SOL_SOCKET, SO_BROADCAST, (const char *)&one, sizeof one);
    struct sockaddr_in a;
    memset(&a, 0, sizeof a);
    a.sin_family = AF_INET;
    a.sin_port = htons(port);
    a.sin_addr.s_addr = htonl(g_host);
    if (bind(s, (struct sockaddr *)&a, sizeof a) != 0) die("bind");
    return s;
}

// "127.0.0.1" in host order; false for anything that is no IPv4 address.
static bool parse_ipv4(const char *text, uint32_t *out) {
    unsigned a, b, c, d;
    char rest;
    if (sscanf(text, "%u.%u.%u.%u%c", &a, &b, &c, &d, &rest) != 4 || a > 255 || b > 255 || c > 255 || d > 255)
        return false;
    *out = (a << 24) | (b << 16) | (c << 8) | d;
    return true;
}

static double g_drop = 0.0;
static bool dropped(void) { return g_drop > 0 && (double)rand() / RAND_MAX < g_drop; }

// The process that started the simulator (--parent): when it ends, so does the simulator.
#ifdef _WIN32
static HANDLE g_parent = NULL;
static void watch_parent(unsigned long pid) { g_parent = OpenProcess(SYNCHRONIZE, FALSE, (DWORD)pid); }
static bool parent_gone(void) { return g_parent != NULL && WaitForSingleObject(g_parent, 0) == WAIT_OBJECT_0; }
#else
static pid_t g_parent = 0;
static void watch_parent(unsigned long pid) { g_parent = (pid_t)pid; }
static bool parent_gone(void) { return g_parent != 0 && getppid() != g_parent; }
#endif

int main(int argc, char **argv) {
    uint16_t artnet_port = ARTNET_PORT, ctrl_port = CTRL_PORT;
    int show = 16;
    bool quiet = false;
    int rdm_lights = 3, box = 0;
    for (int i = 1; i < argc; i++) {
        if (!strcmp(argv[i], "--host") && i + 1 < argc) {
            if (!parse_ipv4(argv[++i], &g_host)) { fprintf(stderr, "--host: \"%s\" is no IPv4 address\n", argv[i]); return 2; }
        }
        else if (!strcmp(argv[i], "--artnet-port") && i + 1 < argc) artnet_port = (uint16_t)atoi(argv[++i]);
        else if (!strcmp(argv[i], "--ctrl-port") && i + 1 < argc) ctrl_port = (uint16_t)atoi(argv[++i]);
        else if (!strcmp(argv[i], "--drop") && i + 1 < argc) g_drop = atof(argv[++i]);
        else if (!strcmp(argv[i], "--presets") && i + 1 < argc) g_preset_file = argv[++i];
        else if (!strcmp(argv[i], "--show") && i + 1 < argc) show = atoi(argv[++i]);
        else if (!strcmp(argv[i], "--quiet")) quiet = true;
        else if (!strcmp(argv[i], "--parent") && i + 1 < argc) watch_parent(strtoul(argv[++i], NULL, 10));
        else if (!strcmp(argv[i], "--rdm-lights") && i + 1 < argc) rdm_lights = atoi(argv[++i]);
        else if (!strcmp(argv[i], "--box") && i + 1 < argc) box = atoi(argv[++i]);
        else { fprintf(stderr, "usage: see top of sim.c\n"); return 2; }
    }
    if (show > DMX_OUTPUT_CHANNELS) show = DMX_OUTPUT_CHANNELS;
    srand((unsigned)time(NULL));
#ifdef _WIN32
    setvbuf(stdout, NULL, _IONBF, 0);      // the Windows C runtime has no line buffering
    WSADATA wsa;
    if (WSAStartup(MAKEWORD(2, 2), &wsa) != 0) die("WSAStartup");
#else
    setvbuf(stdout, NULL, _IOLBF, 0);
#endif

    engine_init(&g_engine);
    load_presets();
    ctrl_init(&g_ctrl, &g_engine, &g_bank, now_ms());
    lights_make(rdm_lights);
    static const uint8_t own_uid[6] = {0x7F, 0xF0, 0x00, 0x00, 0x00, 0x01};
    rdm_init(&g_rdm, own_uid);
    rdm_start_discovery(&g_rdm);                       // a box searches its line when it starts
    g_ctrl.rdm = &g_rdm;
    g_ctrl.can = CTRL_CAN_RDM | CTRL_CAN_MIDI | CTRL_CAN_CHAIN;
    g_ctrl.box_number = (uint8_t)box;
    // The address the node gives in its ArtPollReply: the one it is bound to, or 127.0.0.1.
    uint32_t own = g_host == INADDR_ANY ? 0x7F000001u : g_host;
    g_artnet = (artnet_node_t){.eng = &g_engine, .universe = (uint16_t)(ARTNET_UNIVERSE + box), .rdm = &g_rdm,
                               .ip = {(uint8_t)(own >> 24), (uint8_t)(own >> 16), (uint8_t)(own >> 8), (uint8_t)own},
                               .mac = {0x02, 0, 0, 0, 0, 1}};

    sock_t s_art = udp_bind(artnet_port), s_ctl = udp_bind(ctrl_port);
    printf("dmxbox sim \"%s\": %d output%s (%d channels), Art-Net on udp/%u (universe %d to %d), control on udp/%u, "
           "%d RDM light%s, drop=%.2f\n",
           NODE_SHORT_NAME, DMX_OUTPUTS, DMX_OUTPUTS == 1 ? "" : "s", DMX_CHANNELS, artnet_port, ARTNET_UNIVERSE + box,
           ARTNET_UNIVERSE + box + DMX_OUTPUTS - 1, ctrl_port, light_count, light_count == 1 ? "" : "s", g_drop);

    static uint8_t buf[2048], resp[CTRL_MAX_PKT], frame[DMX_CHANNELS], shown[DMX_CHANNELS];
    uint32_t next_frame = now_ms(), last_print = 0;
    // where the answers go that come later (RDM)
    struct sockaddr_in ctl_peer, art_peer;
    socklen_t ctl_peer_len = 0, art_peer_len = 0;

    for (;;) {
        uint32_t now = now_ms();
        int32_t wait = (int32_t)(next_frame - now);
        if (wait < 0) wait = 0;
        if (g_rdm.discovering || g_rdm.req_owner != RDM_OWNER_NONE) wait = 0;   // keep the RDM line going
        fd_set rf;
        FD_ZERO(&rf);
        FD_SET(s_art, &rf);
        FD_SET(s_ctl, &rf);
        struct timeval tv = {.tv_sec = 0, .tv_usec = wait * 1000};
        int maxfd = (int)(s_art > s_ctl ? s_art : s_ctl);    // ignored by Winsock
        if (select(maxfd + 1, &rf, NULL, NULL, &tv) < 0) die("select");

        for (int k = 0; k < 2; k++) {
            sock_t s = k == 0 ? s_art : s_ctl;
            if (!FD_ISSET(s, &rf)) continue;
            struct sockaddr_in from;
            socklen_t fl = sizeof from;
            int n = (int)recvfrom(s, (char *)buf, sizeof buf, 0, (struct sockaddr *)&from, &fl);
            if (n <= 0) continue;
            int r;
            if (s == s_art) {
                r = artnet_handle(&g_artnet, buf, n, now_ms(), resp, sizeof resp);
                if (g_artnet.deferred_new) { g_artnet.deferred_new = 0; art_peer = from; art_peer_len = fl; }
            } else {
                if (dropped()) continue;                         // lost on the way in
                r = ctrl_handle(&g_ctrl, buf, n, now_ms(), resp, sizeof resp);
                if (g_ctrl.deferred_new) { g_ctrl.deferred_new = 0; ctl_peer = from; ctl_peer_len = fl; }
                if (r > 0 && dropped()) r = 0;                   // ACK lost on the way back
            }
            if (r > 0) sendto(s, (const char *)resp, r, 0, (struct sockaddr *)&from, fl);
        }

        // RDM: the line answers at once here, so a few steps per pass
        for (int step = 0; step < 40 && (g_rdm.discovering || g_rdm.req_owner != RDM_OWNER_NONE); step++)
            rdm_poll(&g_rdm, now_ms());
        int later = ctrl_poll(&g_ctrl, now_ms(), resp, sizeof resp);
        if (later > 0 && ctl_peer_len && !dropped())
            sendto(s_ctl, (const char *)resp, later, 0, (struct sockaddr *)&ctl_peer, ctl_peer_len);
        later = artnet_poll(&g_artnet, resp, sizeof resp);
        if (later > 0 && art_peer_len)
            sendto(s_art, (const char *)resp, later, 0, (struct sockaddr *)&art_peer, art_peer_len);

        now = now_ms();
        if ((int32_t)(now - next_frame) >= 0) {                 // ~43 Hz, like the real box
            next_frame = now + 23;
            if (parent_gone()) return 0;
            engine_render(&g_engine, now, frame, true);
            if (!quiet && show > 0 && now - last_print >= 100) {
                for (int out = 0; out < DMX_OUTPUTS; out++) {
                    const uint8_t *levels = frame + out * DMX_OUTPUT_CHANNELS;
                    uint8_t *before = shown + out * DMX_OUTPUT_CHANNELS;
                    if (!memcmp(levels, before, (size_t)show)) continue;
                    last_print = now;
                    memcpy(before, levels, (size_t)show);
                    if (DMX_OUTPUTS == 1) printf("DMX 1-%d:", show);
                    else printf("DMX output %d, 1-%d:", out + 1, show);
                    for (int i = 0; i < show; i++) printf(" %3u", levels[i]);
                    printf("\n");
                }
            }
        }
    }
}
