/* SPDX-License-Identifier: MIT
 * rocola-pulso: el contexto (pa_context) y las operaciones (pa_operation).
 *
 * No hay servidor: «conectar» es abrir (o crear) el anillo. Los cambios de
 * estado y el final de cada operacion se entregan en el hilo del bucle, como
 * en libpulse, por una cola de tareas que vacia un evento defer del bucle. */
#define _GNU_SOURCE
#include "interno.h"

#include <errno.h>
#include <stdlib.h>
#include <string.h>
#include <unistd.h>

static uint32_t indice_global;

uint32_t ctx_nuevo_indice(void)
{
    return __atomic_fetch_add(&indice_global, 1, __ATOMIC_RELAXED);
}

void ctx_error(pa_context *c, int e)
{
    if (c)
        c->error = e;
}

static void ctx_libera(pa_context *c);

static void ctx_ref(pa_context *c) { c->ref++; }
static void ctx_unref(pa_context *c)
{
    if (--c->ref == 0)
        ctx_libera(c);
}

/* ---- la cola de tareas ---- */
static void tarea_suelta(tarea *t)
{
    if (t->s)
        pa_stream_unref(t->s);
    if (t->o)
        pa_operation_unref(t->o);
    free(t);
}

void ctx_encola(pa_context *c, int tipo, pa_stream *s, pa_operation *o, uint32_t evento, uint32_t indice)
{
    tarea *t = calloc(1, sizeof *t);
    if (!t) {
        pulso_aviso("sin memoria para una tarea");
        return;
    }
    t->tipo = tipo;
    if (s) {
        s->ref++;
        t->s = s;
    }
    if (o) {
        o->ref++;
        t->o = o;
    }
    t->evento = evento;
    t->indice = indice;
    if (c->tareas_fin)
        c->tareas_fin->sig = t;
    else
        c->tareas = t;
    c->tareas_fin = t;
    if (c->defer)
        c->api->defer_enable(c->defer, 1);
}

/* Quita las tareas de un flujo (o todas con s NULL). */
void ctx_quita_tareas_de(pa_context *c, pa_stream *s)
{
    tarea *quitadas = NULL;
    tarea **p = &c->tareas;
    c->tareas_fin = NULL;
    while (*p) {
        tarea *t = *p;
        if (!s || t->s == s || (t->o && t->o->s == s)) {
            *p = t->sig;
            t->sig = quitadas;
            quitadas = t;
        } else {
            c->tareas_fin = t;
            p = &t->sig;
        }
    }
    while (quitadas) {
        tarea *t = quitadas;
        quitadas = t->sig;
        tarea_suelta(t);
    }
}

static void ctx_pon_estado(pa_context *c, pa_context_state_t st);

static void completa_op_ctx(pa_operation *o)
{
    pa_context *c = o->c;
    switch (o->tipo) {
    case O_EXITO_CTX:
        if (o->cb)
            ((pa_context_success_cb_t)o->cb)(c, 1, o->ud);
        break;
    case O_VOLUMEN: {
        pa_stream *s = flujo_por_indice(c, o->indice);
        if (s) {
            flujo_pon_volumen(s, &o->vol);
            if (o->cb)
                ((pa_context_success_cb_t)o->cb)(c, 1, o->ud);
            ctx_evento(c, PA_SUBSCRIPTION_EVENT_SINK_INPUT | PA_SUBSCRIPTION_EVENT_CHANGE, o->indice);
        } else {
            ctx_error(c, PA_ERR_NOENTITY);
            if (o->cb)
                ((pa_context_success_cb_t)o->cb)(c, 0, o->ud);
        }
        break;
    }
    case O_INFO_ENTRADA: {
        pa_sink_input_info_cb_t cb = (pa_sink_input_info_cb_t)o->cb;
        pa_stream *s = flujo_por_indice(c, o->indice);
        if (s) {
            pa_sink_input_info i;
            pa_format_info fmt;
            flujo_info(s, &i, &fmt);
            if (cb)
                cb(c, &i, 0, o->ud);
            if (cb && o->estado == PA_OPERATION_RUNNING)
                cb(c, NULL, 1, o->ud);
        } else {
            ctx_error(c, PA_ERR_NOENTITY);
            if (cb)
                cb(c, NULL, -1, o->ud);
        }
        break;
    }
    case O_INFO_SERVIDOR: {
        pa_server_info i;
        memset(&i, 0, sizeof i);
        char host[64] = "rocola";
        gethostname(host, sizeof host - 1);
        const char *u = getenv("USER");
        i.user_name = u ? u : "rocola";
        i.host_name = host;
        i.server_version = "17.0.0";
        i.server_name = "rocola-pulso";
        i.sample_spec.format = PA_SAMPLE_FLOAT32LE;
        i.sample_spec.rate = 48000;
        i.sample_spec.channels = 2;
        i.default_sink_name = "rocola";
        i.default_source_name = NULL;
        i.cookie = (uint32_t)getpid();
        pa_channel_map_init_stereo(&i.channel_map);
        if (o->cb)
            ((pa_server_info_cb_t)o->cb)(c, &i, o->ud);
        break;
    }
    case O_INFO_SALIDA: {
        pa_sink_info_cb_t cb = (pa_sink_info_cb_t)o->cb;
        int vale = (o->nombre && strcmp(o->nombre, "rocola") == 0) || (!o->nombre && (o->exito || o->indice == 0));
        if (vale) {
            pa_sink_info i;
            memset(&i, 0, sizeof i);
            i.name = "rocola";
            i.index = 0;
            i.description = "Rocola (anillo de rocola-pulso)";
            i.sample_spec.format = PA_SAMPLE_FLOAT32LE;
            i.sample_spec.rate = 48000;
            i.sample_spec.channels = 2;
            pa_channel_map_init_stereo(&i.channel_map);
            i.owner_module = PA_INVALID_INDEX;
            pa_cvolume_set(&i.volume, 2, PA_VOLUME_NORM);
            i.monitor_source = PA_INVALID_INDEX;
            i.driver = "rocola-pulso";
            i.proplist = c->props;
            i.base_volume = PA_VOLUME_NORM;
            i.state = PA_SINK_RUNNING;
            i.card = PA_INVALID_INDEX;
            if (cb)
                cb(c, &i, 0, o->ud);
            if (cb && o->estado == PA_OPERATION_RUNNING)
                cb(c, NULL, 1, o->ud);
        } else {
            ctx_error(c, PA_ERR_NOENTITY);
            if (cb)
                cb(c, NULL, -1, o->ud);
        }
        break;
    }
    case O_INFO_FUENTE: {
        /* no hay entradas de sonido: la lista sale vacia y un indice no existe */
        typedef void (*fuente_cb_t)(pa_context *, const void *, int, void *);
        fuente_cb_t cb = (fuente_cb_t)o->cb;
        if (!o->exito)
            ctx_error(c, PA_ERR_NOENTITY);
        if (cb)
            cb(c, NULL, o->exito ? 1 : -1, o->ud);
        break;
    }
    default:
        break;
    }
}

static void corre_tarea(pa_context *c, tarea *t)
{
    switch (t->tipo) {
    case T_CONECTA:
        if (c->estado != PA_CONTEXT_CONNECTING)
            break;
        {
            int e = anillo_abre(&c->an, anillo_ruta());
            if (e) {
                pulso_aviso("no se pudo abrir ni crear el anillo %s: %s", anillo_ruta(), strerror(e));
                ctx_error(c, PA_ERR_CONNECTIONREFUSED);
                ctx_pon_estado(c, PA_CONTEXT_FAILED);
                break;
            }
            c->an_abierto = 1;
            TRAZA("contexto %p: anillo %s abierto", (void *)c, c->an.ruta);
        }
        ctx_pon_estado(c, PA_CONTEXT_AUTHORIZING);
        if (c->estado == PA_CONTEXT_AUTHORIZING)
            ctx_pon_estado(c, PA_CONTEXT_SETTING_NAME);
        if (c->estado == PA_CONTEXT_SETTING_NAME)
            ctx_pon_estado(c, PA_CONTEXT_READY);
        break;
    case T_FLUJO_LISTO:
        flujo_listo(t->s);
        break;
    case T_OPERACION:
        if (t->o->estado != PA_OPERATION_RUNNING)
            break;
        if (t->o->s) {
            if (flujo_completa_op(t->o) && t->o->estado == PA_OPERATION_RUNNING)
                op_termina(t->o);
        } else {
            completa_op_ctx(t->o);
            if (t->o->estado == PA_OPERATION_RUNNING)
                op_termina(t->o);
        }
        break;
    case T_EVENTO:
        if (c->estado == PA_CONTEXT_READY && c->sus_cb && (c->mascara & (1u << (t->evento & PA_SUBSCRIPTION_EVENT_FACILITY_MASK))))
            c->sus_cb(c, (pa_subscription_event_type_t)t->evento, t->indice, c->sus_ud);
        break;
    }
}

static void ctx_defer_cb(pa_mainloop_api *a, pa_defer_event *e, void *ud)
{
    (void)e;
    pa_context *c = ud;
    ctx_ref(c);
    while (c->tareas) {
        tarea *t = c->tareas;
        c->tareas = t->sig;
        if (!c->tareas)
            c->tareas_fin = NULL;
        corre_tarea(c, t);
        tarea_suelta(t);
    }
    if (c->defer)
        a->defer_enable(c->defer, 0);
    ctx_unref(c);
}

void ctx_evento(pa_context *c, uint32_t evento, uint32_t indice)
{
    if (c && c->sus_cb && c->estado == PA_CONTEXT_READY)
        ctx_encola(c, T_EVENTO, NULL, NULL, evento, indice);
}

/* ---- estados ---- */
static void ctx_desenlaza(pa_context *c)
{
    ctx_quita_tareas_de(c, NULL);
    pa_stream *s = c->flujos;
    if (s)
        s->ref++;
    while (s) {
        pa_stream *n = s->sig;
        if (n)
            n->ref++;
        if (s->estado == PA_STREAM_CREATING || s->estado == PA_STREAM_READY)
            flujo_termina(s, c->estado == PA_CONTEXT_FAILED ? PA_STREAM_FAILED : PA_STREAM_TERMINATED, c->error);
        pa_stream_unref(s);
        s = n;
    }
    while (c->ops)
        op_cancela(c->ops);
    if (c->an_abierto) {
        anillo_cierra(&c->an);
        c->an_abierto = 0;
    }
}

static void ctx_pon_estado(pa_context *c, pa_context_state_t st)
{
    if (c->estado == st)
        return;
    ctx_ref(c);
    TRAZA("contexto %p: estado %d -> %d", (void *)c, c->estado, st);
    c->estado = st;
    if (c->estado_cb)
        c->estado_cb(c, c->estado_ud);
    if (st == PA_CONTEXT_FAILED || st == PA_CONTEXT_TERMINATED)
        ctx_desenlaza(c);
    ctx_unref(c);
}

/* ---- operaciones ---- */
pa_operation *op_nueva(pa_context *c, pa_stream *s, int tipo, void *cb, void *ud)
{
    pa_operation *o = calloc(1, sizeof *o);
    if (!o) {
        ctx_error(c, PA_ERR_INTERNAL);
        return NULL;
    }
    o->ref = 2; /* la de quien la pidio y la de la lista del contexto */
    o->estado = PA_OPERATION_RUNNING;
    o->c = c;
    ctx_ref(c);
    if (s) {
        o->s = s;
        s->ref++;
    }
    o->tipo = tipo;
    o->cb = cb;
    o->ud = ud;
    o->sig = c->ops;
    c->ops = o;
    ctx_encola(c, T_OPERACION, NULL, o, 0, 0);
    return o;
}

static void op_desenlaza(pa_operation *o)
{
    pa_context *c = o->c;
    pa_stream *s = o->s;
    if (!c)
        return;
    for (pa_operation **p = &c->ops; *p; p = &(*p)->sig)
        if (*p == o) {
            *p = o->sig;
            break;
        }
    o->sig = NULL;
    o->c = NULL;
    o->s = NULL;
    o->cb = NULL;
    o->est_cb = NULL;
    if (s)
        pa_stream_unref(s);
    ctx_unref(c);
    pa_operation_unref(o); /* la de la lista */
}

static void op_pon_estado(pa_operation *o, pa_operation_state_t st)
{
    if (o->estado != PA_OPERATION_RUNNING)
        return;
    o->ref++;
    o->estado = st;
    if (o->est_cb)
        o->est_cb(o, o->est_ud);
    op_desenlaza(o);
    pa_operation_unref(o);
}

void op_termina(pa_operation *o) { op_pon_estado(o, PA_OPERATION_DONE); }
void op_cancela(pa_operation *o) { op_pon_estado(o, PA_OPERATION_CANCELLED); }

EXPORTA pa_operation *pa_operation_ref(pa_operation *o)
{
    if (o)
        o->ref++;
    return o;
}

EXPORTA void pa_operation_unref(pa_operation *o)
{
    if (!o || o->ref <= 0)
        return;
    if (--o->ref == 0) {
        free(o->nombre);
        free(o);
    }
}

EXPORTA pa_operation_state_t pa_operation_get_state(const pa_operation *o)
{
    return o ? o->estado : PA_OPERATION_CANCELLED;
}

EXPORTA void pa_operation_cancel(pa_operation *o)
{
    if (o)
        op_cancela(o);
}

EXPORTA void pa_operation_set_state_callback(pa_operation *o, pa_operation_notify_cb_t cb, void *ud)
{
    if (!o || o->estado != PA_OPERATION_RUNNING)
        return;
    o->est_cb = cb;
    o->est_ud = ud;
}

/* ---- el contexto ---- */
EXPORTA pa_context *pa_context_new_with_proplist(pa_mainloop_api *api, const char *nombre, const pa_proplist *props)
{
    pulso_inicia();
    if (!api || !api->defer_new) {
        TRAZA("pa_context_new(%p, %s) = NULL (sin api)", (void *)api, nombre ? nombre : "-");
        return NULL;
    }
    pa_context *c = calloc(1, sizeof *c);
    if (!c)
        return NULL;
    c->ref = 1;
    c->api = api;
    c->estado = PA_CONTEXT_UNCONNECTED;
    c->an.fd = -1;
    c->props = props ? pa_proplist_copy(props) : pa_proplist_new();
    if (nombre) {
        c->nombre = strdup(nombre);
        if (c->props)
            pa_proplist_sets(c->props, "application.name", nombre);
    }
    c->defer = api->defer_new(api, ctx_defer_cb, c);
    if (c->defer)
        api->defer_enable(c->defer, 0);
    TRAZA("pa_context_new(%p, %s) = %p", (void *)api, nombre ? nombre : "-", (void *)c);
    return c;
}

EXPORTA pa_context *pa_context_new(pa_mainloop_api *api, const char *nombre)
{
    return pa_context_new_with_proplist(api, nombre, NULL);
}

static void ctx_libera(pa_context *c)
{
    TRAZA("contexto %p: se libera", (void *)c);
    c->ref = 1; /* por si algo de aqui abajo lo toca */
    ctx_quita_tareas_de(c, NULL);
    while (c->ops)
        op_cancela(c->ops);
    if (c->an_abierto)
        anillo_cierra(&c->an);
    if (c->defer)
        c->api->defer_free(c->defer);
    if (c->props)
        pa_proplist_free(c->props);
    free(c->nombre);
    free(c);
}

EXPORTA pa_context *pa_context_ref(pa_context *c)
{
    if (c)
        ctx_ref(c);
    return c;
}

EXPORTA void pa_context_unref(pa_context *c)
{
    TRAZA("pa_context_unref(%p)", (void *)c);
    if (!c || c->ref <= 0)
        return;
    ctx_unref(c);
}

EXPORTA void pa_context_set_state_callback(pa_context *c, pa_context_notify_cb_t cb, void *ud)
{
    TRAZA("pa_context_set_state_callback(%p, %p, %p)", (void *)c, (void *)cb, ud);
    if (!c || c->estado == PA_CONTEXT_TERMINATED || c->estado == PA_CONTEXT_FAILED)
        return;
    c->estado_cb = cb;
    c->estado_ud = ud;
}

EXPORTA void pa_context_set_subscribe_callback(pa_context *c, pa_context_subscribe_cb_t cb, void *ud)
{
    TRAZA("pa_context_set_subscribe_callback(%p, %p, %p)", (void *)c, (void *)cb, ud);
    if (!c || c->estado == PA_CONTEXT_TERMINATED || c->estado == PA_CONTEXT_FAILED)
        return;
    c->sus_cb = cb;
    c->sus_ud = ud;
}

EXPORTA void pa_context_set_event_callback(pa_context *c, void *cb, void *ud)
{
    (void)c; (void)cb; (void)ud; /* no hay eventos de servidor */
}

EXPORTA int pa_context_connect(pa_context *c, const char *servidor, pa_context_flags_t banderas, const pa_spawn_api *api)
{
    (void)api;
    if (!c) {
        TRAZA("pa_context_connect(NULL) = -%d", PA_ERR_INVALID);
        return -PA_ERR_INVALID;
    }
    if (c->estado != PA_CONTEXT_UNCONNECTED) {
        ctx_error(c, PA_ERR_BADSTATE);
        TRAZA("pa_context_connect(%p) = -%d (estado %d)", (void *)c, PA_ERR_BADSTATE, c->estado);
        return -PA_ERR_BADSTATE;
    }
    TRAZA("pa_context_connect(%p, %s, %d) = 0 (anillo %s)", (void *)c, servidor ? servidor : "NULL", (int)banderas, anillo_ruta());
    ctx_ref(c);
    ctx_pon_estado(c, PA_CONTEXT_CONNECTING);
    if (c->estado == PA_CONTEXT_CONNECTING)
        ctx_encola(c, T_CONECTA, NULL, NULL, 0, 0);
    ctx_unref(c);
    return 0;
}

EXPORTA void pa_context_disconnect(pa_context *c)
{
    TRAZA("pa_context_disconnect(%p)", (void *)c);
    if (!c)
        return;
    if (c->estado >= PA_CONTEXT_CONNECTING && c->estado <= PA_CONTEXT_READY)
        ctx_pon_estado(c, PA_CONTEXT_TERMINATED);
}

EXPORTA pa_context_state_t pa_context_get_state(const pa_context *c)
{
    return c ? c->estado : PA_CONTEXT_FAILED;
}

EXPORTA int pa_context_errno(const pa_context *c)
{
    return c ? c->error : PA_ERR_INVALID;
}

EXPORTA int pa_context_is_pending(const pa_context *c)
{
    if (!c || c->estado != PA_CONTEXT_READY)
        return 0;
    return c->tareas != NULL || c->ops != NULL;
}

EXPORTA int pa_context_is_local(const pa_context *c) { return c && c->estado == PA_CONTEXT_READY ? 1 : -1; }
EXPORTA const char *pa_context_get_server(const pa_context *c) { return c && c->an_abierto ? c->an.ruta : NULL; }
EXPORTA uint32_t pa_context_get_protocol_version(const pa_context *c) { (void)c; return 35; }
EXPORTA uint32_t pa_context_get_server_protocol_version(const pa_context *c)
{
    return c && c->estado == PA_CONTEXT_READY ? 35 : PA_INVALID_INDEX;
}
EXPORTA uint32_t pa_context_get_index(const pa_context *c) { return c && c->estado == PA_CONTEXT_READY ? 0 : PA_INVALID_INDEX; }

static int listo(pa_context *c)
{
    if (!c)
        return 0;
    if (c->estado != PA_CONTEXT_READY) {
        ctx_error(c, PA_ERR_BADSTATE);
        return 0;
    }
    return 1;
}

EXPORTA pa_operation *pa_context_subscribe(pa_context *c, pa_subscription_mask_t m, pa_context_success_cb_t cb, void *ud)
{
    if (!listo(c)) {
        TRAZA("pa_context_subscribe(%p, 0x%x) = NULL", (void *)c, (unsigned)m);
        return NULL;
    }
    c->mascara = (uint32_t)m;
    pa_operation *o = op_nueva(c, NULL, O_EXITO_CTX, (void *)cb, ud);
    TRAZA("pa_context_subscribe(%p, 0x%x) = %p", (void *)c, (unsigned)m, (void *)o);
    return o;
}

EXPORTA pa_operation *pa_context_get_sink_input_info(pa_context *c, uint32_t idx, pa_sink_input_info_cb_t cb, void *ud)
{
    if (!listo(c) || !cb) {
        if (c && !cb)
            ctx_error(c, PA_ERR_INVALID);
        TRAZA("pa_context_get_sink_input_info(%p, %u) = NULL", (void *)c, idx);
        return NULL;
    }
    if (idx == PA_INVALID_INDEX) {
        ctx_error(c, PA_ERR_INVALID);
        return NULL;
    }
    pa_operation *o = op_nueva(c, NULL, O_INFO_ENTRADA, (void *)cb, ud);
    if (o)
        o->indice = idx;
    TRAZA("pa_context_get_sink_input_info(%p, %u) = %p", (void *)c, idx, (void *)o);
    return o;
}

EXPORTA pa_operation *pa_context_set_sink_input_volume(pa_context *c, uint32_t idx, const pa_cvolume *v, pa_context_success_cb_t cb, void *ud)
{
    if (!listo(c)) {
        TRAZA("pa_context_set_sink_input_volume(%p, %u) = NULL", (void *)c, idx);
        return NULL;
    }
    if (idx == PA_INVALID_INDEX || !pa_cvolume_valid(v)) {
        ctx_error(c, PA_ERR_INVALID);
        TRAZA("pa_context_set_sink_input_volume(%p, %u) = NULL (invalido)", (void *)c, idx);
        return NULL;
    }
    pa_operation *o = op_nueva(c, NULL, O_VOLUMEN, (void *)cb, ud);
    if (o) {
        o->indice = idx;
        o->vol = *v;
    }
    TRAZA("pa_context_set_sink_input_volume(%p, %u, media %u) = %p", (void *)c, idx, pa_cvolume_avg(v), (void *)o);
    return o;
}

EXPORTA pa_operation *pa_context_set_sink_input_mute(pa_context *c, uint32_t idx, int mute, pa_context_success_cb_t cb, void *ud)
{
    (void)mute; /* el silencio tampoco se aplica: el volumen es el de la rocola */
    if (!listo(c))
        return NULL;
    pa_operation *o = op_nueva(c, NULL, O_EXITO_CTX, (void *)cb, ud);
    if (o)
        o->indice = idx;
    return o;
}

EXPORTA pa_operation *pa_context_drain(pa_context *c, pa_context_notify_cb_t cb, void *ud)
{
    (void)cb; (void)ud;
    /* Nada queda pendiente hacia un servidor: como libpulse sin nada pendiente. */
    if (c)
        ctx_error(c, PA_ERR_BADSTATE);
    return NULL;
}

EXPORTA pa_operation *pa_context_get_server_info(pa_context *c, pa_server_info_cb_t cb, void *ud)
{
    if (!listo(c) || !cb)
        return NULL;
    return op_nueva(c, NULL, O_INFO_SERVIDOR, (void *)cb, ud);
}

static pa_operation *info_salida(pa_context *c, const char *nombre, uint32_t idx, int lista, pa_sink_info_cb_t cb, void *ud)
{
    if (!listo(c) || !cb)
        return NULL;
    pa_operation *o = op_nueva(c, NULL, O_INFO_SALIDA, (void *)cb, ud);
    if (o) {
        o->nombre = nombre ? strdup(nombre) : NULL;
        o->indice = idx;
        o->exito = lista;
    }
    return o;
}

EXPORTA pa_operation *pa_context_get_sink_info_by_name(pa_context *c, const char *n, pa_sink_info_cb_t cb, void *ud)
{
    return info_salida(c, n ? n : "rocola", PA_INVALID_INDEX, 0, cb, ud);
}

EXPORTA pa_operation *pa_context_get_sink_info_by_index(pa_context *c, uint32_t idx, pa_sink_info_cb_t cb, void *ud)
{
    return info_salida(c, NULL, idx, 0, cb, ud);
}

EXPORTA pa_operation *pa_context_get_sink_info_list(pa_context *c, pa_sink_info_cb_t cb, void *ud)
{
    return info_salida(c, NULL, PA_INVALID_INDEX, 1, cb, ud);
}

EXPORTA pa_operation *pa_context_get_source_info_list(pa_context *c, void *cb, void *ud)
{
    if (!listo(c) || !cb)
        return NULL;
    pa_operation *o = op_nueva(c, NULL, O_INFO_FUENTE, cb, ud);
    if (o)
        o->exito = 1;
    return o;
}

EXPORTA pa_operation *pa_context_get_source_info_by_index(pa_context *c, uint32_t idx, void *cb, void *ud)
{
    (void)idx;
    if (!listo(c) || !cb)
        return NULL;
    return op_nueva(c, NULL, O_INFO_FUENTE, cb, ud);
}

EXPORTA pa_operation *pa_context_set_name(pa_context *c, const char *nombre, pa_context_success_cb_t cb, void *ud)
{
    if (!listo(c) || !nombre)
        return NULL;
    if (c->props)
        pa_proplist_sets(c->props, "application.name", nombre);
    return op_nueva(c, NULL, O_EXITO_CTX, (void *)cb, ud);
}

/* Tiempos con el reloj monotono (los de pa_context_rttime_*). */
static struct timeval *tv_rt(struct timeval *tv, pa_usec_t us)
{
    tv->tv_sec = (time_t)(us / PA_USEC_PER_SEC);
    tv->tv_usec = (suseconds_t)(us % PA_USEC_PER_SEC) | PA_TIMEVAL_RTCLOCK;
    return tv;
}

EXPORTA pa_time_event *pa_context_rttime_new(const pa_context *c, pa_usec_t us, pa_time_event_cb_t cb, void *ud)
{
    if (!c || !cb)
        return NULL;
    struct timeval tv;
    return c->api->time_new(c->api, us == PA_USEC_INVALID ? NULL : tv_rt(&tv, us), cb, ud);
}

EXPORTA void pa_context_rttime_restart(const pa_context *c, pa_time_event *e, pa_usec_t us)
{
    if (!c || !e)
        return;
    struct timeval tv;
    c->api->time_restart(e, us == PA_USEC_INVALID ? NULL : tv_rt(&tv, us));
}

