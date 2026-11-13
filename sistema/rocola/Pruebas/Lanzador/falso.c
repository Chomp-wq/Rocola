// programa falso para probar el lanzador: apunta como lo llamaron, su entorno y la senal con la que lo cierran
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <signal.h>
#include <unistd.h>
static const char *yo; static volatile int term;
static void apunta(const char *t) { FILE *f = fopen("/tmp/falso.log", "a"); if (f) { fprintf(f, "%s: %s\n", yo, t); fclose(f); } }
static void alterm(int s) { term = 1; }
int main(int c, char **v)
{
	char b[2000]; const char *e;
	yo = strrchr(v[0], '/'); yo = yo ? yo + 1 : v[0];
	if (c > 1 && !strcmp(v[1], "--version")) { printf("%s programa 0 (local) compilado 1700000000\n", yo); return 0; }
	signal(SIGTERM, alterm);
	snprintf(b, sizeof b, "arranca args=[%s] DATOS=%s LETRAS=%s ALSA=%s PANTALLA=%s", c > 1 ? v[1] : "", getenv("ROCOLA_DATOS") ? getenv("ROCOLA_DATOS") : "-",
		getenv("ROCOLA_LETRAS_XP") ? getenv("ROCOLA_LETRAS_XP") : "-", getenv("ROCOLA_ALSA") ? getenv("ROCOLA_ALSA") : "-", getenv("ROCOLA_PANTALLA") ? getenv("ROCOLA_PANTALLA") : "-");
	apunta(b);
	FILE *f = fopen("/tmp/falso.sale", "r"); int sale = -1, dura = 600;
	if (f) { fscanf(f, "%d %d", &sale, &dura); fclose(f); }
	for (int i = 0; i < dura * 10 && !term; i++) usleep(100000);
	if (term) { apunta("SIGTERM (cierre a proposito: borra el estado)"); return 0; }
	snprintf(b, sizeof b, "sale con %d", sale); apunta(b);
	return sale < 0 ? 0 : sale;
}
