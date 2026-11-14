/* SPDX-License-Identifier: MIT
 * rocola-pulso: el bucle de eventos (pa_mainloop), el bucle con hilo
 * (pa_threaded_mainloop) y las senales (pa_signal_*).
 *
 * El bucle sirve un pa_mainloop_api COMPLETO (io, tiempos, defer, quit): el
 * contexto y los flujos de rocola-pulso trabajan sobre el, como libpulse, y un
 * cliente lo puede usar directo. Sin esperas activas: poll() hasta lo
 * siguiente. Idea general como la de apulse (MIT, github.com/i-rinat/apulse):
 * el bucle con hilo suelta su cerrojo solo mientras espera en poll(). El
 * cerrojo es recursivo como el de libpulse, pero la cuenta la llevamos
 * nosotros (un pthread_mutex normal), asi wait() lo suelta del todo aunque se
 * haya tomado varias veces. */
#define _GNU_SOURCE
#include "interno.h"

#include <errno.h>
#include <fcntl.h>
#include <signal.h>
#include <stdlib.h>
#include <string.h>
#include <sys/eventfd.h>
#include <time.h>
#include <unistd.h>

uint64_t pulso_ahora_us(void)
{
    struct timespec t;
    clock_gettime(CLOCK_MONOTONIC, &t);
    return (uint64_t)t.tv_sec * 1000000ull + (uint64_t)t.tv_nsec / 1000ull;
}

static uint64_t tv_a_mono(const struct timeval *tv)
{
    if (tv->tv_usec & PA_TIMEVAL_RTCLOCK) {
        uint64_t us = (uint64_t)(tv->tv_usec & ~PA_TIMEVAL_RTCLOCK);
        return (uint64_t)tv->tv_sec * 1000000ull + us;
    }
    struct timeval ahora;
    gettimeofday(&ahora, NULL);
    int64_t dif = ((int64_t)tv->tv_sec - (int64_t)ahora.tv_sec) * 1000000ll + ((int64_t)tv->tv_usec - (int64_t)ahora.tv_usec);
    int64_t m = (int64_t)pulso_ahora_us() + dif;
    return m < 0 ? 0 : (uint64_t)m;
}

static void despierta(pa_mainloop *m)
{
    if (m && m->en_poll && m->despierta_fd >= 0) {
        uint64_t uno = 1;
        ssize_t r = write(m->despierta_fd, &uno, sizeof uno);
        (void)r;
    }
}

/* ---- el cerrojo del bucle con hilo ---- */
static unsigned long yo(void) { return (unsigned long)pthread_self(); }

void hilo_bloquea(pa_threaded_mainloop *t)
{
    if (__atomic_load_n(&t->dueno, __ATOMIC_RELAXED) == yo()) {
        t->cuenta++;
        return;
    }
    pthread_mutex_lock(&t->mx);
    __atomic_store_n(&t->dueno, yo(), __ATOMIC_RELAXED);
    t->cuenta = 1;
}

void hilo_suelta(pa_threaded_mainloop *t)
{
    if (__atomic_load_n(&t->dueno, __ATOMIC_RELAXED) != yo()) {
        TRAZA("aviso: unlock sin tener el cerrojo (se ignora)");
        return;
    }
    if (--t->cuenta > 0)
        return;
    __atomic_store_n(&t->dueno, 0ul, __ATOMIC_RELAXED);
    pthread_mutex_unlock(&t->mx);
}

/* Suelta del todo (para esperar) y devuelve cuantas veces lo tenia. */
static int suelta_todo(pa_threaded_mainloop *t)
{
    if (__atomic_load_n(&t->dueno, __ATOMIC_RELAXED) != yo())
        return 0;
    int n = t->cuenta;
    t->cuenta = 0;
    __atomic_store_n(&t->dueno, 0ul, __ATOMIC_RELAXED);
    pthread_mutex_unlock(&t->mx);
    return n;
}

static void retoma(pa_threaded_mainloop *t, int n)
{
    if (n <= 0)
        return;
    pthread_mutex_lock(&t->mx);
    __atomic_store_n(&t->dueno, yo(), __ATOMIC_RELAXED);
    t->cuenta = n;
}

/* Espera en una condicion soltando el cerrojo del todo (lo tiene quien llama). */
static void espera_cond(pa_threaded_mainloop *t, pthread_cond_t *c)
{
    int n = t->cuenta;
    t->cuenta = 0;
    __atomic_store_n(&t->dueno, 0ul, __ATOMIC_RELAXED);
    pthread_cond_wait(c, &t->mx);
    __atomic_store_n(&t->dueno, yo(), __ATOMIC_RELAXED);
    t->cuenta = n;
}

int bucle_en_hilo(pa_mainloop *m)
{
    return m && m->hilo && m->hilo->corriendo && pthread_equal(pthread_self(), m->hilo->hilo);
}

/* ---- el api ---- */
static pa_io_event *api_io_new(pa_mainloop_api *a, int fd, pa_io_event_flags_t ev, pa_io_event_cb_t cb, void *ud)
{
    if (!a || fd < 0)
        return NULL;
    pa_mainloop *m = a->userdata;
    pa_io_event *e = calloc(1, sizeof *e);
    if (!e)
        return NULL;
    e->m = m;
    e->fd = fd;
    e->eventos = ev;
    e->cb = cb;
    e->ud = ud;
    e->pos = -1;
    e->sig = m->ios;
    m->ios = e;
    despierta(m);
    return e;
}

static void api_io_enable(pa_io_event *e, pa_io_event_flags_t ev)
{
    if (!e || e->muerto)
        return;
    e->eventos = ev;
    despierta(e->m);
}

static void api_io_free(pa_io_event *e)
{
    if (!e || e->muerto)
        return;
    e->muerto = 1;
    e->m->muertos++;
    despierta(e->m);
}

static void api_io_set_destroy(pa_io_event *e, pa_io_event_destroy_cb_t cb)
{
    if (e)
        e->destruye = cb;
}

static pa_time_event *api_time_new(pa_mainloop_api *a, const struct timeval *tv, pa_time_event_cb_t cb, void *ud)
{
    if (!a)
        return NULL;
    pa_mainloop *m = a->userdata;
    pa_time_event *e = calloc(1, sizeof *e);
    if (!e)
        return NULL;
    e->m = m;
    e->cb = cb;
    e->ud = ud;
    if (tv) {
        e->activo = 1;
        e->tv = *tv;
        e->cuando_us = tv_a_mono(tv);
    }
    e->sig = m->tiempos;
    m->tiempos = e;
    despierta(m);
    return e;
}

static void api_time_restart(pa_time_event *e, const struct timeval *tv)
{
    if (!e || e->muerto)
        return;
    if (tv) {
        e->activo = 1;
        e->tv = *tv;
        e->cuando_us = tv_a_mono(tv);
    } else {
        e->activo = 0;
    }
    despierta(e->m);
}

static void api_time_free(pa_time_event *e)
{
    if (!e || e->muerto)
        return;
    e->muerto = 1;
    e->activo = 0;
    e->m->muertos++;
    despierta(e->m);
}

static void api_time_set_destroy(pa_time_event *e, pa_time_event_destroy_cb_t cb)
{
    if (e)
        e->destruye = cb;
}

static pa_defer_event *api_defer_new(pa_mainloop_api *a, pa_defer_event_cb_t cb, void *ud)
{
    if (!a)
        return NULL;
    pa_mainloop *m = a->userdata;
    pa_defer_event *e = calloc(1, sizeof *e);
    if (!e)
        return NULL;
    e->m = m;
    e->cb = cb;
    e->ud = ud;
    e->activo = 1;
    e->sig = m->defers;
    m->defers = e;
    despierta(m);
    return e;
}

static void api_defer_enable(pa_defer_event *e, int b)
{
    if (!e || e->muerto)
        return;
    e->activo = b ? 1 : 0;
    if (b)
        despierta(e->m);
}

static void api_defer_free(pa_defer_event *e)
{
    if (!e || e->muerto)
        return;
    e->muerto = 1;
    e->activo = 0;
    e->m->muertos++;
    despierta(e->m);
}

static void api_defer_set_destroy(pa_defer_event *e, pa_defer_event_destroy_cb_t cb)
{
    if (e)
        e->destruye = cb;
}

static void api_quit(pa_mainloop_api *a, int retval)
{
    if (!a)
        return;
    pa_mainloop *m = a->userdata;
    m->salir = 1;
    m->retval = retval;
    despierta(m);
}

static const pa_mainloop_api api_modelo = {
    NULL,
    api_io_new, api_io_enable, api_io_free, api_io_set_destroy,
    api_time_new, api_time_restart, api_time_free, api_time_set_destroy,
    api_defer_new, api_defer_enable, api_defer_free, api_defer_set_destroy,
    api_quit,
};

/* Quita los eventos muertos, llamando a su destruye. */
static void limpia(pa_mainloop *m, int todos)
{
    while (m->muertos > 0 || todos) {
        int hubo = 0;
        for (pa_io_event **p = &m->ios; *p;) {
            pa_io_event *e = *p;
            if (e->muerto || todos) {
                *p = e->sig;
                if (!e->muerto)
                    e->muerto = 1;
                else
                    m->muertos--;
                if (e->destruye)
                    e->destruye(&m->api, e, e->ud);
                free(e);
                hubo = 1;
            } else {
                p = &e->sig;
            }
        }
        for (pa_time_event **p = &m->tiempos; *p;) {
            pa_time_event *e = *p;
            if (e->muerto || todos) {
                *p = e->sig;
                if (!e->muerto)
                    e->muerto = 1;
                else
                    m->muertos--;
                if (e->destruye)
                    e->destruye(&m->api, e, e->ud);
                free(e);
                hubo = 1;
            } else {
                p = &e->sig;
            }
        }
        for (pa_defer_event **p = &m->defers; *p;) {
            pa_defer_event *e = *p;
            if (e->muerto || todos) {
                *p = e->sig;
                if (!e->muerto)
                    e->muerto = 1;
                else
                    m->muertos--;
                if (e->destruye)
                    e->destruye(&m->api, e, e->ud);
                free(e);
                hubo = 1;
            } else {
                p = &e->sig;
            }
        }
        if (todos) {
            if (!m->ios && !m->tiempos && !m->defers)
                break;
        } else if (!hubo) {
            break;
        }
    }
    if (m->muertos < 0)
        m->muertos = 0;
}

static short a_poll(pa_io_event_flags_t f)
{
    short r = 0;
    if (f & PA_IO_EVENT_INPUT)
        r |= POLLIN;
    if (f & PA_IO_EVENT_OUTPUT)
        r |= POLLOUT;
    return r;
}

static pa_io_event_flags_t de_poll(short r)
{
    int f = 0;
    if (r & POLLIN)
        f |= PA_IO_EVENT_INPUT;
    if (r & POLLOUT)
        f |= PA_IO_EVENT_OUTPUT;
    if (r & POLLHUP)
        f |= PA_IO_EVENT_HANGUP;
    if (r & (POLLERR | POLLNVAL))
        f |= PA_IO_EVENT_ERROR;
    return (pa_io_event_flags_t)f;
}

static int corre_defers(pa_mainloop *m)
{
    int n = 0;
    for (pa_defer_event *e = m->defers; e; e = e->sig) {
        if (e->muerto || !e->activo)
            continue;
        e->cb(&m->api, e, e->ud);
        n++;
        if (m->salir)
            break;
    }
    return n;
}

static int iterate(pa_mainloop *m, int bloquea)
{
    if (m->salir)
        return -2;
    int n = corre_defers(m);
    if (m->salir)
        return -2;
    limpia(m, 0);

    /* lo que esperar */
    int espera = -1;
    if (n > 0 || !bloquea) {
        espera = 0;
    } else {
        for (pa_defer_event *e = m->defers; e; e = e->sig)
            if (!e->muerto && e->activo)
                espera = 0;
        if (espera != 0) {
            uint64_t ahora = pulso_ahora_us(), prox = UINT64_MAX;
            for (pa_time_event *e = m->tiempos; e; e = e->sig)
                if (!e->muerto && e->activo && e->cuando_us < prox)
                    prox = e->cuando_us;
            if (prox != UINT64_MAX) {
                if (prox <= ahora)
                    espera = 0;
                else {
                    uint64_t ms = (prox - ahora + 999) / 1000;
                    espera = ms > 60000 ? 60000 : (int)ms;
                }
            }
        }
    }
    unsigned cuantos = 1;
    for (pa_io_event *e = m->ios; e; e = e->sig)
        cuantos++;
    if (cuantos > m->cap_pfd) {
        struct pollfd *nuevo = realloc(m->pfd, cuantos * 2 * sizeof *nuevo);
        if (!nuevo)
            return -1;
        m->pfd = nuevo;
        m->cap_pfd = cuantos * 2;
    }
    unsigned k = 0;
    m->pfd[k].fd = m->despierta_fd;
    m->pfd[k].events = POLLIN;
    m->pfd[k].revents = 0;
    k++;
    for (pa_io_event *e = m->ios; e; e = e->sig) {
        e->pos = -1;
        if (e->muerto)
            continue;
        e->pos = (int)k;
        m->pfd[k].fd = e->fd;
        m->pfd[k].events = a_poll(e->eventos);
        m->pfd[k].revents = 0;
        k++;
    }

    m->en_poll = 1;
    int cuenta = 0;
    if (m->hilo)
        cuenta = suelta_todo(m->hilo);
    int r;
    if (m->poll_func)
        r = m->poll_func(m->pfd, k, espera, m->poll_ud);
    else
        r = poll(m->pfd, k, espera);
    int err = errno;
    if (m->hilo)
        retoma(m->hilo, cuenta);
    m->en_poll = 0;
    if (r < 0 && err != EINTR) {
        TRAZA("poll fallo: %s", strerror(err));
        return -1;
    }
    if (r > 0 && (m->pfd[0].revents & POLLIN)) {
        uint64_t v;
        ssize_t x = read(m->despierta_fd, &v, sizeof v);
        (void)x;
    }

    /* tiempos */
    uint64_t ahora = pulso_ahora_us();
    for (pa_time_event *e = m->tiempos; e; e = e->sig) {
        if (e->muerto || !e->activo || e->cuando_us > ahora)
            continue;
        e->activo = 0;
        struct timeval tv = e->tv;
        e->cb(&m->api, e, &tv, e->ud);
        n++;
        if (m->salir)
            return -2;
    }
    /* entradas y salidas */
    if (r > 0) {
        for (pa_io_event *e = m->ios; e; e = e->sig) {
            if (e->muerto || e->pos < 0 || (unsigned)e->pos >= k)
                continue;
            short rv = m->pfd[e->pos].revents;
            e->pos = -1;
            if (!rv)
                continue;
            e->cb(&m->api, e, e->fd, de_poll(rv), e->ud);
            n++;
            if (m->salir)
                return -2;
        }
    }
    limpia(m, 0);
    return n;
}

/* ---- pa_mainloop ---- */
EXPORTA pa_mainloop *pa_mainloop_new(void)
{
    pulso_inicia();
    pa_mainloop *m = calloc(1, sizeof *m);
    if (!m)
        return NULL;
    m->api = api_modelo;
    m->api.userdata = m;
    m->despierta_fd = eventfd(0, EFD_CLOEXEC | EFD_NONBLOCK);
    if (m->despierta_fd < 0) {
        free(m);
        return NULL;
    }
    TRAZA("pa_mainloop_new() = %p", (void *)m);
    return m;
}

EXPORTA void pa_mainloop_free(pa_mainloop *m)
{
    TRAZA("pa_mainloop_free(%p)", (void *)m);
    if (!m)
        return;
    limpia(m, 1);
    if (m->despierta_fd >= 0)
        close(m->despierta_fd);
    free(m->pfd);
    free(m);
}

EXPORTA pa_mainloop_api *pa_mainloop_get_api(pa_mainloop *m)
{
    return m ? &m->api : NULL;
}

EXPORTA int pa_mainloop_iterate(pa_mainloop *m, int block, int *retval)
{
    if (!m)
        return -1;
    int r = iterate(m, block);
    if (r == -2 && retval)
        *retval = m->retval;
    return r;
}

EXPORTA int pa_mainloop_run(pa_mainloop *m, int *retval)
{
    if (!m)
        return -1;
    int r;
    while ((r = pa_mainloop_iterate(m, 1, retval)) >= 0)
        ;
    return r == -2 ? 1 : -1;
}

EXPORTA void pa_mainloop_quit(pa_mainloop *m, int retval)
{
    if (m)
        api_quit(&m->api, retval);
}

EXPORTA int pa_mainloop_get_retval(const pa_mainloop *m)
{
    return m ? m->retval : -1;
}

EXPORTA void pa_mainloop_wakeup(pa_mainloop *m)
{
    if (m && m->despierta_fd >= 0) {
        uint64_t uno = 1;
        ssize_t r = write(m->despierta_fd, &uno, sizeof uno);
        (void)r;
    }
}

EXPORTA void pa_mainloop_set_poll_func(pa_mainloop *m, pa_poll_func f, void *ud)
{
    if (!m)
        return;
    m->poll_func = f;
    m->poll_ud = ud;
}

/* ---- pa_threaded_mainloop ---- */
static void *hilo_principal(void *arg)
{
    pa_threaded_mainloop *t = arg;
    pthread_setname_np(pthread_self(), t->nombre[0] ? t->nombre : "threaded-ml");
    hilo_bloquea(t);
    TRAZA("hilo del bucle %p: empieza", (void *)t);
    while (iterate(t->m, 1) >= 0)
        ;
    TRAZA("hilo del bucle %p: sale", (void *)t);
    hilo_suelta(t);
    return NULL;
}

EXPORTA pa_threaded_mainloop *pa_threaded_mainloop_new(void)
{
    pulso_inicia();
    pa_threaded_mainloop *t = calloc(1, sizeof *t);
    if (!t)
        return NULL;
    t->m = pa_mainloop_new();
    if (!t->m) {
        free(t);
        return NULL;
    }
    t->m->hilo = t;
    pthread_mutex_init(&t->mx, NULL);
    pthread_cond_init(&t->cond, NULL);
    pthread_cond_init(&t->cond_acepta, NULL);
    TRAZA("pa_threaded_mainloop_new() = %p", (void *)t);
    return t;
}

EXPORTA int pa_threaded_mainloop_start(pa_threaded_mainloop *t)
{
    TRAZA("pa_threaded_mainloop_start(%p)", (void *)t);
    if (!t || t->corriendo)
        return -1;
    /* Las senales van al hilo principal del programa, como en libpulse. */
    sigset_t todo, antes;
    sigfillset(&todo);
    pthread_sigmask(SIG_BLOCK, &todo, &antes);
    t->m->salir = 0;
    t->corriendo = 1;
    int r = pthread_create(&t->hilo, NULL, hilo_principal, t);
    pthread_sigmask(SIG_SETMASK, &antes, NULL);
    if (r != 0) {
        t->corriendo = 0;
        pulso_aviso("no se pudo crear el hilo del bucle: %s", strerror(r));
        return -1;
    }
    return 0;
}

EXPORTA void pa_threaded_mainloop_stop(pa_threaded_mainloop *t)
{
    TRAZA("pa_threaded_mainloop_stop(%p)", (void *)t);
    if (!t || !t->corriendo)
        return;
    if (pthread_equal(pthread_self(), t->hilo)) {
        /* desde el propio hilo: solo se le pide salir */
        api_quit(&t->m->api, 0);
        return;
    }
    int cuenta = suelta_todo(t); /* por si quien llama lo tenia (libpulse no lo permite) */
    hilo_bloquea(t);
    api_quit(&t->m->api, 0);
    despierta(t->m);
    hilo_suelta(t);
    pthread_join(t->hilo, NULL);
    t->corriendo = 0;
    t->m->salir = 0;
    retoma(t, cuenta);
}

EXPORTA void pa_threaded_mainloop_free(pa_threaded_mainloop *t)
{
    TRAZA("pa_threaded_mainloop_free(%p)", (void *)t);
    if (!t)
        return;
    if (t->corriendo) {
        if (pthread_equal(pthread_self(), t->hilo)) {
            pulso_aviso("pa_threaded_mainloop_free desde su propio hilo: no se libera");
            return;
        }
        pa_threaded_mainloop_stop(t);
    }
    pa_mainloop_free(t->m);
    pthread_mutex_destroy(&t->mx);
    pthread_cond_destroy(&t->cond);
    pthread_cond_destroy(&t->cond_acepta);
    free(t);
}

EXPORTA void pa_threaded_mainloop_lock(pa_threaded_mainloop *t)
{
    if (t)
        hilo_bloquea(t);
}

EXPORTA void pa_threaded_mainloop_unlock(pa_threaded_mainloop *t)
{
    if (t)
        hilo_suelta(t);
}

EXPORTA void pa_threaded_mainloop_wait(pa_threaded_mainloop *t)
{
    if (!t)
        return;
    if (t->corriendo && pthread_equal(pthread_self(), t->hilo)) {
        TRAZA("aviso: wait desde el hilo del bucle (se ignora)");
        return;
    }
    if (__atomic_load_n(&t->dueno, __ATOMIC_RELAXED) != yo()) {
        TRAZA("aviso: wait sin el cerrojo (se ignora)");
        return;
    }
    espera_cond(t, &t->cond);
}

EXPORTA void pa_threaded_mainloop_signal(pa_threaded_mainloop *t, int wait_for_accept)
{
    if (!t)
        return;
    pthread_cond_broadcast(&t->cond);
    if (wait_for_accept && __atomic_load_n(&t->dueno, __ATOMIC_RELAXED) == yo()) {
        t->por_aceptar++;
        while (t->por_aceptar > 0)
            espera_cond(t, &t->cond_acepta);
    }
}

EXPORTA void pa_threaded_mainloop_accept(pa_threaded_mainloop *t)
{
    if (!t)
        return;
    if (t->por_aceptar > 0)
        t->por_aceptar--;
    pthread_cond_broadcast(&t->cond_acepta);
}

EXPORTA int pa_threaded_mainloop_get_retval(const pa_threaded_mainloop *t)
{
    return t ? t->m->retval : -1;
}

EXPORTA pa_mainloop_api *pa_threaded_mainloop_get_api(pa_threaded_mainloop *t)
{
    return t ? &t->m->api : NULL;
}

EXPORTA int pa_threaded_mainloop_in_thread(pa_threaded_mainloop *t)
{
    return t && t->corriendo && pthread_equal(pthread_self(), t->hilo);
}

EXPORTA void pa_threaded_mainloop_set_name(pa_threaded_mainloop *t, const char *nombre)
{
    if (!t || !nombre)
        return;
    strncpy(t->nombre, nombre, sizeof t->nombre - 1);
    t->nombre[sizeof t->nombre - 1] = 0;
    if (t->corriendo)
        pthread_setname_np(t->hilo, t->nombre);
}

/* ---- senales (pa_signal_*): una tuberia y un io del bucle ---- */
struct pa_signal_event {
    int sig;
    pa_signal_cb_t cb;
    void *ud;
    pa_signal_destroy_cb_t destruye;
    struct sigaction antes;
    struct pa_signal_event *ant, *sig_ev;
};

static pa_mainloop_api *sen_api;
static int sen_tubo[2] = {-1, -1};
static pa_io_event *sen_io;
static pa_signal_event *sen_lista;

static void sen_manejador(int s)
{
    int e = errno;
    ssize_t r = write(sen_tubo[1], &s, sizeof s);
    (void)r;
    errno = e;
}

static void sen_io_cb(pa_mainloop_api *a, pa_io_event *e, int fd, pa_io_event_flags_t f, void *ud)
{
    (void)e; (void)f; (void)ud;
    int s;
    while (read(fd, &s, sizeof s) == (ssize_t)sizeof s) {
        for (pa_signal_event *ev = sen_lista; ev; ev = ev->sig_ev)
            if (ev->sig == s) {
                ev->cb(a, ev, s, ev->ud);
                break;
            }
    }
}

EXPORTA int pa_signal_init(pa_mainloop_api *api)
{
    if (!api || sen_api)
        return -1;
    if (pipe2(sen_tubo, O_CLOEXEC | O_NONBLOCK) != 0)
        return -1;
    sen_api = api;
    sen_io = api->io_new(api, sen_tubo[0], PA_IO_EVENT_INPUT, sen_io_cb, NULL);
    return 0;
}

EXPORTA void pa_signal_free(pa_signal_event *e)
{
    if (!e)
        return;
    if (e->ant)
        e->ant->sig_ev = e->sig_ev;
    else
        sen_lista = e->sig_ev;
    if (e->sig_ev)
        e->sig_ev->ant = e->ant;
    sigaction(e->sig, &e->antes, NULL);
    if (e->destruye)
        e->destruye(sen_api, e, e->ud);
    free(e);
}

EXPORTA void pa_signal_done(void)
{
    while (sen_lista)
        pa_signal_free(sen_lista);
    if (sen_io && sen_api)
        sen_api->io_free(sen_io);
    sen_io = NULL;
    if (sen_tubo[0] >= 0) {
        close(sen_tubo[0]);
        close(sen_tubo[1]);
    }
    sen_tubo[0] = sen_tubo[1] = -1;
    sen_api = NULL;
}

EXPORTA pa_signal_event *pa_signal_new(int sig, pa_signal_cb_t cb, void *ud)
{
    if (!sen_api || sig <= 0 || !cb)
        return NULL;
    pa_signal_event *e = calloc(1, sizeof *e);
    if (!e)
        return NULL;
    e->sig = sig;
    e->cb = cb;
    e->ud = ud;
    struct sigaction sa;
    memset(&sa, 0, sizeof sa);
    sa.sa_handler = sen_manejador;
    sigemptyset(&sa.sa_mask);
    sa.sa_flags = SA_RESTART;
    if (sigaction(sig, &sa, &e->antes) != 0) {
        free(e);
        return NULL;
    }
    e->sig_ev = sen_lista;
    if (sen_lista)
        sen_lista->ant = e;
    sen_lista = e;
    return e;
}

EXPORTA void pa_signal_set_destroy(pa_signal_event *e, pa_signal_destroy_cb_t cb)
{
    if (e)
        e->destruye = cb;
}
