/* UNA TARJETA DE PRUEBA CON RELOJ (port nativo de XP, pista windows; solo pruebas, fuera del programa).
 * Plugin externo de ALSA (ioplug): suena a su ritmo, como una tarjeta de verdad (el puntero avanza con el reloj del sistema a la frecuencia
 * puesta), y guarda en un archivo crudo lo que de verdad SONO (lo que se tira con drop no se guarda). Si el programa no le da a tiempo, se
 * queda sin nada (XRUN) como una tarjeta. En otro archivo (registro) apunta cada arranque, parada, pausa y XRUN con la hora y lo sonado.
 *   pcm_type.reloj { lib "/ruta/libasound_module_pcm_reloj.so" }
 *   pcm.reloj { type reloj  file "/ruta/sonado.raw"  registro "/ruta/registro.txt" }
 * gcc -shared -fPIC -O2 -o libasound_module_pcm_reloj.so reloj.c -lasound
 */
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <stdint.h>
#include <time.h>
#include <errno.h>
#include <unistd.h>
#include <poll.h>
#include <sys/timerfd.h>
#include <alsa/asoundlib.h>
#include <alsa/pcm_external.h>

typedef struct {
	snd_pcm_ioplug_t io;
	int fd;
	FILE *salida, *registro;
	unsigned char *anillo;          /* lo dado y aun no sonado (buffer_size cuadros) */
	size_t bpc;                     /* bytes por cuadro */
	uint64_t dados, sonados;        /* desde el ultimo prepare */
	uint64_t total_sonados;         /* desde que se abrio */
	struct timespec t0;             /* cuando empezo (o siguio tras la pausa) */
	uint64_t sonados_t0;            /* lo sonado hasta t0 */
	int corriendo, pausa;
} reloj_t;

static void apunta(reloj_t *r, const char *que)
{
	if (!r->registro) return;
	struct timespec t; clock_gettime(CLOCK_REALTIME, &t);
	fprintf(r->registro, "%ld.%03ld %s dados=%llu sonados=%llu total=%llu rate=%u ch=%u fmt=%d buf=%lu per=%lu\n", (long)t.tv_sec, t.tv_nsec / 1000000, que,
		(unsigned long long)r->dados, (unsigned long long)r->sonados, (unsigned long long)r->total_sonados,
		r->io.rate, r->io.channels, (int)r->io.format, (unsigned long)r->io.buffer_size, (unsigned long)r->io.period_size);
	fflush(r->registro);
}
/* lo que ya debio sonar segun el reloj (sin pasar de lo dado); lo sonado se escribe al archivo */
static int avanza(reloj_t *r)
{
	if (!r->corriendo || r->pausa) return 0;
	struct timespec t; clock_gettime(CLOCK_MONOTONIC, &t);
	double dt = (t.tv_sec - r->t0.tv_sec) + (t.tv_nsec - r->t0.tv_nsec) / 1e9;
	uint64_t debe = r->sonados_t0 + (uint64_t)(dt * r->io.rate);
	int xrun = 0;
	if (debe > r->dados) { debe = r->dados; xrun = 1; }
	while (r->sonados < debe) {
		uint64_t i = r->sonados % r->io.buffer_size;
		uint64_t n = debe - r->sonados;
		if (n > r->io.buffer_size - i) n = r->io.buffer_size - i;
		if (r->salida) fwrite(r->anillo + i * r->bpc, r->bpc, n, r->salida);
		r->sonados += n; r->total_sonados += n;
	}
	if (xrun) { apunta(r, "XRUN"); r->corriendo = 0; return -EPIPE; }
	return 0;
}
static snd_pcm_sframes_t r_pointer(snd_pcm_ioplug_t *io)
{
	reloj_t *r = io->private_data;
	if (avanza(r) < 0) return -EPIPE;
	return (snd_pcm_sframes_t)(r->sonados % io->buffer_size);
}
static snd_pcm_sframes_t r_transfer(snd_pcm_ioplug_t *io, const snd_pcm_channel_area_t *areas, snd_pcm_uframes_t offset, snd_pcm_uframes_t size)
{
	reloj_t *r = io->private_data;
	const unsigned char *src = (const unsigned char *)areas->addr + (areas->first + areas->step * offset) / 8;
	for (snd_pcm_uframes_t k = 0; k < size; k++) {
		uint64_t i = (r->dados + k) % io->buffer_size;
		memcpy(r->anillo + i * r->bpc, src + k * r->bpc, r->bpc);
	}
	r->dados += size;
	return size;
}
static int r_start(snd_pcm_ioplug_t *io)
{
	reloj_t *r = io->private_data;
	clock_gettime(CLOCK_MONOTONIC, &r->t0); r->sonados_t0 = r->sonados; r->corriendo = 1; r->pausa = 0;
	apunta(r, "ARRANCA");
	return 0;
}
static int r_stop(snd_pcm_ioplug_t *io)
{
	reloj_t *r = io->private_data;
	if (r->corriendo) avanza(r);
	apunta(r, "PARA");
	r->corriendo = 0;
	return 0;
}
static int r_pause(snd_pcm_ioplug_t *io, int enable)
{
	reloj_t *r = io->private_data;
	if (enable) { avanza(r); r->pausa = 1; apunta(r, "PAUSA"); }
	else { clock_gettime(CLOCK_MONOTONIC, &r->t0); r->sonados_t0 = r->sonados; r->pausa = 0; apunta(r, "SIGUE"); }
	return 0;
}
static int r_prepare(snd_pcm_ioplug_t *io)
{
	reloj_t *r = io->private_data;
	r->dados = r->sonados = 0; r->corriendo = 0; r->pausa = 0;
	apunta(r, "PREPARA");
	return 0;
}
static int r_hw_params(snd_pcm_ioplug_t *io, snd_pcm_hw_params_t *params)
{
	reloj_t *r = io->private_data;
	r->bpc = snd_pcm_format_physical_width(io->format) / 8 * io->channels;
	free(r->anillo);
	r->anillo = calloc(io->buffer_size, r->bpc);
	return r->anillo ? 0 : -ENOMEM;
}
static int r_poll_revents(snd_pcm_ioplug_t *io, struct pollfd *pfd, unsigned int nfds, unsigned short *revents)
{
	reloj_t *r = io->private_data;
	uint64_t x; if (pfd[0].revents & POLLIN) { ssize_t n = read(r->fd, &x, sizeof x); (void)n; }
	avanza(r);
	snd_pcm_uframes_t libres = io->buffer_size - (snd_pcm_uframes_t)(r->dados - r->sonados);
	snd_pcm_uframes_t minimo = 1;
	snd_pcm_sw_params_t *sw; snd_pcm_sw_params_alloca(&sw);
	if (snd_pcm_sw_params_current(io->pcm, sw) == 0) snd_pcm_sw_params_get_avail_min(sw, &minimo);
	*revents = (libres >= minimo || !r->corriendo) ? POLLOUT : 0;
	if (io->state == SND_PCM_STATE_XRUN) *revents |= POLLERR;
	return 0;
}
static int r_close(snd_pcm_ioplug_t *io)
{
	reloj_t *r = io->private_data;
	apunta(r, "CIERRA");
	if (r->salida) fclose(r->salida);
	if (r->registro) fclose(r->registro);
	close(r->fd); free(r->anillo); free(r);
	return 0;
}
static const snd_pcm_ioplug_callback_t llamadas = {
	.start = r_start, .stop = r_stop, .pointer = r_pointer, .transfer = r_transfer, .close = r_close,
	.hw_params = r_hw_params, .prepare = r_prepare, .pause = r_pause, .poll_revents = r_poll_revents,
};

SND_PCM_PLUGIN_DEFINE_FUNC(reloj)
{
	snd_config_iterator_t i, next;
	const char *archivo = NULL, *registro = NULL;
	snd_config_for_each(i, next, conf) {
		snd_config_t *n = snd_config_iterator_entry(i);
		const char *id; if (snd_config_get_id(n, &id) < 0) continue;
		if (!strcmp(id, "comment") || !strcmp(id, "type") || !strcmp(id, "hint")) continue;
		if (!strcmp(id, "file")) { snd_config_get_string(n, &archivo); continue; }
		if (!strcmp(id, "registro")) { snd_config_get_string(n, &registro); continue; }
		SNDERR("reloj: no se que es %s", id); return -EINVAL;
	}
	if (stream != SND_PCM_STREAM_PLAYBACK) return -EINVAL;
	reloj_t *r = calloc(1, sizeof *r);
	if (!r) return -ENOMEM;
	if (archivo) r->salida = fopen(archivo, "ab");
	if (registro) r->registro = fopen(registro, "a");
	r->fd = timerfd_create(CLOCK_MONOTONIC, TFD_NONBLOCK | TFD_CLOEXEC);
	struct itimerspec ts = { { 0, 2000000 }, { 0, 2000000 } };
	timerfd_settime(r->fd, 0, &ts, NULL);
	r->io.version = SND_PCM_IOPLUG_VERSION;
	r->io.name = "reloj de prueba";
	r->io.callback = &llamadas;
	r->io.private_data = r;
	r->io.poll_fd = r->fd;
	r->io.poll_events = POLLIN;
	r->io.mmap_rw = 0;
	int err = snd_pcm_ioplug_create(&r->io, name, stream, mode);
	if (err < 0) { if (r->salida) fclose(r->salida); if (r->registro) fclose(r->registro); close(r->fd); free(r); return err; }
	static const unsigned int acceso[] = { SND_PCM_ACCESS_RW_INTERLEAVED };
	static const unsigned int formatos[] = { SND_PCM_FORMAT_S16_LE, SND_PCM_FORMAT_FLOAT_LE, SND_PCM_FORMAT_S32_LE, SND_PCM_FORMAT_U8, SND_PCM_FORMAT_S24_3LE };
	snd_pcm_ioplug_set_param_list(&r->io, SND_PCM_IOPLUG_HW_ACCESS, 1, acceso);
	snd_pcm_ioplug_set_param_list(&r->io, SND_PCM_IOPLUG_HW_FORMAT, 5, formatos);
	snd_pcm_ioplug_set_param_minmax(&r->io, SND_PCM_IOPLUG_HW_CHANNELS, 1, 8);
	snd_pcm_ioplug_set_param_minmax(&r->io, SND_PCM_IOPLUG_HW_RATE, 8000, 192000);
	snd_pcm_ioplug_set_param_minmax(&r->io, SND_PCM_IOPLUG_HW_PERIOD_BYTES, 64, 1 << 20);
	snd_pcm_ioplug_set_param_minmax(&r->io, SND_PCM_IOPLUG_HW_PERIODS, 2, 1024);
	snd_pcm_ioplug_set_param_minmax(&r->io, SND_PCM_IOPLUG_HW_BUFFER_BYTES, 128, 8 << 20);
	*pcmp = r->io.pcm;
	return 0;
}
SND_PCM_PLUGIN_SYMBOL(reloj);
