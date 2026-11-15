/* SOLO PARA LA PRUEBA EN QEMU (sistema/ACT_qemu_spotify.md; no va en Rocola OS): reenvia conexiones TCP de 127.0.0.1:PUERTO, dentro de la
 * maquina, a DESTINO:PUERTO (la direccion que QEMU, con su red de usuario y guestfwd, lleva a un servidor de mentira del anfitrion). Asi
 * rocola-xp, el vigilante y curl hablan con los falsos (la API web, el CDN de Soloist) en http://127.0.0.1, como en las demas pruebas.
 *   reenvia DESTINO PUERTO [PUERTO...]   (un proceso por puerto; uno hijo por conexion)
 * Se compila estatico en el anfitrion: gcc -static -O2 -o reenvia reenvia.c */
#include <arpa/inet.h>
#include <netinet/in.h>
#include <poll.h>
#include <signal.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <sys/socket.h>
#include <sys/wait.h>
#include <unistd.h>

static void copia(int a, int b) {
	struct pollfd p[2] = {{a, POLLIN, 0}, {b, POLLIN, 0}};
	char buf[65536];
	int abiertos = 2;
	while (abiertos > 0 && poll(p, 2, -1) > 0) {
		for (int i = 0; i < 2; i++) {
			if (p[i].fd < 0 || !(p[i].revents & (POLLIN | POLLHUP | POLLERR))) continue;
			ssize_t n = read(p[i].fd, buf, sizeof buf);
			int otro = i == 0 ? b : a;
			if (n <= 0) { shutdown(otro, SHUT_WR); p[i].fd = -1; abiertos--; continue; }
			for (ssize_t h = 0; h < n;) { ssize_t w = write(otro, buf + h, n - h); if (w <= 0) return; h += w; }
		}
	}
}

static void escucha(const char *destino, int puerto) {
	int s = socket(AF_INET, SOCK_STREAM, 0), uno = 1;
	setsockopt(s, SOL_SOCKET, SO_REUSEADDR, &uno, sizeof uno);
	struct sockaddr_in a = {0}; a.sin_family = AF_INET; a.sin_port = htons(puerto); a.sin_addr.s_addr = htonl(INADDR_LOOPBACK);
	if (bind(s, (struct sockaddr *)&a, sizeof a) < 0 || listen(s, 64) < 0) { perror("reenvia: bind"); exit(1); }
	for (;;) {
		int c = accept(s, NULL, NULL);
		if (c < 0) continue;
		if (fork() == 0) {
			close(s);
			int d = socket(AF_INET, SOCK_STREAM, 0);
			struct sockaddr_in b = {0}; b.sin_family = AF_INET; b.sin_port = htons(puerto); inet_pton(AF_INET, destino, &b.sin_addr);
			if (connect(d, (struct sockaddr *)&b, sizeof b) == 0) copia(c, d);
			_exit(0);
		}
		close(c);
	}
}

int main(int argc, char **argv) {
	if (argc < 3) { fprintf(stderr, "uso: reenvia DESTINO PUERTO [PUERTO...]\n"); return 2; }
	signal(SIGCHLD, SIG_IGN); signal(SIGPIPE, SIG_IGN);
	for (int i = 2; i < argc; i++) if (fork() == 0) escucha(argv[1], atoi(argv[i]));
	for (;;) pause();
}
