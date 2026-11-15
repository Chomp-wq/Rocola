/* SPDX-License-Identifier: MIT
 * Pruebas de rocola-pulso: la senal con contador. El cuadro k lleva
 * a = k mod 2^b en el canal izquierdo y c = (k >> b) mod 2^b en el derecho
 * (b = 16, u 8 con U8), asi el lector sabe que cuadro le llego y si vienen
 * seguidos. Los otros canales llevan basura, que no debe llegar al anillo. */
#ifndef SENAL_H
#define SENAL_H
#include <math.h>
#include <stdint.h>
#include <string.h>
#include "../pulso_abi.h"

static inline int senal_bits(pa_sample_format_t f) { return f == PA_SAMPLE_U8 ? 8 : 16; }

/* el valor con signo del canal (16 bits o 8 bits) */
static inline int32_t senal_valor(uint64_t k, int derecho, int bits)
{
    uint64_t m = (1ull << bits) - 1;
    uint64_t v = derecho ? (k >> bits) & m : k & m;
    return (int32_t)v - (int32_t)(1u << (bits - 1));
}

static inline void pon16(uint8_t *p, uint16_t v, int be) { if (be) { p[0] = v >> 8; p[1] = v & 0xff; } else { p[0] = v & 0xff; p[1] = v >> 8; } }
static inline void pon32(uint8_t *p, uint32_t v, int be)
{
    if (be) { p[0] = v >> 24; p[1] = v >> 16; p[2] = v >> 8; p[3] = v; }
    else { p[0] = v; p[1] = v >> 8; p[2] = v >> 16; p[3] = v >> 24; }
}

/* una muestra de 16 bits (o 8) en el formato del cliente */
static inline void senal_muestra(uint8_t *p, int32_t v, pa_sample_format_t f)
{
    switch (f) {
    case PA_SAMPLE_U8: p[0] = (uint8_t)(v + 128); break;
    case PA_SAMPLE_S16LE: pon16(p, (uint16_t)(int16_t)v, 0); break;
    case PA_SAMPLE_S16BE: pon16(p, (uint16_t)(int16_t)v, 1); break;
    case PA_SAMPLE_S32LE: pon32(p, (uint32_t)(v * 65536), 0); break;
    case PA_SAMPLE_S32BE: pon32(p, (uint32_t)(v * 65536), 1); break;
    case PA_SAMPLE_S24LE: { uint32_t u = (uint32_t)(v * 256); p[0] = u; p[1] = u >> 8; p[2] = u >> 16; break; }
    case PA_SAMPLE_S24BE: { uint32_t u = (uint32_t)(v * 256); p[2] = u; p[1] = u >> 8; p[0] = u >> 16; break; }
    case PA_SAMPLE_S24_32LE: pon32(p, (uint32_t)(v * 256) & 0x00ffffffu, 0); break;
    case PA_SAMPLE_S24_32BE: pon32(p, (uint32_t)(v * 256) & 0x00ffffffu, 1); break;
    case PA_SAMPLE_FLOAT32LE: case PA_SAMPLE_FLOAT32BE: {
        float x = (float)v / 32768.0f;
        uint32_t u;
        memcpy(&u, &x, 4);
        pon32(p, u, f == PA_SAMPLE_FLOAT32BE);
        break;
    }
    default: break;
    }
}

static inline size_t senal_tam(pa_sample_format_t f)
{
    switch (f) {
    case PA_SAMPLE_U8: return 1;
    case PA_SAMPLE_S16LE: case PA_SAMPLE_S16BE: return 2;
    case PA_SAMPLE_S24LE: case PA_SAMPLE_S24BE: return 3;
    default: return 4;
    }
}

/* llena n cuadros desde k0; ci y cd son los canales izquierdo y derecho */
static inline void senal_llena(uint8_t *dst, uint64_t k0, size_t n, pa_sample_format_t f, int canales, int ci, int cd)
{
    size_t tm = senal_tam(f);
    int bits = senal_bits(f);
    for (size_t i = 0; i < n; i++) {
        uint64_t k = k0 + i;
        for (int c = 0; c < canales; c++) {
            int32_t v;
            if (canales == 1)
                v = senal_valor(k, 0, bits);
            else if (c == ci)
                v = senal_valor(k, 0, bits);
            else if (c == cd)
                v = senal_valor(k, 1, bits);
            else
                v = bits == 8 ? 77 : 12345 + c; /* basura */
            senal_muestra(dst + (i * (size_t)canales + (size_t)c) * tm, v, f);
        }
    }
}

/* lo que el lector espera ver del valor v en float */
static inline float senal_float(int32_t v, int bits) { return bits == 8 ? (float)v / 128.0f : (float)v / 32768.0f; }

/* del float al valor (o -100000 si no es uno de la senal) */
static inline int32_t senal_de_float(float x, int bits)
{
    float esc = bits == 8 ? 128.0f : 32768.0f;
    float y = x * esc;
    int32_t v = (int32_t)lrintf(y);
    if ((float)v != y)
        return -100000;
    return v;
}
#endif
