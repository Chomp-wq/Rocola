/* SPDX-License-Identifier: MIT
 * Pruebas de rocola-pulso: un LECTOR del anillo como lo hara rocola-xp, al
 * ritmo de una tarjeta (cada periodo toma periodo*frecuencia cuadros), con
 * todo lo que I1 le pide al lector: lector_presente, latidos, leido con
 * release, el vaciado (leido = max(leido, vaciado_hasta)), subdesbordes,
 * latencia_lector_us y reabrir si cambia el inodo. Ademas comprueba la senal
 * con contador cuadro a cuadro (senal.h).
 *
 *   lector [--bits 16|8] [--mono] [--periodo-ms 10] [--latencia-us 20000]
 *          [--reporte archivo] [--sin-verificar] [--no-lee]
 * La ruta es ROCOLA_SPOTIFY_ANILLO. Sale con SIGTERM y escribe el reporte. */
#define _GNU_SOURCE
#include "../anillo.h"
#include "senal.h"

#include <errno.h>
#include <signal.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <time.h>
#include <unistd.h>

static volatile sig_atomic_t salir;
static void al_salir(int s) { (void)s; salir = 1; }

int main(int argc, char **argv)
{
    int bits = 16, mono = 0, periodo_ms = 10, verificar = 1, lee = 1;
    unsigned latencia = 20000;
    const char *reporte = NULL;
    for (int i = 1; i < argc; i++) {
        if (!strcmp(argv[i], "--bits") && i + 1 < argc) bits = atoi(argv[++i]);
        else if (!strcmp(argv[i], "--mono")) mono = 1;
        else if (!strcmp(argv[i], "--periodo-ms") && i + 1 < argc) periodo_ms = atoi(argv[++i]);
        else if (!strcmp(argv[i], "--latencia-us") && i + 1 < argc) latencia = (unsigned)atoi(argv[++i]);
        else if (!strcmp(argv[i], "--reporte") && i + 1 < argc) reporte = argv[++i];
        else if (!strcmp(argv[i], "--sin-verificar")) verificar = 0;
        else if (!strcmp(argv[i], "--no-lee")) lee = 0;
    }
    struct sigaction sa;
    memset(&sa, 0, sizeof sa);
    sa.sa_handler = al_salir;
    sigaction(SIGTERM, &sa, NULL);
    sigaction(SIGINT, &sa, NULL);

    anillo a;
    int e = anillo_abre(&a, anillo_ruta());
    if (e) {
        fprintf(stderr, "lector: no abre el anillo: %s\n", strerror(e));
        return 2;
    }
    anillo_cabecera *h = a.cab;
    A_GUARDA32(&h->latencia_lector_us, latencia);
    A_GUARDA64(&h->latido_lector_ns, anillo_ahora_ns());
    A_GUARDA32(&h->lector_presente, 1);
    /* al abrir, lo vaciado antes ya no cuenta (I1: leido = max(leido, vaciado_hasta)) */
    if (A_CARGA64(&h->vaciado_hasta) > A_CARGA64(&h->leido))
        A_GUARDA64(&h->leido, A_CARGA64(&h->vaciado_hasta));

    uint64_t cuadros = 0, errores = 0, saltos = 0, subdes = 0, reaperturas = 0, flujos = 0, sin_dato = 0;
    char primer_error[256] = "";
    char frecs[256] = "";
    uint32_t gen_f = A_CARGA32(&h->gen_flujo), gen_v = A_CARGA32(&h->gen_vaciado);
    uint32_t frec = A_CARGA32(&h->frecuencia);
    int tengo_k = 0;
    uint64_t k_esp = 0;
    const uint64_t modulo = mono ? (1ull << bits) : (1ull << (2 * bits));
    double resto = 0;
    struct timespec prox;
    clock_gettime(CLOCK_MONOTONIC, &prox);
    uint64_t prox_inodo = anillo_ahora_ns() + 1000000000ull;

    while (!salir) {
        prox.tv_nsec += (long)periodo_ms * 1000000L;
        while (prox.tv_nsec >= 1000000000L) {
            prox.tv_nsec -= 1000000000L;
            prox.tv_sec++;
        }
        while (clock_nanosleep(CLOCK_MONOTONIC, TIMER_ABSTIME, &prox, NULL) == EINTR && !salir)
            ;
        if (salir)
            break;
        uint64_t ahora = anillo_ahora_ns();
        if (ahora >= prox_inodo) {
            prox_inodo = ahora + 1000000000ull;
            if (anillo_cambio(&a)) {
                anillo_cierra(&a);
                if (anillo_abre(&a, anillo_ruta()) != 0)
                    continue;
                h = a.cab;
                reaperturas++;
                A_GUARDA32(&h->latencia_lector_us, latencia);
                A_GUARDA32(&h->lector_presente, 1);
                gen_f = A_CARGA32(&h->gen_flujo) - 1; /* fuerza a releer el flujo */
                gen_v = A_CARGA32(&h->gen_vaciado);
                if (A_CARGA64(&h->vaciado_hasta) > A_CARGA64(&h->leido))
                    A_GUARDA64(&h->leido, A_CARGA64(&h->vaciado_hasta));
                tengo_k = 0;
            }
        }
        A_GUARDA64(&h->latido_lector_ns, ahora);
        uint32_t gf = A_CARGA32(&h->gen_flujo);
        if (gf != gen_f) {
            gen_f = gf;
            frec = A_CARGA32(&h->frecuencia);
            if (frec) {
                flujos++;
                char t[16];
                snprintf(t, sizeof t, "%s%u", frecs[0] ? "," : "", frec);
                if (strlen(frecs) + strlen(t) < sizeof frecs - 1)
                    strcat(frecs, t);
            }
            tengo_k = 0;
        }
        uint32_t gv = A_CARGA32(&h->gen_vaciado);
        if (gv != gen_v) {
            gen_v = gv;
            uint64_t l = A_CARGA64(&h->leido), v = A_CARGA64(&h->vaciado_hasta);
            if (v > l)
                A_GUARDA64(&h->leido, v);
            tengo_k = 0;
            saltos++;
        }
        if (!lee || frec == 0 || A_CARGA32(&h->tapado))
            continue;
        /* el escritor murio sin avisar (latido de hace mas de 2 s): no consumir */
        uint64_t le = A_CARGA64(&h->latido_escritor_ns);
        if (le + 2000000000ull < ahora)
            continue;
        resto += (double)frec * periodo_ms / 1000.0;
        uint64_t quiere = (uint64_t)resto;
        resto -= (double)quiere;
        uint64_t esc = A_CARGA64(&h->escrito);
        uint64_t l = A_CARGA64(&h->leido), vh = A_CARGA64(&h->vaciado_hasta);
        if (vh > l)
            l = vh; /* I1: pendientes = escrito - max(leido, vaciado_hasta) */
        uint64_t pend = esc > l ? esc - l : 0;
        if (pend > ANILLO_CAPACIDAD)
            pend = ANILLO_CAPACIDAD;
        uint64_t toma = pend < quiere ? pend : quiere;
        for (uint64_t i = 0; i < toma; i++) {
            size_t pos = (size_t)((l + i) % ANILLO_CAPACIDAD);
            float L = a.datos[pos * 2], R = a.datos[pos * 2 + 1];
            if (!verificar)
                continue;
            int32_t vl = senal_de_float(L, bits), vr = senal_de_float(R, bits);
            int bien = vl != -100000 && vr != -100000;
            uint64_t k = 0;
            if (bien) {
                uint64_t ul = (uint64_t)(vl + (1 << (bits - 1)));
                uint64_t ur = (uint64_t)(vr + (1 << (bits - 1)));
                if (mono)
                    bien = (vl == vr), k = ul;
                else
                    k = ul | (ur << bits);
            }
            if (bien && tengo_k && k != k_esp % modulo)
                bien = 0;
            if (!bien) {
                errores++;
                if (!primer_error[0])
                    snprintf(primer_error, sizeof primer_error, "cuadro %llu: L=%.9g R=%.9g esperado k=%llu",
                             (unsigned long long)cuadros + i, L, R, (unsigned long long)(k_esp % modulo));
                tengo_k = 0;
            } else {
                tengo_k = 1;
                k_esp = k + 1;
            }
        }
        cuadros += toma;
        A_GUARDA64(&h->leido, l + toma);
        if (toma < quiere) {
            sin_dato++;
            subdes++;
            __atomic_add_fetch(&h->subdesbordes, 1, __ATOMIC_RELEASE);
        }
    }
    A_GUARDA32(&h->lector_presente, 0);
    FILE *f = reporte ? fopen(reporte, "w") : stdout;
    if (f) {
        fprintf(f, "cuadros=%llu\nerrores=%llu\nsaltos=%llu\nsubdesbordes=%llu\nreaperturas=%llu\nflujos=%llu\nfrecuencias=%s\nprimer_error=%s\n",
                (unsigned long long)cuadros, (unsigned long long)errores, (unsigned long long)saltos,
                (unsigned long long)subdes, (unsigned long long)reaperturas, (unsigned long long)flujos, frecs, primer_error);
        if (f != stdout)
            fclose(f);
    }
    anillo_cierra(&a);
    return 0;
}
