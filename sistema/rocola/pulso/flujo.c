/* SPDX-License-Identifier: MIT
 * rocola-pulso: los flujos (pa_stream). Un flujo de reproduccion escribe en
 * el anillo (I1): convierte cada formato a float32 estereo, el ritmo lo marca
 * el lector (rocola-xp) por pa_stream_writable_size, y los tiempos, el
 * «started» y el «underflow» salen de lo que el lector ya consumio.
 *
 * Un solo flujo por proceso escribe en el anillo (el ultimo que se conecto);
 * si otro lo desplaza, el anterior pasa a FAILED (PA_ERR_KILLED), como cuando
 * un servidor mata una entrada. g_mx ordena las escrituras al anillo entre
 * bucles distintos del mismo proceso. */
#define _GNU_SOURCE
#include "interno.h"

#include <errno.h>
#include <math.h>
#include <stdlib.h>
#include <string.h>
#include <unistd.h>

static pthread_mutex_t g_mx = PTHREAD_MUTEX_INITIALIZER;
static pa_stream *g_activo;
static uint64_t g_gen;

#define LIMITE_FABRICA_BYTES (4u * 1024u * 1024u)

/* ---- conversion a float32 estereo ---- */
static inline uint16_t lee16(const uint8_t *p, int be) { return be ? (uint16_t)(p[0] << 8 | p[1]) : (uint16_t)(p[1] << 8 | p[0]); }
static inline uint32_t lee32(const uint8_t *p, int be)
{
    return be ? ((uint32_t)p[0] << 24 | (uint32_t)p[1] << 16 | (uint32_t)p[2] << 8 | p[3])
              : ((uint32_t)p[3] << 24 | (uint32_t)p[2] << 16 | (uint32_t)p[1] << 8 | p[0]);
}

static inline float muestra(const uint8_t *p, pa_sample_format_t f)
{
    switch (f) {
    case PA_SAMPLE_U8: return ((int)p[0] - 128) / 128.0f;
    case PA_SAMPLE_S16LE: return (int16_t)lee16(p, 0) / 32768.0f;
    case PA_SAMPLE_S16BE: return (int16_t)lee16(p, 1) / 32768.0f;
    case PA_SAMPLE_S32LE: return (float)((int32_t)lee32(p, 0) / 2147483648.0);
    case PA_SAMPLE_S32BE: return (float)((int32_t)lee32(p, 1) / 2147483648.0);
    case PA_SAMPLE_S24LE: { int32_t v = (int32_t)((uint32_t)p[2] << 24 | (uint32_t)p[1] << 16 | (uint32_t)p[0] << 8) >> 8; return v / 8388608.0f; }
    case PA_SAMPLE_S24BE: { int32_t v = (int32_t)((uint32_t)p[0] << 24 | (uint32_t)p[1] << 16 | (uint32_t)p[2] << 8) >> 8; return v / 8388608.0f; }
    case PA_SAMPLE_S24_32LE: { int32_t v = (int32_t)(lee32(p, 0) << 8) >> 8; return v / 8388608.0f; }
    case PA_SAMPLE_S24_32BE: { int32_t v = (int32_t)(lee32(p, 1) << 8) >> 8; return v / 8388608.0f; }
    case PA_SAMPLE_FLOAT32LE: case PA_SAMPLE_FLOAT32BE: {
        uint32_t u = lee32(p, f == PA_SAMPLE_FLOAT32BE);
        float x;
        memcpy(&x, &u, 4);
        return isfinite(x) ? x : 0.0f; /* un NaN no debe llegar a la cadena de la rocola */
    }
    default: return 0.0f;
    }
}

/* n cuadros del cliente -> out (2*n floats) */
static void convierte(const pa_stream *s, const uint8_t *in, size_t n, float *out)
{
    const size_t tm = pulso_tam_muestra(s->ss.format);
    const size_t oi = (size_t)s->canal_i * tm, od = (size_t)s->canal_d * tm;
    if (s->ss.format == PA_SAMPLE_FLOAT32LE && s->ss.channels == 2 && s->canal_i == 0 && s->canal_d == 1) {
        memcpy(out, in, n * 8);
        for (size_t k = 0; k < 2 * n; k++)
            if (!isfinite(out[k]))
                out[k] = 0.0f;
        return;
    }
    for (size_t k = 0; k < n; k++) {
        const uint8_t *c = in + k * s->tc;
        out[2 * k] = muestra(c + oi, s->ss.format);
        out[2 * k + 1] = muestra(c + od, s->ss.format);
    }
}

/* ---- el anillo visto por el flujo ---- */
static anillo_cabecera *cab(const pa_stream *s)
{
    return s->c && s->c->an_abierto ? s->c->an.cab : NULL;
}

static uint64_t efectivo(const anillo_cabecera *h)
{
    uint64_t l = A_CARGA64(&h->leido), v = A_CARGA64(&h->vaciado_hasta);
    return l > v ? l : v;
}

/* pendientes en el anillo (solo si este flujo es el que escribe) */
static uint64_t pend_anillo(const pa_stream *s)
{
    anillo_cabecera *h = cab(s);
    return s->activo && h ? anillo_pendientes(h) : 0;
}

static int hay_lector(const pa_stream *s)
{
    anillo_cabecera *h = cab(s);
    return s->activo && h && anillo_hay_lector(h, anillo_ahora_ns());
}

/* con g_mx */
static void publica_conexion(pa_stream *s)
{
    anillo_cabecera *h = cab(s);
    if (!h)
        return;
    pa_volume_t v = pa_cvolume_avg(&s->vol);
    A_GUARDA32(&h->tapado, 1);
    A_GUARDA32(&h->frecuencia, s->ss.rate);
    A_GUARDA32(&h->formato_original, (uint32_t)s->ss.format);
    A_GUARDA32(&h->pid_escritor, (uint32_t)getpid());
    A_GUARDA32(&h->volumen_pedido, v > 65536 ? 65536 : v);
    A_GUARDA64(&h->vaciado_hasta, A_CARGA64(&h->escrito));
    __atomic_add_fetch(&h->gen_vaciado, 1, __ATOMIC_RELEASE);
    __atomic_add_fetch(&h->gen_flujo, 1, __ATOMIC_RELEASE);
    A_GUARDA64(&h->latido_escritor_ns, anillo_ahora_ns());
    A_GUARDA32(&h->tapado, s->tapado ? 1 : 0);
    s->ult_subdesbordes = A_CARGA64(&h->subdesbordes);
    s->esperando_inicio = 1;
    s->marca = efectivo(h);
    s->ult_latido_us = pulso_ahora_us();
}

/* con g_mx */
static void publica_desconexion(pa_stream *s)
{
    anillo_cabecera *h = cab(s);
    if (!h)
        return;
    A_GUARDA32(&h->tapado, 1);
    A_GUARDA32(&h->frecuencia, 0);
    __atomic_add_fetch(&h->gen_flujo, 1, __ATOMIC_RELEASE);
    A_GUARDA64(&h->latido_escritor_ns, anillo_ahora_ns());
}

/* con g_mx: copia n cuadros estereo al anillo (ya se sabe que caben) */
static void escribe_anillo(pa_stream *s, const float *src, size_t n)
{
    anillo_cabecera *h = cab(s);
    float *d = s->c->an.datos;
    uint64_t e = __atomic_load_n(&h->escrito, __ATOMIC_RELAXED);
    while (n > 0) {
        size_t pos = (size_t)(e % ANILLO_CAPACIDAD);
        size_t k = ANILLO_CAPACIDAD - pos;
        if (k > n)
            k = n;
        memcpy(d + pos * 2, src, k * 2 * sizeof(float));
        src += k * 2;
        n -= k;
        e += k;
    }
    A_GUARDA64(&h->escrito, e);
    A_GUARDA64(&h->latido_escritor_ns, anillo_ahora_ns());
}

/* con g_mx: pasa al anillo lo que cabe de la cola local */
static void drena_cola(pa_stream *s)
{
    anillo_cabecera *h = cab(s);
    if (!s->activo || !h || s->cola_n == 0)
        return;
    uint64_t libre = ANILLO_CAPACIDAD - anillo_pendientes(h);
    size_t k = s->cola_n < libre ? s->cola_n : (size_t)libre;
    if (k == 0)
        return;
    escribe_anillo(s, s->cola + s->cola_ini * 2, k);
    s->cola_ini += k;
    s->cola_n -= k;
    if (s->cola_n == 0)
        s->cola_ini = 0;
}

static int cola_agrega(pa_stream *s, const float *src, size_t n)
{
    if (s->cola_ini > 0 && s->cola_ini + s->cola_n + n > s->cola_cap) {
        memmove(s->cola, s->cola + s->cola_ini * 2, s->cola_n * 2 * sizeof(float));
        s->cola_ini = 0;
    }
    if (s->cola_ini + s->cola_n + n > s->cola_cap) {
        size_t cap = (s->cola_ini + s->cola_n + n) * 2;
        float *nuevo = realloc(s->cola, cap * 2 * sizeof(float));
        if (!nuevo)
            return -1;
        s->cola = nuevo;
        s->cola_cap = cap;
    }
    memcpy(s->cola + (s->cola_ini + s->cola_n) * 2, src, n * 2 * sizeof(float));
    s->cola_n += n;
    return 0;
}

/* Empuja n cuadros convertidos. Devuelve cuantos se aceptaron (en orden: si
 * no cabe todo dentro del limite, se quedan los primeros). */
static size_t empuja(pa_stream *s, const float *src, size_t n)
{
    pthread_mutex_lock(&g_mx);
    s->activo = (g_activo == s && s->gen_activo == g_gen);
    anillo_cabecera *h = cab(s);
    size_t aceptados = 0;
    if (s->activo && h) {
        drena_cola(s);
        if (s->cola_n == 0) {
            uint64_t libre = ANILLO_CAPACIDAD - anillo_pendientes(h);
            size_t k = n < libre ? n : (size_t)libre;
            if (k) {
                escribe_anillo(s, src, k);
                src += k * 2;
                n -= k;
                aceptados += k;
            }
        }
    }
    if (n > 0) {
        uint64_t ocupado = pend_anillo(s) + s->cola_n;
        uint64_t cabe = s->limite > ocupado ? s->limite - ocupado : 0;
        size_t k = n < cabe ? n : (size_t)cabe;
        if (k && cola_agrega(s, src, k) == 0) {
            aceptados += k;
            n -= k;
        }
    }
    pthread_mutex_unlock(&g_mx);
    return aceptados;
}

/* ---- tiempos ---- */
static void actualiza_tiempos(pa_stream *s)
{
    anillo_cabecera *h = cab(s);
    uint64_t pend = pend_anillo(s) + s->cola_n;
    uint32_t lat = (s->activo && h) ? A_CARGA32(&h->latencia_lector_us) : 0;
    int64_t r = s->w - (int64_t)pend;
    if (r < 0)
        r = 0;
    pa_timing_info *t = &s->ti;
    gettimeofday(&t->timestamp, NULL);
    t->synchronized_clocks = 1;
    t->sink_usec = pend * PA_USEC_PER_SEC / s->ss.rate + lat;
    t->source_usec = 0;
    t->transport_usec = 0;
    t->playing = !s->tapado && pend > 0;
    t->write_index_corrupt = 0;
    t->write_index = s->w * (int64_t)s->tc;
    t->read_index_corrupt = 0;
    t->read_index = r * (int64_t)s->tc;
    t->configured_sink_usec = s->objetivo * PA_USEC_PER_SEC / s->ss.rate;
    t->configured_source_usec = 0;
    t->since_underrun = (r - s->r_subdesborde) * (int64_t)s->tc;
    if (t->since_underrun < 0)
        t->since_underrun = 0;
    s->ti_valido = 1;
}

static size_t escribible(pa_stream *s)
{
    if (!hay_lector(s))
        return 0;
    uint64_t pend = pend_anillo(s) + s->cola_n;
    return s->objetivo > pend ? (size_t)(s->objetivo - pend) * s->tc : 0;
}

/* ---- el reloj del flujo: latido, inodo, started, underflow, drenar ---- */
static uint64_t intervalo_us(pa_stream *s)
{
    if (s->tapado)
        return 250000;
    if (!hay_lector(s))
        return 100000;
    return 10000;
}

static void programa_reloj(pa_stream *s, uint64_t us)
{
    if (!s->reloj)
        return;
    struct timeval tv;
    uint64_t cuando = pulso_ahora_us() + us;
    tv.tv_sec = (time_t)(cuando / 1000000ull);
    tv.tv_usec = (suseconds_t)(cuando % 1000000ull) | PA_TIMEVAL_RTCLOCK;
    s->c->api->time_restart(s->reloj, &tv);
}

static void revisa_inodo(pa_stream *s, uint64_t ahora)
{
    pa_context *c = s->c;
    if (ahora < s->prox_inodo_us)
        return;
    s->prox_inodo_us = ahora + 1000000;
    if (c->an_abierto && !anillo_cambio(&c->an))
        return;
    pthread_mutex_lock(&g_mx);
    if (c->an_abierto) {
        anillo_cierra(&c->an);
        c->an_abierto = 0;
    }
    int e = anillo_abre(&c->an, anillo_ruta());
    if (e == 0) {
        c->an_abierto = 1;
        TRAZA("flujo %p: el anillo cambio; reabierto", (void *)s);
        if (s->activo && g_activo == s)
            publica_conexion(s);
    } else {
        TRAZA("flujo %p: no se pudo reabrir el anillo: %s", (void *)s, strerror(e));
    }
    pthread_mutex_unlock(&g_mx);
}

static void reloj_cb(pa_mainloop_api *a, pa_time_event *e, const struct timeval *tv, void *ud)
{
    (void)a; (void)e; (void)tv;
    pa_stream *s = ud;
    if (s->estado != PA_STREAM_READY)
        return;
    s->ref++;
    uint64_t ahora = pulso_ahora_us();

    /* desplazado por otro flujo de este proceso o por otro proceso */
    pthread_mutex_lock(&g_mx);
    int mio = (g_activo == s && s->gen_activo == g_gen);
    pthread_mutex_unlock(&g_mx);
    anillo_cabecera *h = cab(s);
    uint32_t pid = h ? A_CARGA32(&h->pid_escritor) : 0;
    if (!mio || (h && pid != 0 && pid != (uint32_t)getpid())) {
        pulso_aviso("otro flujo ocupa el anillo (pid %u): este flujo se termina", pid);
        ctx_error(s->c, PA_ERR_KILLED);
        flujo_termina(s, PA_STREAM_FAILED, PA_ERR_KILLED);
        pa_stream_unref(s);
        return;
    }

    revisa_inodo(s, ahora);
    h = cab(s);

    pthread_mutex_lock(&g_mx);
    if (h) {
        drena_cola(s);
        A_GUARDA64(&h->latido_escritor_ns, anillo_ahora_ns());
    }
    pthread_mutex_unlock(&g_mx);

    if (h) {
        uint64_t ef = efectivo(h);
        if (!s->tapado && s->esperando_inicio && ef > s->marca) {
            s->esperando_inicio = 0;
            TRAZA("flujo %p: started", (void *)s);
            if (s->empezo_cb)
                s->empezo_cb(s, s->empezo_ud);
        }
        uint64_t sub = A_CARGA64(&h->subdesbordes);
        if (s->estado == PA_STREAM_READY && sub != s->ult_subdesbordes) {
            s->ult_subdesbordes = sub;
            if (!s->tapado && !s->esperando_inicio && pend_anillo(s) == 0 && s->cola_n == 0) {
                s->esperando_inicio = 1;
                s->marca = efectivo(h);
                s->r_subdesborde = s->w;
                TRAZA("flujo %p: underflow", (void *)s);
                if (s->subdesborde_cb)
                    s->subdesborde_cb(s, s->subdesborde_ud);
            }
        }
    }

    if (s->estado == PA_STREAM_READY) {
        actualiza_tiempos(s);
        if (s->latencia_cb && (s->banderas & PA_STREAM_AUTO_TIMING_UPDATE))
            s->latencia_cb(s, s->latencia_ud);
    }
    /* drenar: acaba cuando ya no queda nada por leer */
    if (s->estado == PA_STREAM_READY && pend_anillo(s) == 0 && s->cola_n == 0) {
        for (pa_operation *o = s->c->ops; o;) {
            pa_operation *n = o->sig;
            if (o->s == s && o->tipo == O_DRENA && o->estado == PA_OPERATION_RUNNING) {
                if (o->cb)
                    ((pa_stream_success_cb_t)o->cb)(s, 1, o->ud);
                op_termina(o);
                break; /* la lista pudo cambiar: el resto, en la siguiente vuelta */
            }
            o = n;
        }
    }
    if (s->estado == PA_STREAM_READY && s->escribe_cb) {
        size_t w = escribible(s);
        if (w > 0)
            s->escribe_cb(s, w, s->escribe_ud);
    }
    if (s->estado == PA_STREAM_READY)
        programa_reloj(s, intervalo_us(s));
    pa_stream_unref(s);
}

/* ---- ciclo de vida ---- */
static int busca_canal(const pa_channel_map *m, pa_channel_position_t p)
{
    for (unsigned i = 0; i < m->channels; i++)
        if (m->map[i] == p)
            return (int)i;
    return -1;
}

static void elige_canales(pa_stream *s)
{
    if (s->ss.channels == 1) {
        s->canal_i = s->canal_d = 0;
        return;
    }
    int i = busca_canal(&s->mapa, PA_CHANNEL_POSITION_FRONT_LEFT);
    int d = busca_canal(&s->mapa, PA_CHANNEL_POSITION_FRONT_RIGHT);
    if (i < 0 || d < 0) {
        i = busca_canal(&s->mapa, PA_CHANNEL_POSITION_SIDE_LEFT);
        d = busca_canal(&s->mapa, PA_CHANNEL_POSITION_SIDE_RIGHT);
    }
    if (i < 0 || d < 0) {
        int mono = busca_canal(&s->mapa, PA_CHANNEL_POSITION_MONO);
        if (mono < 0)
            mono = busca_canal(&s->mapa, PA_CHANNEL_POSITION_FRONT_CENTER);
        if (mono >= 0) {
            i = d = mono;
        } else {
            i = 0;
            d = 1;
        }
    }
    s->canal_i = i;
    s->canal_d = d;
}

EXPORTA pa_stream *pa_stream_new_with_proplist(pa_context *c, const char *nombre, const pa_sample_spec *ss,
                                               const pa_channel_map *mapa, pa_proplist *props)
{
    if (!c) {
        TRAZA("pa_stream_new(NULL) = NULL");
        return NULL;
    }
    if (!(c->estado >= PA_CONTEXT_CONNECTING && c->estado <= PA_CONTEXT_READY)) {
        ctx_error(c, PA_ERR_BADSTATE);
        TRAZA("pa_stream_new(%p) = NULL (contexto en %d)", (void *)c, c->estado);
        return NULL;
    }
    if (!pulso_spec_valida(ss) || (mapa && (!pa_channel_map_valid(mapa) || mapa->channels != ss->channels))) {
        ctx_error(c, PA_ERR_INVALID);
        TRAZA("pa_stream_new(%p) = NULL (formato invalido)", (void *)c);
        return NULL;
    }
    if (ss->format == PA_SAMPLE_ALAW || ss->format == PA_SAMPLE_ULAW) {
        ctx_error(c, PA_ERR_NOTSUPPORTED);
        TRAZA("pa_stream_new(%p) = NULL (alaw/ulaw no)", (void *)c);
        return NULL;
    }
    pa_stream *s = calloc(1, sizeof *s);
    if (!s) {
        ctx_error(c, PA_ERR_INTERNAL);
        return NULL;
    }
    s->ref = 1;
    s->c = c;
    pa_context_ref(c);
    s->sig = c->flujos;
    c->flujos = s;
    s->ss = *ss;
    if (mapa)
        s->mapa = *mapa;
    else
        pulso_mapa_fabrica(&s->mapa, ss->channels);
    elige_canales(s);
    s->tc = pa_frame_size(ss);
    s->estado = PA_STREAM_UNCONNECTED;
    s->indice = PA_INVALID_INDEX;
    s->nombre = nombre ? strdup(nombre) : NULL;
    s->props = props ? pa_proplist_copy(props) : pa_proplist_new();
    if (s->props && nombre)
        pa_proplist_sets(s->props, "media.name", nombre);
    pa_cvolume_set(&s->vol, ss->channels, PA_VOLUME_NORM);
    s->tapado = 1;
    TRAZA("pa_stream_new(%p, %s, %s %u Hz %u canales) = %p (canales %d,%d)", (void *)c, nombre ? nombre : "-",
          pulso_nombre_formato(ss->format), ss->rate, ss->channels, (void *)s, s->canal_i, s->canal_d);
    return s;
}

EXPORTA pa_stream *pa_stream_new(pa_context *c, const char *nombre, const pa_sample_spec *ss, const pa_channel_map *mapa)
{
    return pa_stream_new_with_proplist(c, nombre, ss, mapa, NULL);
}

/* strtoul sin locale (glibc >= 2.38 lo cambia a __isoc23_strtoul: asi la
 * biblioteca sigue pidiendo glibc 2.34 como mucho). */
static unsigned long numero(const char *p)
{
    unsigned long v = 0;
    while (*p == ' ' || *p == '"')
        p++;
    while (*p >= '0' && *p <= '9')
        v = v * 10 + (unsigned long)(*p++ - '0');
    return v;
}

/* Lee «format.*» de un pa_format_info PCM (para pa_stream_new_extended). */
static int de_formato(pa_format_info *f, pa_sample_spec *ss, pa_channel_map *mapa, int *con_mapa)
{
    if (!f || f->encoding != PA_ENCODING_PCM || !f->plist)
        return 0;
    const char *r = pa_proplist_gets(f->plist, "format.rate");
    const char *c = pa_proplist_gets(f->plist, "format.channels");
    const char *sf = pa_proplist_gets(f->plist, "format.sample_format");
    if (!r || !c || !sf)
        return 0;
    ss->rate = (uint32_t)numero(r);
    ss->channels = (uint8_t)numero(c);
    ss->format = PA_SAMPLE_INVALID;
    char nom[32];
    size_t n = 0;
    for (const char *p = sf; *p && n < sizeof nom - 1; p++)
        if (*p != '"' && *p != ' ')
            nom[n++] = *p;
    nom[n] = 0;
    for (int k = 0; k < PA_SAMPLE_MAX; k++)
        if (strcmp(nom, pulso_nombre_formato((pa_sample_format_t)k)) == 0)
            ss->format = (pa_sample_format_t)k;
    *con_mapa = 0;
    const char *cm = pa_proplist_gets(f->plist, "format.channel_map");
    if (cm) {
        pa_channel_map_init(mapa);
        char t[512];
        n = 0;
        for (const char *p = cm; *p && n < sizeof t - 1; p++)
            if (*p != '"' && *p != ' ')
                t[n++] = *p;
        t[n] = 0;
        unsigned k = 0;
        for (char *tok = strtok(t, ","); tok && k < PA_CHANNELS_MAX; tok = strtok(NULL, ",")) {
            pa_channel_position_t pos = PA_CHANNEL_POSITION_INVALID;
            for (int q = 0; q < PA_CHANNEL_POSITION_MAX; q++) {
                const char *nm = pa_channel_position_to_string((pa_channel_position_t)q);
                if (nm && strcmp(nm, tok) == 0)
                    pos = (pa_channel_position_t)q;
            }
            if (strcmp(tok, "left") == 0) pos = PA_CHANNEL_POSITION_FRONT_LEFT;
            if (strcmp(tok, "right") == 0) pos = PA_CHANNEL_POSITION_FRONT_RIGHT;
            if (strcmp(tok, "center") == 0) pos = PA_CHANNEL_POSITION_FRONT_CENTER;
            mapa->map[k++] = pos;
        }
        mapa->channels = (uint8_t)k;
        *con_mapa = pa_channel_map_valid(mapa) && mapa->channels == ss->channels;
    }
    return pulso_spec_valida(ss);
}

EXPORTA pa_stream *pa_stream_new_extended(pa_context *c, const char *nombre, pa_format_info *const *formatos,
                                          unsigned n, pa_proplist *props)
{
    for (unsigned i = 0; formatos && i < n; i++) {
        pa_sample_spec ss;
        pa_channel_map m;
        int con_mapa = 0;
        if (de_formato(formatos[i], &ss, &m, &con_mapa))
            return pa_stream_new_with_proplist(c, nombre, &ss, con_mapa ? &m : NULL, props);
    }
    if (c)
        ctx_error(c, PA_ERR_NOTSUPPORTED);
    return NULL;
}

int flujo_pon_volumen(pa_stream *s, const pa_cvolume *v)
{
    if (!pa_cvolume_valid(v))
        return 0;
    pa_cvolume antes = s->vol;
    if (v->channels == s->ss.channels)
        s->vol = *v;
    else
        pa_cvolume_set(&s->vol, s->ss.channels, pa_cvolume_avg(v));
    int cambio = !pa_cvolume_equal(&antes, &s->vol);
    pa_volume_t m = pa_cvolume_avg(&s->vol);
    pthread_mutex_lock(&g_mx);
    anillo_cabecera *h = cab(s);
    if (s->activo && g_activo == s && h)
        A_GUARDA32(&h->volumen_pedido, m > 65536 ? 65536 : m);
    pthread_mutex_unlock(&g_mx);
    TRAZA("flujo %p: volumen pedido %u (no se aplica)%s", (void *)s, m, cambio ? "" : " (el mismo)");
    return cambio;
}

EXPORTA int pa_stream_connect_playback(pa_stream *s, const char *disp, const pa_buffer_attr *attr,
                                       pa_stream_flags_t banderas, const pa_cvolume *vol, pa_stream *sync)
{
    (void)sync;
    if (!s) {
        TRAZA("pa_stream_connect_playback(NULL) = -%d", PA_ERR_INVALID);
        return -PA_ERR_INVALID;
    }
    if (s->estado != PA_STREAM_UNCONNECTED || s->c->estado != PA_CONTEXT_READY) {
        ctx_error(s->c, PA_ERR_BADSTATE);
        TRAZA("pa_stream_connect_playback(%p) = -%d", (void *)s, PA_ERR_BADSTATE);
        return -PA_ERR_BADSTATE;
    }
    if (vol && !pa_cvolume_valid(vol)) {
        ctx_error(s->c, PA_ERR_INVALID);
        return -PA_ERR_INVALID;
    }
    s->dir = PA_STREAM_PLAYBACK;
    s->banderas = banderas;
    s->tapado = (banderas & PA_STREAM_START_CORKED) ? 1 : 0;
    const uint64_t rate = s->ss.rate;
    memset(&s->attr, 0xff, sizeof s->attr);
    if (attr)
        s->attr = *attr;
    if (attr && attr->tlength != (uint32_t)-1 && attr->tlength != 0)
        s->objetivo = attr->tlength / s->tc;
    else
        s->objetivo = rate / 4; /* 250 ms */
    if (s->objetivo < rate / 20)
        s->objetivo = rate / 20;
    if (s->objetivo > ANILLO_CAPACIDAD)
        s->objetivo = ANILLO_CAPACIDAD;
    uint64_t lim = (attr && attr->maxlength != (uint32_t)-1 && attr->maxlength != 0) ? attr->maxlength : LIMITE_FABRICA_BYTES;
    s->limite = lim / s->tc;
    if (s->limite > rate * 20)
        s->limite = rate * 20;
    if (s->limite < s->objetivo)
        s->limite = s->objetivo;
    /* lo que se devuelve en pa_stream_get_buffer_attr */
    s->attr.tlength = (uint32_t)(s->objetivo * s->tc);
    s->attr.maxlength = (uint32_t)(s->limite * s->tc);
    if (s->attr.prebuf == (uint32_t)-1)
        s->attr.prebuf = s->attr.tlength;
    if (s->attr.minreq == (uint32_t)-1 || s->attr.minreq == 0)
        s->attr.minreq = s->attr.tlength / 4;
    s->attr.fragsize = (uint32_t)-1;
    if (vol)
        flujo_pon_volumen(s, vol);
    TRAZA("pa_stream_connect_playback(%p, %s, tlength %u minreq %u maxlength %u prebuf %u, banderas 0x%x, vol %u) = 0 "
          "(objetivo %llu cuadros, limite %llu)",
          (void *)s, disp ? disp : "NULL", attr ? attr->tlength : 0, attr ? attr->minreq : 0, attr ? attr->maxlength : 0,
          attr ? attr->prebuf : 0, (unsigned)banderas, vol ? pa_cvolume_avg(vol) : 0,
          (unsigned long long)s->objetivo, (unsigned long long)s->limite);
    s->ref++;
    s->estado = PA_STREAM_CREATING;
    if (s->estado_cb)
        s->estado_cb(s, s->estado_ud);
    if (s->estado == PA_STREAM_CREATING)
        ctx_encola(s->c, T_FLUJO_LISTO, s, NULL, 0, 0);
    pa_stream_unref(s);
    return 0;
}

EXPORTA int pa_stream_connect_record(pa_stream *s, const char *d, const pa_buffer_attr *a, pa_stream_flags_t f)
{
    (void)d; (void)a; (void)f;
    if (s)
        ctx_error(s->c, PA_ERR_NOTSUPPORTED);
    return -PA_ERR_NOTSUPPORTED;
}

static void pon_estado(pa_stream *s, pa_stream_state_t st)
{
    if (s->estado == st)
        return;
    s->ref++;
    TRAZA("flujo %p: estado %d -> %d", (void *)s, s->estado, st);
    s->estado = st;
    if (s->estado_cb)
        s->estado_cb(s, s->estado_ud);
    pa_stream_unref(s);
}

void flujo_listo(pa_stream *s)
{
    pa_context *c = s->c;
    if (s->estado != PA_STREAM_CREATING || c->estado != PA_CONTEXT_READY)
        return;
    s->indice = ctx_nuevo_indice();
    /* el anillo pudo cambiar mientras no habia flujo: mirarlo ya */
    s->prox_inodo_us = 0;
    revisa_inodo(s, pulso_ahora_us());
    pthread_mutex_lock(&g_mx);
    if (g_activo && g_activo != s)
        TRAZA("flujo %p desplaza al flujo %p en el anillo", (void *)s, (void *)g_activo);
    s->gen_activo = ++g_gen;
    g_activo = s;
    s->activo = 1;
    publica_conexion(s);
    pthread_mutex_unlock(&g_mx);
    s->r_subdesborde = 0;
    s->prox_inodo_us = pulso_ahora_us() + 1000000;
    struct timeval tv;
    uint64_t cuando = pulso_ahora_us() + 10000;
    tv.tv_sec = (time_t)(cuando / 1000000ull);
    tv.tv_usec = (suseconds_t)(cuando % 1000000ull) | PA_TIMEVAL_RTCLOCK;
    s->reloj = c->api->time_new(c->api, &tv, reloj_cb, s);
    actualiza_tiempos(s);
    s->ref++;
    pon_estado(s, PA_STREAM_READY);
    if (s->estado == PA_STREAM_READY) {
        ctx_evento(c, PA_SUBSCRIPTION_EVENT_SINK_INPUT | PA_SUBSCRIPTION_EVENT_NEW, s->indice);
        if (s->escribe_cb) {
            size_t w = escribible(s);
            if (w > 0)
                s->escribe_cb(s, w, s->escribe_ud);
        }
    }
    pa_stream_unref(s);
}

void flujo_termina(pa_stream *s, pa_stream_state_t est, int error)
{
    if (s->estado != PA_STREAM_CREATING && s->estado != PA_STREAM_READY)
        return;
    pa_context *c = s->c;
    s->ref++;
    int estaba_listo = s->estado == PA_STREAM_READY;
    pthread_mutex_lock(&g_mx);
    if (g_activo == s) {
        if (c->an_abierto)
            publica_desconexion(s);
        g_activo = NULL;
    }
    s->activo = 0;
    pthread_mutex_unlock(&g_mx);
    if (s->reloj) {
        c->api->time_free(s->reloj);
        s->reloj = NULL;
    }
    ctx_quita_tareas_de(c, s);
    for (pa_operation *o = c->ops; o;) {
        if (o->s == s) {
            op_cancela(o);
            o = c->ops; /* la lista cambio */
        } else {
            o = o->sig;
        }
    }
    s->cola_n = 0;
    s->cola_ini = 0;
    if (error)
        ctx_error(c, error);
    if (estaba_listo && c->estado == PA_CONTEXT_READY)
        ctx_evento(c, PA_SUBSCRIPTION_EVENT_SINK_INPUT | PA_SUBSCRIPTION_EVENT_REMOVE, s->indice);
    pon_estado(s, est);
    pa_stream_unref(s);
}

EXPORTA int pa_stream_disconnect(pa_stream *s)
{
    if (!s) {
        TRAZA("pa_stream_disconnect(NULL) = -%d", PA_ERR_INVALID);
        return -PA_ERR_INVALID;
    }
    if (s->estado != PA_STREAM_CREATING && s->estado != PA_STREAM_READY) {
        ctx_error(s->c, PA_ERR_BADSTATE);
        TRAZA("pa_stream_disconnect(%p) = -%d (estado %d)", (void *)s, PA_ERR_BADSTATE, s->estado);
        return -PA_ERR_BADSTATE;
    }
    TRAZA("pa_stream_disconnect(%p) = 0", (void *)s);
    flujo_termina(s, PA_STREAM_TERMINATED, 0);
    return 0;
}

static void flujo_libera(pa_stream *s)
{
    pa_context *c = s->c;
    TRAZA("flujo %p: se libera", (void *)s);
    s->ref = 1;
    s->estado_cb = NULL;
    s->escribe_cb = NULL;
    flujo_termina(s, PA_STREAM_TERMINATED, 0);
    for (pa_stream **p = &c->flujos; *p; p = &(*p)->sig)
        if (*p == s) {
            *p = s->sig;
            break;
        }
    if (s->props)
        pa_proplist_free(s->props);
    free(s->nombre);
    free(s->cola);
    free(s->conv);
    free(s->buf_escritura);
    free(s);
    pa_context_unref(c);
}

EXPORTA pa_stream *pa_stream_ref(pa_stream *s)
{
    if (s)
        s->ref++;
    return s;
}

EXPORTA void pa_stream_unref(pa_stream *s)
{
    if (!s || s->ref <= 0)
        return;
    if (--s->ref == 0)
        flujo_libera(s);
}

/* ---- escribir ---- */
EXPORTA size_t pa_stream_writable_size(const pa_stream *cs)
{
    pa_stream *s = (pa_stream *)cs;
    if (!s)
        return (size_t)-1;
    if (s->estado != PA_STREAM_READY) {
        ctx_error(s->c, PA_ERR_BADSTATE);
        TRAZA("pa_stream_writable_size(%p) = -1 (estado %d)", (void *)s, s->estado);
        return (size_t)-1;
    }
    size_t w = escribible(s);
    TRAZA("pa_stream_writable_size(%p) = %zu", (void *)s, w);
    return w;
}

static int escribe(pa_stream *s, const void *datos, size_t nbytes, pa_free_cb_t free_cb, void *free_ud, int64_t offset, pa_seek_mode_t seek)
{
    if (!s)
        return -PA_ERR_INVALID;
    if (s->estado != PA_STREAM_READY) {
        ctx_error(s->c, PA_ERR_BADSTATE);
        return -PA_ERR_BADSTATE;
    }
    if (!datos || nbytes % s->tc != 0) {
        ctx_error(s->c, PA_ERR_INVALID);
        return -PA_ERR_INVALID;
    }
    if (seek != PA_SEEK_RELATIVE || offset < 0 || offset % (int64_t)s->tc != 0) {
        ctx_error(s->c, PA_ERR_NOTSUPPORTED);
        return -PA_ERR_NOTSUPPORTED;
    }
    int del_buffer = s->buf_escritura && (const uint8_t *)datos >= (uint8_t *)s->buf_escritura &&
                     (const uint8_t *)datos < (uint8_t *)s->buf_escritura + s->buf_tam;
    size_t silencio = (size_t)(offset / (int64_t)s->tc);
    size_t n = nbytes / s->tc;
    size_t total = silencio + n;
    size_t aceptados = 0;
    /* por tandas para no pedir mucha memoria de golpe */
    const size_t TANDA = 4096;
    if (s->conv_cap < TANDA * 2) {
        float *nuevo = realloc(s->conv, TANDA * 2 * sizeof(float));
        if (!nuevo) {
            ctx_error(s->c, PA_ERR_INTERNAL);
            return -PA_ERR_INTERNAL;
        }
        s->conv = nuevo;
        s->conv_cap = TANDA * 2;
    }
    size_t hecho = 0;
    int corto = 0;
    while (hecho < total && !corto) {
        size_t k = total - hecho < TANDA ? total - hecho : TANDA;
        if (hecho < silencio) {
            if (k > silencio - hecho)
                k = silencio - hecho;
            memset(s->conv, 0, k * 2 * sizeof(float));
        } else {
            convierte(s, (const uint8_t *)datos + (hecho - silencio) * s->tc, k, s->conv);
        }
        size_t a = empuja(s, s->conv, k);
        aceptados += a;
        hecho += k;
        if (a < k)
            corto = 1;
    }
    s->w += (int64_t)aceptados;
    if (del_buffer)
        s->buf_en_uso = 0;
    else if (free_cb)
        free_cb(free_ud);
    if (aceptados < total) {
        TRAZA("flujo %p: desborde: %zu de %zu cuadros fuera del limite (se descartan los ultimos)", (void *)s,
              total - aceptados, total);
        if (s->desborde_cb)
            s->desborde_cb(s, s->desborde_ud);
    }
    return 0;
}

EXPORTA int pa_stream_write(pa_stream *s, const void *datos, size_t nbytes, pa_free_cb_t free_cb, int64_t offset, pa_seek_mode_t seek)
{
    int r = escribe(s, datos, nbytes, free_cb, (void *)datos, offset, seek);
    TRAZA("pa_stream_write(%p, %p, %zu, %p, %lld, %d) = %d", (void *)s, datos, nbytes, (void *)free_cb, (long long)offset, (int)seek, r);
    return r;
}

EXPORTA int pa_stream_write_ext_free(pa_stream *s, const void *datos, size_t nbytes, pa_free_cb_t free_cb, void *free_ud,
                                     int64_t offset, pa_seek_mode_t seek)
{
    return escribe(s, datos, nbytes, free_cb, free_ud, offset, seek);
}

EXPORTA int pa_stream_begin_write(pa_stream *s, void **datos, size_t *nbytes)
{
    if (!s || !datos || !nbytes)
        return -PA_ERR_INVALID;
    if (s->estado != PA_STREAM_READY) {
        ctx_error(s->c, PA_ERR_BADSTATE);
        return -PA_ERR_BADSTATE;
    }
    size_t n = *nbytes;
    if (n == (size_t)-1 || n == 0)
        n = s->attr.tlength ? s->attr.tlength : 65536;
    if (n > 1024 * 1024)
        n = 1024 * 1024;
    n -= n % s->tc;
    if (n == 0)
        n = s->tc;
    if (s->buf_tam < n) {
        void *b = realloc(s->buf_escritura, n);
        if (!b) {
            ctx_error(s->c, PA_ERR_INTERNAL);
            return -PA_ERR_INTERNAL;
        }
        s->buf_escritura = b;
        s->buf_tam = n;
    }
    s->buf_en_uso = 1;
    *datos = s->buf_escritura;
    *nbytes = n;
    return 0;
}

EXPORTA int pa_stream_cancel_write(pa_stream *s)
{
    if (!s)
        return -PA_ERR_INVALID;
    s->buf_en_uso = 0;
    return 0;
}

/* ---- operaciones del flujo ---- */
static pa_operation *op_flujo(pa_stream *s, int tipo, pa_stream_success_cb_t cb, void *ud)
{
    if (!s)
        return NULL;
    if (s->estado != PA_STREAM_READY || s->c->estado != PA_CONTEXT_READY) {
        ctx_error(s->c, PA_ERR_BADSTATE);
        return NULL;
    }
    return op_nueva(s->c, s, tipo, (void *)cb, ud);
}

int flujo_completa_op(pa_operation *o)
{
    pa_stream *s = o->s;
    if (o->tipo == O_DRENA)
        return 0; /* la acaba el reloj del flujo */
    if (o->tipo == O_TIEMPOS && s->estado == PA_STREAM_READY) {
        actualiza_tiempos(s);
        if (s->latencia_cb)
            s->latencia_cb(s, s->latencia_ud);
    }
    if (o->cb && o->estado == PA_OPERATION_RUNNING)
        ((pa_stream_success_cb_t)o->cb)(s, 1, o->ud);
    return 1;
}

EXPORTA pa_operation *pa_stream_cork(pa_stream *s, int b, pa_stream_success_cb_t cb, void *ud)
{
    pa_operation *o = op_flujo(s, O_EXITO_FLUJO, cb, ud);
    if (o) {
        int antes = s->tapado;
        s->tapado = b ? 1 : 0;
        pthread_mutex_lock(&g_mx);
        anillo_cabecera *h = cab(s);
        if (s->activo && g_activo == s && h)
            A_GUARDA32(&h->tapado, s->tapado);
        if (antes && !s->tapado && h) {
            s->esperando_inicio = 1;
            s->marca = efectivo(h);
        }
        pthread_mutex_unlock(&g_mx);
        programa_reloj(s, s->tapado ? 250000 : 10000);
    }
    TRAZA("pa_stream_cork(%p, %d) = %p", (void *)s, b, (void *)o);
    return o;
}

EXPORTA pa_operation *pa_stream_flush(pa_stream *s, pa_stream_success_cb_t cb, void *ud)
{
    pa_operation *o = op_flujo(s, O_EXITO_FLUJO, cb, ud);
    if (o) {
        pthread_mutex_lock(&g_mx);
        anillo_cabecera *h = cab(s);
        uint64_t pend = pend_anillo(s) + s->cola_n;
        if (s->activo && g_activo == s && h) {
            A_GUARDA64(&h->vaciado_hasta, A_CARGA64(&h->escrito));
            __atomic_add_fetch(&h->gen_vaciado, 1, __ATOMIC_RELEASE);
            s->marca = efectivo(h);
        }
        s->cola_n = 0;
        s->cola_ini = 0;
        s->w -= (int64_t)pend;
        if (s->w < 0)
            s->w = 0;
        s->esperando_inicio = 1;
        pthread_mutex_unlock(&g_mx);
        actualiza_tiempos(s);
        TRAZA("pa_stream_flush(%p): %llu cuadros fuera", (void *)s, (unsigned long long)pend);
    }
    TRAZA("pa_stream_flush(%p) = %p", (void *)s, (void *)o);
    return o;
}

EXPORTA pa_operation *pa_stream_update_timing_info(pa_stream *s, pa_stream_success_cb_t cb, void *ud)
{
    pa_operation *o = op_flujo(s, O_TIEMPOS, cb, ud);
    TRAZA("pa_stream_update_timing_info(%p) = %p", (void *)s, (void *)o);
    return o;
}

EXPORTA pa_operation *pa_stream_drain(pa_stream *s, pa_stream_success_cb_t cb, void *ud)
{
    return op_flujo(s, O_DRENA, cb, ud);
}

EXPORTA pa_operation *pa_stream_trigger(pa_stream *s, pa_stream_success_cb_t cb, void *ud)
{
    return op_flujo(s, O_EXITO_FLUJO, cb, ud);
}

EXPORTA pa_operation *pa_stream_prebuf(pa_stream *s, pa_stream_success_cb_t cb, void *ud)
{
    return op_flujo(s, O_EXITO_FLUJO, cb, ud);
}

EXPORTA pa_operation *pa_stream_set_name(pa_stream *s, const char *nombre, pa_stream_success_cb_t cb, void *ud)
{
    pa_operation *o = op_flujo(s, O_EXITO_FLUJO, cb, ud);
    if (o && nombre && s->props)
        pa_proplist_sets(s->props, "media.name", nombre);
    return o;
}

EXPORTA pa_operation *pa_stream_set_buffer_attr(pa_stream *s, const pa_buffer_attr *attr, pa_stream_success_cb_t cb, void *ud)
{
    pa_operation *o = op_flujo(s, O_EXITO_FLUJO, cb, ud);
    if (o && attr && attr->tlength != (uint32_t)-1 && attr->tlength != 0) {
        uint64_t rate = s->ss.rate;
        s->objetivo = attr->tlength / s->tc;
        if (s->objetivo < rate / 20)
            s->objetivo = rate / 20;
        if (s->objetivo > ANILLO_CAPACIDAD)
            s->objetivo = ANILLO_CAPACIDAD;
        if (s->limite < s->objetivo)
            s->limite = s->objetivo;
        s->attr.tlength = (uint32_t)(s->objetivo * s->tc);
    }
    return o;
}

/* ---- lo que se pregunta ---- */
EXPORTA const pa_timing_info *pa_stream_get_timing_info(pa_stream *s)
{
    if (!s)
        return NULL;
    if (s->estado != PA_STREAM_READY) {
        ctx_error(s->c, PA_ERR_BADSTATE);
        TRAZA("pa_stream_get_timing_info(%p) = NULL (estado %d)", (void *)s, s->estado);
        return NULL;
    }
    if (!s->ti_valido) {
        ctx_error(s->c, PA_ERR_NODATA);
        return NULL;
    }
    TRAZA("pa_stream_get_timing_info(%p): sink_usec %llu playing %d write_index %lld read_index %lld", (void *)s,
          (unsigned long long)s->ti.sink_usec, s->ti.playing, (long long)s->ti.write_index, (long long)s->ti.read_index);
    return &s->ti;
}

EXPORTA int pa_stream_get_time(pa_stream *s, pa_usec_t *us)
{
    if (!s || !us)
        return -PA_ERR_INVALID;
    if (s->estado != PA_STREAM_READY) {
        ctx_error(s->c, PA_ERR_BADSTATE);
        return -PA_ERR_BADSTATE;
    }
    actualiza_tiempos(s);
    anillo_cabecera *h = cab(s);
    uint64_t lat = (s->activo && h) ? A_CARGA32(&h->latencia_lector_us) : 0;
    uint64_t t = (uint64_t)(s->ti.read_index / (int64_t)s->tc) * PA_USEC_PER_SEC / s->ss.rate;
    *us = t > lat ? t - lat : 0;
    return 0;
}

EXPORTA int pa_stream_get_latency(pa_stream *s, pa_usec_t *us, int *negativo)
{
    if (!s || !us)
        return -PA_ERR_INVALID;
    if (s->estado != PA_STREAM_READY) {
        ctx_error(s->c, PA_ERR_BADSTATE);
        return -PA_ERR_BADSTATE;
    }
    actualiza_tiempos(s);
    *us = s->ti.sink_usec;
    if (negativo)
        *negativo = 0;
    return 0;
}

EXPORTA pa_stream_state_t pa_stream_get_state(const pa_stream *s)
{
    return s ? s->estado : PA_STREAM_FAILED;
}

EXPORTA uint32_t pa_stream_get_index(const pa_stream *s)
{
    if (!s)
        return PA_INVALID_INDEX;
    if (s->estado != PA_STREAM_READY) {
        ctx_error(s->c, PA_ERR_BADSTATE);
        return PA_INVALID_INDEX;
    }
    return s->indice;
}

EXPORTA int pa_stream_is_corked(const pa_stream *s)
{
    if (!s)
        return -PA_ERR_INVALID;
    if (s->estado != PA_STREAM_READY) {
        ctx_error(s->c, PA_ERR_BADSTATE);
        return -PA_ERR_BADSTATE;
    }
    return s->tapado;
}

EXPORTA int pa_stream_is_suspended(const pa_stream *s)
{
    if (!s)
        return -PA_ERR_INVALID;
    return s->estado == PA_STREAM_READY ? 0 : -PA_ERR_BADSTATE;
}

EXPORTA pa_context *pa_stream_get_context(const pa_stream *s) { return s ? s->c : NULL; }
EXPORTA const pa_sample_spec *pa_stream_get_sample_spec(pa_stream *s) { return s ? &s->ss : NULL; }
EXPORTA const pa_channel_map *pa_stream_get_channel_map(pa_stream *s) { return s ? &s->mapa : NULL; }
EXPORTA const pa_buffer_attr *pa_stream_get_buffer_attr(pa_stream *s)
{
    if (!s || s->estado != PA_STREAM_READY)
        return NULL;
    return &s->attr;
}
EXPORTA uint32_t pa_stream_get_device_index(const pa_stream *s) { return s && s->estado == PA_STREAM_READY ? 0 : PA_INVALID_INDEX; }
EXPORTA const char *pa_stream_get_device_name(const pa_stream *s) { return s && s->estado == PA_STREAM_READY ? "rocola" : NULL; }

EXPORTA int pa_stream_set_monitor_stream(pa_stream *s, uint32_t idx)
{
    (void)idx;
    if (s)
        ctx_error(s->c, PA_ERR_NOTSUPPORTED);
    return -PA_ERR_NOTSUPPORTED;
}

EXPORTA size_t pa_stream_readable_size(const pa_stream *s)
{
    if (s)
        ctx_error(s->c, PA_ERR_BADSTATE);
    return (size_t)-1;
}

EXPORTA int pa_stream_peek(pa_stream *s, const void **d, size_t *n)
{
    if (d)
        *d = NULL;
    if (n)
        *n = 0;
    if (s)
        ctx_error(s->c, PA_ERR_BADSTATE);
    return -PA_ERR_BADSTATE;
}

EXPORTA int pa_stream_drop(pa_stream *s)
{
    if (s)
        ctx_error(s->c, PA_ERR_BADSTATE);
    return -PA_ERR_BADSTATE;
}

/* ---- los avisos que se registran ---- */
#define PON_AVISO(fn, campo, tipo)                                    \
    EXPORTA void fn(pa_stream *s, tipo cb, void *ud)                  \
    {                                                                 \
        TRAZA(#fn "(%p, %p, %p)", (void *)s, (void *)cb, ud);         \
        if (!s || s->estado == PA_STREAM_TERMINATED || s->estado == PA_STREAM_FAILED) \
            return;                                                   \
        s->campo##_cb = cb;                                           \
        s->campo##_ud = ud;                                           \
    }

PON_AVISO(pa_stream_set_state_callback, estado, pa_stream_notify_cb_t)
PON_AVISO(pa_stream_set_write_callback, escribe, pa_stream_request_cb_t)
PON_AVISO(pa_stream_set_read_callback, lee, pa_stream_request_cb_t)
PON_AVISO(pa_stream_set_started_callback, empezo, pa_stream_notify_cb_t)
PON_AVISO(pa_stream_set_underflow_callback, subdesborde, pa_stream_notify_cb_t)
PON_AVISO(pa_stream_set_overflow_callback, desborde, pa_stream_notify_cb_t)
PON_AVISO(pa_stream_set_latency_update_callback, latencia, pa_stream_notify_cb_t)
PON_AVISO(pa_stream_set_moved_callback, movido, pa_stream_notify_cb_t)
PON_AVISO(pa_stream_set_suspended_callback, suspendido, pa_stream_notify_cb_t)
PON_AVISO(pa_stream_set_buffer_attr_callback, attr, pa_stream_notify_cb_t)
PON_AVISO(pa_stream_set_event_callback, evento, pa_stream_event_cb_t)

/* ---- para la info de la entrada (pa_context_get_sink_input_info) ---- */
pa_stream *flujo_por_indice(pa_context *c, uint32_t idx)
{
    for (pa_stream *s = c->flujos; s; s = s->sig)
        if (s->estado == PA_STREAM_READY && s->indice == idx)
            return s;
    return NULL;
}

void flujo_info(pa_stream *s, pa_sink_input_info *i, pa_format_info *fmt)
{
    memset(i, 0, sizeof *i);
    i->index = s->indice;
    i->name = s->nombre ? s->nombre : "";
    i->owner_module = PA_INVALID_INDEX;
    i->client = 0;
    i->sink = 0;
    i->sample_spec = s->ss;
    i->channel_map = s->mapa;
    i->volume = s->vol;
    actualiza_tiempos(s);
    i->buffer_usec = s->ti.sink_usec;
    i->sink_usec = 0;
    i->resample_method = "trivial";
    i->driver = "rocola-pulso";
    i->mute = 0;
    i->proplist = s->props;
    i->corked = s->tapado;
    i->has_volume = 1;
    i->volume_writable = 1;
    fmt->encoding = PA_ENCODING_PCM; /* su lista de propiedades es la del flujo */
    fmt->plist = s->props;
    i->format = fmt;
}
