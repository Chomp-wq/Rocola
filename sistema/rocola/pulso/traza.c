/* SPDX-License-Identifier: MIT
 * rocola-pulso: avisos en stderr («rocola-pulso: ...», pocos) y el rastreo
 * opcional para depurar en el A4: ROCOLA_PULSO_TRAZA=1 apunta cada llamada,
 * con sus argumentos y lo que devolvio, en
 * $ROCOLA_RUN/rocola-soloist/pulso-traza.txt (de fabrica /run/...), o en
 * ROCOLA_PULSO_TRAZA_ARCHIVO. Al pasar de 2 MB se mueve a .1 y sigue. */
#define _GNU_SOURCE
#include "interno.h"

#include <errno.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <sys/stat.h>
#include <sys/syscall.h>
#include <time.h>
#include <unistd.h>

#define TRAZA_TOPE (2L * 1024 * 1024)
#define AVISOS_TOPE 40

int pulso_traza_activa;
static pthread_mutex_t traza_mx = PTHREAD_MUTEX_INITIALIZER;
static pthread_once_t una_vez = PTHREAD_ONCE_INIT;
static FILE *traza_f;
static long traza_tam;
static char traza_ruta[600];
static int avisos;

static void carpetas(const char *ruta)
{
    char t[600];
    snprintf(t, sizeof t, "%s", ruta);
    for (char *p = t + 1; *p; p++)
        if (*p == '/') {
            *p = 0;
            mkdir(t, 0755);
            *p = '/';
        }
}

static void abre_traza(void)
{
    traza_f = fopen(traza_ruta, "ae");
    if (traza_f) {
        fseek(traza_f, 0, SEEK_END);
        traza_tam = ftell(traza_f);
    }
}

static void inicia(void)
{
    const char *t = getenv("ROCOLA_PULSO_TRAZA");
    if (!t || strcmp(t, "1") != 0)
        return;
    const char *a = getenv("ROCOLA_PULSO_TRAZA_ARCHIVO");
    if (a && *a) {
        snprintf(traza_ruta, sizeof traza_ruta, "%s", a);
    } else {
        const char *run = getenv("ROCOLA_RUN");
        snprintf(traza_ruta, sizeof traza_ruta, "%s/rocola-soloist/pulso-traza.txt", run && *run ? run : "/run");
    }
    carpetas(traza_ruta);
    abre_traza();
    if (traza_f)
        __atomic_store_n(&pulso_traza_activa, 1, __ATOMIC_RELAXED);
}

void pulso_inicia(void) { pthread_once(&una_vez, inicia); }

__attribute__((constructor)) static void al_cargar(void) { pulso_inicia(); }

static void escribe(const char *fmt, va_list ap)
{
    pthread_mutex_lock(&traza_mx);
    if (traza_f) {
        if (traza_tam > TRAZA_TOPE) {
            fclose(traza_f);
            char viejo[620];
            snprintf(viejo, sizeof viejo, "%s.1", traza_ruta);
            rename(traza_ruta, viejo);
            abre_traza();
            traza_tam = 0;
        }
    }
    if (traza_f) {
        struct timespec ts;
        clock_gettime(CLOCK_MONOTONIC, &ts);
        int n = fprintf(traza_f, "%ld.%06ld [%ld] ", (long)ts.tv_sec, ts.tv_nsec / 1000, (long)syscall(SYS_gettid));
        n += vfprintf(traza_f, fmt, ap);
        n += fprintf(traza_f, "\n");
        fflush(traza_f);
        if (n > 0)
            traza_tam += n;
    }
    pthread_mutex_unlock(&traza_mx);
}

void pulso_traza(const char *fmt, ...)
{
    va_list ap;
    va_start(ap, fmt);
    escribe(fmt, ap);
    va_end(ap);
}

void pulso_aviso(const char *fmt, ...)
{
    char m[512];
    va_list ap;
    va_start(ap, fmt);
    vsnprintf(m, sizeof m, fmt, ap);
    va_end(ap);
    int n = __atomic_add_fetch(&avisos, 1, __ATOMIC_RELAXED);
    if (n <= AVISOS_TOPE) {
        int e = errno;
        fprintf(stderr, "rocola-pulso: %s\n", m);
        errno = e;
    }
    TRAZA("AVISO %s", m);
}
