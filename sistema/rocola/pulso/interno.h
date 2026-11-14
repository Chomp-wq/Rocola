/* SPDX-License-Identifier: MIT
 * rocola-pulso: lo de dentro (no se exporta: el guion de versiones deja
 * fuera todo lo que no empieza por pa_). */
#ifndef PULSO_INTERNO_H
#define PULSO_INTERNO_H

#include "pulso_abi.h"
#include "anillo.h"
#include "pulso_api.h"

#include <pthread.h>
#include <poll.h>
#include <stdarg.h>

#define EXPORTA __attribute__((visibility("default")))

/* ---- traza y avisos (traza.c) ---- */
extern int pulso_traza_activa;
void pulso_traza(const char *fmt, ...) __attribute__((format(printf, 1, 2)));
void pulso_aviso(const char *fmt, ...) __attribute__((format(printf, 1, 2)));
#define TRAZA(...) do { if (__atomic_load_n(&pulso_traza_activa, __ATOMIC_RELAXED)) pulso_traza(__VA_ARGS__); } while (0)
void pulso_inicia(void);

/* ---- el bucle (bucle.c) ---- */
struct pa_io_event {
    pa_mainloop *m;
    int fd;
    pa_io_event_flags_t eventos;
    pa_io_event_cb_t cb;
    void *ud;
    pa_io_event_destroy_cb_t destruye;
    int muerto;
    int pos; /* en el arreglo de poll de esta vuelta, -1 si no esta */
    struct pa_io_event *sig;
};

struct pa_time_event {
    pa_mainloop *m;
    int activo;
    uint64_t cuando_us; /* CLOCK_MONOTONIC */
    struct timeval tv;  /* el que dio el cliente, para devolverselo */
    pa_time_event_cb_t cb;
    void *ud;
    pa_time_event_destroy_cb_t destruye;
    int muerto;
    struct pa_time_event *sig;
};

struct pa_defer_event {
    pa_mainloop *m;
    int activo;
    pa_defer_event_cb_t cb;
    void *ud;
    pa_defer_event_destroy_cb_t destruye;
    int muerto;
    struct pa_defer_event *sig;
};

struct pa_mainloop {
    pa_mainloop_api api;
    pa_io_event *ios;
    pa_time_event *tiempos;
    pa_defer_event *defers;
    int muertos;
    int salir;
    int retval;
    int despierta_fd;          /* eventfd */
    int en_poll;
    pa_threaded_mainloop *hilo; /* si es el bucle de uno con hilo */
    pa_poll_func poll_func;
    void *poll_ud;
    struct pollfd *pfd;
    unsigned cap_pfd;
};

struct pa_threaded_mainloop {
    pa_mainloop *m;
    pthread_mutex_t mx;          /* no recursivo: la cuenta es nuestra */
    pthread_cond_t cond;
    pthread_cond_t cond_acepta;
    unsigned long dueno;         /* pthread_self() de quien lo tiene (atomico) */
    int cuenta;
    int por_aceptar;
    int corriendo;
    pthread_t hilo;
    char nombre[16];
};

uint64_t pulso_ahora_us(void);
int bucle_en_hilo(pa_mainloop *m);
void hilo_bloquea(pa_threaded_mainloop *t);
void hilo_suelta(pa_threaded_mainloop *t);

/* ---- contexto, flujos y operaciones (contexto.c, flujo.c) ---- */
typedef struct tarea {
    struct tarea *sig;
    int tipo;
    pa_stream *s;       /* con referencia */
    pa_operation *o;    /* con referencia */
    uint32_t evento;
    uint32_t indice;
} tarea;

enum { T_CONECTA = 1, T_FLUJO_LISTO, T_OPERACION, T_EVENTO };

struct pa_context {
    int ref;
    pa_mainloop_api *api;
    char *nombre;
    pa_proplist *props;
    pa_context_state_t estado;
    int error;
    pa_context_notify_cb_t estado_cb;
    void *estado_ud;
    pa_context_subscribe_cb_t sus_cb;
    void *sus_ud;
    uint32_t mascara;
    pa_context_notify_cb_t evento_cb_dummy;
    pa_defer_event *defer;
    tarea *tareas, *tareas_fin;
    pa_stream *flujos;
    pa_operation *ops;
    anillo an;
    int an_abierto;
    uint64_t prox_revision_us;
};

enum { O_EXITO_CTX = 1, O_EXITO_FLUJO, O_TIEMPOS, O_DRENA, O_INFO_ENTRADA, O_VOLUMEN, O_INFO_SERVIDOR, O_INFO_SALIDA, O_NADA };

struct pa_operation {
    int ref;
    pa_operation_state_t estado;
    pa_context *c;   /* con referencia mientras corre */
    pa_stream *s;    /* con referencia mientras corre */
    int tipo;
    void *cb;
    void *ud;
    uint32_t indice;
    int exito;
    pa_cvolume vol;
    char *nombre;
    pa_operation_notify_cb_t est_cb;
    void *est_ud;
    struct pa_operation *sig; /* en c->ops */
};

struct pa_stream {
    int ref;
    pa_context *c;
    struct pa_stream *sig; /* en c->flujos */
    char *nombre;
    pa_proplist *props;
    pa_sample_spec ss;
    pa_channel_map mapa;
    int canal_i, canal_d;
    size_t tc; /* tamano de un cuadro del cliente */
    pa_stream_state_t estado;
    pa_stream_direction_t dir;
    uint32_t indice;
    pa_stream_flags_t banderas;
    pa_buffer_attr attr;
    uint64_t objetivo;  /* cuadros */
    uint64_t limite;    /* cuadros que se aceptan sin leer */
    pa_cvolume vol;
    int tapado;

    pa_stream_notify_cb_t estado_cb; void *estado_ud;
    pa_stream_request_cb_t escribe_cb; void *escribe_ud;
    pa_stream_request_cb_t lee_cb; void *lee_ud;
    pa_stream_notify_cb_t empezo_cb; void *empezo_ud;
    pa_stream_notify_cb_t subdesborde_cb; void *subdesborde_ud;
    pa_stream_notify_cb_t desborde_cb; void *desborde_ud;
    pa_stream_notify_cb_t latencia_cb; void *latencia_ud;
    pa_stream_notify_cb_t movido_cb; void *movido_ud;
    pa_stream_notify_cb_t suspendido_cb; void *suspendido_ud;
    pa_stream_notify_cb_t attr_cb; void *attr_ud;
    pa_stream_event_cb_t evento_cb; void *evento_ud;

    /* contadores en cuadros del flujo */
    int64_t w;            /* indice de escritura */
    float *cola;          /* lo aceptado que aun no cabe en el anillo (estereo) */
    size_t cola_ini, cola_n, cola_cap;
    float *conv;
    size_t conv_cap;
    void *buf_escritura;
    size_t buf_tam;
    int buf_en_uso;

    /* el anillo */
    int activo;           /* es el flujo de este proceso que escribe en el anillo */
    uint64_t gen_activo;
    pa_time_event *reloj;
    uint64_t prox_inodo_us;
    uint64_t ult_latido_us;
    int esperando_inicio;
    uint64_t marca;       /* leido efectivo cuando se empezo a esperar */
    uint64_t ult_subdesbordes;
    int64_t r_subdesborde; /* indice de lectura en el ultimo subdesborde */
    uint32_t ult_volumen;
    pa_timing_info ti;
    int ti_valido;
    int en_reloj;
};

/* contexto.c */
void ctx_encola(pa_context *c, int tipo, pa_stream *s, pa_operation *o, uint32_t evento, uint32_t indice);
void ctx_quita_tareas_de(pa_context *c, pa_stream *s);
void ctx_error(pa_context *c, int e);
void ctx_evento(pa_context *c, uint32_t evento, uint32_t indice);
pa_operation *op_nueva(pa_context *c, pa_stream *s, int tipo, void *cb, void *ud);
void op_cancela(pa_operation *o);
void op_termina(pa_operation *o);
uint32_t ctx_nuevo_indice(void);

/* flujo.c */
void flujo_listo(pa_stream *s);
void flujo_termina(pa_stream *s, pa_stream_state_t est, int error);
int flujo_completa_op(pa_operation *o); /* 1 si ya acabo */
pa_stream *flujo_por_indice(pa_context *c, uint32_t idx);
void flujo_info(pa_stream *s, pa_sink_input_info *i, pa_format_info *fmt);
void flujo_pon_volumen(pa_stream *s, const pa_cvolume *v);

/* utiles.c */
size_t pulso_tam_muestra(pa_sample_format_t f);
int pulso_spec_valida(const pa_sample_spec *ss);
const char *pulso_nombre_formato(pa_sample_format_t f);
void pulso_mapa_fabrica(pa_channel_map *m, unsigned canales);

#endif
