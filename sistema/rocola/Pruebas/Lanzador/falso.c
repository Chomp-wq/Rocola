// rocola-xp FALSO para probar el lanzador y los guiones del sistema: apunta en /tmp/falso.log como lo llamaron (con la hora, en
// milesimas desde que prendio la maquina: para medir las esperas), su entorno y la senal que lo cierra.
//   --version              la linea de rocola-xp («rocola-xp programa 0 (local) compilado 1700000000») y sale con 0
//   --importa-xp ARCHIVO   el importador: apunta «importa ...» y, con /tmp/falso.importa = «CODIGO SEGUNDOS», tarda esos segundos y sale
//                          con ese codigo (sin el archivo: 0 enseguida). No atiende senales (SIGTERM o SIGUSR1 lo cortan).
//   (sin nada)             la rocola: /tmp/falso.sale = «CODIGO SEGUNDOS» (sin el archivo: 600 s y 0); /tmp/falso.cierra = CODIGO la
//                          cierra en ese momento con ese codigo (como Alt+F4 o una caida, cuando la prueba quiere). SIGUSR1 (los reinicios
//                          propios de Rocola OS), como rocola-xp: cierre ordenado que conserva el estado, sale con 0; con /tmp/falso.usr1
//                          = «muere» le llega antes de atenderla (el arranque): lo corta, 138; «sordo» = no se cierra (lo cortan a los
//                          20 s). SIGTERM (el apagado): sale con 0 («borra el estado»).
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <signal.h>
#include <time.h>
#include <unistd.h>
static const char *yo; static volatile int term, usr1;
static long ms(void) { struct timespec t; clock_gettime(CLOCK_MONOTONIC, &t); return t.tv_sec * 1000L + t.tv_nsec / 1000000L; }
static void apunta(const char *t) { FILE *f = fopen("/tmp/falso.log", "a"); if (f) { fprintf(f, "%s: [%ld] %s\n", yo, ms(), t); fclose(f); } }
static void alterm(int s) { term = 1; }
static void alusr1(int s) { usr1 = 1; }
static const char *env(const char *n) { const char *v = getenv(n); return v ? v : "-"; }
int main(int c, char **v)
{
	char b[3000];
	yo = strrchr(v[0], '/'); yo = yo ? yo + 1 : v[0];
	if (c > 1 && !strcmp(v[1], "--version")) { printf("%s programa 0 (local) compilado 1700000000\n", yo); return 0; }
	if (c > 1 && !strcmp(v[1], "--importa-xp")) {
		snprintf(b, sizeof b, "importa args=[%s %s] DATOS=%s IA=%s", v[1], c > 2 ? v[2] : "", env("ROCOLA_DATOS"), env("ROCOLA_IA_DATOS"));
		apunta(b);
		FILE *f = fopen("/tmp/falso.importa", "r"); int sale = 0, dura = 0;
		if (f) { fscanf(f, "%d %d", &sale, &dura); fclose(f); }
		sleep(dura);
		snprintf(b, sizeof b, "importa sale con %d", sale); apunta(b);
		return sale;
	}
	signal(SIGTERM, alterm);
	{ char m[20] = ""; FILE *u = fopen("/tmp/falso.usr1", "r"); if (u) { fscanf(u, "%19s", m); fclose(u); }
	  if (!strcmp(m, "sordo")) signal(SIGUSR1, SIG_IGN); else if (strcmp(m, "muere")) signal(SIGUSR1, alusr1); }
	snprintf(b, sizeof b, "arranca args=[%s] DATOS=%s LETRAS=%s ALSA=%s PANTALLA=%s IA=%s", c > 1 ? v[1] : "", env("ROCOLA_DATOS"),
		env("ROCOLA_LETRAS_XP"), env("ROCOLA_ALSA"), env("ROCOLA_PANTALLA"), env("ROCOLA_IA_DATOS"));
	apunta(b);
	FILE *f = fopen("/tmp/falso.sale", "r"); int sale = 0, dura = 600, cierra = -1;
	if (f) { fscanf(f, "%d %d", &sale, &dura); fclose(f); }
	for (int i = 0; i < dura * 10 && !term && !usr1; i++) {
		usleep(100000);
		FILE *k = fopen("/tmp/falso.cierra", "r");
		if (k) { fscanf(k, "%d", &cierra); fclose(k); unlink("/tmp/falso.cierra"); break; }
	}
	if (usr1) { apunta("SIGUSR1 (cierre ordenado: conserva el estado), sale con 0"); return 0; }
	if (term) { apunta("SIGTERM (cierre a proposito: borra el estado)"); return 0; }
	if (cierra >= 0) sale = cierra;
	snprintf(b, sizeof b, "sale con %d", sale); apunta(b);
	return sale;
}
