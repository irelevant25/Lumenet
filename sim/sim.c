// dmxbox simulator: runs the exact same engine / Art-Net / control-protocol
// code as the Pico, but on your PC over normal UDP sockets. Develop your PC
// software against this before the hardware exists.
//
//   dmxbox_sim [--artnet-port 6454] [--ctrl-port 7000] [--drop 0.2]
//              [--presets presets.bin] [--show 16] [--quiet]
//
// --drop P randomly drops a fraction P of control packets in both directions,
// to prove the retransmit/dedupe logic works.
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

static engine_t      g_engine;
static preset_bank_t g_bank;
static ctrl_t        g_ctrl;
static artnet_node_t g_artnet;
static const char   *g_preset_file = "presets.bin";

void plat_lock(void) {}
void plat_unlock(void) {}

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
    a.sin_addr.s_addr = htonl(INADDR_ANY);
    if (bind(s, (struct sockaddr *)&a, sizeof a) != 0) die("bind");
    return s;
}

static double g_drop = 0.0;
static bool dropped(void) { return g_drop > 0 && (double)rand() / RAND_MAX < g_drop; }

int main(int argc, char **argv) {
    uint16_t artnet_port = ARTNET_PORT, ctrl_port = CTRL_PORT;
    int show = 16;
    bool quiet = false;
    for (int i = 1; i < argc; i++) {
        if (!strcmp(argv[i], "--artnet-port") && i + 1 < argc) artnet_port = (uint16_t)atoi(argv[++i]);
        else if (!strcmp(argv[i], "--ctrl-port") && i + 1 < argc) ctrl_port = (uint16_t)atoi(argv[++i]);
        else if (!strcmp(argv[i], "--drop") && i + 1 < argc) g_drop = atof(argv[++i]);
        else if (!strcmp(argv[i], "--presets") && i + 1 < argc) g_preset_file = argv[++i];
        else if (!strcmp(argv[i], "--show") && i + 1 < argc) show = atoi(argv[++i]);
        else if (!strcmp(argv[i], "--quiet")) quiet = true;
        else { fprintf(stderr, "usage: see top of sim.c\n"); return 2; }
    }
    if (show > DMX_CHANNELS) show = DMX_CHANNELS;
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
    g_artnet = (artnet_node_t){.eng = &g_engine, .universe = ARTNET_UNIVERSE,
                               .ip = {127, 0, 0, 1}, .mac = {0x02, 0, 0, 0, 0, 1}};

    sock_t s_art = udp_bind(artnet_port), s_ctl = udp_bind(ctrl_port);
    printf("dmxbox sim: Art-Net on udp/%u, control on udp/%u, drop=%.2f\n",
           artnet_port, ctrl_port, g_drop);

    static uint8_t buf[2048], resp[CTRL_MAX_PKT], frame[DMX_CHANNELS], shown[DMX_CHANNELS];
    uint32_t next_frame = now_ms(), last_print = 0;

    for (;;) {
        uint32_t now = now_ms();
        int32_t wait = (int32_t)(next_frame - now);
        if (wait < 0) wait = 0;
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
            } else {
                if (dropped()) continue;                         // lost on the way in
                r = ctrl_handle(&g_ctrl, buf, n, now_ms(), resp, sizeof resp);
                if (r > 0 && dropped()) r = 0;                   // ACK lost on the way back
            }
            if (r > 0) sendto(s, (const char *)resp, r, 0, (struct sockaddr *)&from, fl);
        }

        now = now_ms();
        if ((int32_t)(now - next_frame) >= 0) {                 // ~43 Hz, like the real box
            next_frame = now + 23;
            engine_render(&g_engine, now, frame, true);
            if (!quiet && show > 0 && memcmp(frame, shown, (size_t)show) && now - last_print >= 100) {
                last_print = now;
                memcpy(shown, frame, (size_t)show);
                printf("DMX 1-%d:", show);
                for (int i = 0; i < show; i++) printf(" %3u", frame[i]);
                printf("\n");
            }
        }
    }
}
