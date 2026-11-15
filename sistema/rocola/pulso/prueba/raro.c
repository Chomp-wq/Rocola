/* SPDX-License-Identifier: MIT
 * Prueba (2) de rocola-pulso: lo raro. Todos los formatos, sin lector, lector
 * que llega tarde y se muere, anillo borrado y rehecho, dos flujos seguidos y
 * a la vez, vaciado en medio, cliente que muere, anillo imposible, argumentos
 * NULL, el bucle sin hilo (api completo) y el cerrojo recursivo.
 * Enlaza directo con la libpulse.so.0 de su carpeta (rpath $ORIGIN).
 *   raro [caso]   (sin caso: todos) */
#include "comun.h"
#include "senal.h"
#include "../pulso_api.h"
#include "../anillo.h"

#include <fcntl.h>
#include <pthread.h>
#include <sys/stat.h>

static char base[512];
static int n_caso;

static void ruta_caso(const char *nombre)
{
    char r[700];
    snprintf(r, sizeof r, "%s/%02d-%s/run/rocola-xp/spotify/sonido.anillo", base, ++n_caso, nombre);
    setenv("ROCOLA_SPOTIFY_ANILLO", r, 1);
}

static const char *rep(void)
{
    static char r[800];
    snprintf(r, sizeof r, "%s.rep", anillo_ruta());
    return r;
}

/* ---- un cliente sencillo con bucle de hilo ---- */
typedef struct {
    pa_threaded_mainloop *m;
    pa_context *c;
    pa_stream *s;
    pa_sample_spec ss;
    size_t tc;
    int ci, cd;
    uint64_t k;
    int empezo, subdes;
} cli_t;

static void sig_ctx(pa_context *c, void *u) { (void)c; pa_threaded_mainloop_signal(((cli_t *)u)->m, 0); }
static void sig_s(pa_stream *s, void *u) { (void)s; pa_threaded_mainloop_signal(((cli_t *)u)->m, 0); }
static void ok_s(pa_stream *s, int ok, void *u) { (void)s; (void)ok; pa_threaded_mainloop_signal(((cli_t *)u)->m, 0); }
static void empezo(pa_stream *s, void *u) { (void)s; ((cli_t *)u)->empezo++; pa_threaded_mainloop_signal(((cli_t *)u)->m, 0); }
static void subdes(pa_stream *s, void *u) { (void)s; ((cli_t *)u)->subdes++; pa_threaded_mainloop_signal(((cli_t *)u)->m, 0); }

static void espera(cli_t *c, pa_operation *o)
{
    if (!o)
        return;
    while (pa_operation_get_state(o) == PA_OPERATION_RUNNING)
        pa_threaded_mainloop_wait(c->m);
    pa_operation_unref(o);
}

static int cli_abre(cli_t *c)
{
    memset(c, 0, sizeof *c);
    c->k = 5000;
    c->m = pa_threaded_mainloop_new();
    pa_threaded_mainloop_start(c->m);
    pa_threaded_mainloop_lock(c->m);
    c->c = pa_context_new(pa_threaded_mainloop_get_api(c->m), "raro");
    pa_context_set_state_callback(c->c, sig_ctx, c);
    pa_context_connect(c->c, NULL, PA_CONTEXT_NOAUTOSPAWN, NULL);
    pa_context_state_t st;
    while ((st = pa_context_get_state(c->c)) != PA_CONTEXT_READY && st != PA_CONTEXT_FAILED && st != PA_CONTEXT_TERMINATED)
        pa_threaded_mainloop_wait(c->m);
    pa_threaded_mainloop_unlock(c->m);
    return st == PA_CONTEXT_READY;
}

/* con lock: crea y conecta un flujo (destapado) */
static pa_stream *cli_flujo(cli_t *c, pa_sample_format_t f, int canales, uint32_t rate, const pa_channel_map *mapa)
{
    c->ss.format = f;
    c->ss.rate = rate;
    c->ss.channels = (uint8_t)canales;
    c->tc = senal_tam(f) * (size_t)canales;
    pa_stream *s = pa_stream_new(c->c, "raro", &c->ss, mapa);
    if (!s)
        return NULL;
    pa_stream_set_state_callback(s, sig_s, c);
    pa_stream_set_started_callback(s, empezo, c);
    pa_stream_set_underflow_callback(s, subdes, c);
    pa_stream_connect_playback(s, NULL, NULL, PA_STREAM_NOFLAGS, NULL, NULL);
    while (pa_stream_get_state(s) == PA_STREAM_CREATING)
        pa_threaded_mainloop_wait(c->m);
    c->s = s;
    return s;
}

/* con lock: escribe n cuadros de la senal mirando writable_size (tope en s) */
static uint64_t cli_escribe(cli_t *c, uint64_t n, double tope)
{
    uint8_t *buf = malloc(4096 * 64);
    uint64_t hecho = 0;
    double fin = ahora_s() + tope;
    while (hecho < n && ahora_s() < fin && pa_stream_get_state(c->s) == PA_STREAM_READY) {
        size_t w = pa_stream_writable_size(c->s);
        if (w == 0 || w == (size_t)-1) {
            pa_threaded_mainloop_unlock(c->m);
            duerme_ms(3);
            pa_threaded_mainloop_lock(c->m);
            continue;
        }
        size_t k = w / c->tc;
        if (k > 4096)
            k = 4096;
        if (k > n - hecho)
            k = (size_t)(n - hecho);
        senal_llena(buf, c->k, k, c->ss.format, c->ss.channels, c->ci, c->cd);
        if (pa_stream_write(c->s, buf, k * c->tc, NULL, 0, PA_SEEK_RELATIVE) == 0) {
            c->k += k;
            hecho += k;
        }
    }
    free(buf);
    return hecho;
}

static const pa_timing_info *cli_tiempos(cli_t *c)
{
    espera(c, pa_stream_update_timing_info(c->s, ok_s, c));
    return pa_stream_get_timing_info(c->s);
}

static int cli_drena(cli_t *c, double tope)
{
    double fin = ahora_s() + tope;
    while (ahora_s() < fin) {
        const pa_timing_info *t = cli_tiempos(c);
        if (t && t->write_index == t->read_index)
            return 1;
        pa_threaded_mainloop_unlock(c->m);
        duerme_ms(10);
        pa_threaded_mainloop_lock(c->m);
    }
    return 0;
}

static void cli_cierra(cli_t *c)
{
    pa_threaded_mainloop_lock(c->m);
    if (c->s) {
        pa_stream_disconnect(c->s);
        pa_stream_unref(c->s);
    }
    pa_context_disconnect(c->c);
    pa_context_unref(c->c);
    pa_threaded_mainloop_unlock(c->m);
    pa_threaded_mainloop_stop(c->m);
    pa_threaded_mainloop_free(c->m);
}

/* ---- casos ---- */
static void caso_formatos(void)
{
    pa_sample_format_t fs[] = {PA_SAMPLE_U8, PA_SAMPLE_S16LE, PA_SAMPLE_S16BE, PA_SAMPLE_FLOAT32LE, PA_SAMPLE_FLOAT32BE,
                               PA_SAMPLE_S32LE, PA_SAMPLE_S32BE, PA_SAMPLE_S24LE, PA_SAMPLE_S24BE, PA_SAMPLE_S24_32LE,
                               PA_SAMPLE_S24_32BE};
    int bien = 0;
    for (unsigned i = 0; i < sizeof fs / sizeof fs[0]; i++) {
        ruta_caso("formato");
        pid_t l = lanza_lector(rep(), senal_bits(fs[i]), 0, NULL);
        cli_t c;
        cli_abre(&c);
        pa_threaded_mainloop_lock(c.m);
        c.ci = 0;
        c.cd = 1;
        cli_flujo(&c, fs[i], 2, 48000, NULL);
        uint64_t n = cli_escribe(&c, 9600, 3.0);
        int dr = cli_drena(&c, 3.0);
        const pa_timing_info *t = cli_tiempos(&c);
        uint64_t cons = t ? (uint64_t)t->read_index / c.tc : 0;
        pa_threaded_mainloop_unlock(c.m);
        cli_cierra(&c);
        reporte_t R;
        para_lector(l, rep(), &R);
        int ok = n == 9600 && dr && R.errores == 0 && R.cuadros == cons && cons == 9600;
        COMPRUEBA(ok, "formato %s: escritos %llu, lector %llu cuadros %llu errores (%s)", pa_sample_format_to_string(fs[i]),
                  (unsigned long long)n, R.cuadros, R.errores, R.primer_error);
        bien += ok;
    }
    printf("  formatos: %d de 11 exactos cuadro a cuadro (u8, s16, float32, s32, s24, s24-32; LE y BE)\n", bien);

    /* aLaw/uLaw no; mapa de 6 con FL en 2 y FR en 5; mono con mapa MONO */
    ruta_caso("mapas");
    cli_t c;
    cli_abre(&c);
    pa_threaded_mainloop_lock(c.m);
    pa_sample_spec al = {PA_SAMPLE_ALAW, 8000, 1};
    pa_stream *x = pa_stream_new(c.c, "alaw", &al, NULL);
    COMPRUEBA(x == NULL && pa_context_errno(c.c) == PA_ERR_NOTSUPPORTED, "alaw no se acepta (NOTSUPPORTED)");
    pa_threaded_mainloop_unlock(c.m);
    cli_cierra(&c);

    pa_channel_map m6;
    pa_channel_map_init(&m6);
    m6.channels = 6;
    pa_channel_position_t pos[6] = {PA_CHANNEL_POSITION_FRONT_CENTER, PA_CHANNEL_POSITION_LFE, PA_CHANNEL_POSITION_FRONT_LEFT,
                                    PA_CHANNEL_POSITION_REAR_LEFT, PA_CHANNEL_POSITION_REAR_RIGHT, PA_CHANNEL_POSITION_FRONT_RIGHT};
    memcpy(m6.map, pos, sizeof pos);
    struct { int canales; const pa_channel_map *m; int ci, cd; const char *n; } mp[2] = {{6, &m6, 2, 5, "6 canales con FL en 2 y FR en 5"}, {1, NULL, 0, 0, "mono"}};
    pa_channel_map mono;
    pa_channel_map_init_mono(&mono);
    mp[1].m = &mono;
    for (int i = 0; i < 2; i++) {
        ruta_caso("mapa");
        pid_t l = lanza_lector(rep(), 16, mp[i].canales == 1, NULL);
        cli_abre(&c);
        pa_threaded_mainloop_lock(c.m);
        c.ci = mp[i].ci;
        c.cd = mp[i].cd;
        cli_flujo(&c, PA_SAMPLE_S16LE, mp[i].canales, 44100, mp[i].m);
        cli_escribe(&c, 8820, 3.0);
        cli_drena(&c, 3.0);
        pa_threaded_mainloop_unlock(c.m);
        cli_cierra(&c);
        reporte_t R;
        para_lector(l, rep(), &R);
        COMPRUEBA(R.errores == 0 && R.cuadros == 8820, "%s: %llu cuadros, %llu errores (%s)", mp[i].n, R.cuadros, R.errores, R.primer_error);
    }
}

static void caso_sin_lector_y_tarde(void)
{
    ruta_caso("sin-lector");
    cli_t c;
    cli_abre(&c);
    pa_threaded_mainloop_lock(c.m);
    c.ci = 0;
    c.cd = 1;
    cli_flujo(&c, PA_SAMPLE_FLOAT32LE, 2, 48000, NULL);
    int ceros = 0, vueltas = 0;
    for (double fin = ahora_s() + 0.3; ahora_s() < fin; vueltas++) {
        ceros += pa_stream_writable_size(c.s) == 0;
        pa_threaded_mainloop_unlock(c.m);
        duerme_ms(10);
        pa_threaded_mainloop_lock(c.m);
    }
    COMPRUEBA(ceros == vueltas, "sin lector: writable_size 0 (%d de %d)", ceros, vueltas);
    COMPRUEBA(c.empezo == 0, "sin lector: sin started");
    /* escribir sin mirar: se acepta (cola local) y no se pierde */
    uint8_t buf[4800 * 8];
    senal_llena(buf, c.k, 4800, PA_SAMPLE_FLOAT32LE, 2, 0, 1);
    COMPRUEBA(pa_stream_write(c.s, buf, sizeof buf, NULL, 0, PA_SEEK_RELATIVE) == 0, "escribir sin lector se acepta");
    c.k += 4800;
    pa_threaded_mainloop_unlock(c.m);

    /* el lector llega tarde */
    pid_t l = lanza_lector(rep(), 16, 0, NULL);
    pa_threaded_mainloop_lock(c.m);
    uint64_t n = cli_escribe(&c, 24000, 3.0);
    COMPRUEBA(n == 24000, "con lector tardio se escribe (%llu)", (unsigned long long)n);
    COMPRUEBA(c.empezo >= 1, "started al llegar el lector");
    COMPRUEBA(cli_drena(&c, 3.0), "drena");
    pa_threaded_mainloop_unlock(c.m);
    /* y se muere sin avisar */
    kill(l, SIGKILL);
    waitpid(l, NULL, 0);
    double t0 = ahora_s(), cuando = -1;
    pa_threaded_mainloop_lock(c.m);
    while (ahora_s() - t0 < 4.0) {
        if (pa_stream_writable_size(c.s) == 0) {
            cuando = ahora_s() - t0;
            break;
        }
        pa_threaded_mainloop_unlock(c.m);
        duerme_ms(20);
        pa_threaded_mainloop_lock(c.m);
    }
    COMPRUEBA(cuando >= 1.5 && cuando <= 2.6, "lector muerto: writable 0 a los %.2f s (latido de mas de 2 s)", cuando);
    int sub_antes = c.subdes;
    pa_threaded_mainloop_unlock(c.m);
    duerme_ms(200);
    pa_threaded_mainloop_lock(c.m);
    COMPRUEBA(c.subdes == sub_antes, "sin lector no se inventan underflows");
    pa_threaded_mainloop_unlock(c.m);
    /* otro lector: sigue */
    char r2[900];
    snprintf(r2, sizeof r2, "%s.2", rep());
    l = lanza_lector(r2, 16, 0, NULL);
    pa_threaded_mainloop_lock(c.m);
    espera(&c, pa_stream_cork(c.s, 0, ok_s, &c)); /* por si el underflow lo tapo (no lo hace este cliente) */
    n = cli_escribe(&c, 9600, 3.0);
    COMPRUEBA(n == 9600 && cli_drena(&c, 3.0), "otro lector: sigue el flujo");
    pa_threaded_mainloop_unlock(c.m);
    cli_cierra(&c);
    reporte_t R;
    para_lector(l, r2, &R);
    COMPRUEBA(R.errores == 0 && R.cuadros == 9600, "segundo lector: %llu cuadros, %llu errores", R.cuadros, R.errores);
    printf("  sin lector, lector tardio y lector muerto: writable 0 a los %.2f s de morir\n", cuando);
}

static void caso_anillo_rehecho(void)
{
    ruta_caso("rehecho");
    pid_t l = lanza_lector(rep(), 16, 0, NULL);
    cli_t c;
    cli_abre(&c);
    pa_threaded_mainloop_lock(c.m);
    c.cd = 1;
    cli_flujo(&c, PA_SAMPLE_S16LE, 2, 48000, NULL);
    cli_escribe(&c, 9600, 3.0);
    pa_threaded_mainloop_unlock(c.m);
    /* 1) se borra */
    COMPRUEBA(unlink(anillo_ruta()) == 0, "borrar el anillo");
    pa_threaded_mainloop_lock(c.m);
    uint64_t n = cli_escribe(&c, 48000 * 3, 6.0);
    COMPRUEBA(n == 48000 * 3, "tras borrarlo se siguen escribiendo 3 s (%llu)", (unsigned long long)n);
    struct stat st;
    COMPRUEBA(stat(anillo_ruta(), &st) == 0 && (size_t)st.st_size == ANILLO_TAMANO, "el anillo se rehizo");
    COMPRUEBA(cli_drena(&c, 3.0), "drena en el anillo nuevo");
    pa_threaded_mainloop_unlock(c.m);
    /* 2) alguien deja uno invalido (por renombre, como manda I1) */
    char t[800];
    snprintf(t, sizeof t, "%s.t.basura", anillo_ruta());
    int fd = open(t, O_WRONLY | O_CREAT | O_TRUNC, 0644);
    COMPRUEBA(fd >= 0 && write(fd, "basura", 6) == 6, "escribir basura");
    close(fd);
    rename(t, anillo_ruta());
    pa_threaded_mainloop_lock(c.m);
    n = cli_escribe(&c, 48000 * 3, 6.0);
    COMPRUEBA(n == 48000 * 3, "tras la basura se siguen escribiendo 3 s (%llu)", (unsigned long long)n);
    COMPRUEBA(stat(anillo_ruta(), &st) == 0 && (size_t)st.st_size == ANILLO_TAMANO, "el anillo invalido se rehizo");
    COMPRUEBA(cli_drena(&c, 3.0), "drena");
    COMPRUEBA(pa_stream_get_state(c.s) == PA_STREAM_READY, "el flujo sigue READY");
    pa_threaded_mainloop_unlock(c.m);
    cli_cierra(&c);
    reporte_t R;
    para_lector(l, rep(), &R);
    COMPRUEBA(R.errores == 0, "lector: %llu errores (%s)", R.errores, R.primer_error);
    COMPRUEBA(R.reaperturas >= 2, "el lector reabrio %llu veces", R.reaperturas);
    COMPRUEBA(R.cuadros >= 48000 * 5, "el lector siguio recibiendo (%llu cuadros)", R.cuadros);
    printf("  anillo borrado y anillo invalido: rehechos; el lector reabrio %llu veces y recibio %llu cuadros sin errores\n",
           R.reaperturas, R.cuadros);
}

static void caso_dos_flujos(void)
{
    ruta_caso("dos-seguidos");
    pid_t l = lanza_lector(rep(), 16, 0, NULL);
    cli_t c;
    cli_abre(&c);
    pa_threaded_mainloop_lock(c.m);
    c.cd = 1;
    cli_flujo(&c, PA_SAMPLE_S16LE, 2, 44100, NULL);
    cli_escribe(&c, 8820, 3.0);
    cli_drena(&c, 3.0);
    pa_stream_disconnect(c.s);
    pa_stream_unref(c.s);
    cli_flujo(&c, PA_SAMPLE_FLOAT32LE, 2, 48000, NULL);
    cli_escribe(&c, 9600, 3.0);
    cli_drena(&c, 3.0);
    pa_threaded_mainloop_unlock(c.m);
    cli_cierra(&c);
    reporte_t R;
    para_lector(l, rep(), &R);
    COMPRUEBA(R.errores == 0 && R.cuadros == 8820 + 9600 && R.flujos == 2 && !strcmp(R.frecuencias, "44100,48000"),
              "dos flujos seguidos: %llu cuadros, %llu flujos, frecuencias %s, %llu errores", R.cuadros, R.flujos,
              R.frecuencias, R.errores);

    /* a la vez en el mismo proceso: el nuevo desplaza al viejo (FAILED, KILLED) */
    ruta_caso("dos-a-la-vez");
    l = lanza_lector(rep(), 16, 0, NULL);
    cli_abre(&c);
    pa_threaded_mainloop_lock(c.m);
    c.cd = 1;
    pa_stream *viejo = cli_flujo(&c, PA_SAMPLE_S16LE, 2, 48000, NULL);
    cli_escribe(&c, 4800, 3.0);
    pa_stream *nuevo = cli_flujo(&c, PA_SAMPLE_S16LE, 2, 48000, NULL);
    double fin = ahora_s() + 1.0;
    while (pa_stream_get_state(viejo) == PA_STREAM_READY && ahora_s() < fin) {
        pa_threaded_mainloop_unlock(c.m);
        duerme_ms(5);
        pa_threaded_mainloop_lock(c.m);
    }
    COMPRUEBA(pa_stream_get_state(viejo) == PA_STREAM_FAILED && pa_context_errno(c.c) == PA_ERR_KILLED,
              "el flujo viejo pasa a FAILED con KILLED (estado %d)", pa_stream_get_state(viejo));
    uint64_t n = cli_escribe(&c, 9600, 3.0);
    COMPRUEBA(n == 9600 && cli_drena(&c, 3.0), "el nuevo sigue");
    pa_stream_unref(viejo);
    (void)nuevo;
    pa_threaded_mainloop_unlock(c.m);
    cli_cierra(&c);
    para_lector(l, rep(), &R);
    COMPRUEBA(R.errores == 0, "dos a la vez: %llu errores (%s)", R.errores, R.primer_error);
    printf("  dos flujos seguidos (44100 y 48000 Hz) y dos a la vez (el viejo, FAILED/KILLED)\n");
}

static void caso_vaciado(void)
{
    ruta_caso("vaciado");
    pid_t l = lanza_lector(rep(), 16, 0, NULL);
    cli_t c;
    cli_abre(&c);
    pa_threaded_mainloop_lock(c.m);
    c.cd = 1;
    cli_flujo(&c, PA_SAMPLE_S16LE, 2, 48000, NULL);
    cli_escribe(&c, 48000, 3.0);
    const pa_timing_info *t = cli_tiempos(&c);
    int64_t pend = t ? t->write_index - t->read_index : -1;
    COMPRUEBA(pend > 0, "hay pendientes al vaciar (%lld bytes)", (long long)pend);
    espera(&c, pa_stream_flush(c.s, ok_s, &c));
    t = cli_tiempos(&c);
    COMPRUEBA(t && t->write_index == t->read_index, "tras vaciar, write_index == read_index");
    uint64_t r1 = t ? (uint64_t)t->read_index / c.tc : 0;
    cli_escribe(&c, 24000, 3.0);
    COMPRUEBA(cli_drena(&c, 3.0), "drena tras vaciar");
    t = cli_tiempos(&c);
    uint64_t total = t ? (uint64_t)t->read_index / c.tc : 0;
    COMPRUEBA(total == r1 + 24000, "lo de despues del vaciado llega entero (%llu)", (unsigned long long)total);
    pa_threaded_mainloop_unlock(c.m);
    cli_cierra(&c);
    reporte_t R;
    para_lector(l, rep(), &R);
    COMPRUEBA(R.errores == 0 && R.cuadros == total && R.saltos >= 2, "vaciado en medio: %llu cuadros, %llu saltos, %llu errores",
              R.cuadros, R.saltos, R.errores);
    printf("  vaciado en medio: %lld bytes fuera, %llu cuadros leidos, sin errores\n", (long long)pend, R.cuadros);
}

static void caso_cliente_muere(void)
{
    ruta_caso("muere");
    pid_t l = lanza_lector(rep(), 16, 0, NULL);
    pid_t hijo = fork();
    if (hijo == 0) {
        cli_t c;
        cli_abre(&c);
        pa_threaded_mainloop_lock(c.m);
        c.cd = 1;
        cli_flujo(&c, PA_SAMPLE_S16LE, 2, 48000, NULL);
        cli_escribe(&c, 48000 * 100, 100.0);
        _exit(0);
    }
    duerme_ms(500);
    kill(hijo, SIGKILL);
    waitpid(hijo, NULL, 0);
    duerme_ms(1500);
    anillo a;
    COMPRUEBA(anillo_abre(&a, anillo_ruta()) == 0, "abrir el anillo");
    uint64_t edad = anillo_ahora_ns() - A_CARGA64(&a.cab->latido_escritor_ns);
    COMPRUEBA(A_CARGA32(&a.cab->pid_escritor) == (uint32_t)hijo, "pid_escritor era el del hijo");
    COMPRUEBA(edad > 1000000000ull, "el latido del escritor muerto se para (%.2f s)", edad / 1e9);
    uint32_t gf = A_CARGA32(&a.cab->gen_flujo);
    cli_t c;
    cli_abre(&c);
    pa_threaded_mainloop_lock(c.m);
    c.cd = 1;
    cli_flujo(&c, PA_SAMPLE_S16LE, 2, 48000, NULL);
    COMPRUEBA(A_CARGA32(&a.cab->pid_escritor) == (uint32_t)getpid(), "el cliente nuevo toma el anillo");
    COMPRUEBA(A_CARGA32(&a.cab->gen_flujo) == gf + 1, "gen_flujo sube");
    uint64_t n = cli_escribe(&c, 9600, 3.0);
    COMPRUEBA(n == 9600 && cli_drena(&c, 3.0), "el cliente nuevo suena");
    pa_threaded_mainloop_unlock(c.m);
    cli_cierra(&c);
    anillo_cierra(&a);
    reporte_t R;
    para_lector(l, rep(), &R);
    COMPRUEBA(R.errores == 0, "cliente muerto: %llu errores (%s)", R.errores, R.primer_error);
    printf("  cliente que muere: latido parado a los %.2f s, el siguiente toma el anillo\n", edad / 1e9);
}

static void caso_anillo_imposible(void)
{
    setenv("ROCOLA_SPOTIFY_ANILLO", "/proc/no-se-puede/sonido.anillo", 1);
    cli_t c;
    int ok = cli_abre(&c);
    COMPRUEBA(!ok && pa_context_get_state(c.c) == PA_CONTEXT_FAILED && pa_context_errno(c.c) == PA_ERR_CONNECTIONREFUSED,
              "anillo imposible: FAILED con CONNECTIONREFUSED (estado %d, error %d)", pa_context_get_state(c.c), pa_context_errno(c.c));
    cli_cierra(&c);
    printf("  anillo imposible: el contexto falla (como sin servidor)\n");
}

static void nada_ctx(pa_context *c, void *u) { (void)c; (void)u; }

static void caso_nulos(void)
{
    int antes = bien_n;
    /* las 48, con NULL o basura: nada se cae y devuelven error */
    COMPRUEBA(pa_context_connect(NULL, NULL, 0, NULL) < 0, "connect(NULL)");
    pa_context_disconnect(NULL);
    COMPRUEBA(pa_context_errno(NULL) == PA_ERR_INVALID, "errno(NULL)");
    COMPRUEBA(pa_context_get_sink_input_info(NULL, 0, NULL, NULL) == NULL, "get_sink_input_info(NULL)");
    COMPRUEBA(pa_context_get_state(NULL) == PA_CONTEXT_FAILED, "get_state(NULL)");
    COMPRUEBA(pa_context_new(NULL, "x") == NULL, "context_new(NULL api)");
    COMPRUEBA(pa_context_set_sink_input_volume(NULL, 0, NULL, NULL, NULL) == NULL, "set_sink_input_volume(NULL)");
    pa_context_set_state_callback(NULL, NULL, NULL);
    pa_context_set_subscribe_callback(NULL, NULL, NULL);
    COMPRUEBA(pa_context_subscribe(NULL, 0, NULL, NULL) == NULL, "subscribe(NULL)");
    pa_context_unref(NULL);
    COMPRUEBA(pa_cvolume_avg(NULL) == PA_VOLUME_MUTED, "cvolume_avg(NULL)");
    COMPRUEBA(pa_cvolume_set(NULL, 2, 1) == NULL, "cvolume_set(NULL)");
    pa_cvolume cv;
    COMPRUEBA(pa_cvolume_set(&cv, 0, 1) == NULL && pa_cvolume_set(&cv, 99, 1) == NULL, "cvolume_set con canales malos");
    COMPRUEBA(pa_gettimeofday(NULL) == NULL, "gettimeofday(NULL)");
    COMPRUEBA(pa_operation_get_state(NULL) == PA_OPERATION_CANCELLED, "operation_get_state(NULL)");
    pa_operation_unref(NULL);
    pa_proplist_free(NULL);
    COMPRUEBA(pa_proplist_sets(NULL, "a", "b") < 0, "proplist_sets(NULL)");
    pa_proplist *pl = pa_proplist_new();
    COMPRUEBA(pa_proplist_sets(pl, "con espacio", "b") < 0 && pa_proplist_sets(pl, "a", NULL) < 0, "proplist_sets con clave o valor malos");
    pa_proplist_free(pl);
    COMPRUEBA(pa_stream_connect_playback(NULL, NULL, NULL, 0, NULL, NULL) < 0, "connect_playback(NULL)");
    COMPRUEBA(pa_stream_cork(NULL, 1, NULL, NULL) == NULL, "cork(NULL)");
    COMPRUEBA(pa_stream_disconnect(NULL) < 0, "stream_disconnect(NULL)");
    COMPRUEBA(pa_stream_flush(NULL, NULL, NULL) == NULL, "flush(NULL)");
    COMPRUEBA(pa_stream_get_index(NULL) == PA_INVALID_INDEX, "get_index(NULL)");
    COMPRUEBA(pa_stream_get_state(NULL) == PA_STREAM_FAILED, "stream_get_state(NULL)");
    COMPRUEBA(pa_stream_get_timing_info(NULL) == NULL, "get_timing_info(NULL)");
    COMPRUEBA(pa_stream_is_corked(NULL) < 0, "is_corked(NULL)");
    COMPRUEBA(pa_stream_new(NULL, "x", NULL, NULL) == NULL, "stream_new(NULL)");
    COMPRUEBA(pa_stream_new_with_proplist(NULL, "x", NULL, NULL, NULL) == NULL, "stream_new_with_proplist(NULL)");
    pa_stream_set_started_callback(NULL, NULL, NULL);
    pa_stream_set_state_callback(NULL, NULL, NULL);
    pa_stream_set_underflow_callback(NULL, NULL, NULL);
    pa_stream_unref(NULL);
    COMPRUEBA(pa_stream_update_timing_info(NULL, NULL, NULL) == NULL, "update_timing_info(NULL)");
    COMPRUEBA(pa_stream_writable_size(NULL) == (size_t)-1, "writable_size(NULL)");
    COMPRUEBA(pa_stream_write(NULL, "x", 1, NULL, 0, 0) < 0, "write(NULL)");
    COMPRUEBA(pa_strerror(9999) == NULL && pa_strerror(0) && pa_strerror(-3) && !strcmp(pa_strerror(-3), "Invalid argument"), "strerror");
    pa_threaded_mainloop_free(NULL);
    COMPRUEBA(pa_threaded_mainloop_get_api(NULL) == NULL, "get_api(NULL)");
    pa_threaded_mainloop_lock(NULL);
    pa_threaded_mainloop_signal(NULL, 0);
    COMPRUEBA(pa_threaded_mainloop_start(NULL) < 0, "start(NULL)");
    pa_threaded_mainloop_stop(NULL);
    pa_threaded_mainloop_unlock(NULL);
    pa_threaded_mainloop_wait(NULL);
    COMPRUEBA(pa_timeval_diff(NULL, NULL) == 0, "timeval_diff(NULL)");
    COMPRUEBA(pa_usec_to_bytes(1000, NULL) == 0, "usec_to_bytes(NULL)");
    pa_sample_spec malo = {PA_SAMPLE_S16LE, 0, 2};
    COMPRUEBA(pa_usec_to_bytes(1000, &malo) == 0, "usec_to_bytes con frecuencia 0");

    /* objetos vivos usados fuera de orden */
    ruta_caso("nulos");
    pa_threaded_mainloop *m = pa_threaded_mainloop_new();
    pa_threaded_mainloop_unlock(m); /* sin tenerlo */
    pa_threaded_mainloop_wait(m);   /* sin tenerlo */
    pa_threaded_mainloop_start(m);
    COMPRUEBA(pa_threaded_mainloop_start(m) < 0, "start dos veces");
    pa_threaded_mainloop_lock(m);
    pa_context *c = pa_context_new(pa_threaded_mainloop_get_api(m), "nulos");
    pa_sample_spec ss = {PA_SAMPLE_S16LE, 48000, 2};
    COMPRUEBA(pa_stream_new(c, "x", &ss, NULL) == NULL && pa_context_errno(c) == PA_ERR_BADSTATE, "stream_new sin conectar el contexto");
    COMPRUEBA(pa_context_subscribe(c, 4, NULL, NULL) == NULL, "subscribe sin conectar");
    pa_context_set_state_callback(c, nada_ctx, NULL);
    pa_context_connect(c, NULL, 0, NULL);
    COMPRUEBA(pa_context_connect(c, NULL, 0, NULL) == -PA_ERR_BADSTATE, "connect dos veces");
    while (pa_context_get_state(c) != PA_CONTEXT_READY) {
        pa_threaded_mainloop_unlock(m);
        duerme_ms(5);
        pa_threaded_mainloop_lock(m);
    }
    pa_sample_spec mal2 = {PA_SAMPLE_S16LE, 48000, 0};
    COMPRUEBA(pa_stream_new(c, "x", &mal2, NULL) == NULL && pa_context_errno(c) == PA_ERR_INVALID, "stream_new con 0 canales");
    COMPRUEBA(pa_stream_new(c, "x", NULL, NULL) == NULL, "stream_new sin formato");
    pa_stream *s = pa_stream_new(c, "x", &ss, NULL);
    COMPRUEBA(pa_stream_writable_size(s) == (size_t)-1, "writable_size sin conectar");
    COMPRUEBA(pa_stream_write(s, "abcd", 4, NULL, 0, 0) == -PA_ERR_BADSTATE, "write sin conectar");
    COMPRUEBA(pa_stream_get_timing_info(s) == NULL, "timing sin conectar");
    COMPRUEBA(pa_stream_cork(s, 1, NULL, NULL) == NULL, "cork sin conectar");
    COMPRUEBA(pa_stream_get_index(s) == PA_INVALID_INDEX, "index sin conectar");
    COMPRUEBA(pa_stream_disconnect(s) == -PA_ERR_BADSTATE, "disconnect sin conectar");
    pa_stream_connect_playback(s, NULL, NULL, 0, NULL, NULL);
    while (pa_stream_get_state(s) == PA_STREAM_CREATING) {
        pa_threaded_mainloop_unlock(m);
        duerme_ms(5);
        pa_threaded_mainloop_lock(m);
    }
    COMPRUEBA(pa_stream_write(s, NULL, 4, NULL, 0, 0) == -PA_ERR_INVALID, "write(NULL datos)");
    COMPRUEBA(pa_stream_write(s, "abc", 3, NULL, 0, 0) == -PA_ERR_INVALID, "write que no es de cuadros enteros");
    COMPRUEBA(pa_stream_write(s, "abcd", 4, NULL, 0, PA_SEEK_ABSOLUTE) == -PA_ERR_NOTSUPPORTED, "write con seek absoluto");
    COMPRUEBA(pa_stream_write(s, "abcd", 4, NULL, 8, PA_SEEK_RELATIVE) == 0, "write con salto relativo (silencio)");
    COMPRUEBA(pa_context_set_sink_input_volume(c, PA_INVALID_INDEX, &cv, NULL, NULL) == NULL, "volumen a indice invalido");
    pa_cvolume_set(&cv, 2, 1000);
    pa_operation *o = pa_context_set_sink_input_volume(c, 12345678, &cv, NULL, NULL);
    while (o && pa_operation_get_state(o) == PA_OPERATION_RUNNING) {
        pa_threaded_mainloop_unlock(m);
        duerme_ms(5);
        pa_threaded_mainloop_lock(m);
    }
    COMPRUEBA(o && pa_context_errno(c) == PA_ERR_NOENTITY, "volumen a una entrada que no existe: NOENTITY");
    pa_operation_unref(o);
    /* unref sin disconnect: el flujo se termina solo */
    pa_stream_unref(s);
    pa_context_unref(c); /* sin disconnect */
    pa_threaded_mainloop_unlock(m);
    pa_threaded_mainloop_free(m); /* sin stop */
    printf("  argumentos NULL y fuera de orden: %d comprobaciones sin caerse\n", bien_n - antes);
}

/* ---- el bucle sin hilo: api completo ---- */
static int io_veces, t_veces, d_veces, destruidos;
static void io_cb(pa_mainloop_api *a, pa_io_event *e, int fd, pa_io_event_flags_t f, void *u)
{
    (void)e; (void)u;
    char b[8];
    if ((f & PA_IO_EVENT_INPUT) && read(fd, b, sizeof b) > 0)
        io_veces++;
    (void)a;
}
static void t_cb(pa_mainloop_api *a, pa_time_event *e, const struct timeval *tv, void *u)
{
    (void)tv; (void)u;
    t_veces++;
    if (t_veces < 3) {
        struct timeval n;
        pa_gettimeofday(&n);
        pa_timeval_add(&n, 20000);
        a->time_restart(e, &n);
    } else {
        a->quit(a, 42);
    }
}
static void d_cb(pa_mainloop_api *a, pa_defer_event *e, void *u)
{
    (void)u;
    d_veces++;
    a->defer_enable(e, 0);
}
static void des_io(pa_mainloop_api *a, pa_io_event *e, void *u) { (void)a; (void)e; (void)u; destruidos++; }
static void des_t(pa_mainloop_api *a, pa_time_event *e, void *u) { (void)a; (void)e; (void)u; destruidos++; }
static void des_d(pa_mainloop_api *a, pa_defer_event *e, void *u) { (void)a; (void)e; (void)u; destruidos++; }

static void *senala(void *x)
{
    pa_threaded_mainloop *mm = *(pa_threaded_mainloop **)x;
    duerme_ms(50);
    pa_threaded_mainloop_lock(mm);
    pa_threaded_mainloop_signal(mm, 1); /* espera el accept */
    pa_threaded_mainloop_unlock(mm);
    return NULL;
}

static void caso_bucle(void)
{
    pa_mainloop *m = pa_mainloop_new();
    pa_mainloop_api *a = pa_mainloop_get_api(m);
    int tubo[2];
    COMPRUEBA(pipe(tubo) == 0, "pipe");
    pa_io_event *io = a->io_new(a, tubo[0], PA_IO_EVENT_INPUT, io_cb, NULL);
    a->io_set_destroy(io, des_io);
    struct timeval tv;
    pa_gettimeofday(&tv);
    pa_timeval_add(&tv, 20000);
    pa_time_event *te = a->time_new(a, &tv, t_cb, NULL);
    a->time_set_destroy(te, des_t);
    pa_defer_event *de = a->defer_new(a, d_cb, NULL);
    a->defer_set_destroy(de, des_d);
    COMPRUEBA(write(tubo[1], "x", 1) == 1, "escribir en la tuberia");
    double t0 = ahora_s();
    int ret = 0;
    int r = pa_mainloop_run(m, &ret);
    double dt = ahora_s() - t0;
    COMPRUEBA(r >= 0 && ret == 42, "run acaba con quit(42) (r=%d ret=%d)", r, ret);
    COMPRUEBA(io_veces == 1 && t_veces == 3 && d_veces == 1, "io %d, tiempo %d, defer %d", io_veces, t_veces, d_veces);
    COMPRUEBA(dt >= 0.055 && dt < 0.5, "tres tiempos de 20 ms: %.3f s", dt);
    a->io_free(io);
    pa_mainloop_free(m);
    COMPRUEBA(destruidos == 3, "los tres destruye llamados (%d)", destruidos);
    close(tubo[0]);
    close(tubo[1]);

    /* el cerrojo recursivo: wait lo suelta del todo */
    pa_threaded_mainloop *tm = pa_threaded_mainloop_new();
    pa_threaded_mainloop_start(tm);
    pa_threaded_mainloop_lock(tm);
    pa_threaded_mainloop_lock(tm);
    /* otro hilo le hace signal tomando el cerrojo: si wait no lo soltara del todo, se trabaria */
    pthread_t h;
    struct { pa_threaded_mainloop *m; } arg = {tm};
    pthread_create(&h, NULL, senala, &arg);
    double w0 = ahora_s();
    pa_threaded_mainloop_wait(tm);
    pa_threaded_mainloop_accept(tm);
    double dw = ahora_s() - w0;
    pa_threaded_mainloop_unlock(tm);
    pa_threaded_mainloop_unlock(tm);
    pthread_join(h, NULL);
    COMPRUEBA(dw >= 0.04 && dw < 1.0, "wait con el cerrojo tomado dos veces, signal con accept: %.3f s", dw);
    COMPRUEBA(pa_threaded_mainloop_in_thread(tm) == 0, "in_thread desde fuera es 0");
    pa_threaded_mainloop_stop(tm);
    pa_threaded_mainloop_free(tm);
    printf("  bucle sin hilo (io, tiempos, defer, quit, destruye) y cerrojo recursivo con wait/signal/accept\n");
}

int main(int argc, char **argv)
{
    pon_dir_lector(argv[0]);
    COMPRUEBA(!strcmp(pa_get_library_version(), "17.0.0") && pa_context_get_server(NULL) == NULL,
              "esta es la libpulse de rocola-pulso");
    snprintf(base, sizeof base, "%s", getenv("PRUEBA_DIR") ? getenv("PRUEBA_DIR") : "/tmp");
    const char *solo = argc > 1 ? argv[1] : NULL;
#define CASO(n, f) if (!solo || !strcmp(solo, n)) f()
    CASO("formatos", caso_formatos);
    CASO("lector", caso_sin_lector_y_tarde);
    CASO("rehecho", caso_anillo_rehecho);
    CASO("dos", caso_dos_flujos);
    CASO("vaciado", caso_vaciado);
    CASO("muere", caso_cliente_muere);
    CASO("imposible", caso_anillo_imposible);
    CASO("nulos", caso_nulos);
    CASO("bucle", caso_bucle);
    printf("raro: comprobaciones %d bien, %d mal\n", bien_n, mal_n);
    return mal_n ? 1 : 0;
}
