/* SPDX-License-Identifier: MIT
 * rocola-pulso: lo pequeno de libpulse: muestras, mapas de canales,
 * volumenes, lista de propiedades, errores y tiempos. Cada funcion hace lo
 * que dice la documentacion publica de libpulse; donde libpulse se rompe con
 * un argumento malo (assert), aqui se devuelve un error o nada. */
#define _GNU_SOURCE
#include "interno.h"

#include <errno.h>
#include <stdarg.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

/* ---- muestras ---- */
size_t pulso_tam_muestra(pa_sample_format_t f)
{
    switch (f) {
    case PA_SAMPLE_U8: case PA_SAMPLE_ALAW: case PA_SAMPLE_ULAW: return 1;
    case PA_SAMPLE_S16LE: case PA_SAMPLE_S16BE: return 2;
    case PA_SAMPLE_S24LE: case PA_SAMPLE_S24BE: return 3;
    case PA_SAMPLE_FLOAT32LE: case PA_SAMPLE_FLOAT32BE:
    case PA_SAMPLE_S32LE: case PA_SAMPLE_S32BE:
    case PA_SAMPLE_S24_32LE: case PA_SAMPLE_S24_32BE: return 4;
    default: return 0;
    }
}

int pulso_spec_valida(const pa_sample_spec *ss)
{
    return ss && pulso_tam_muestra(ss->format) > 0 && ss->rate > 0 && ss->rate <= PA_RATE_MAX &&
           ss->channels > 0 && ss->channels <= PA_CHANNELS_MAX;
}

const char *pulso_nombre_formato(pa_sample_format_t f)
{
    static const char *n[] = {"u8", "aLaw", "uLaw", "s16le", "s16be", "float32le", "float32be",
                              "s32le", "s32be", "s24le", "s24be", "s24-32le", "s24-32be"};
    if ((int)f < 0 || f >= PA_SAMPLE_MAX)
        return NULL;
    return n[f];
}

EXPORTA size_t pa_sample_size_of_format(pa_sample_format_t f) { return pulso_tam_muestra(f); }
EXPORTA size_t pa_sample_size(const pa_sample_spec *ss) { return ss ? pulso_tam_muestra(ss->format) : 0; }
EXPORTA size_t pa_frame_size(const pa_sample_spec *ss)
{
    return pulso_spec_valida(ss) ? pulso_tam_muestra(ss->format) * ss->channels : 0;
}
EXPORTA size_t pa_bytes_per_second(const pa_sample_spec *ss)
{
    return pulso_spec_valida(ss) ? pa_frame_size(ss) * ss->rate : 0;
}
EXPORTA int pa_sample_spec_valid(const pa_sample_spec *ss) { return pulso_spec_valida(ss); }
EXPORTA int pa_sample_format_valid(unsigned f) { return f < PA_SAMPLE_MAX; }
EXPORTA int pa_sample_rate_valid(uint32_t r) { return r > 0 && r <= PA_RATE_MAX; }
EXPORTA int pa_channels_valid(uint8_t c) { return c > 0 && c <= PA_CHANNELS_MAX; }
EXPORTA int pa_sample_spec_equal(const pa_sample_spec *a, const pa_sample_spec *b)
{
    if (!a || !b)
        return 0;
    return a->format == b->format && a->rate == b->rate && a->channels == b->channels;
}
EXPORTA pa_sample_spec *pa_sample_spec_init(pa_sample_spec *ss)
{
    if (!ss)
        return NULL;
    ss->format = PA_SAMPLE_INVALID;
    ss->rate = 0;
    ss->channels = 0;
    return ss;
}
EXPORTA const char *pa_sample_format_to_string(pa_sample_format_t f) { return pulso_nombre_formato(f); }

EXPORTA size_t pa_usec_to_bytes(pa_usec_t t, const pa_sample_spec *ss)
{
    if (!pulso_spec_valida(ss))
        return 0;
    return (size_t)((t * ss->rate) / PA_USEC_PER_SEC) * pa_frame_size(ss);
}

EXPORTA pa_usec_t pa_bytes_to_usec(uint64_t n, const pa_sample_spec *ss)
{
    if (!pulso_spec_valida(ss))
        return 0;
    return ((n / pa_frame_size(ss)) * PA_USEC_PER_SEC) / ss->rate;
}

EXPORTA char *pa_sample_spec_snprint(char *s, size_t l, const pa_sample_spec *ss)
{
    if (!s || !l)
        return s;
    if (!pulso_spec_valida(ss))
        snprintf(s, l, "(invalid)");
    else
        snprintf(s, l, "%s %uch %uHz", pulso_nombre_formato(ss->format), ss->channels, ss->rate);
    return s;
}

EXPORTA const char *pa_get_library_version(void) { return "17.0.0"; }

/* ---- mapas de canales ---- */
EXPORTA pa_channel_map *pa_channel_map_init(pa_channel_map *m)
{
    if (!m)
        return NULL;
    m->channels = 0;
    for (unsigned i = 0; i < PA_CHANNELS_MAX; i++)
        m->map[i] = PA_CHANNEL_POSITION_INVALID;
    return m;
}

EXPORTA pa_channel_map *pa_channel_map_init_mono(pa_channel_map *m)
{
    if (!pa_channel_map_init(m))
        return NULL;
    m->channels = 1;
    m->map[0] = PA_CHANNEL_POSITION_MONO;
    return m;
}

EXPORTA pa_channel_map *pa_channel_map_init_stereo(pa_channel_map *m)
{
    if (!pa_channel_map_init(m))
        return NULL;
    m->channels = 2;
    m->map[0] = PA_CHANNEL_POSITION_FRONT_LEFT;
    m->map[1] = PA_CHANNEL_POSITION_FRONT_RIGHT;
    return m;
}

/* Los mapas de cada definicion, tal como los da libpulse (observados con
 * pa_channel_map_init_auto; 0 = no hay mapa para esos canales). */
static int mapa_auto(pa_channel_map *m, unsigned c, pa_channel_map_def_t d)
{
    enum { FL = 1, FR, FC, RC, RL, RR, LFE, FLC, FRC, SL, SR };
    static const int aiff[7][6] = {{0}, {0}, {FL, FR}, {FL, FR, FC}, {FL, FC, FR, RC}, {FL, FR, FC, RL, RR}, {FL, FLC, FC, FR, FRC, RC}};
    static const int alsa[9][8] = {{0}, {0}, {FL, FR}, {0}, {FL, FR, RL, RR}, {FL, FR, RL, RR, FC}, {FL, FR, RL, RR, FC, LFE}, {0}, {FL, FR, RL, RR, FC, LFE, SL, SR}};
    static const int waveex[9][8] = {{0}, {0}, {FL, FR}, {FL, FR, FC}, {FL, FR, FC, LFE}, {0}, {FL, FR, FC, LFE, RL, RR}, {0}, {FL, FR, FC, LFE, RL, RR, FLC, FRC}};
    static const int oss[9][8] = {{0}, {0}, {FL, FR}, {FL, FR, FC}, {FL, FR, FC, LFE}, {0}, {FL, FR, FC, LFE, SL, SR}, {0}, {FL, FR, FC, LFE, SL, SR, RL, RR}};
    if (!m || c == 0 || c > PA_CHANNELS_MAX)
        return 0;
    pa_channel_map_init(m);
    m->channels = (uint8_t)c;
    if (d == PA_CHANNEL_MAP_AUX) {
        for (unsigned i = 0; i < c; i++)
            m->map[i] = (pa_channel_position_t)(PA_CHANNEL_POSITION_AUX0 + i);
        return 1;
    }
    if (c == 1) {
        m->map[0] = PA_CHANNEL_POSITION_MONO;
        return 1;
    }
    const int *t = NULL;
    if (d == PA_CHANNEL_MAP_AIFF && c <= 6)
        t = aiff[c];
    else if (d == PA_CHANNEL_MAP_ALSA && c <= 8)
        t = alsa[c];
    else if (d == PA_CHANNEL_MAP_WAVEEX && c <= 8)
        t = waveex[c];
    else if (d == PA_CHANNEL_MAP_OSS && c <= 8)
        t = oss[c];
    if (!t || t[0] == 0) {
        pa_channel_map_init(m);
        return 0;
    }
    for (unsigned i = 0; i < c; i++)
        m->map[i] = (pa_channel_position_t)t[i];
    return 1;
}

EXPORTA pa_channel_map *pa_channel_map_init_auto(pa_channel_map *m, unsigned c, pa_channel_map_def_t d)
{
    return mapa_auto(m, c, d) ? m : NULL;
}

EXPORTA pa_channel_map *pa_channel_map_init_extend(pa_channel_map *m, unsigned c, pa_channel_map_def_t d)
{
    if (!m || c == 0 || c > PA_CHANNELS_MAX)
        return NULL;
    for (unsigned k = c; k > 0; k--) {
        if (mapa_auto(m, k, d)) {
            for (unsigned i = k; i < c; i++)
                m->map[i] = (pa_channel_position_t)(PA_CHANNEL_POSITION_AUX0 + (i - k));
            m->channels = (uint8_t)c;
            return m;
        }
    }
    return NULL;
}

void pulso_mapa_fabrica(pa_channel_map *m, unsigned canales)
{
    if (!pa_channel_map_init_auto(m, canales, PA_CHANNEL_MAP_DEFAULT))
        pa_channel_map_init_extend(m, canales, PA_CHANNEL_MAP_DEFAULT);
}

EXPORTA int pa_channel_map_valid(const pa_channel_map *m)
{
    if (!m || m->channels == 0 || m->channels > PA_CHANNELS_MAX)
        return 0;
    for (unsigned i = 0; i < m->channels; i++)
        if (m->map[i] < 0 || m->map[i] >= PA_CHANNEL_POSITION_MAX)
            return 0;
    return 1;
}

EXPORTA int pa_channel_map_compatible(const pa_channel_map *m, const pa_sample_spec *ss)
{
    return pa_channel_map_valid(m) && pulso_spec_valida(ss) && m->channels == ss->channels;
}

EXPORTA int pa_channel_map_equal(const pa_channel_map *a, const pa_channel_map *b)
{
    if (!pa_channel_map_valid(a) || !pa_channel_map_valid(b) || a->channels != b->channels)
        return 0;
    for (unsigned i = 0; i < a->channels; i++)
        if (a->map[i] != b->map[i])
            return 0;
    return 1;
}

static const char *nombre_posicion(pa_channel_position_t p)
{
    static const char *n[] = {"mono", "front-left", "front-right", "front-center", "rear-center", "rear-left",
                              "rear-right", "lfe", "front-left-of-center", "front-right-of-center", "side-left",
                              "side-right"};
    static char aux[32][8];
    static const char *top[] = {"top-center", "top-front-left", "top-front-right", "top-front-center",
                                "top-rear-left", "top-rear-right", "top-rear-center"};
    if (p >= 0 && p <= PA_CHANNEL_POSITION_SIDE_RIGHT)
        return n[p];
    if (p >= PA_CHANNEL_POSITION_AUX0 && p < PA_CHANNEL_POSITION_AUX0 + 32) {
        int i = p - PA_CHANNEL_POSITION_AUX0;
        snprintf(aux[i], sizeof aux[i], "aux%d", i);
        return aux[i];
    }
    if (p >= PA_CHANNEL_POSITION_TOP_CENTER && p < PA_CHANNEL_POSITION_MAX)
        return top[p - PA_CHANNEL_POSITION_TOP_CENTER];
    return NULL;
}

EXPORTA const char *pa_channel_position_to_string(pa_channel_position_t p) { return nombre_posicion(p); }

EXPORTA char *pa_channel_map_snprint(char *s, size_t l, const pa_channel_map *m)
{
    if (!s || !l)
        return s;
    if (!pa_channel_map_valid(m)) {
        snprintf(s, l, "(invalid)");
        return s;
    }
    size_t u = 0;
    s[0] = 0;
    for (unsigned i = 0; i < m->channels && u < l; i++)
        u += (size_t)snprintf(s + u, l - u, "%s%s", i ? "," : "", nombre_posicion(m->map[i]));
    return s;
}

/* ---- volumen (el que pide el cliente; rocola-pulso NUNCA lo aplica) ---- */
EXPORTA pa_cvolume *pa_cvolume_init(pa_cvolume *a)
{
    if (!a)
        return NULL;
    a->channels = 0;
    for (unsigned i = 0; i < PA_CHANNELS_MAX; i++)
        a->values[i] = PA_VOLUME_INVALID;
    return a;
}

EXPORTA pa_cvolume *pa_cvolume_set(pa_cvolume *a, unsigned channels, pa_volume_t v)
{
    if (!a || channels == 0 || channels > PA_CHANNELS_MAX)
        return NULL;
    if (v > PA_VOLUME_MAX)
        v = PA_VOLUME_MAX;
    a->channels = (uint8_t)channels;
    for (unsigned i = 0; i < channels; i++)
        a->values[i] = v;
    return a;
}

EXPORTA int pa_cvolume_valid(const pa_cvolume *a)
{
    if (!a || a->channels == 0 || a->channels > PA_CHANNELS_MAX)
        return 0;
    for (unsigned i = 0; i < a->channels; i++)
        if (a->values[i] > PA_VOLUME_MAX)
            return 0;
    return 1;
}

EXPORTA pa_volume_t pa_cvolume_avg(const pa_cvolume *a)
{
    if (!pa_cvolume_valid(a))
        return PA_VOLUME_MUTED;
    uint64_t s = 0;
    for (unsigned i = 0; i < a->channels; i++)
        s += a->values[i];
    return (pa_volume_t)(s / a->channels);
}

EXPORTA pa_volume_t pa_cvolume_max(const pa_cvolume *a)
{
    if (!pa_cvolume_valid(a))
        return PA_VOLUME_MUTED;
    pa_volume_t m = 0;
    for (unsigned i = 0; i < a->channels; i++)
        if (a->values[i] > m)
            m = a->values[i];
    return m;
}

EXPORTA pa_volume_t pa_cvolume_min(const pa_cvolume *a)
{
    if (!pa_cvolume_valid(a))
        return PA_VOLUME_MUTED;
    pa_volume_t m = PA_VOLUME_MAX;
    for (unsigned i = 0; i < a->channels; i++)
        if (a->values[i] < m)
            m = a->values[i];
    return m;
}

EXPORTA int pa_cvolume_equal(const pa_cvolume *a, const pa_cvolume *b)
{
    if (!a || !b || a->channels != b->channels)
        return 0;
    for (unsigned i = 0; i < a->channels; i++)
        if (a->values[i] != b->values[i])
            return 0;
    return 1;
}

EXPORTA int pa_cvolume_channels_equal_to(const pa_cvolume *a, pa_volume_t v)
{
    if (!pa_cvolume_valid(a))
        return 0;
    for (unsigned i = 0; i < a->channels; i++)
        if (a->values[i] != v)
            return 0;
    return 1;
}

EXPORTA int pa_cvolume_compatible(const pa_cvolume *v, const pa_sample_spec *ss)
{
    return pa_cvolume_valid(v) && pulso_spec_valida(ss) && v->channels == ss->channels;
}

/* La escala de software de libpulse es cubica, asi que multiplicar dos
 * volumenes es a*b/NORM (redondeado) y dividir es a*NORM/b. */
EXPORTA pa_volume_t pa_sw_volume_multiply(pa_volume_t a, pa_volume_t b)
{
    if (a > PA_VOLUME_MAX || b > PA_VOLUME_MAX)
        return PA_VOLUME_INVALID;
    uint64_t r = ((uint64_t)a * b + PA_VOLUME_NORM / 2) / PA_VOLUME_NORM;
    return r > PA_VOLUME_MAX ? PA_VOLUME_MAX : (pa_volume_t)r;
}

EXPORTA pa_volume_t pa_sw_volume_divide(pa_volume_t a, pa_volume_t b)
{
    if (a > PA_VOLUME_MAX || b > PA_VOLUME_MAX)
        return PA_VOLUME_INVALID;
    if (b == 0)
        return 0;
    uint64_t r = ((uint64_t)a * PA_VOLUME_NORM + b / 2) / b;
    return r > PA_VOLUME_MAX ? PA_VOLUME_MAX : (pa_volume_t)r;
}

EXPORTA pa_cvolume *pa_sw_cvolume_multiply_scalar(pa_cvolume *d, const pa_cvolume *a, pa_volume_t b)
{
    if (!d || !pa_cvolume_valid(a))
        return NULL;
    d->channels = a->channels;
    for (unsigned i = 0; i < a->channels; i++)
        d->values[i] = pa_sw_volume_multiply(a->values[i], b);
    return d;
}

/* ---- errores ---- */
EXPORTA const char *pa_strerror(int e)
{
    static const char *t[PA_ERR_MAX] = {
        "OK", "Access denied", "Unknown command", "Invalid argument", "Entity exists", "No such entity",
        "Connection refused", "Protocol error", "Timeout", "No authentication key", "Internal error",
        "Connection terminated", "Entity killed", "Invalid server", "Module initialization failed",
        "Bad state", "No data", "Incompatible protocol version", "Too large", "Not supported",
        "Unknown error code", "No such extension", "Obsolete functionality", "Missing implementation",
        "Client forked", "Input/Output error", "Device or resource busy"};
    if (e < 0)
        e = -e;
    if (e >= PA_ERR_MAX)
        return NULL;
    return t[e];
}

/* ---- tiempos ---- */
EXPORTA struct timeval *pa_gettimeofday(struct timeval *tv)
{
    if (!tv)
        return NULL;
    gettimeofday(tv, NULL);
    return tv;
}

EXPORTA pa_usec_t pa_timeval_diff(const struct timeval *a, const struct timeval *b)
{
    if (!a || !b)
        return 0;
    int64_t x = (int64_t)a->tv_sec * 1000000ll + a->tv_usec;
    int64_t y = (int64_t)b->tv_sec * 1000000ll + b->tv_usec;
    return (pa_usec_t)(x > y ? x - y : y - x);
}

EXPORTA int pa_timeval_cmp(const struct timeval *a, const struct timeval *b)
{
    if (!a || !b)
        return 0;
    if (a->tv_sec != b->tv_sec)
        return a->tv_sec < b->tv_sec ? -1 : 1;
    if (a->tv_usec != b->tv_usec)
        return a->tv_usec < b->tv_usec ? -1 : 1;
    return 0;
}

EXPORTA pa_usec_t pa_timeval_age(const struct timeval *tv)
{
    struct timeval ahora;
    if (!tv)
        return 0;
    gettimeofday(&ahora, NULL);
    return pa_timeval_diff(&ahora, tv);
}

EXPORTA pa_usec_t pa_timeval_load(const struct timeval *tv)
{
    if (!tv)
        return PA_USEC_INVALID;
    return (pa_usec_t)tv->tv_sec * PA_USEC_PER_SEC + (pa_usec_t)tv->tv_usec;
}

EXPORTA struct timeval *pa_timeval_store(struct timeval *tv, pa_usec_t v)
{
    if (!tv)
        return NULL;
    tv->tv_sec = (time_t)(v / PA_USEC_PER_SEC);
    tv->tv_usec = (suseconds_t)(v % PA_USEC_PER_SEC);
    return tv;
}

EXPORTA struct timeval *pa_timeval_add(struct timeval *tv, pa_usec_t v)
{
    if (!tv)
        return NULL;
    return pa_timeval_store(tv, pa_timeval_load(tv) + v);
}

EXPORTA struct timeval *pa_timeval_sub(struct timeval *tv, pa_usec_t v)
{
    if (!tv)
        return NULL;
    pa_usec_t a = pa_timeval_load(tv);
    return pa_timeval_store(tv, a > v ? a - v : 0);
}

/* ---- lista de propiedades ---- */
typedef struct prop {
    char *clave;
    void *valor;
    size_t n;
    struct prop *sig;
} prop;

struct pa_proplist {
    prop *primero;
};

EXPORTA pa_proplist *pa_proplist_new(void)
{
    return calloc(1, sizeof(pa_proplist));
}

EXPORTA void pa_proplist_clear(pa_proplist *p)
{
    if (!p)
        return;
    while (p->primero) {
        prop *x = p->primero;
        p->primero = x->sig;
        free(x->clave);
        free(x->valor);
        free(x);
    }
}

EXPORTA void pa_proplist_free(pa_proplist *p)
{
    if (!p)
        return;
    pa_proplist_clear(p);
    free(p);
}

EXPORTA int pa_proplist_key_valid(const char *k)
{
    if (!k || !*k)
        return 0;
    for (const char *c = k; *c; c++)
        if ((unsigned char)*c < 32 || (unsigned char)*c > 126 || *c == '=' || *c == '"' || *c == '\'' || *c == ' ')
            return 0;
    return 1;
}

static prop *busca(const pa_proplist *p, const char *k)
{
    for (prop *x = p ? p->primero : NULL; x; x = x->sig)
        if (strcmp(x->clave, k) == 0)
            return x;
    return NULL;
}

EXPORTA int pa_proplist_set(pa_proplist *p, const char *k, const void *d, size_t n)
{
    if (!p || !pa_proplist_key_valid(k) || (!d && n))
        return -1;
    void *copia = malloc(n ? n : 1);
    if (!copia)
        return -1;
    if (n)
        memcpy(copia, d, n);
    prop *x = busca(p, k);
    if (!x) {
        x = calloc(1, sizeof *x);
        if (!x || !(x->clave = strdup(k))) {
            free(x);
            free(copia);
            return -1;
        }
        x->sig = p->primero;
        p->primero = x;
    } else {
        free(x->valor);
    }
    x->valor = copia;
    x->n = n;
    return 0;
}

EXPORTA int pa_proplist_sets(pa_proplist *p, const char *k, const char *v)
{
    if (!v)
        return -1;
    return pa_proplist_set(p, k, v, strlen(v) + 1);
}

EXPORTA int pa_proplist_setp(pa_proplist *p, const char *par)
{
    if (!par)
        return -1;
    const char *igual = strchr(par, '=');
    if (!igual)
        return -1;
    char *k = strndup(par, (size_t)(igual - par));
    if (!k)
        return -1;
    int r = pa_proplist_sets(p, k, igual + 1);
    free(k);
    return r;
}

EXPORTA int pa_proplist_setf(pa_proplist *p, const char *k, const char *fmt, ...)
{
    if (!fmt)
        return -1;
    char *v = NULL;
    va_list ap;
    va_start(ap, fmt);
    int n = vasprintf(&v, fmt, ap);
    va_end(ap);
    if (n < 0)
        return -1;
    int r = pa_proplist_sets(p, k, v);
    free(v);
    return r;
}

EXPORTA const char *pa_proplist_gets(const pa_proplist *p, const char *k)
{
    if (!k)
        return NULL;
    prop *x = busca(p, k);
    if (!x || x->n == 0 || ((char *)x->valor)[x->n - 1] != 0 || memchr(x->valor, 0, x->n - 1))
        return NULL;
    return x->valor;
}

EXPORTA int pa_proplist_get(const pa_proplist *p, const char *k, const void **d, size_t *n)
{
    if (!k || !d || !n)
        return -1;
    prop *x = busca(p, k);
    if (!x)
        return -1;
    *d = x->valor;
    *n = x->n;
    return 0;
}

EXPORTA int pa_proplist_contains(const pa_proplist *p, const char *k)
{
    if (!p || !pa_proplist_key_valid(k))
        return -1;
    return busca(p, k) ? 1 : 0;
}

EXPORTA int pa_proplist_unset(pa_proplist *p, const char *k)
{
    if (!p || !pa_proplist_key_valid(k))
        return -1;
    for (prop **x = &p->primero; *x; x = &(*x)->sig)
        if (strcmp((*x)->clave, k) == 0) {
            prop *b = *x;
            *x = b->sig;
            free(b->clave);
            free(b->valor);
            free(b);
            return 0;
        }
    return -2;
}

EXPORTA int pa_proplist_unset_many(pa_proplist *p, const char *const keys[])
{
    if (!p || !keys)
        return -1;
    int n = 0;
    for (int i = 0; keys[i]; i++)
        if (pa_proplist_unset(p, keys[i]) == 0)
            n++;
    return n;
}

EXPORTA unsigned pa_proplist_size(const pa_proplist *p)
{
    unsigned n = 0;
    for (prop *x = p ? p->primero : NULL; x; x = x->sig)
        n++;
    return n;
}

EXPORTA int pa_proplist_isempty(const pa_proplist *p) { return pa_proplist_size(p) == 0; }

EXPORTA const char *pa_proplist_iterate(const pa_proplist *p, void **estado)
{
    if (!p || !estado)
        return NULL;
    prop *x = *estado ? ((prop *)*estado)->sig : p->primero;
    *estado = x;
    return x ? x->clave : NULL;
}

EXPORTA void pa_proplist_update(pa_proplist *p, pa_update_mode_t modo, const pa_proplist *otro)
{
    if (!p || !otro)
        return;
    if (modo == PA_UPDATE_SET)
        pa_proplist_clear(p);
    for (prop *x = otro->primero; x; x = x->sig) {
        if (modo == PA_UPDATE_MERGE && busca(p, x->clave))
            continue;
        pa_proplist_set(p, x->clave, x->valor, x->n);
    }
}

EXPORTA pa_proplist *pa_proplist_copy(const pa_proplist *p)
{
    pa_proplist *c = pa_proplist_new();
    if (c && p)
        pa_proplist_update(c, PA_UPDATE_REPLACE, p);
    return c;
}

EXPORTA char *pa_proplist_to_string_sep(const pa_proplist *p, const char *sep)
{
    if (!sep)
        sep = ",";
    size_t cap = 64, u = 0;
    char *s = malloc(cap);
    if (!s)
        return NULL;
    s[0] = 0;
    for (prop *x = p ? p->primero : NULL; x; x = x->sig) {
        const char *v = pa_proplist_gets(p, x->clave);
        size_t falta = strlen(x->clave) + (v ? strlen(v) : 16) + strlen(sep) + 8;
        if (u + falta >= cap) {
            cap = (u + falta) * 2;
            char *n = realloc(s, cap);
            if (!n) {
                free(s);
                return NULL;
            }
            s = n;
        }
        u += (size_t)snprintf(s + u, cap - u, "%s%s = \"%s\"", u ? sep : "", x->clave, v ? v : "(binario)");
    }
    return s;
}

EXPORTA char *pa_proplist_to_string(const pa_proplist *p)
{
    char *s = pa_proplist_to_string_sep(p, "\n");
    if (!s)
        return NULL;
    size_t n = strlen(s);
    char *t = realloc(s, n + 2);
    if (!t)
        return s;
    if (n) {
        t[n] = '\n';
        t[n + 1] = 0;
    }
    return t;
}

EXPORTA int pa_proplist_equal(const pa_proplist *a, const pa_proplist *b)
{
    if (pa_proplist_size(a) != pa_proplist_size(b))
        return 0;
    for (prop *x = a ? a->primero : NULL; x; x = x->sig) {
        prop *y = busca(b, x->clave);
        if (!y || y->n != x->n || memcmp(x->valor, y->valor, x->n) != 0)
            return 0;
    }
    return 1;
}

/* ---- formatos (pa_format_info) ---- */
EXPORTA pa_format_info *pa_format_info_new(void)
{
    pa_format_info *f = calloc(1, sizeof *f);
    if (!f)
        return NULL;
    f->encoding = PA_ENCODING_INVALID;
    f->plist = pa_proplist_new();
    return f;
}

EXPORTA void pa_format_info_free(pa_format_info *f)
{
    if (!f)
        return;
    pa_proplist_free(f->plist);
    free(f);
}

EXPORTA pa_format_info *pa_format_info_copy(const pa_format_info *f)
{
    if (!f)
        return NULL;
    pa_format_info *c = calloc(1, sizeof *c);
    if (!c)
        return NULL;
    c->encoding = f->encoding;
    c->plist = f->plist ? pa_proplist_copy(f->plist) : pa_proplist_new();
    return c;
}

EXPORTA int pa_format_info_valid(const pa_format_info *f)
{
    return f && f->encoding >= 0 && f->encoding < PA_ENCODING_MAX && f->plist;
}

EXPORTA int pa_format_info_is_pcm(const pa_format_info *f) { return f && f->encoding == PA_ENCODING_PCM; }

EXPORTA void pa_format_info_set_sample_format(pa_format_info *f, pa_sample_format_t sf)
{
    const char *n = pulso_nombre_formato(sf);
    if (f && n) {
        char v[32];
        snprintf(v, sizeof v, "\"%s\"", n);
        pa_proplist_sets(f->plist, "format.sample_format", v);
    }
}

EXPORTA void pa_format_info_set_rate(pa_format_info *f, int r)
{
    if (f) {
        char v[32];
        snprintf(v, sizeof v, "%d", r);
        pa_proplist_sets(f->plist, "format.rate", v);
    }
}

EXPORTA void pa_format_info_set_channels(pa_format_info *f, int c)
{
    if (f) {
        char v[32];
        snprintf(v, sizeof v, "%d", c);
        pa_proplist_sets(f->plist, "format.channels", v);
    }
}

EXPORTA void pa_format_info_set_channel_map(pa_format_info *f, const pa_channel_map *m)
{
    if (f && m) {
        char t[400], v[410];
        pa_channel_map_snprint(t, sizeof t, m);
        snprintf(v, sizeof v, "\"%s\"", t);
        pa_proplist_sets(f->plist, "format.channel_map", v);
    }
}
