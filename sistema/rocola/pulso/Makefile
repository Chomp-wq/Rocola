# SPDX-License-Identifier: MIT
# rocola-pulso: nuestra libpulse.so.0 (solo libc). CC y CFLAGS vienen de fuera
# (Buildroot, la compilacion rapida); aqui solo lo que hace falta siempre.
CC ?= gcc
CFLAGS ?= -O2 -g
LDFLAGS ?=
OBJ := anillo.o bucle.o contexto.o flujo.o traza.o utiles.o
DEP := pulso_abi.h pulso_api.h anillo.h interno.h
PROPIAS := -fPIC -pthread -std=gnu11 -Wall -Wextra -Wno-unused-parameter

.PHONY: todo pruebas limpia
todo: libpulse.so.0

%.o: %.c $(DEP)
	$(CC) $(CFLAGS) $(PROPIAS) -c -o $@ $<

libpulse.so.0: $(OBJ) pulso.map
	$(CC) $(CFLAGS) $(LDFLAGS) -shared -fPIC -pthread -Wl,-soname,libpulse.so.0 \
	  -Wl,--version-script,pulso.map -Wl,-z,defs -Wl,--as-needed -o $@ $(OBJ)

pruebas: libpulse.so.0
	$(MAKE) -C prueba todo

limpia:
	rm -f $(OBJ) libpulse.so.0
	-$(MAKE) -C prueba limpia
