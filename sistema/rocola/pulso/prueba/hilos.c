/* SPDX-License-Identifier: MIT
 * Prueba de rocola-pulso: HILOS Y ROBUSTEZ (revision del 15-11-2026).
 * Todo en un solo proceso (asi ThreadSanitizer ve todos los hilos):
 *
 *   carrera   un hilo escribe como Soloist (trozos enteros con solo mirar que
 *             writable_size no sea 0), otro tapa, destapa, vacia, pide tiempos
 *             y cambia el volumen esperando las operaciones, el principal
 *             recibe los avisos de la suscripcion, y un LECTOR en otro hilo
 *             (con su propio mapa del anillo, como rocola-xp) lee al ritmo de
 *             una tarjeta, a veces copiando despacio (en dos mitades), a veces
 *             ausente y a veces rehaciendo el anillo por renombre. Comprueba que
 *             el contador de la senal NUNCA baja (un vaciado con el anillo lleno
 *             no debe pisar lo que el lector esta copiando) y que nada se traba.
 *   ciclos    200 vueltas de crear, conectar, escribir y cerrar (en el orden de
 *             Soloist y en el de libpulse): sin descriptores ni hilos de mas.
 *   cabecera  un latido del lector «del futuro» no cuenta como lector, y una
 *             cabecera pisada (sin la magia) se rehace por renombre.
 *   consumo   el hilo del bucle: cuantas veces despierta por segundo y cuanto
 *             procesador gasta sin flujo, tapado, sin lector y sonando.
 *
 *   hilos [caso]   (sin caso: todos). La carpeta, PRUEBA_DIR. */
#include "comun.h"
#include "../pulso_api.h"
#include "../anillo.h"

#include <dirent.h>
#include <fcntl.h>
#include <pthread.h>
#include <sys/mman.h>
#include <sys/stat.h>

static char base[512];
static int n_caso;

static void ruta_caso(const char *nombre)
{
    char r[700];
    snprintf(r, sizeof r, "%s/%02d-%s/run/rocola-xp/spotify/sonido.anillo", base, ++n_caso, nombre);
    setenv("ROCOLA_SPOTIFY_ANILLO", r, 1);
}

static void duerme_us(long us)
{
    struct timespec t = {us / 1000000, (us % 1000000) * 1000L};
    while (nanosleep(&t, &t) == -1 && errno == EINTR)
        ;
}

/* ---- el cliente ---- */
typedef struct {
    pa_threaded_mainloop *m;
    pa_context *c;
    pa_stream *s;
    int eventos;
    int fin;
    uint64_t k; /* siguiente valor del contador */
    unsigned escritos, vaciados, tapados, tiempos, volumenes, infos;
} cli_t;

static void sig_ctx(pa_context *c, void *u) { (void)c; pa_threaded_mainloop_signal(((cli_t *)u)->m, 0); }
static void sig_s(pa_stream *s, void *u) { (void)s; pa_threaded_mainloop_signal(((cli_t *)u)->m, 0); }
static void ok_s(pa_stream *s, int ok, void *u) { (void)s; (void)ok; pa_threaded_mainloop_signal(((cli_t *)u)->m, 0); }
static void ok_c(pa_context *c, int ok, void *u) { (void)c; (void)ok; pa_threaded_mainloop_signal(((cli_t *)u)->m, 0); }
static void info_cb(pa_context *c, const pa_sink_input_info *i, int eol, void *u)
{
    (void)c; (void)i; (void)eol;
    pa_threaded_mainloop_signal(((cli_t *)u)->m, 0);
}
static void sus_cb(pa_context *c, pa_subscription_event_type_t t, uint32_t idx, void *u)
{
    (void)c; (void)t; (void)idx;
    ((cli_t *)u)->eventos++;
}

static void espera(cli_t *c, pa_operation *o)
{
    if (!o)
        return;
    while (pa_operation_get_state(o) == PA_OPERATION_RUNNING)
        pa_threaded_mainloop_wait(c->m);
    pa_operation_unref(o);
}

/* crea el bucle y el contexto (READY) y, si flujo, el flujo como Soloist */
static int tlength_us = 100000;

static int cli_abre(cli_t *c, int flujo, int tapado)
{
    memset(c, 0, sizeof *c);
    c->m = pa_threaded_mainloop_new();
    if (!c->m || pa_threaded_mainloop_start(c->m) != 0)
        return 0;
    pa_threaded_mainloop_lock(c->m);
    c->c = pa_context_new(pa_threaded_mainloop_get_api(c->m), "hilos");
    pa_context_set_state_callback(c->c, sig_ctx, c);
    pa_context_set_subscribe_callback(c->c, sus_cb, c);
    pa_context_connect(c->c, NULL, PA_CONTEXT_NOAUTOSPAWN, NULL);
    pa_context_state_t st;
    while ((st = pa_context_get_state(c->c)) != PA_CONTEXT_READY && st != PA_CONTEXT_FAILED && st != PA_CONTEXT_TERMINATED)
        pa_threaded_mainloop_wait(c->m);
    int ok = st == PA_CONTEXT_READY;
    if (ok)
        espera(c, pa_context_subscribe(c->c, PA_SUBSCRIPTION_MASK_SINK_INPUT, ok_c, c));
    if (ok && flujo) {
        pa_sample_spec ss = {PA_SAMPLE_FLOAT32LE, 48000, 2};
        pa_proplist *p = pa_proplist_new();
        pa_proplist_sets(p, "media.role", "music");
        c->s = pa_stream_new_with_proplist(c->c, "Spotify", &ss, NULL, p);
        pa_proplist_free(p);
        pa_stream_set_state_callback(c->s, sig_s, c);
        pa_buffer_attr a = {(uint32_t)-1, (uint32_t)pa_usec_to_bytes((pa_usec_t)tlength_us, &ss), 0, (uint32_t)pa_usec_to_bytes(20000, &ss), 0};
        pa_cvolume v;
        pa_cvolume_set(&v, 2, PA_VOLUME_NORM);
        pa_stream_connect_playback(c->s, NULL, &a, (pa_stream_flags_t)(0x200b & ~(tapado ? 0 : PA_STREAM_START_CORKED)), &v, NULL);
        while (pa_stream_get_state(c->s) == PA_STREAM_CREATING)
            pa_threaded_mainloop_wait(c->m);
        ok = pa_stream_get_state(c->s) == PA_STREAM_READY;
    }
    pa_threaded_mainloop_unlock(c->m);
    return ok;
}

/* cierre en el orden de Soloist (contexto primero) o en el de libpulse */
static void cli_cierra(cli_t *c, int como_soloist)
{
    pa_threaded_mainloop_lock(c->m);
    if (como_soloist) {
        pa_context_disconnect(c->c);
        pa_context_unref(c->c);
        if (c->s) {
            pa_stream_disconnect(c->s);
            pa_stream_unref(c->s);
        }
    } else {
        if (c->s) {
            pa_stream_disconnect(c->s);
            pa_stream_unref(c->s);
        }
        pa_context_disconnect(c->c);
        pa_context_unref(c->c);
    }
    pa_threaded_mainloop_unlock(c->m);
    pa_threaded_mainloop_stop(c->m);
    pa_threaded_mainloop_free(c->m);
}

/* el hilo que escribe, como el «PulseAudioDriver Thread» de Soloist */
static void *escritor(void *x)
{
    cli_t *c = x;
    float buf[2 * 4096];
    for (;;) {
        pa_threaded_mainloop_lock(c->m);
        if (__atomic_load_n(&c->fin, __ATOMIC_ACQUIRE)) {
            pa_threaded_mainloop_unlock(c->m);
            break;
        }
        int escribio = 0;
        if (pa_stream_get_state(c->s) == PA_STREAM_READY) {
            size_t w = pa_stream_writable_size(c->s);
            if (w != 0 && w != (size_t)-1) {
                /* trozo entero (4096), aunque writable diga menos */
                for (int i = 0; i < 4096; i++) {
                    float v = (float)(c->k + (uint64_t)i);
                    buf[2 * i] = v;
                    buf[2 * i + 1] = -v;
                }
                if (pa_stream_write(c->s, buf, sizeof buf, NULL, 0, PA_SEEK_RELATIVE) == 0) {
                    c->k += 4096;
                    c->escritos++;
                    escribio = 1;
                }
            }
        }
        pa_threaded_mainloop_unlock(c->m);
        duerme_us(escribio ? 1000 : 3000);
    }
    return NULL;
}

/* el hilo que manda: tapa, destapa, vacia, pide tiempos y volumen */
static void *mandos(void *x)
{
    cli_t *c = x;
    unsigned r = 12345;
    while (!__atomic_load_n(&c->fin, __ATOMIC_ACQUIRE)) {
        r = r * 1103515245u + 12345u;
        unsigned que = (r >> 16) % 6;
        pa_threaded_mainloop_lock(c->m);
        if (pa_stream_get_state(c->s) == PA_STREAM_READY) {
            switch (que) {
            case 0: /* pausa como Soloist: tapar y vaciar esperando */
                pa_operation_unref(pa_stream_cork(c->s, 1, NULL, NULL));
                espera(c, pa_stream_flush(c->s, ok_s, c));
                c->vaciados++;
                c->tapados++;
                break;
            case 1:
                pa_operation_unref(pa_stream_cork(c->s, 0, NULL, NULL));
                break;
            case 2:
                espera(c, pa_stream_flush(c->s, ok_s, c));
                c->vaciados++;
                break;
            case 3:
                espera(c, pa_stream_update_timing_info(c->s, ok_s, c));
                if (pa_stream_get_timing_info(c->s))
                    c->tiempos++;
                break;
            case 4: {
                pa_cvolume v;
                pa_cvolume_set(&v, 2, (pa_volume_t)((r >> 8) % 65537));
                espera(c, pa_context_set_sink_input_volume(c->c, pa_stream_get_index(c->s), &v, ok_c, c));
                c->volumenes++;
                break;
            }
            default:
                espera(c, pa_context_get_sink_input_info(c->c, pa_stream_get_index(c->s), info_cb, c));
                c->infos++;
                break;
            }
        }
        pa_threaded_mainloop_unlock(c->m);
        duerme_us(1000 + (long)((r >> 4) % 6000));
        /* casi siempre destapado, para que suene */
        if (que == 0) {
            pa_threaded_mainloop_lock(c->m);
            if (pa_stream_get_state(c->s) == PA_STREAM_READY)
                pa_operation_unref(pa_stream_cork(c->s, 0, NULL, NULL));
            pa_threaded_mainloop_unlock(c->m);
        }
    }
    return NULL;
}

/* ---- el lector (como rocola-xp), en un hilo, con su propio mapa ---- */
typedef struct {
    const char *ruta;
    int fin;
    int lento;        /* copiar en dos mitades con una pausa */
    int ausencias;    /* quitar lector_presente a ratos */
    int rehace;       /* rehacer el anillo por renombre a ratos */
    uint64_t cuadros, bajadas, malos, reaperturas, rehechos, ultimo;
    int64_t ultimo_k;
    char primer[200];
} lector_t;

static int rehace_desde_fuera(const char *ruta)
{
    char t[800];
    snprintf(t, sizeof t, "%s.t.prueba", ruta);
    int fd = open(t, O_RDWR | O_CREAT | O_TRUNC | O_CLOEXEC, 0644);
    if (fd < 0)
        return -1;
    anillo_cabecera h;
    memset(&h, 0, sizeof h);
    h.magia = ANILLO_MAGIA;
    h.version = ANILLO_VERSION;
    h.capacidad = ANILLO_CAPACIDAD;
    h.canales = ANILLO_CANALES;
    h.tapado = 1;
    int ok = ftruncate(fd, (off_t)ANILLO_TAMANO) == 0 && pwrite(fd, &h, sizeof h, 0) == (ssize_t)sizeof h;
    close(fd);
    return ok && rename(t, ruta) == 0 ? 0 : -1;
}

static void *lector(void *x)
{
    lector_t *L = x;
    anillo a;
    if (anillo_abre(&a, L->ruta) != 0)
        return NULL;
    anillo_cabecera *h = a.cab;
    float *copia = malloc(ANILLO_CAPACIDAD * 2 * sizeof(float));
    A_GUARDA32(&h->latencia_lector_us, 20000);
    A_GUARDA64(&h->latido_lector_ns, anillo_ahora_ns());
    A_GUARDA32(&h->lector_presente, 1);
    uint32_t gv = A_CARGA32(&h->gen_vaciado);
    /* al abrir: lo vaciado antes ya no cuenta */
    {
        uint64_t l = A_CARGA64(&h->leido), v = A_CARGA64(&h->vaciado_hasta);
        if (v > l)
            A_GUARDA64(&h->leido, v);
    }
    L->ultimo_k = -1;
    double resto = 0;
    unsigned vuelta = 0, r = 777;
    double prox_inodo = ahora_s() + 1.0;
    while (!__atomic_load_n(&L->fin, __ATOMIC_ACQUIRE)) {
        duerme_us(5000);
        vuelta++;
        r = r * 1103515245u + 12345u;
        double t = ahora_s();
        if (L->rehace && vuelta % 140 == 0 && rehace_desde_fuera(L->ruta) == 0)
            L->rehechos++;
        if (t >= prox_inodo) {
            prox_inodo = t + 1.0;
            if (anillo_cambio(&a)) {
                A_GUARDA32(&h->lector_presente, 0);
                anillo_cierra(&a);
                if (anillo_abre(&a, L->ruta) != 0)
                    break;
                h = a.cab;
                L->reaperturas++;
                A_GUARDA32(&h->latencia_lector_us, 20000);
                A_GUARDA64(&h->latido_lector_ns, anillo_ahora_ns());
                A_GUARDA32(&h->lector_presente, 1);
                gv = A_CARGA32(&h->gen_vaciado);
                uint64_t l = A_CARGA64(&h->leido), v = A_CARGA64(&h->vaciado_hasta);
                if (v > l)
                    A_GUARDA64(&h->leido, v);
            }
        }
        /* ausencias: 300 ms sin lector cada ~2 s */
        int ausente = L->ausencias && (vuelta % 400) >= 340;
        A_GUARDA32(&h->lector_presente, ausente ? 0 : 1);
        A_GUARDA64(&h->latido_lector_ns, anillo_ahora_ns());
        uint32_t g = A_CARGA32(&h->gen_vaciado);
        if (g != gv) {
            gv = g;
            uint64_t l = A_CARGA64(&h->leido), v = A_CARGA64(&h->vaciado_hasta);
            if (v > l)
                A_GUARDA64(&h->leido, v);
        }
        uint32_t frec = A_CARGA32(&h->frecuencia);
        if (ausente || frec == 0 || A_CARGA32(&h->tapado))
            continue;
        resto += frec * 0.005;
        uint64_t quiere = (uint64_t)resto;
        resto -= (double)quiere;
        uint64_t e = A_CARGA64(&h->escrito);
        uint64_t l = A_CARGA64(&h->leido), v = A_CARGA64(&h->vaciado_hasta);
        uint64_t b = l > v ? l : v;
        uint64_t pend = e > b ? e - b : 0;
        if (pend > ANILLO_CAPACIDAD)
            pend = ANILLO_CAPACIDAD;
        uint64_t toma = pend < quiere ? pend : quiere;
        int despacio = L->lento && (r >> 20) % 3 == 0;
        for (uint64_t i = 0; i < toma; i++) {
            if (despacio && i == toma / 2)
                duerme_us(2000); /* a media copia: aqui entra el vaciado */
            size_t pos = (size_t)((b + i) % ANILLO_CAPACIDAD);
            copia[2 * i] = a.datos[pos * 2];
            copia[2 * i + 1] = a.datos[pos * 2 + 1];
        }
        for (uint64_t i = 0; i < toma; i++) {
            float lv = copia[2 * i], rv = copia[2 * i + 1];
            if (lv != -rv || lv != (float)(int64_t)lv) {
                if (!L->primer[0])
                    snprintf(L->primer, sizeof L->primer, "cuadro raro L=%g R=%g", lv, rv);
                L->malos++;
                continue;
            }
            int64_t k = (int64_t)lv;
            if (k <= L->ultimo_k) {
                if (!L->primer[0])
                    snprintf(L->primer, sizeof L->primer, "el contador bajo de %lld a %lld", (long long)L->ultimo_k, (long long)k);
                L->bajadas++;
            }
            L->ultimo_k = k;
        }
        L->cuadros += toma;
        A_GUARDA64(&h->leido, b + toma);
        if (toma < quiere)
            __atomic_add_fetch(&h->subdesbordes, 1, __ATOMIC_RELEASE);
    }
    A_GUARDA32(&h->lector_presente, 0);
    anillo_cierra(&a);
    free(copia);
    return NULL;
}

/* tlength de 2 s (se acota a la capacidad): el anillo va casi lleno y un
 * vaciado cae mientras el lector copia lo de antes */
static void caso_carrera(int segundos, int rehace)
{
    ruta_caso(rehace ? "carrera-rehace" : "carrera");
    tlength_us = 2000000;
    lector_t L;
    memset(&L, 0, sizeof L);
    L.ruta = anillo_ruta();
    L.lento = 1;
    L.ausencias = 1;
    L.rehace = rehace;
    pthread_t tl, te, tm;
    pthread_create(&tl, NULL, lector, &L);
    duerme_ms(50);
    cli_t c;
    int ok = cli_abre(&c, 1, 0);
    COMPRUEBA(ok, "carrera: el flujo llega a READY");
    if (!ok) {
        __atomic_store_n(&L.fin, 1, __ATOMIC_RELEASE);
        pthread_join(tl, NULL);
        return;
    }
    pthread_create(&te, NULL, escritor, &c);
    pthread_create(&tm, NULL, mandos, &c);
    double fin = ahora_s() + segundos;
    while (ahora_s() < fin)
        duerme_ms(50);
    pa_threaded_mainloop_lock(c.m);
    __atomic_store_n(&c.fin, 1, __ATOMIC_RELEASE);
    pa_threaded_mainloop_unlock(c.m);
    pthread_join(te, NULL);
    pthread_join(tm, NULL);
    pa_threaded_mainloop_lock(c.m);
    pa_stream_state_t st = pa_stream_get_state(c.s);
    pa_threaded_mainloop_unlock(c.m);
    cli_cierra(&c, 1);
    __atomic_store_n(&L.fin, 1, __ATOMIC_RELEASE);
    pthread_join(tl, NULL);
    COMPRUEBA(st == PA_STREAM_READY, "carrera: el flujo sigue READY (%d)", st);
    COMPRUEBA(L.cuadros > 20000, "carrera: el lector recibio %llu cuadros", (unsigned long long)L.cuadros);
    COMPRUEBA(L.bajadas == 0 && L.malos == 0, "carrera: %llu bajadas del contador y %llu cuadros raros (%s)",
              (unsigned long long)L.bajadas, (unsigned long long)L.malos, L.primer);
    COMPRUEBA(c.vaciados > 20 && c.tiempos > 20 && c.volumenes > 20 && c.infos > 20, "carrera: mandos %u/%u/%u/%u",
              c.vaciados, c.tiempos, c.volumenes, c.infos);
    COMPRUEBA(!rehace || (L.rehechos > 0 && L.reaperturas > 0), "carrera: %llu rehechos, %llu reaperturas",
              (unsigned long long)L.rehechos, (unsigned long long)L.reaperturas);
    printf("  carrera%s (%d s): %u trozos escritos, %u vaciados, %u tiempos, %u volumenes, %u infos, %d avisos; "
           "lector %llu cuadros, %llu bajadas, %llu raros, %llu rehechos, %llu reaperturas\n",
           rehace ? " con el anillo rehecho" : "", segundos, c.escritos, c.vaciados, c.tiempos, c.volumenes, c.infos,
           c.eventos, (unsigned long long)L.cuadros, (unsigned long long)L.bajadas, (unsigned long long)L.malos,
           (unsigned long long)L.rehechos, (unsigned long long)L.reaperturas);
    tlength_us = 100000;
}

/* ---- ciclos: descriptores e hilos ---- */
static int cuenta_dir(const char *d)
{
    DIR *x = opendir(d);
    if (!x)
        return -1;
    int n = 0;
    struct dirent *e;
    while ((e = readdir(x)))
        if (e->d_name[0] != '.')
            n++;
    closedir(x);
    return n;
}

static void caso_ciclos(int vueltas)
{
    ruta_caso("ciclos");
    lector_t L;
    memset(&L, 0, sizeof L);
    L.ruta = anillo_ruta();
    pthread_t tl;
    pthread_create(&tl, NULL, lector, &L);
    duerme_ms(50);
    /* una vuelta antes de contar (lo que se abre una sola vez: la traza) */
    cli_t c;
    cli_abre(&c, 1, 0);
    cli_cierra(&c, 1);
    int fd0 = cuenta_dir("/proc/self/fd"), h0 = cuenta_dir("/proc/self/task");
    int bien = 0;
    float buf[2 * 960];
    memset(buf, 0, sizeof buf);
    for (int i = 0; i < vueltas; i++) {
        int ok = cli_abre(&c, 1, i % 2);
        pa_threaded_mainloop_lock(c.m);
        if (ok && pa_stream_writable_size(c.s) != (size_t)-1)
            pa_stream_write(c.s, buf, sizeof buf, NULL, 0, PA_SEEK_RELATIVE);
        if (ok && i % 3 == 0)
            espera(&c, pa_stream_flush(c.s, ok_s, &c));
        pa_threaded_mainloop_unlock(c.m);
        cli_cierra(&c, i % 2);
        bien += ok;
    }
    int fd1 = cuenta_dir("/proc/self/fd"), h1 = cuenta_dir("/proc/self/task");
    __atomic_store_n(&L.fin, 1, __ATOMIC_RELEASE);
    pthread_join(tl, NULL);
    COMPRUEBA(bien == vueltas, "ciclos: %d de %d llegaron a READY", bien, vueltas);
    COMPRUEBA(fd1 == fd0 && h1 == h0, "ciclos: descriptores %d -> %d, hilos %d -> %d", fd0, fd1, h0, h1);
    printf("  ciclos: %d de %d vueltas (crear, conectar, escribir, vaciar, cerrar en los dos ordenes); descriptores %d -> %d, hilos %d -> %d\n",
           bien, vueltas, fd0, fd1, h0, h1);
}

/* ---- consumo del hilo del bucle ---- */
static int tid_del_bucle(void)
{
    DIR *x = opendir("/proc/self/task");
    if (!x)
        return -1;
    int tid = -1;
    struct dirent *e;
    while ((e = readdir(x))) {
        if (e->d_name[0] == '.')
            continue;
        char r[300], nom[64] = "";
        snprintf(r, sizeof r, "/proc/self/task/%s/comm", e->d_name);
        FILE *f = fopen(r, "r");
        if (f) {
            if (fgets(nom, sizeof nom, f))
                nom[strcspn(nom, "\n")] = 0;
            fclose(f);
        }
        if (!strcmp(nom, "threaded-ml"))
            tid = atoi(e->d_name);
    }
    closedir(x);
    return tid;
}

/* despertares (cambios de contexto voluntarios) y tiempo de procesador del hilo */
static void mide_hilo(int tid, unsigned long long *desp, double *cpu)
{
    char r[300], l[256];
    *desp = 0;
    *cpu = 0;
    snprintf(r, sizeof r, "/proc/self/task/%d/status", tid);
    FILE *f = fopen(r, "r");
    if (f) {
        while (fgets(l, sizeof l, f)) {
            unsigned long long v;
            /* solo los voluntarios: cada vez que se duerme en poll (los
             * otros son del planificador y dependen de la carga de la maquina) */
            if (sscanf(l, "voluntary_ctxt_switches: %llu", &v) == 1)
                *desp += v;
        }
        fclose(f);
    }
    /* tiempo en el procesador, en ns (schedstat) */
    snprintf(r, sizeof r, "/proc/self/task/%d/schedstat", tid);
    f = fopen(r, "r");
    if (f) {
        unsigned long long ns = 0;
        if (fscanf(f, "%llu", &ns) == 1)
            *cpu = (double)ns / 1e9;
        fclose(f);
    }
}

static void tramo(const char *que, double segundos, double max_desp, double *desp_s, double *cpu_pct)
{
    int tid = tid_del_bucle();
    unsigned long long d0, d1;
    double c0, c1;
    mide_hilo(tid, &d0, &c0);
    double t0 = ahora_s();
    duerme_ms((int)(segundos * 1000));
    double t = ahora_s() - t0;
    mide_hilo(tid, &d1, &c1);
    *desp_s = (double)(d1 - d0) / t;
    *cpu_pct = 100.0 * (c1 - c0) / t;
    COMPRUEBA(tid > 0 && *desp_s <= max_desp, "consumo %s: %.1f despertares/s (tope %.0f)", que, *desp_s, max_desp);
}

static void caso_consumo(void)
{
    ruta_caso("consumo");
    double d[4], p[4];
    cli_t c;
    /* 1: contexto listo, sin flujo */
    cli_abre(&c, 0, 0);
    duerme_ms(100);
    tramo("sin flujo", 2.0, 2, &d[0], &p[0]);
    cli_cierra(&c, 0);
    /* 2: flujo tapado (Soloist en pausa), con lector */
    lector_t L;
    memset(&L, 0, sizeof L);
    L.ruta = anillo_ruta();
    pthread_t tl;
    pthread_create(&tl, NULL, lector, &L);
    duerme_ms(50);
    cli_abre(&c, 1, 1);
    duerme_ms(100);
    tramo("tapado", 2.0, 8, &d[1], &p[1]);
    /* 3: destapado sin lector */
    __atomic_store_n(&L.fin, 1, __ATOMIC_RELEASE);
    pthread_join(tl, NULL);
    pa_threaded_mainloop_lock(c.m);
    pa_operation_unref(pa_stream_cork(c.s, 0, NULL, NULL));
    pa_threaded_mainloop_unlock(c.m);
    duerme_ms(2300); /* que el latido del lector caduque */
    tramo("sin lector", 2.0, 15, &d[2], &p[2]);
    /* 4: sonando: lector y escritor */
    memset(&L, 0, sizeof L);
    L.ruta = anillo_ruta();
    pthread_create(&tl, NULL, lector, &L);
    pthread_t te;
    pthread_create(&te, NULL, escritor, &c);
    duerme_ms(300);
    tramo("sonando", 3.0, 130, &d[3], &p[3]);
    pa_threaded_mainloop_lock(c.m);
    __atomic_store_n(&c.fin, 1, __ATOMIC_RELEASE);
    pa_threaded_mainloop_unlock(c.m);
    pthread_join(te, NULL);
    cli_cierra(&c, 1);
    __atomic_store_n(&L.fin, 1, __ATOMIC_RELEASE);
    pthread_join(tl, NULL);
    COMPRUEBA(L.cuadros > 100000 && L.bajadas == 0, "consumo: sonando, el lector recibio %llu cuadros (%llu bajadas)",
              (unsigned long long)L.cuadros, (unsigned long long)L.bajadas);
    printf("  consumo del hilo del bucle: sin flujo %.1f despertares/s y %.2f %% de un nucleo; tapado %.1f/s %.2f %%; "
           "sin lector %.1f/s %.2f %%; sonando %.1f/s %.2f %%\n",
           d[0], p[0], d[1], p[1], d[2], p[2], d[3], p[3]);
}

/* ---- la cabecera estropeada y el latido del futuro ---- */
static size_t escribible_con_lock(cli_t *c)
{
    pa_threaded_mainloop_lock(c->m);
    size_t w = pa_stream_writable_size(c->s);
    pa_threaded_mainloop_unlock(c->m);
    return w;
}

static void caso_cabecera(void)
{
    ruta_caso("cabecera");
    const char *ruta = anillo_ruta();
    cli_t c;
    int ok = cli_abre(&c, 1, 0);
    COMPRUEBA(ok, "cabecera: el flujo llega a READY");
    if (!ok)
        return;
    /* un lector a mano: presente, con el latido 10 s POR DELANTE (basura) */
    anillo a;
    COMPRUEBA(anillo_abre(&a, ruta) == 0, "cabecera: el lector abre");
    A_GUARDA64(&a.cab->latido_lector_ns, anillo_ahora_ns() + 10000000000ull);
    A_GUARDA32(&a.cab->lector_presente, 1);
    duerme_ms(50);
    size_t w_futuro = escribible_con_lock(&c);
    A_GUARDA64(&a.cab->latido_lector_ns, anillo_ahora_ns() + 500000000ull); /* medio segundo: vale */
    duerme_ms(50);
    size_t w_cerca = escribible_con_lock(&c);
    COMPRUEBA(w_futuro == 0 && w_cerca > 0, "cabecera: latido 10 s por delante: writable %zu (debe ser 0); 0,5 s: %zu",
              w_futuro, w_cerca);
    /* alguien pisa la cabecera sin cambiar el tamano (la magia a 0): el
     * escritor tiene que rehacerlo por renombre (otro inodo) en ~1 s */
    struct stat st0, st1;
    stat(ruta, &st0);
    int fd = open(ruta, O_WRONLY | O_CLOEXEC);
    uint32_t cero = 0;
    COMPRUEBA(fd >= 0 && pwrite(fd, &cero, 4, 0) == 4, "cabecera: se pisa la magia");
    if (fd >= 0)
        close(fd);
    double t0 = ahora_s(), t = 0;
    int rehecho = 0;
    while ((t = ahora_s() - t0) < 3.0) {
        duerme_ms(20);
        if (stat(ruta, &st1) == 0 && st1.st_ino != st0.st_ino) {
            rehecho = 1;
            break;
        }
    }
    anillo b;
    int valido = anillo_abre(&b, ruta) == 0 && b.cab->magia == ANILLO_MAGIA && A_CARGA32(&b.cab->frecuencia) == 48000 &&
                 A_CARGA32(&b.cab->pid_escritor) == (uint32_t)getpid();
    COMPRUEBA(rehecho && valido, "cabecera: la magia pisada se rehace (rehecho %d en %.2f s, nuevo valido con el flujo %d)",
              rehecho, t, valido);
    printf("  cabecera: latido del lector 10 s por delante -> writable %zu (0,5 s -> %zu); magia pisada -> rehecho en %.2f s por renombre%s\n",
           w_futuro, w_cerca, t, valido ? ", con el flujo publicado" : "");
    if (b.cab)
        anillo_cierra(&b);
    anillo_cierra(&a);
    cli_cierra(&c, 1);
}

int main(int argc, char **argv)
{
    (void)argc;
    snprintf(base, sizeof base, "%s", getenv("PRUEBA_DIR") ? getenv("PRUEBA_DIR") : "/tmp");
    const char *solo = argc > 1 ? argv[1] : NULL;
    int corto = getenv("HILOS_CORTO") != NULL; /* con los sanitizadores: menos vueltas */
    alarm(corto ? 300 : 120); /* un bloqueo mutuo no se queda colgado para siempre */
#define CASO(n, f) if (!solo || !strcmp(solo, n)) f
    CASO("carrera", caso_carrera)(corto ? 4 : 6, 0);
    CASO("rehace", caso_carrera)(corto ? 4 : 6, 1);
    CASO("ciclos", caso_ciclos)(corto ? 50 : 200);
    CASO("cabecera", caso_cabecera)();
    if (!corto)
        CASO("consumo", caso_consumo)();
    printf("hilos: comprobaciones %d bien, %d mal\n", bien_n, mal_n);
    return mal_n ? 1 : 0;
}
