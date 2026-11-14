/* SPDX-License-Identifier: MIT
 * rocola-pulso: abrir, crear y rehacer el anillo (I1). */
#define _GNU_SOURCE
#include "anillo.h"
#include "interno.h"

#include <errno.h>
#include <fcntl.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <sys/mman.h>
#include <sys/stat.h>
#include <time.h>
#include <unistd.h>

uint64_t anillo_ahora_ns(void)
{
    struct timespec t;
    clock_gettime(CLOCK_MONOTONIC, &t);
    return (uint64_t)t.tv_sec * 1000000000ull + (uint64_t)t.tv_nsec;
}

const char *anillo_ruta(void)
{
    const char *r = getenv("ROCOLA_SPOTIFY_ANILLO");
    if (r && *r)
        return r;
    return ANILLO_RUTA_FABRICA;
}

uint64_t anillo_pendientes(const anillo_cabecera *c)
{
    uint64_t e = A_CARGA64(&c->escrito);
    uint64_t l = A_CARGA64(&c->leido);
    uint64_t v = A_CARGA64(&c->vaciado_hasta);
    uint64_t base = l > v ? l : v;
    if (base >= e)
        return 0;
    uint64_t p = e - base;
    return p > ANILLO_CAPACIDAD ? ANILLO_CAPACIDAD : p;
}

int anillo_hay_lector(const anillo_cabecera *c, uint64_t ahora)
{
    if (A_CARGA32(&c->lector_presente) != 1)
        return 0;
    uint64_t l = A_CARGA64(&c->latido_lector_ns);
    if (l >= ahora)
        return 1;
    return ahora - l <= 2000000000ull;
}

/* mkdir -p de las carpetas de la ruta, 0755. */
static int crea_carpetas(const char *ruta)
{
    char tmp[512];
    size_t n = strlen(ruta);
    if (n >= sizeof tmp)
        return ENAMETOOLONG;
    memcpy(tmp, ruta, n + 1);
    char *fin = strrchr(tmp, '/');
    if (!fin || fin == tmp)
        return 0;
    *fin = 0;
    for (char *p = tmp + 1; ; p++) {
        if (*p == '/' || *p == 0) {
            char c = *p;
            *p = 0;
            if (mkdir(tmp, 0755) == 0)
                chmod(tmp, 0755);
            else if (errno != EEXIST)
                return errno;
            *p = c;
            if (c == 0)
                break;
        }
    }
    return 0;
}

static int valido(int fd, const anillo_cabecera *c)
{
    struct stat st;
    if (fstat(fd, &st) != 0 || (size_t)st.st_size != ANILLO_TAMANO)
        return 0;
    return c->magia == ANILLO_MAGIA && c->version == ANILLO_VERSION && c->capacidad == ANILLO_CAPACIDAD &&
           c->canales == ANILLO_CANALES;
}

/* Escribe uno nuevo completo en <ruta>.t.<pid> y lo renombra encima (atomico). */
static int rehace(const char *ruta)
{
    char tmp[600];
    snprintf(tmp, sizeof tmp, "%s.t.%d", ruta, (int)getpid());
    int fd = open(tmp, O_RDWR | O_CREAT | O_TRUNC | O_CLOEXEC, 0644);
    if (fd < 0)
        return errno;
    fchmod(fd, 0644);
    int err = 0;
    if (ftruncate(fd, (off_t)ANILLO_TAMANO) != 0) {
        err = errno;
    } else {
        anillo_cabecera c;
        memset(&c, 0, sizeof c);
        c.magia = ANILLO_MAGIA;
        c.version = ANILLO_VERSION;
        c.capacidad = ANILLO_CAPACIDAD;
        c.canales = ANILLO_CANALES;
        c.tapado = 1;
        ssize_t w = pwrite(fd, &c, sizeof c, 0);
        if (w != (ssize_t)sizeof c)
            err = w < 0 ? errno : EIO;
    }
    close(fd);
    if (!err && rename(tmp, ruta) != 0)
        err = errno;
    if (err)
        unlink(tmp);
    return err;
}

static int mapea(anillo *a, const char *ruta, int *invalido)
{
    *invalido = 0;
    int fd = open(ruta, O_RDWR | O_CLOEXEC);
    if (fd < 0)
        return errno;
    struct stat st;
    if (fstat(fd, &st) != 0) {
        int e = errno;
        close(fd);
        return e;
    }
    if ((size_t)st.st_size != ANILLO_TAMANO) {
        close(fd);
        *invalido = 1;
        return EINVAL;
    }
    void *m = mmap(NULL, ANILLO_TAMANO, PROT_READ | PROT_WRITE, MAP_SHARED, fd, 0);
    if (m == MAP_FAILED) {
        int e = errno;
        close(fd);
        return e;
    }
    if (!valido(fd, (anillo_cabecera *)m)) {
        munmap(m, ANILLO_TAMANO);
        close(fd);
        *invalido = 1;
        return EINVAL;
    }
    a->fd = fd;
    a->cab = (anillo_cabecera *)m;
    a->datos = (float *)((char *)m + ANILLO_CABECERA);
    a->dev = st.st_dev;
    a->ino = st.st_ino;
    return 0;
}

int anillo_abre(anillo *a, const char *ruta)
{
    memset(a, 0, sizeof *a);
    a->fd = -1;
    if (!ruta || !*ruta)
        return EINVAL;
    if (strlen(ruta) >= sizeof a->ruta)
        return ENAMETOOLONG;
    strcpy(a->ruta, ruta);
    int err = crea_carpetas(ruta);
    if (err)
        return err;
    /* Unos intentos: otro proceso puede estar rehaciendolo a la vez. */
    for (int i = 0; i < 4; i++) {
        int invalido = 0;
        err = mapea(a, ruta, &invalido);
        if (err == 0)
            return 0;
        if (err != ENOENT && !invalido)
            return err;
        err = rehace(ruta);
        if (err)
            return err;
        if (invalido)
            pulso_aviso("el anillo %s no era valido: rehecho", ruta);
        else
            TRAZA("anillo %s creado", ruta);
    }
    return err ? err : EAGAIN;
}

void anillo_cierra(anillo *a)
{
    if (a->cab)
        munmap(a->cab, ANILLO_TAMANO);
    if (a->fd >= 0)
        close(a->fd);
    a->cab = NULL;
    a->datos = NULL;
    a->fd = -1;
}

int anillo_cambio(const anillo *a)
{
    struct stat st;
    if (stat(a->ruta, &st) != 0)
        return 1;
    return st.st_dev != a->dev || st.st_ino != a->ino;
}
