// con-terminal PROGRAMA [ARGUMENTOS]: corre PROGRAMA en una sesion nueva con una pseudoterminal como TERMINAL DE CONTROL, como abre
// S99rocola el lanzador en la rocola (setsid sh -c 'exec /usr/bin/rocola </dev/tty1 >/dev/tty1 2>&1': la sesion nueva toma tty1). Asi, al
// morir el lanzador, el nucleo le manda SIGHUP al grupo de primer plano de esa terminal (el del lanzador, donde va rocola-xp), como en
// la rocola (alli cuelga tty1; con una pseudoterminal el nucleo manda la misma SIGHUP). Sin esto (</dev/null) la prueba no lo ve.
#define _XOPEN_SOURCE 600
#include <fcntl.h>
#include <stdlib.h>
#include <unistd.h>
#include <sys/wait.h>
int main(int c, char **v)
{
	if (c < 2) return 2;
	int m = posix_openpt(O_RDWR | O_NOCTTY);
	if (m < 0 || grantpt(m) || unlockpt(m)) return 3;
	char *n = ptsname(m);
	pid_t p = fork();
	if (p < 0) return 4;
	if (p == 0) {
		setsid();
		int s = open(n, O_RDWR);   // sin O_NOCTTY: la terminal de control de la sesion nueva
		if (s < 0) _exit(5);
		dup2(s, 0); dup2(s, 1); dup2(s, 2);
		if (s > 2) close(s);
		close(m);
		execvp(v[1], v + 1);
		_exit(6);
	}
	// lo que sale por la terminal lo lee (y lo tira) otro proceso, hasta que nadie la tiene abierta (las «sleep» que dejan los
	// vigilantes del lanzador la tienen un rato); este recoge al lanzador en cuanto acaba (si no, quedaria zombi y la prueba lo contaria)
	if (fork() == 0) {
		char b[4096];
		while (read(m, b, sizeof b) > 0) {}
		_exit(0);
	}
	close(m);
	waitpid(p, NULL, 0);
	return 0;
}
