/* UN CONVERTIDOR DE PRUEBA EN LAZO (la salida nueva, 27-11-2026; solo pruebas, fuera del programa): la medida del convertidor
 * (RocolaOs/RocolaOs.SalidaNuevaMide.cs) sin aparatos. Plugin externo de ALSA (ioplug), como Pruebas/Windows/alsa/reloj.c, con dos modos:
 *   salida   suena a su ritmo (el reloj del sistema) y lo que va sonando pasa por un «convertidor» que RECORTA (duro) desde LAZO_RECORTA_DB
 *            dBFS (de fabrica 0: no recorta), con LAZO_GANANCIA_DB de ganancia (lo que baja el nivel analogico) y deja el resultado en un
 *            anillo comun del proceso, por la hora en que sono;
 *   entrada  graba a su ritmo de ese anillo (lo que sono hace LAZO_RETRASO_MS ms, 15 de fabrica) con un ruido de LAZO_RUIDO_DB dBFS (-96).
 * Los dos en el mismo proceso (la rocola, o la orden de consola): la .so se carga una vez y el anillo es el mismo.
 *   pcm_type.lazo { lib "/ruta/libasound_module_pcm_lazo.so" }
 *   pcm.lazo_sal { type lazo modo salida }
 *   pcm.lazo_ent { type lazo modo entrada }
 * gcc -shared -fPIC -O2 -o libasound_module_pcm_lazo.so lazo.c -lasound -lm
 */
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <stdint.h>
#include <math.h>
#include <time.h>
#include <errno.h>
#include <unistd.h>
#include <poll.h>
#include <sys/timerfd.h>
#include <alsa/asoundlib.h>
#include <alsa/pcm_external.h>

#define TASA_ANILLO 44100
#define CUADROS_ANILLO (1u << 21)
static float anillo_l[CUADROS_ANILLO], anillo_r[CUADROS_ANILLO];
static int64_t escrito_hasta = -1;          /* el cuadro absoluto mas alto escrito */
static struct timespec t0_comun; static int t0_puesto;

static int64_t abs_ahora(void)
{
	struct timespec t; clock_gettime(CLOCK_MONOTONIC, &t);
	if (!t0_puesto) { t0_comun = t; t0_puesto = 1; }
	double s = (t.tv_sec - t0_comun.tv_sec) + (t.tv_nsec - t0_comun.tv_nsec) / 1e9;
	return (int64_t)(s * TASA_ANILLO) + 100000;   /* (no negativo) */
}
static double env_db(const char *n, double def) { const char *v = getenv(n); return v && *v ? atof(v) : def; }

typedef struct {
	snd_pcm_ioplug_t io;
	int fd, entrada;
	unsigned char *buf;
	size_t bpc;
	uint64_t dados, sonados;                   /* salida: dados por la app y sonados; entrada: entregados a la app y grabados */
	struct timespec t0; uint64_t sonados_t0;
	int64_t abs0;                              /* el cuadro absoluto del anillo en el que empezo */
	int corriendo;
	double recorta, ganancia, ruido; int retraso;
	uint32_t azar;
} lazo_t;

static double muestra_de(lazo_t *r, const unsigned char *p)
{
	switch (r->io.format) {
	case SND_PCM_FORMAT_S16_LE: return (int16_t)(p[0] | (p[1] << 8)) / 32768.0;
	case SND_PCM_FORMAT_S24_3LE: { int32_t v = (p[0] | (p[1] << 8) | (p[2] << 16)); if (v & 0x800000) v |= ~0xFFFFFF; return v / 8388608.0; }
	case SND_PCM_FORMAT_S32_LE: { int32_t v; memcpy(&v, p, 4); return v / 2147483648.0; }
	case SND_PCM_FORMAT_FLOAT_LE: { float f; memcpy(&f, p, 4); return f; }
	default: return 0;
	}
}
static void muestra_a(lazo_t *r, unsigned char *p, double x)
{
	if (x > 1) x = 1; if (x < -1) x = -1;
	switch (r->io.format) {
	case SND_PCM_FORMAT_S16_LE: { int v = (int)lrint(x * 32767); p[0] = v; p[1] = v >> 8; break; }
	case SND_PCM_FORMAT_S24_3LE: { int v = (int)lrint(x * 8388607); p[0] = v; p[1] = v >> 8; p[2] = v >> 16; break; }
	case SND_PCM_FORMAT_S32_LE: { int32_t v = (int32_t)lrint(x * 2147483647.0); memcpy(p, &v, 4); break; }
	case SND_PCM_FORMAT_FLOAT_LE: { float f = (float)x; memcpy(p, &f, 4); break; }
	default: break;
	}
}
static double ruido(lazo_t *r)
{
	double s = 0; for (int i = 0; i < 4; i++) { r->azar ^= r->azar << 13; r->azar ^= r->azar >> 17; r->azar ^= r->azar << 5; s += r->azar / 4294967296.0 - 0.5; }
	return s * r->ruido * 1.732;
}
/* lo que ya debio sonar (salida) o grabarse (entrada) segun el reloj */
static int avanza(lazo_t *r)
{
	if (!r->corriendo) return 0;
	struct timespec t; clock_gettime(CLOCK_MONOTONIC, &t);
	double dt = (t.tv_sec - r->t0.tv_sec) + (t.tv_nsec - r->t0.tv_nsec) / 1e9;
	uint64_t debe = r->sonados_t0 + (uint64_t)(dt * r->io.rate);
	if (!r->entrada) {
		if (debe > r->dados) { r->sonados = r->dados; r->corriendo = 0; return -EPIPE; }
		while (r->sonados < debe) {
			uint64_t i = r->sonados % r->io.buffer_size;
			const unsigned char *p = r->buf + i * r->bpc;
			int64_t a = r->abs0 + (int64_t)((double)r->sonados * TASA_ANILLO / r->io.rate);
			double l = muestra_de(r, p), d = r->io.channels > 1 ? muestra_de(r, p + r->bpc / r->io.channels) : l;
			/* el convertidor: recorte duro desde «recorta» y su ganancia analogica */
			if (l > r->recorta) l = r->recorta; if (l < -r->recorta) l = -r->recorta;
			if (d > r->recorta) d = r->recorta; if (d < -r->recorta) d = -r->recorta;
			anillo_l[a % CUADROS_ANILLO] = (float)(l * r->ganancia); anillo_r[a % CUADROS_ANILLO] = (float)(d * r->ganancia);
			if (a > escrito_hasta) escrito_hasta = a;
			r->sonados++;
		}
	} else {
		r->sonados = debe;   /* grabados */
		if (r->sonados - r->dados > r->io.buffer_size) { r->corriendo = 0; return -EPIPE; }
	}
	return 0;
}
static snd_pcm_sframes_t l_pointer(snd_pcm_ioplug_t *io)
{
	lazo_t *r = io->private_data;
	if (avanza(r) < 0) return -EPIPE;
	return (snd_pcm_sframes_t)(r->sonados % io->buffer_size);
}
static snd_pcm_sframes_t l_transfer(snd_pcm_ioplug_t *io, const snd_pcm_channel_area_t *areas, snd_pcm_uframes_t offset, snd_pcm_uframes_t size)
{
	lazo_t *r = io->private_data;
	unsigned char *dst = (unsigned char *)areas->addr + (areas->first + areas->step * offset) / 8;
	if (!r->entrada) {
		for (snd_pcm_uframes_t k = 0; k < size; k++) memcpy(r->buf + ((r->dados + k) % io->buffer_size) * r->bpc, dst + k * r->bpc, r->bpc);
		r->dados += size;
		return size;
	}
	for (snd_pcm_uframes_t k = 0; k < size; k++) {
		int64_t a = r->abs0 + (int64_t)((double)(r->dados + k) * TASA_ANILLO / io->rate) - r->retraso;
		double l = 0, d = 0;
		if (a >= 0 && a <= escrito_hasta && escrito_hasta - a < CUADROS_ANILLO) { l = anillo_l[a % CUADROS_ANILLO]; d = anillo_r[a % CUADROS_ANILLO]; }
		l += ruido(r); d += ruido(r);
		unsigned char *p = dst + k * r->bpc;
		muestra_a(r, p, l);
		if (io->channels > 1) muestra_a(r, p + r->bpc / io->channels, d);
	}
	r->dados += size;
	return size;
}
static int l_start(snd_pcm_ioplug_t *io)
{
	lazo_t *r = io->private_data;
	clock_gettime(CLOCK_MONOTONIC, &r->t0); r->sonados_t0 = r->sonados; r->corriendo = 1;
	r->abs0 = abs_ahora() - (int64_t)((double)r->sonados * TASA_ANILLO / io->rate);
	return 0;
}
static int l_stop(snd_pcm_ioplug_t *io) { lazo_t *r = io->private_data; if (r->corriendo) avanza(r); r->corriendo = 0; return 0; }
static int l_prepare(snd_pcm_ioplug_t *io) { lazo_t *r = io->private_data; r->dados = r->sonados = 0; r->corriendo = 0; return 0; }
static int l_hw_params(snd_pcm_ioplug_t *io, snd_pcm_hw_params_t *params)
{
	lazo_t *r = io->private_data;
	r->bpc = snd_pcm_format_physical_width(io->format) / 8 * io->channels;
	free(r->buf); r->buf = calloc(io->buffer_size, r->bpc);
	return r->buf ? 0 : -ENOMEM;
}
static int l_poll_revents(snd_pcm_ioplug_t *io, struct pollfd *pfd, unsigned int nfds, unsigned short *revents)
{
	lazo_t *r = io->private_data;
	uint64_t x; if (pfd[0].revents & POLLIN) { ssize_t n = read(r->fd, &x, sizeof x); (void)n; }
	avanza(r);
	snd_pcm_uframes_t minimo = 1;
	snd_pcm_sw_params_t *sw; snd_pcm_sw_params_alloca(&sw);
	if (snd_pcm_sw_params_current(io->pcm, sw) == 0) snd_pcm_sw_params_get_avail_min(sw, &minimo);
	if (!r->entrada) {
		snd_pcm_uframes_t libres = io->buffer_size - (snd_pcm_uframes_t)(r->dados - r->sonados);
		*revents = (libres >= minimo || !r->corriendo) ? POLLOUT : 0;
	} else {
		snd_pcm_uframes_t hay = (snd_pcm_uframes_t)(r->sonados - r->dados);
		*revents = (hay >= minimo) ? POLLIN : 0;
	}
	if (io->state == SND_PCM_STATE_XRUN) *revents |= POLLERR;
	return 0;
}
static int l_close(snd_pcm_ioplug_t *io) { lazo_t *r = io->private_data; close(r->fd); free(r->buf); free(r); return 0; }
static const snd_pcm_ioplug_callback_t llamadas = {
	.start = l_start, .stop = l_stop, .pointer = l_pointer, .transfer = l_transfer, .close = l_close,
	.hw_params = l_hw_params, .prepare = l_prepare, .poll_revents = l_poll_revents,
};

SND_PCM_PLUGIN_DEFINE_FUNC(lazo)
{
	snd_config_iterator_t i, next;
	const char *modo = "salida";
	snd_config_for_each(i, next, conf) {
		snd_config_t *n = snd_config_iterator_entry(i);
		const char *id; if (snd_config_get_id(n, &id) < 0) continue;
		if (!strcmp(id, "comment") || !strcmp(id, "type") || !strcmp(id, "hint")) continue;
		if (!strcmp(id, "modo")) { snd_config_get_string(n, &modo); continue; }
		SNDERR("lazo: no se que es %s", id); return -EINVAL;
	}
	int entrada = !strcmp(modo, "entrada");
	if ((stream == SND_PCM_STREAM_CAPTURE) != entrada) return -EINVAL;
	lazo_t *r = calloc(1, sizeof *r);
	if (!r) return -ENOMEM;
	r->entrada = entrada;
	r->recorta = pow(10.0, env_db("LAZO_RECORTA_DB", 0) / 20.0);
	r->ganancia = pow(10.0, env_db("LAZO_GANANCIA_DB", -2) / 20.0);
	r->ruido = pow(10.0, env_db("LAZO_RUIDO_DB", -96) / 20.0);
	r->retraso = (int)(env_db("LAZO_RETRASO_MS", 15) * TASA_ANILLO / 1000);
	r->azar = 0x12345678u ^ (uint32_t)entrada;
	abs_ahora();
	r->fd = timerfd_create(CLOCK_MONOTONIC, TFD_NONBLOCK | TFD_CLOEXEC);
	struct itimerspec ts = { { 0, 2000000 }, { 0, 2000000 } };
	timerfd_settime(r->fd, 0, &ts, NULL);
	r->io.version = SND_PCM_IOPLUG_VERSION;
	r->io.name = entrada ? "lazo de prueba (entrada)" : "lazo de prueba (salida)";
	r->io.callback = &llamadas;
	r->io.private_data = r;
	r->io.poll_fd = r->fd;
	r->io.poll_events = POLLIN;
	r->io.mmap_rw = 0;
	int err = snd_pcm_ioplug_create(&r->io, name, stream, mode);
	if (err < 0) { close(r->fd); free(r); return err; }
	static const unsigned int acceso[] = { SND_PCM_ACCESS_RW_INTERLEAVED };
	static const unsigned int formatos[] = { SND_PCM_FORMAT_S16_LE, SND_PCM_FORMAT_S32_LE, SND_PCM_FORMAT_S24_3LE, SND_PCM_FORMAT_FLOAT_LE };
	snd_pcm_ioplug_set_param_list(&r->io, SND_PCM_IOPLUG_HW_ACCESS, 1, acceso);
	snd_pcm_ioplug_set_param_list(&r->io, SND_PCM_IOPLUG_HW_FORMAT, 4, formatos);
	snd_pcm_ioplug_set_param_minmax(&r->io, SND_PCM_IOPLUG_HW_CHANNELS, 1, 2);
	/* (LAZO_SOLO_TASA: una tarjeta que solo da esa frecuencia, para la salida nueva que se rinde) */
	int solo = (int)env_db("LAZO_SOLO_TASA", 0);
	snd_pcm_ioplug_set_param_minmax(&r->io, SND_PCM_IOPLUG_HW_RATE, solo > 0 ? solo : 8000, solo > 0 ? solo : 192000);
	snd_pcm_ioplug_set_param_minmax(&r->io, SND_PCM_IOPLUG_HW_PERIOD_BYTES, 64, 1 << 20);
	snd_pcm_ioplug_set_param_minmax(&r->io, SND_PCM_IOPLUG_HW_PERIODS, 2, 1024);
	snd_pcm_ioplug_set_param_minmax(&r->io, SND_PCM_IOPLUG_HW_BUFFER_BYTES, 128, 8 << 20);
	*pcmp = r->io.pcm;
	return 0;
}
SND_PCM_PLUGIN_SYMBOL(lazo);
