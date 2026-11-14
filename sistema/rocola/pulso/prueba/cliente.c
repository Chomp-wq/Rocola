/* SPDX-License-Identifier: MIT
 * Prueba (1) de rocola-pulso: un cliente que usa EXACTAMENTE las 48 funciones
 * que Soloist busca (dlopen + dlsym, como Soloist) y como las usa Soloist
 * (visto en su codigo por fuera: contexto con NOAUTOSPAWN, suscripcion a las
 * entradas, flujo «Spotify» con media.role, START_CORKED|INTERPOLATE_TIMING|
 * AUTO_TIMING_UPDATE|ADJUST_LATENCY, tlength y minreq por pa_usec_to_bytes,
 * prebuf 0, un hilo propio que escribe mirando writable_size, cork en el
 * underflow, flush con espera de la operacion, update_timing_info...), y un
 * lector del anillo en otro proceso que comprueba cuadro a cuadro.
 *
 *   cliente ruta/a/libpulse.so.0 [rapido]
 * Usa ROCOLA_SPOTIFY_ANILLO (lo pone prueba.sh en una carpeta temporal). */
#include "comun.h"
#include "senal.h"
#include "soloist48.h"
#include "../anillo.h"

#include <pthread.h>

typedef struct {
    pa_threaded_mainloop *m;
    pa_context *c;
    pa_stream *s;
    int empezo, subdesbordes;
    int eventos_cambio;
    uint32_t vol_info;
    int info_llegadas;
    pa_sample_spec ss;
    int ci, cd;
    uint64_t k;           /* siguiente cuadro de la senal */
    uint64_t por_escribir;
    size_t tc;
    double t_primera, t_ultima;
} prueba_t;

static void ctx_estado_cb(pa_context *c, void *ud) { (void)c; prueba_t *p = ud; pa_threaded_mainloop_signal(p->m, 0); }
static void exito_ctx_cb(pa_context *c, int ok, void *ud) { (void)c; (void)ok; prueba_t *p = ud; pa_threaded_mainloop_signal(p->m, 0); }

static void info_cb(pa_context *c, const pa_sink_input_info *i, int eol, void *ud)
{
    (void)c;
    prueba_t *p = ud;
    if (eol < 0 || !i) {
        pa_threaded_mainloop_signal(p->m, 0);
        return;
    }
    p->vol_info = pa_cvolume_avg(&i->volume);
    p->info_llegadas++;
    pa_threaded_mainloop_signal(p->m, 0);
}

/* como Soloist: si el aviso es un cambio de SU entrada, pide la info */
static void sus_cb(pa_context *c, pa_subscription_event_type_t t, uint32_t idx, void *ud)
{
    prueba_t *p = ud;
    if (!p->s)
        return;
    uint32_t mio = pa_stream_get_index(p->s);
    if ((t & PA_SUBSCRIPTION_EVENT_TYPE_MASK) != PA_SUBSCRIPTION_EVENT_CHANGE || idx != mio)
        return;
    p->eventos_cambio++;
    pa_operation *o = pa_context_get_sink_input_info(c, idx, info_cb, p);
    if (o)
        pa_operation_unref(o);
}

static void flujo_estado_cb(pa_stream *s, void *ud) { (void)s; prueba_t *p = ud; pa_threaded_mainloop_signal(p->m, 0); }
static void exito_flujo_cb(pa_stream *s, int ok, void *ud) { (void)s; (void)ok; prueba_t *p = ud; pa_threaded_mainloop_signal(p->m, 0); }
static void empezo_cb(pa_stream *s, void *ud) { (void)s; prueba_t *p = ud; p->empezo++; pa_threaded_mainloop_signal(p->m, 0); }

/* como Soloist: en el underflow tapa el flujo */
static void subdesborde_cb(pa_stream *s, void *ud)
{
    prueba_t *p = ud;
    p->subdesbordes++;
    pa_operation *o = pa_stream_cork(s, 1, exito_flujo_cb, p);
    if (o)
        pa_operation_unref(o);
    pa_threaded_mainloop_signal(p->m, 0);
}

static void espera_op(prueba_t *p, pa_operation *o)
{
    if (!o)
        return;
    while (pa_operation_get_state(o) == PA_OPERATION_RUNNING)
        pa_threaded_mainloop_wait(p->m);
    pa_operation_unref(o);
}

/* el hilo que escribe («PulseAudioDriver Thread»): mira writable_size */
static void *escritor(void *arg)
{
    prueba_t *p = arg;
    uint8_t *buf = malloc(4096 * 64);
    while (p->por_escribir > 0) {
        pa_threaded_mainloop_lock(p->m);
        if (pa_stream_get_state(p->s) != PA_STREAM_READY) {
            fprintf(stderr, "can't write to stream, %s\n", pa_strerror(pa_context_errno(p->c)));
            pa_threaded_mainloop_unlock(p->m);
            break;
        }
        size_t n = pa_stream_writable_size(p->s);
        if (n == 0 || n == (size_t)-1) {
            pa_threaded_mainloop_unlock(p->m);
            duerme_ms(2);
            continue;
        }
        size_t k = n / p->tc;
        if (k > 4096)
            k = 4096;
        if (k > p->por_escribir)
            k = (size_t)p->por_escribir;
        senal_llena(buf, p->k, k, p->ss.format, p->ss.channels, p->ci, p->cd);
        int r = pa_stream_write(p->s, buf, k * p->tc, NULL, 0, PA_SEEK_RELATIVE);
        if (r == 0) {
            if (p->t_primera == 0)
                p->t_primera = ahora_s();
            p->k += k;
            p->por_escribir -= k;
        }
        pa_threaded_mainloop_unlock(p->m);
    }
    p->t_ultima = ahora_s();
    free(buf);
    return NULL;
}

static void escribe_frames(prueba_t *p, uint64_t n)
{
    p->por_escribir = n;
    p->t_primera = 0;
    pthread_t h;
    pthread_create(&h, NULL, escritor, p);
    pthread_join(h, NULL);
}

static const pa_timing_info *tiempos(prueba_t *p)
{
    pa_operation *o = pa_stream_update_timing_info(p->s, exito_flujo_cb, p);
    if (o) {
        while (pa_context_get_state(p->c) == PA_CONTEXT_READY && pa_stream_get_state(p->s) == PA_STREAM_READY &&
               pa_operation_get_state(o) == PA_OPERATION_RUNNING)
            pa_threaded_mainloop_wait(p->m);
        pa_operation_unref(o);
    }
    return pa_stream_get_timing_info(p->s);
}

/* espera a que el lector lo tome todo (lock tomado) */
static int espera_drenado(prueba_t *p, double tope)
{
    double fin = ahora_s() + tope;
    while (ahora_s() < fin) {
        const pa_timing_info *t = tiempos(p);
        if (t && t->write_index == t->read_index)
            return 1;
        pa_threaded_mainloop_unlock(p->m);
        duerme_ms(10);
        pa_threaded_mainloop_lock(p->m);
    }
    return 0;
}

static pa_stream *conecta_flujo(prueba_t *p, int con_props)
{
    pa_stream *s;
    if (con_props) {
        pa_proplist *pl = pa_proplist_new();
        pa_proplist_sets(pl, "media.role", "music");
        s = pa_stream_new_with_proplist(p->c, "Spotify", &p->ss, NULL, pl);
        pa_proplist_free(pl);
    } else {
        s = pa_stream_new(p->c, "spotify", &p->ss, NULL);
    }
    if (!s)
        return NULL;
    p->s = s;
    pa_stream_set_underflow_callback(s, subdesborde_cb, p);
    pa_stream_set_started_callback(s, empezo_cb, p);
    pa_stream_set_state_callback(s, flujo_estado_cb, p);
    pa_cvolume v;
    pa_cvolume_set(&v, p->ss.channels, 65536);
    pa_buffer_attr a;
    a.maxlength = (uint32_t)-1;
    a.tlength = (uint32_t)pa_usec_to_bytes(200 * 1000, &p->ss);
    a.prebuf = 0;
    a.minreq = (uint32_t)pa_usec_to_bytes(50 * 1000, &p->ss);
    a.fragsize = 0;
    int r = pa_stream_connect_playback(s, NULL, &a, (pa_stream_flags_t)0x200b, &v, NULL);
    COMPRUEBA(r == 0, "connect_playback = %d", r);
    pa_stream_state_t st = pa_stream_get_state(s);
    COMPRUEBA(st == PA_STREAM_CREATING, "justo tras conectar el flujo esta en CREATING (%d)", st);
    while (st == PA_STREAM_CREATING) {
        pa_threaded_mainloop_wait(p->m);
        st = pa_stream_get_state(s);
    }
    COMPRUEBA(st == PA_STREAM_READY, "flujo READY (%d)", st);
    COMPRUEBA(pa_stream_is_corked(s) == 1, "empieza tapado (START_CORKED)");
    return s;
}

static const char *nombre_fmt(pa_sample_format_t f)
{
    switch (f) {
    case PA_SAMPLE_S16LE: return "s16le";
    case PA_SAMPLE_FLOAT32LE: return "float32le";
    case PA_SAMPLE_S32LE: return "s32le";
    default: return "?";
    }
}

static int escenario(const char *lib, pa_sample_format_t f, int canales, uint32_t rate, int completo)
{
    (void)lib;
    int mal_antes = mal_n;
    char rep[600];
    snprintf(rep, sizeof rep, "%s.rep", anillo_ruta());
    int bits = senal_bits(f);
    pid_t lec = lanza_lector(rep, bits, canales == 1, NULL);

    prueba_t P;
    memset(&P, 0, sizeof P);
    prueba_t *p = &P;
    p->ss.format = f;
    p->ss.rate = rate;
    p->ss.channels = (uint8_t)canales;
    p->tc = senal_tam(f) * (size_t)canales;
    p->ci = 0;
    p->cd = canales == 1 ? 0 : canales == 6 ? 3 : 1; /* AIFF de 6: FL en 0, FR en 3 */
    p->k = 1000;

    p->m = pa_threaded_mainloop_new();
    COMPRUEBA(pa_threaded_mainloop_start(p->m) == 0, "start");
    pa_threaded_mainloop_lock(p->m);
    p->c = pa_context_new(pa_threaded_mainloop_get_api(p->m), "spotify");
    pa_context_set_state_callback(p->c, ctx_estado_cb, p);
    pa_context_set_subscribe_callback(p->c, sus_cb, p);
    int r = pa_context_connect(p->c, NULL, PA_CONTEXT_NOAUTOSPAWN, NULL);
    COMPRUEBA(r == 0, "context_connect = %d", r);
    COMPRUEBA(pa_context_get_state(p->c) == PA_CONTEXT_CONNECTING, "justo tras conectar: CONNECTING");
    for (;;) {
        pa_context_state_t st = pa_context_get_state(p->c);
        if (st == PA_CONTEXT_READY)
            break;
        if (st >= PA_CONTEXT_CONNECTING && st <= PA_CONTEXT_SETTING_NAME) {
            pa_threaded_mainloop_wait(p->m);
            continue;
        }
        COMPRUEBA(0, "el contexto fallo: %s", pa_strerror(pa_context_errno(p->c)));
        break;
    }
    espera_op(p, pa_context_subscribe(p->c, PA_SUBSCRIPTION_MASK_SINK_INPUT, exito_ctx_cb, p));
    pa_stream *s = conecta_flujo(p, 1);
    COMPRUEBA(s != NULL, "stream_new_with_proplist");
    espera_op(p, pa_stream_cork(s, 0, exito_flujo_cb, p));
    COMPRUEBA(pa_stream_is_corked(s) == 0, "destapado");
    pa_threaded_mainloop_unlock(p->m);

    /* 0,5 s de senal: el ritmo lo marca el lector */
    uint64_t T = rate / 2;
    struct timeval t0, t1;
    pa_gettimeofday(&t0);
    escribe_frames(p, T);
    pa_threaded_mainloop_lock(p->m);
    int drenado = espera_drenado(p, 3.0);
    pa_gettimeofday(&t1);
    double seg = pa_timeval_diff(&t1, &t0) / 1e6;
    COMPRUEBA(drenado, "el lector lo tomo todo");
    double minimo = (double)T / rate - 0.25;
    COMPRUEBA(seg >= minimo * 0.9 && seg <= (double)T / rate + 1.0,
              "el ritmo lo marca el lector: %.3f s para %.3f s de audio", seg, (double)T / rate);
    COMPRUEBA(p->empezo >= 1, "started (%d)", p->empezo);
    const pa_timing_info *ti = tiempos(p);
    COMPRUEBA(ti && ti->write_index == (int64_t)(T * p->tc), "write_index = %lld (esperado %llu)",
              ti ? (long long)ti->write_index : -1LL, (unsigned long long)(T * p->tc));
    /* el escritor se detuvo: el lector se queda sin datos -> underflow -> tapa (como Soloist) */
    double fin = ahora_s() + 2.0;
    while (p->subdesbordes == 0 && ahora_s() < fin) {
        pa_threaded_mainloop_unlock(p->m);
        duerme_ms(10);
        pa_threaded_mainloop_lock(p->m);
    }
    COMPRUEBA(p->subdesbordes >= 1, "underflow al acabarse los datos (%d)", p->subdesbordes);
    uint64_t consumidos = ti ? (uint64_t)ti->read_index / p->tc : 0;

    if (completo) {
        /* volumen: se guarda, sale en la info y en el anillo, nunca se aplica */
        pa_cvolume v2;
        pa_cvolume_set(&v2, p->ss.channels, 30000);
        int ev0 = p->eventos_cambio;
        espera_op(p, pa_context_set_sink_input_volume(p->c, pa_stream_get_index(s), &v2, exito_ctx_cb, p));
        fin = ahora_s() + 2.0;
        while ((p->eventos_cambio == ev0 || p->vol_info != 30000) && ahora_s() < fin)
            pa_threaded_mainloop_wait(p->m);
        COMPRUEBA(p->eventos_cambio > ev0, "aviso de cambio de la entrada tras el volumen");
        COMPRUEBA(p->vol_info == 30000, "get_sink_input_info da el volumen pedido (%u)", p->vol_info);
        anillo an;
        if (anillo_abre(&an, anillo_ruta()) == 0) {
            COMPRUEBA(A_CARGA32(&an.cab->volumen_pedido) == 30000, "volumen_pedido en el anillo (%u)", A_CARGA32(&an.cab->volumen_pedido));
            COMPRUEBA(A_CARGA32(&an.cab->formato_original) == (uint32_t)f, "formato_original en el anillo");
            COMPRUEBA(A_CARGA32(&an.cab->frecuencia) == rate, "frecuencia en el anillo");
            COMPRUEBA(A_CARGA32(&an.cab->pid_escritor) == (uint32_t)getpid(), "pid_escritor");
            anillo_cierra(&an);
        }

        /* tapado: el lector no toma nada; vaciar deja write_index = read_index */
        COMPRUEBA(pa_stream_is_corked(s) == 1, "tapado por el underflow");
        pa_threaded_mainloop_unlock(p->m);
        escribe_frames(p, rate / 10);
        pa_threaded_mainloop_lock(p->m);
        ti = tiempos(p);
        int64_t r0 = ti ? ti->read_index : -1;
        COMPRUEBA(ti && ti->playing == 0, "tapado: playing 0");
        pa_threaded_mainloop_unlock(p->m);
        duerme_ms(300);
        pa_threaded_mainloop_lock(p->m);
        ti = tiempos(p);
        COMPRUEBA(ti && ti->read_index == r0, "tapado: el lector no consume");
        COMPRUEBA(ti && ti->write_index > ti->read_index, "hay pendientes antes del vaciado");
        espera_op(p, pa_stream_flush(s, exito_flujo_cb, p));
        ti = tiempos(p);
        COMPRUEBA(ti && ti->write_index == ti->read_index, "tras el vaciado write_index == read_index");
        espera_op(p, pa_stream_cork(s, 0, exito_flujo_cb, p));
        pa_threaded_mainloop_unlock(p->m);
        escribe_frames(p, rate / 5);
        pa_threaded_mainloop_lock(p->m);
        COMPRUEBA(espera_drenado(p, 3.0), "drenado tras destapar");
        ti = tiempos(p);
        consumidos = ti ? (uint64_t)ti->read_index / p->tc : 0;
        COMPRUEBA(p->empezo >= 2, "started otra vez tras destapar (%d)", p->empezo);

        /* desconectar y volver a conectar otro flujo (pa_stream_new) */
        COMPRUEBA(pa_stream_disconnect(s) == 0, "stream_disconnect");
        pa_stream_unref(s);
        p->s = NULL;
        s = conecta_flujo(p, 0);
        COMPRUEBA(s != NULL, "stream_new (segundo flujo)");
        espera_op(p, pa_stream_cork(s, 0, exito_flujo_cb, p));
        pa_threaded_mainloop_unlock(p->m);
        escribe_frames(p, rate / 5);
        pa_threaded_mainloop_lock(p->m);
        COMPRUEBA(espera_drenado(p, 3.0), "drenado del segundo flujo");
        ti = tiempos(p);
        consumidos += ti ? (uint64_t)ti->read_index / p->tc : 0;
    }

    /* el cierre de Soloist: primero el contexto, despues el flujo */
    pa_context *ctx = p->c;
    pa_context_disconnect(ctx);
    pa_context_unref(ctx); /* sigue vivo: el flujo lo tiene */
    COMPRUEBA(pa_stream_get_state(s) == PA_STREAM_TERMINATED, "el flujo termina con el contexto");
    int rd = pa_stream_disconnect(s);
    COMPRUEBA(rd < 0, "disconnect de un flujo ya terminado devuelve error (%d)", rd);
    const char *err = pa_strerror(pa_context_errno(ctx));
    COMPRUEBA(err && !strcmp(err, "Bad state"), "el error del contexto es «Bad state» (%s)", err ? err : "NULL");
    pa_stream_unref(s);
    pa_threaded_mainloop_unlock(p->m);
    pa_threaded_mainloop_stop(p->m);
    pa_threaded_mainloop_free(p->m);

    reporte_t R;
    duerme_ms(50);
    COMPRUEBA(para_lector(lec, rep, &R) == 0, "reporte del lector");
    COMPRUEBA(R.errores == 0, "lector: %llu errores (%s)", R.errores, R.primer_error);
    COMPRUEBA(R.cuadros == consumidos, "lector: %llu cuadros, el cliente dice %llu", R.cuadros, (unsigned long long)consumidos);
    COMPRUEBA(R.flujos == (completo ? 2u : 1u), "lector: %llu flujos", R.flujos);
    printf("  %-10s %d canal(es) %6u Hz: %llu cuadros comprobados, %.3f s para %.3f s, started %d, underflow %d, %s\n",
           nombre_fmt(f), canales, rate, R.cuadros, seg, (double)T / rate, p->empezo, p->subdesbordes,
           mal_n == mal_antes ? "bien" : "MAL");
    return mal_n == mal_antes;
}

int main(int argc, char **argv)
{
    if (argc < 2) {
        fprintf(stderr, "uso: cliente libpulse.so.0 [rapido]\n");
        return 2;
    }
    pon_dir_lector(argv[0]);
    if (carga48(argv[1]) != 0)
        return 1;
    int rapido = argc > 2 && !strcmp(argv[2], "rapido");
    pa_sample_format_t fs[] = {PA_SAMPLE_S16LE, PA_SAMPLE_FLOAT32LE, PA_SAMPLE_S32LE};
    int cs[] = {1, 2, 6};
    uint32_t rs[] = {44100, 48000};
    int esc = 0, esc_bien = 0;
    for (int i = 0; i < 3; i++)
        for (int j = 0; j < 3; j++)
            for (int k = 0; k < 2; k++) {
                if (rapido && !(i == 1 && j == 1))
                    continue;
                esc++;
                esc_bien += escenario(argv[1], fs[i], cs[j], rs[k], i == 1 || !rapido);
            }
    int usadas = 0;
    for (int i = 0; i < K_TOTAL; i++) {
        if (cuenta48[i])
            usadas++;
        else
            fprintf(stderr, "  no se uso %s\n", nombres48[i]);
    }
    COMPRUEBA(usadas == 48, "se usaron %d de las 48 funciones", usadas);
    printf("cliente: %d de %d escenarios bien; %d de 48 funciones usadas; comprobaciones %d bien, %d mal\n", esc_bien,
           esc, usadas, bien_n, mal_n);
    return mal_n ? 1 : 0;
}
