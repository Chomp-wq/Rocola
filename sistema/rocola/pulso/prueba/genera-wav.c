/* SPDX-License-Identifier: MIT
 * Pruebas de rocola-pulso: un WAV con la senal con contador (senal.h), para
 * pasarlo por clientes de verdad (paplay, mpv) y comprobar lo que llega.
 *   genera-wav salida.wav frecuencia segundos [s16|float] */
#include "senal.h"
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

static void u32(FILE *f, uint32_t v) { fputc(v, f); fputc(v >> 8, f); fputc(v >> 16, f); fputc(v >> 24, f); }
static void u16(FILE *f, uint16_t v) { fputc(v, f); fputc(v >> 8, f); }

int main(int argc, char **argv)
{
    if (argc < 4)
        return 2;
    uint32_t rate = (uint32_t)atoi(argv[2]);
    uint32_t n = (uint32_t)(atof(argv[3]) * rate);
    int flt = argc > 4 && !strcmp(argv[4], "float");
    pa_sample_format_t fmt = flt ? PA_SAMPLE_FLOAT32LE : PA_SAMPLE_S16LE;
    uint32_t tm = flt ? 4 : 2, bloque = tm * 2;
    FILE *f = fopen(argv[1], "wb");
    if (!f)
        return 1;
    fwrite("RIFF", 1, 4, f); u32(f, 36 + n * bloque); fwrite("WAVEfmt ", 1, 8, f);
    u32(f, 16); u16(f, flt ? 3 : 1); u16(f, 2); u32(f, rate); u32(f, rate * bloque); u16(f, (uint16_t)bloque); u16(f, (uint16_t)(tm * 8));
    fwrite("data", 1, 4, f); u32(f, n * bloque);
    uint8_t buf[4096 * 8];
    for (uint32_t k = 0; k < n; k += 4096) {
        uint32_t c = n - k < 4096 ? n - k : 4096;
        senal_llena(buf, 7000 + k, c, fmt, 2, 0, 1);
        fwrite(buf, bloque, c, f);
    }
    fclose(f);
    return 0;
}
