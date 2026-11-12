/* PRUEBA (no es de XP): una libGLESv2 que reenvia todo a la de Mesa (copiada como libGLESr2, gl_falla.sh) salvo glGetShaderiv: mientras
   exista el archivo que dice la variable F12_FALLA_FLAG, contesta que ningun sombreador compila (GL_COMPILE_STATUS = 0). Asi el dispositivo de
   la ventana F12 no se crea (SombreadoresTarjeta.Compila da null), igual en la rocola de la referencia y en la traducida: la escena f12fallo. */
#define _GNU_SOURCE
#include <dlfcn.h>
#include <stdlib.h>
#include <unistd.h>
typedef void (*fn)(unsigned, unsigned, int *);
void glGetShaderiv(unsigned s, unsigned p, int *v)
{
    static fn real; if (!real) real = (fn)dlsym(RTLD_NEXT, "glGetShaderiv");
    real(s, p, v);
    const char *f = getenv("F12_FALLA_FLAG");
    if (p == 0x8B81 && f && access(f, F_OK) == 0) *v = 0;
}
