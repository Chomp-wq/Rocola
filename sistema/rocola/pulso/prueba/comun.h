/* SPDX-License-Identifier: MIT
 * Pruebas de rocola-pulso: lo comun (comprobaciones contadas, el lector como
 * proceso aparte, el reporte, relojes). */
#ifndef COMUN_H
#define COMUN_H
#define _GNU_SOURCE
#include <errno.h>
#include <signal.h>
#include <stdarg.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <sys/wait.h>
#include <time.h>
#include <unistd.h>

static int bien_n, mal_n;
#define COMPRUEBA(cond, ...)                                     \
    do {                                                         \
        if (cond) {                                              \
            bien_n++;                                            \
        } else {                                                 \
            mal_n++;                                             \
            fprintf(stderr, "  MAL %s:%d: ", __FILE__, __LINE__); \
            fprintf(stderr, __VA_ARGS__);                        \
            fprintf(stderr, "\n");                               \
        }                                                        \
    } while (0)

static double ahora_s(void)
{
    struct timespec t;
    clock_gettime(CLOCK_MONOTONIC, &t);
    return t.tv_sec + t.tv_nsec / 1e9;
}

static void duerme_ms(int ms)
{
    struct timespec t = {ms / 1000, (long)(ms % 1000) * 1000000L};
    while (nanosleep(&t, &t) == -1 && errno == EINTR)
        ;
}

static char dir_lector[512];

/* Lanza el lector (proceso aparte) y devuelve su pid. */
static pid_t lanza_lector(const char *reporte, int bits, int mono, const char *extra)
{
    pid_t p = fork();
    if (p == 0) {
        char b[8], exe[600];
        snprintf(b, sizeof b, "%d", bits);
        snprintf(exe, sizeof exe, "%s/lector", dir_lector);
        const char *args[12];
        int n = 0;
        args[n++] = exe;
        args[n++] = "--bits";
        args[n++] = b;
        args[n++] = "--reporte";
        args[n++] = reporte;
        if (mono)
            args[n++] = "--mono";
        if (extra)
            args[n++] = extra;
        args[n] = NULL;
        execv(exe, (char *const *)args);
        _exit(127);
    }
    duerme_ms(60);
    return p;
}

typedef struct {
    unsigned long long cuadros, errores, saltos, subdesbordes, reaperturas, flujos;
    char frecuencias[256];
    char primer_error[256];
} reporte_t;

static int para_lector(pid_t p, const char *ruta, reporte_t *r)
{
    memset(r, 0, sizeof *r);
    kill(p, SIGTERM);
    int st;
    waitpid(p, &st, 0);
    FILE *f = fopen(ruta, "r");
    if (!f)
        return -1;
    char l[512];
    while (fgets(l, sizeof l, f)) {
        l[strcspn(l, "\n")] = 0;
        char *v = strchr(l, '=');
        if (!v)
            continue;
        *v++ = 0;
        if (!strcmp(l, "cuadros")) r->cuadros = strtoull(v, NULL, 10);
        else if (!strcmp(l, "errores")) r->errores = strtoull(v, NULL, 10);
        else if (!strcmp(l, "saltos")) r->saltos = strtoull(v, NULL, 10);
        else if (!strcmp(l, "subdesbordes")) r->subdesbordes = strtoull(v, NULL, 10);
        else if (!strcmp(l, "reaperturas")) r->reaperturas = strtoull(v, NULL, 10);
        else if (!strcmp(l, "flujos")) r->flujos = strtoull(v, NULL, 10);
        else if (!strcmp(l, "frecuencias")) snprintf(r->frecuencias, sizeof r->frecuencias, "%s", v);
        else if (!strcmp(l, "primer_error")) snprintf(r->primer_error, sizeof r->primer_error, "%s", v);
    }
    fclose(f);
    return 0;
}

static void pon_dir_lector(const char *argv0)
{
    snprintf(dir_lector, sizeof dir_lector, "%s", argv0);
    char *b = strrchr(dir_lector, '/');
    if (b)
        *b = 0;
    else
        strcpy(dir_lector, ".");
}
#endif
