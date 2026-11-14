/* SPDX-License-Identifier: MIT
 * rocola-pulso: el ANILLO (interfaz I1 de la fase 11, Spotify con Soloist).
 *
 * Un archivo en memoria (por omision /run/rocola-xp/spotify/sonido.anillo, o
 * ROCOLA_SPOTIFY_ANILLO) con una cabecera de 4096 bytes y 65536 cuadros
 * estereo float32. Lo ESCRIBE rocola-pulso dentro del proceso de Soloist y lo
 * LEE rocola-xp al ritmo de su tarjeta. Todo en little endian (x86_64).
 * Este archivo es la referencia de la cabecera: rocola-xp la lee con los
 * mismos desplazamientos (LEEME.md, «El anillo»).
 */
#ifndef PULSO_ANILLO_H
#define PULSO_ANILLO_H

#include <stdint.h>
#include <stddef.h>
#include <sys/types.h>

#define ANILLO_MAGIA 0x4F4C5550u      /* los bytes «PULO» */
#define ANILLO_VERSION 1u
#define ANILLO_CAPACIDAD 65536u       /* cuadros */
#define ANILLO_CANALES 2u
#define ANILLO_CABECERA 4096u
#define ANILLO_TAMANO ((size_t)ANILLO_CABECERA + (size_t)ANILLO_CAPACIDAD * ANILLO_CANALES * 4u)
#define ANILLO_RUTA_FABRICA "/run/rocola-xp/spotify/sonido.anillo"

/* La cabecera. Quien escribe cada campo va entre corchetes:
 * [E] el escritor (rocola-pulso), [L] el lector (rocola-xp). */
typedef struct anillo_cabecera {
    uint32_t magia;              /*  0 [E al crear] */
    uint32_t version;            /*  4 [E al crear] */
    uint32_t capacidad;          /*  8 [E al crear] cuadros */
    uint32_t canales;            /* 12 [E al crear] siempre 2 */
    uint32_t frecuencia;         /* 16 [E] Hz del flujo actual, 0 sin flujo */
    uint32_t gen_flujo;          /* 20 [E] +1 al conectar o desconectar un flujo */
    uint32_t tapado;             /* 24 [E] 1 sin flujo o en pausa (cork), 0 sonando */
    uint32_t gen_vaciado;        /* 28 [E] +1 en cada flush y en cada conexion */
    uint64_t escrito;            /* 32 [E] cuadros escritos (release) */
    uint64_t leido;              /* 40 [L] cuadros consumidos (release) */
    uint64_t vaciado_hasta;      /* 48 [E] escrito en el ultimo vaciado */
    uint64_t latido_escritor_ns; /* 56 [E] CLOCK_MONOTONIC */
    uint64_t latido_lector_ns;   /* 64 [L] CLOCK_MONOTONIC */
    uint32_t pid_escritor;       /* 72 [E] */
    uint32_t lector_presente;    /* 76 [L] */
    uint32_t volumen_pedido;     /* 80 [E] 0..65536, SOLO informativo */
    uint32_t formato_original;   /* 84 [E] pa_sample_format_t del flujo */
    uint64_t subdesbordes;       /* 88 [L] veces sin datos con el flujo sonando */
    uint32_t latencia_lector_us; /* 96 [L] */
    uint8_t reservado[ANILLO_CABECERA - 100];
} anillo_cabecera;

_Static_assert(sizeof(anillo_cabecera) == ANILLO_CABECERA, "cabecera de 4096");
_Static_assert(offsetof(anillo_cabecera, escrito) == 32, "escrito en 32");
_Static_assert(offsetof(anillo_cabecera, leido) == 40, "leido en 40");
_Static_assert(offsetof(anillo_cabecera, vaciado_hasta) == 48, "vaciado_hasta en 48");
_Static_assert(offsetof(anillo_cabecera, latido_escritor_ns) == 56, "latido_escritor en 56");
_Static_assert(offsetof(anillo_cabecera, latido_lector_ns) == 64, "latido_lector en 64");
_Static_assert(offsetof(anillo_cabecera, pid_escritor) == 72, "pid en 72");
_Static_assert(offsetof(anillo_cabecera, lector_presente) == 76, "lector_presente en 76");
_Static_assert(offsetof(anillo_cabecera, volumen_pedido) == 80, "volumen en 80");
_Static_assert(offsetof(anillo_cabecera, formato_original) == 84, "formato en 84");
_Static_assert(offsetof(anillo_cabecera, subdesbordes) == 88, "subdesbordes en 88");
_Static_assert(offsetof(anillo_cabecera, latencia_lector_us) == 96, "latencia en 96");

/* Un anillo abierto (mapeado) por este proceso. */
typedef struct anillo {
    char ruta[512];
    int fd;
    anillo_cabecera *cab;
    float *datos;          /* capacidad * 2 floats */
    dev_t dev;
    ino_t ino;
} anillo;

/* Lecturas y escrituras atomicas de la cabecera (memoria compartida). */
#define A_CARGA32(p) __atomic_load_n((p), __ATOMIC_ACQUIRE)
#define A_CARGA64(p) __atomic_load_n((p), __ATOMIC_ACQUIRE)
#define A_GUARDA32(p, v) __atomic_store_n((p), (uint32_t)(v), __ATOMIC_RELEASE)
#define A_GUARDA64(p, v) __atomic_store_n((p), (uint64_t)(v), __ATOMIC_RELEASE)

/* La ruta: ROCOLA_SPOTIFY_ANILLO o la de fabrica. */
const char *anillo_ruta(void);
/* Abre (y crea o rehace por renombre si no es valido). 0 bien, si no el errno. */
int anillo_abre(anillo *a, const char *ruta);
void anillo_cierra(anillo *a);
/* 1 si el archivo de la ruta ya no es el mapeado (otro inodo o borrado). */
int anillo_cambio(const anillo *a);
/* Cuadros pendientes = escrito - max(leido, vaciado_hasta), acotado a [0, capacidad]. */
uint64_t anillo_pendientes(const anillo_cabecera *c);
/* 1 si hay lector: lector_presente y latido_lector de hace 2 s o menos. */
int anillo_hay_lector(const anillo_cabecera *c, uint64_t ahora_ns);
uint64_t anillo_ahora_ns(void);

#endif
