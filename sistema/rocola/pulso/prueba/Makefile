# SPDX-License-Identifier: MIT
# Pruebas de rocola-pulso (make -C .. pruebas). VARIANTE= tsan | asan | (nada)
CC ?= gcc
CFLAGS ?= -O1 -g
B := bin$(if $(VARIANTE),-$(VARIANTE),)
ifeq ($(VARIANTE),tsan)
SAN := -fsanitize=thread
endif
ifeq ($(VARIANTE),asan)
SAN := -fsanitize=address,undefined -fno-sanitize-recover=undefined -fno-omit-frame-pointer
endif
PROPIAS := -pthread -std=gnu11 -Wall -Wextra -Wno-unused-parameter -Wno-unused-function $(SAN)
LIBSRC := $(wildcard ../*.c)
DEP := ../pulso_abi.h ../pulso_api.h ../anillo.h ../interno.h senal.h comun.h soloist48.h

.PHONY: todo limpia
todo: $(B)/libpulse.so.0 $(B)/lector $(B)/cliente $(B)/raro $(B)/abi-nuestro $(B)/abi-real $(B)/genera-wav

$(B):
	mkdir -p $(B)

$(B)/libpulse.so.0: $(LIBSRC) $(DEP) ../pulso.map | $(B)
	$(CC) $(CFLAGS) $(PROPIAS) -fPIC -shared -Wl,-soname,libpulse.so.0 -Wl,--version-script,../pulso.map -o $@ $(LIBSRC)

$(B)/lector: lector.c ../anillo.c ../traza.c $(DEP) | $(B)
	$(CC) $(CFLAGS) $(PROPIAS) -o $@ lector.c ../anillo.c ../traza.c -lm

$(B)/cliente: cliente.c ../anillo.c ../traza.c $(DEP) | $(B)
	$(CC) $(CFLAGS) $(PROPIAS) -o $@ cliente.c ../anillo.c ../traza.c -ldl -lm

$(B)/raro: raro.c ../anillo.c ../traza.c $(DEP) $(B)/libpulse.so.0 | $(B)
	$(CC) $(CFLAGS) $(PROPIAS) -o $@ raro.c ../anillo.c ../traza.c -L$(B) -l:libpulse.so.0 -Wl,-rpath,'$$ORIGIN' -lm

$(B)/abi-nuestro: abi.c ../pulso_abi.h | $(B)
	$(CC) -O0 -DNUESTRO -o $@ abi.c

$(B)/abi-real: abi.c | $(B)
	$(CC) -O0 -o $@ abi.c

$(B)/genera-wav: genera-wav.c senal.h | $(B)
	$(CC) -O1 -o $@ genera-wav.c -lm

limpia:
	rm -rf bin bin-tsan bin-asan
