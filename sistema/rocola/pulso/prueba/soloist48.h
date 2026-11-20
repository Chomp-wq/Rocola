/* Generado de soloist-funciones.txt: las 48 funciones que Soloist busca con
 * dlsym en libpulse.so.0. El cliente de prueba las toma igual (dlopen + dlsym)
 * y cuenta cuantas veces llama a cada una. */
#ifndef SOLOIST48_H
#define SOLOIST48_H
#include <dlfcn.h>
#include <stdio.h>
#include "../pulso_api.h"
enum {
    K_pa_context_connect,
    K_pa_context_disconnect,
    K_pa_context_errno,
    K_pa_context_get_sink_input_info,
    K_pa_context_get_state,
    K_pa_context_new,
    K_pa_context_set_sink_input_volume,
    K_pa_context_set_state_callback,
    K_pa_context_set_subscribe_callback,
    K_pa_context_subscribe,
    K_pa_context_unref,
    K_pa_cvolume_avg,
    K_pa_cvolume_set,
    K_pa_gettimeofday,
    K_pa_operation_get_state,
    K_pa_operation_unref,
    K_pa_proplist_free,
    K_pa_proplist_new,
    K_pa_proplist_sets,
    K_pa_stream_connect_playback,
    K_pa_stream_cork,
    K_pa_stream_disconnect,
    K_pa_stream_flush,
    K_pa_stream_get_index,
    K_pa_stream_get_state,
    K_pa_stream_get_timing_info,
    K_pa_stream_is_corked,
    K_pa_stream_new,
    K_pa_stream_new_with_proplist,
    K_pa_stream_set_started_callback,
    K_pa_stream_set_state_callback,
    K_pa_stream_set_underflow_callback,
    K_pa_stream_unref,
    K_pa_stream_update_timing_info,
    K_pa_stream_writable_size,
    K_pa_stream_write,
    K_pa_strerror,
    K_pa_threaded_mainloop_free,
    K_pa_threaded_mainloop_get_api,
    K_pa_threaded_mainloop_lock,
    K_pa_threaded_mainloop_new,
    K_pa_threaded_mainloop_signal,
    K_pa_threaded_mainloop_start,
    K_pa_threaded_mainloop_stop,
    K_pa_threaded_mainloop_unlock,
    K_pa_threaded_mainloop_wait,
    K_pa_timeval_diff,
    K_pa_usec_to_bytes,
    K_TOTAL
};
static const char *const nombres48[K_TOTAL] = {"pa_context_connect", "pa_context_disconnect", "pa_context_errno", "pa_context_get_sink_input_info", "pa_context_get_state", "pa_context_new", "pa_context_set_sink_input_volume", "pa_context_set_state_callback", "pa_context_set_subscribe_callback", "pa_context_subscribe", "pa_context_unref", "pa_cvolume_avg", "pa_cvolume_set", "pa_gettimeofday", "pa_operation_get_state", "pa_operation_unref", "pa_proplist_free", "pa_proplist_new", "pa_proplist_sets", "pa_stream_connect_playback", "pa_stream_cork", "pa_stream_disconnect", "pa_stream_flush", "pa_stream_get_index", "pa_stream_get_state", "pa_stream_get_timing_info", "pa_stream_is_corked", "pa_stream_new", "pa_stream_new_with_proplist", "pa_stream_set_started_callback", "pa_stream_set_state_callback", "pa_stream_set_underflow_callback", "pa_stream_unref", "pa_stream_update_timing_info", "pa_stream_writable_size", "pa_stream_write", "pa_strerror", "pa_threaded_mainloop_free", "pa_threaded_mainloop_get_api", "pa_threaded_mainloop_lock", "pa_threaded_mainloop_new", "pa_threaded_mainloop_signal", "pa_threaded_mainloop_start", "pa_threaded_mainloop_stop", "pa_threaded_mainloop_unlock", "pa_threaded_mainloop_wait", "pa_timeval_diff", "pa_usec_to_bytes"};
static unsigned long cuenta48[K_TOTAL];
static __typeof__(pa_context_connect) *P_pa_context_connect;
static __typeof__(pa_context_disconnect) *P_pa_context_disconnect;
static __typeof__(pa_context_errno) *P_pa_context_errno;
static __typeof__(pa_context_get_sink_input_info) *P_pa_context_get_sink_input_info;
static __typeof__(pa_context_get_state) *P_pa_context_get_state;
static __typeof__(pa_context_new) *P_pa_context_new;
static __typeof__(pa_context_set_sink_input_volume) *P_pa_context_set_sink_input_volume;
static __typeof__(pa_context_set_state_callback) *P_pa_context_set_state_callback;
static __typeof__(pa_context_set_subscribe_callback) *P_pa_context_set_subscribe_callback;
static __typeof__(pa_context_subscribe) *P_pa_context_subscribe;
static __typeof__(pa_context_unref) *P_pa_context_unref;
static __typeof__(pa_cvolume_avg) *P_pa_cvolume_avg;
static __typeof__(pa_cvolume_set) *P_pa_cvolume_set;
static __typeof__(pa_gettimeofday) *P_pa_gettimeofday;
static __typeof__(pa_operation_get_state) *P_pa_operation_get_state;
static __typeof__(pa_operation_unref) *P_pa_operation_unref;
static __typeof__(pa_proplist_free) *P_pa_proplist_free;
static __typeof__(pa_proplist_new) *P_pa_proplist_new;
static __typeof__(pa_proplist_sets) *P_pa_proplist_sets;
static __typeof__(pa_stream_connect_playback) *P_pa_stream_connect_playback;
static __typeof__(pa_stream_cork) *P_pa_stream_cork;
static __typeof__(pa_stream_disconnect) *P_pa_stream_disconnect;
static __typeof__(pa_stream_flush) *P_pa_stream_flush;
static __typeof__(pa_stream_get_index) *P_pa_stream_get_index;
static __typeof__(pa_stream_get_state) *P_pa_stream_get_state;
static __typeof__(pa_stream_get_timing_info) *P_pa_stream_get_timing_info;
static __typeof__(pa_stream_is_corked) *P_pa_stream_is_corked;
static __typeof__(pa_stream_new) *P_pa_stream_new;
static __typeof__(pa_stream_new_with_proplist) *P_pa_stream_new_with_proplist;
static __typeof__(pa_stream_set_started_callback) *P_pa_stream_set_started_callback;
static __typeof__(pa_stream_set_state_callback) *P_pa_stream_set_state_callback;
static __typeof__(pa_stream_set_underflow_callback) *P_pa_stream_set_underflow_callback;
static __typeof__(pa_stream_unref) *P_pa_stream_unref;
static __typeof__(pa_stream_update_timing_info) *P_pa_stream_update_timing_info;
static __typeof__(pa_stream_writable_size) *P_pa_stream_writable_size;
static __typeof__(pa_stream_write) *P_pa_stream_write;
static __typeof__(pa_strerror) *P_pa_strerror;
static __typeof__(pa_threaded_mainloop_free) *P_pa_threaded_mainloop_free;
static __typeof__(pa_threaded_mainloop_get_api) *P_pa_threaded_mainloop_get_api;
static __typeof__(pa_threaded_mainloop_lock) *P_pa_threaded_mainloop_lock;
static __typeof__(pa_threaded_mainloop_new) *P_pa_threaded_mainloop_new;
static __typeof__(pa_threaded_mainloop_signal) *P_pa_threaded_mainloop_signal;
static __typeof__(pa_threaded_mainloop_start) *P_pa_threaded_mainloop_start;
static __typeof__(pa_threaded_mainloop_stop) *P_pa_threaded_mainloop_stop;
static __typeof__(pa_threaded_mainloop_unlock) *P_pa_threaded_mainloop_unlock;
static __typeof__(pa_threaded_mainloop_wait) *P_pa_threaded_mainloop_wait;
static __typeof__(pa_timeval_diff) *P_pa_timeval_diff;
static __typeof__(pa_usec_to_bytes) *P_pa_usec_to_bytes;
static int carga48(const char *ruta) {
    void *h = dlopen(ruta, RTLD_NOW | RTLD_LOCAL);
    if (!h) { fprintf(stderr, "dlopen: %s\n", dlerror()); return -1; }
    int faltan = 0;
    if (!(P_pa_context_connect = (__typeof__(pa_context_connect) *)dlsym(h, "pa_context_connect"))) { fprintf(stderr, "falta pa_context_connect\n"); faltan++; }
    if (!(P_pa_context_disconnect = (__typeof__(pa_context_disconnect) *)dlsym(h, "pa_context_disconnect"))) { fprintf(stderr, "falta pa_context_disconnect\n"); faltan++; }
    if (!(P_pa_context_errno = (__typeof__(pa_context_errno) *)dlsym(h, "pa_context_errno"))) { fprintf(stderr, "falta pa_context_errno\n"); faltan++; }
    if (!(P_pa_context_get_sink_input_info = (__typeof__(pa_context_get_sink_input_info) *)dlsym(h, "pa_context_get_sink_input_info"))) { fprintf(stderr, "falta pa_context_get_sink_input_info\n"); faltan++; }
    if (!(P_pa_context_get_state = (__typeof__(pa_context_get_state) *)dlsym(h, "pa_context_get_state"))) { fprintf(stderr, "falta pa_context_get_state\n"); faltan++; }
    if (!(P_pa_context_new = (__typeof__(pa_context_new) *)dlsym(h, "pa_context_new"))) { fprintf(stderr, "falta pa_context_new\n"); faltan++; }
    if (!(P_pa_context_set_sink_input_volume = (__typeof__(pa_context_set_sink_input_volume) *)dlsym(h, "pa_context_set_sink_input_volume"))) { fprintf(stderr, "falta pa_context_set_sink_input_volume\n"); faltan++; }
    if (!(P_pa_context_set_state_callback = (__typeof__(pa_context_set_state_callback) *)dlsym(h, "pa_context_set_state_callback"))) { fprintf(stderr, "falta pa_context_set_state_callback\n"); faltan++; }
    if (!(P_pa_context_set_subscribe_callback = (__typeof__(pa_context_set_subscribe_callback) *)dlsym(h, "pa_context_set_subscribe_callback"))) { fprintf(stderr, "falta pa_context_set_subscribe_callback\n"); faltan++; }
    if (!(P_pa_context_subscribe = (__typeof__(pa_context_subscribe) *)dlsym(h, "pa_context_subscribe"))) { fprintf(stderr, "falta pa_context_subscribe\n"); faltan++; }
    if (!(P_pa_context_unref = (__typeof__(pa_context_unref) *)dlsym(h, "pa_context_unref"))) { fprintf(stderr, "falta pa_context_unref\n"); faltan++; }
    if (!(P_pa_cvolume_avg = (__typeof__(pa_cvolume_avg) *)dlsym(h, "pa_cvolume_avg"))) { fprintf(stderr, "falta pa_cvolume_avg\n"); faltan++; }
    if (!(P_pa_cvolume_set = (__typeof__(pa_cvolume_set) *)dlsym(h, "pa_cvolume_set"))) { fprintf(stderr, "falta pa_cvolume_set\n"); faltan++; }
    if (!(P_pa_gettimeofday = (__typeof__(pa_gettimeofday) *)dlsym(h, "pa_gettimeofday"))) { fprintf(stderr, "falta pa_gettimeofday\n"); faltan++; }
    if (!(P_pa_operation_get_state = (__typeof__(pa_operation_get_state) *)dlsym(h, "pa_operation_get_state"))) { fprintf(stderr, "falta pa_operation_get_state\n"); faltan++; }
    if (!(P_pa_operation_unref = (__typeof__(pa_operation_unref) *)dlsym(h, "pa_operation_unref"))) { fprintf(stderr, "falta pa_operation_unref\n"); faltan++; }
    if (!(P_pa_proplist_free = (__typeof__(pa_proplist_free) *)dlsym(h, "pa_proplist_free"))) { fprintf(stderr, "falta pa_proplist_free\n"); faltan++; }
    if (!(P_pa_proplist_new = (__typeof__(pa_proplist_new) *)dlsym(h, "pa_proplist_new"))) { fprintf(stderr, "falta pa_proplist_new\n"); faltan++; }
    if (!(P_pa_proplist_sets = (__typeof__(pa_proplist_sets) *)dlsym(h, "pa_proplist_sets"))) { fprintf(stderr, "falta pa_proplist_sets\n"); faltan++; }
    if (!(P_pa_stream_connect_playback = (__typeof__(pa_stream_connect_playback) *)dlsym(h, "pa_stream_connect_playback"))) { fprintf(stderr, "falta pa_stream_connect_playback\n"); faltan++; }
    if (!(P_pa_stream_cork = (__typeof__(pa_stream_cork) *)dlsym(h, "pa_stream_cork"))) { fprintf(stderr, "falta pa_stream_cork\n"); faltan++; }
    if (!(P_pa_stream_disconnect = (__typeof__(pa_stream_disconnect) *)dlsym(h, "pa_stream_disconnect"))) { fprintf(stderr, "falta pa_stream_disconnect\n"); faltan++; }
    if (!(P_pa_stream_flush = (__typeof__(pa_stream_flush) *)dlsym(h, "pa_stream_flush"))) { fprintf(stderr, "falta pa_stream_flush\n"); faltan++; }
    if (!(P_pa_stream_get_index = (__typeof__(pa_stream_get_index) *)dlsym(h, "pa_stream_get_index"))) { fprintf(stderr, "falta pa_stream_get_index\n"); faltan++; }
    if (!(P_pa_stream_get_state = (__typeof__(pa_stream_get_state) *)dlsym(h, "pa_stream_get_state"))) { fprintf(stderr, "falta pa_stream_get_state\n"); faltan++; }
    if (!(P_pa_stream_get_timing_info = (__typeof__(pa_stream_get_timing_info) *)dlsym(h, "pa_stream_get_timing_info"))) { fprintf(stderr, "falta pa_stream_get_timing_info\n"); faltan++; }
    if (!(P_pa_stream_is_corked = (__typeof__(pa_stream_is_corked) *)dlsym(h, "pa_stream_is_corked"))) { fprintf(stderr, "falta pa_stream_is_corked\n"); faltan++; }
    if (!(P_pa_stream_new = (__typeof__(pa_stream_new) *)dlsym(h, "pa_stream_new"))) { fprintf(stderr, "falta pa_stream_new\n"); faltan++; }
    if (!(P_pa_stream_new_with_proplist = (__typeof__(pa_stream_new_with_proplist) *)dlsym(h, "pa_stream_new_with_proplist"))) { fprintf(stderr, "falta pa_stream_new_with_proplist\n"); faltan++; }
    if (!(P_pa_stream_set_started_callback = (__typeof__(pa_stream_set_started_callback) *)dlsym(h, "pa_stream_set_started_callback"))) { fprintf(stderr, "falta pa_stream_set_started_callback\n"); faltan++; }
    if (!(P_pa_stream_set_state_callback = (__typeof__(pa_stream_set_state_callback) *)dlsym(h, "pa_stream_set_state_callback"))) { fprintf(stderr, "falta pa_stream_set_state_callback\n"); faltan++; }
    if (!(P_pa_stream_set_underflow_callback = (__typeof__(pa_stream_set_underflow_callback) *)dlsym(h, "pa_stream_set_underflow_callback"))) { fprintf(stderr, "falta pa_stream_set_underflow_callback\n"); faltan++; }
    if (!(P_pa_stream_unref = (__typeof__(pa_stream_unref) *)dlsym(h, "pa_stream_unref"))) { fprintf(stderr, "falta pa_stream_unref\n"); faltan++; }
    if (!(P_pa_stream_update_timing_info = (__typeof__(pa_stream_update_timing_info) *)dlsym(h, "pa_stream_update_timing_info"))) { fprintf(stderr, "falta pa_stream_update_timing_info\n"); faltan++; }
    if (!(P_pa_stream_writable_size = (__typeof__(pa_stream_writable_size) *)dlsym(h, "pa_stream_writable_size"))) { fprintf(stderr, "falta pa_stream_writable_size\n"); faltan++; }
    if (!(P_pa_stream_write = (__typeof__(pa_stream_write) *)dlsym(h, "pa_stream_write"))) { fprintf(stderr, "falta pa_stream_write\n"); faltan++; }
    if (!(P_pa_strerror = (__typeof__(pa_strerror) *)dlsym(h, "pa_strerror"))) { fprintf(stderr, "falta pa_strerror\n"); faltan++; }
    if (!(P_pa_threaded_mainloop_free = (__typeof__(pa_threaded_mainloop_free) *)dlsym(h, "pa_threaded_mainloop_free"))) { fprintf(stderr, "falta pa_threaded_mainloop_free\n"); faltan++; }
    if (!(P_pa_threaded_mainloop_get_api = (__typeof__(pa_threaded_mainloop_get_api) *)dlsym(h, "pa_threaded_mainloop_get_api"))) { fprintf(stderr, "falta pa_threaded_mainloop_get_api\n"); faltan++; }
    if (!(P_pa_threaded_mainloop_lock = (__typeof__(pa_threaded_mainloop_lock) *)dlsym(h, "pa_threaded_mainloop_lock"))) { fprintf(stderr, "falta pa_threaded_mainloop_lock\n"); faltan++; }
    if (!(P_pa_threaded_mainloop_new = (__typeof__(pa_threaded_mainloop_new) *)dlsym(h, "pa_threaded_mainloop_new"))) { fprintf(stderr, "falta pa_threaded_mainloop_new\n"); faltan++; }
    if (!(P_pa_threaded_mainloop_signal = (__typeof__(pa_threaded_mainloop_signal) *)dlsym(h, "pa_threaded_mainloop_signal"))) { fprintf(stderr, "falta pa_threaded_mainloop_signal\n"); faltan++; }
    if (!(P_pa_threaded_mainloop_start = (__typeof__(pa_threaded_mainloop_start) *)dlsym(h, "pa_threaded_mainloop_start"))) { fprintf(stderr, "falta pa_threaded_mainloop_start\n"); faltan++; }
    if (!(P_pa_threaded_mainloop_stop = (__typeof__(pa_threaded_mainloop_stop) *)dlsym(h, "pa_threaded_mainloop_stop"))) { fprintf(stderr, "falta pa_threaded_mainloop_stop\n"); faltan++; }
    if (!(P_pa_threaded_mainloop_unlock = (__typeof__(pa_threaded_mainloop_unlock) *)dlsym(h, "pa_threaded_mainloop_unlock"))) { fprintf(stderr, "falta pa_threaded_mainloop_unlock\n"); faltan++; }
    if (!(P_pa_threaded_mainloop_wait = (__typeof__(pa_threaded_mainloop_wait) *)dlsym(h, "pa_threaded_mainloop_wait"))) { fprintf(stderr, "falta pa_threaded_mainloop_wait\n"); faltan++; }
    if (!(P_pa_timeval_diff = (__typeof__(pa_timeval_diff) *)dlsym(h, "pa_timeval_diff"))) { fprintf(stderr, "falta pa_timeval_diff\n"); faltan++; }
    if (!(P_pa_usec_to_bytes = (__typeof__(pa_usec_to_bytes) *)dlsym(h, "pa_usec_to_bytes"))) { fprintf(stderr, "falta pa_usec_to_bytes\n"); faltan++; }
    return faltan ? -1 : 0;
}
#define pa_context_connect(...) (cuenta48[K_pa_context_connect]++, P_pa_context_connect(__VA_ARGS__))
#define pa_context_disconnect(...) (cuenta48[K_pa_context_disconnect]++, P_pa_context_disconnect(__VA_ARGS__))
#define pa_context_errno(...) (cuenta48[K_pa_context_errno]++, P_pa_context_errno(__VA_ARGS__))
#define pa_context_get_sink_input_info(...) (cuenta48[K_pa_context_get_sink_input_info]++, P_pa_context_get_sink_input_info(__VA_ARGS__))
#define pa_context_get_state(...) (cuenta48[K_pa_context_get_state]++, P_pa_context_get_state(__VA_ARGS__))
#define pa_context_new(...) (cuenta48[K_pa_context_new]++, P_pa_context_new(__VA_ARGS__))
#define pa_context_set_sink_input_volume(...) (cuenta48[K_pa_context_set_sink_input_volume]++, P_pa_context_set_sink_input_volume(__VA_ARGS__))
#define pa_context_set_state_callback(...) (cuenta48[K_pa_context_set_state_callback]++, P_pa_context_set_state_callback(__VA_ARGS__))
#define pa_context_set_subscribe_callback(...) (cuenta48[K_pa_context_set_subscribe_callback]++, P_pa_context_set_subscribe_callback(__VA_ARGS__))
#define pa_context_subscribe(...) (cuenta48[K_pa_context_subscribe]++, P_pa_context_subscribe(__VA_ARGS__))
#define pa_context_unref(...) (cuenta48[K_pa_context_unref]++, P_pa_context_unref(__VA_ARGS__))
#define pa_cvolume_avg(...) (cuenta48[K_pa_cvolume_avg]++, P_pa_cvolume_avg(__VA_ARGS__))
#define pa_cvolume_set(...) (cuenta48[K_pa_cvolume_set]++, P_pa_cvolume_set(__VA_ARGS__))
#define pa_gettimeofday(...) (cuenta48[K_pa_gettimeofday]++, P_pa_gettimeofday(__VA_ARGS__))
#define pa_operation_get_state(...) (cuenta48[K_pa_operation_get_state]++, P_pa_operation_get_state(__VA_ARGS__))
#define pa_operation_unref(...) (cuenta48[K_pa_operation_unref]++, P_pa_operation_unref(__VA_ARGS__))
#define pa_proplist_free(...) (cuenta48[K_pa_proplist_free]++, P_pa_proplist_free(__VA_ARGS__))
#define pa_proplist_new(...) (cuenta48[K_pa_proplist_new]++, P_pa_proplist_new(__VA_ARGS__))
#define pa_proplist_sets(...) (cuenta48[K_pa_proplist_sets]++, P_pa_proplist_sets(__VA_ARGS__))
#define pa_stream_connect_playback(...) (cuenta48[K_pa_stream_connect_playback]++, P_pa_stream_connect_playback(__VA_ARGS__))
#define pa_stream_cork(...) (cuenta48[K_pa_stream_cork]++, P_pa_stream_cork(__VA_ARGS__))
#define pa_stream_disconnect(...) (cuenta48[K_pa_stream_disconnect]++, P_pa_stream_disconnect(__VA_ARGS__))
#define pa_stream_flush(...) (cuenta48[K_pa_stream_flush]++, P_pa_stream_flush(__VA_ARGS__))
#define pa_stream_get_index(...) (cuenta48[K_pa_stream_get_index]++, P_pa_stream_get_index(__VA_ARGS__))
#define pa_stream_get_state(...) (cuenta48[K_pa_stream_get_state]++, P_pa_stream_get_state(__VA_ARGS__))
#define pa_stream_get_timing_info(...) (cuenta48[K_pa_stream_get_timing_info]++, P_pa_stream_get_timing_info(__VA_ARGS__))
#define pa_stream_is_corked(...) (cuenta48[K_pa_stream_is_corked]++, P_pa_stream_is_corked(__VA_ARGS__))
#define pa_stream_new(...) (cuenta48[K_pa_stream_new]++, P_pa_stream_new(__VA_ARGS__))
#define pa_stream_new_with_proplist(...) (cuenta48[K_pa_stream_new_with_proplist]++, P_pa_stream_new_with_proplist(__VA_ARGS__))
#define pa_stream_set_started_callback(...) (cuenta48[K_pa_stream_set_started_callback]++, P_pa_stream_set_started_callback(__VA_ARGS__))
#define pa_stream_set_state_callback(...) (cuenta48[K_pa_stream_set_state_callback]++, P_pa_stream_set_state_callback(__VA_ARGS__))
#define pa_stream_set_underflow_callback(...) (cuenta48[K_pa_stream_set_underflow_callback]++, P_pa_stream_set_underflow_callback(__VA_ARGS__))
#define pa_stream_unref(...) (cuenta48[K_pa_stream_unref]++, P_pa_stream_unref(__VA_ARGS__))
#define pa_stream_update_timing_info(...) (cuenta48[K_pa_stream_update_timing_info]++, P_pa_stream_update_timing_info(__VA_ARGS__))
#define pa_stream_writable_size(...) (cuenta48[K_pa_stream_writable_size]++, P_pa_stream_writable_size(__VA_ARGS__))
#define pa_stream_write(...) (cuenta48[K_pa_stream_write]++, P_pa_stream_write(__VA_ARGS__))
#define pa_strerror(...) (cuenta48[K_pa_strerror]++, P_pa_strerror(__VA_ARGS__))
#define pa_threaded_mainloop_free(...) (cuenta48[K_pa_threaded_mainloop_free]++, P_pa_threaded_mainloop_free(__VA_ARGS__))
#define pa_threaded_mainloop_get_api(...) (cuenta48[K_pa_threaded_mainloop_get_api]++, P_pa_threaded_mainloop_get_api(__VA_ARGS__))
#define pa_threaded_mainloop_lock(...) (cuenta48[K_pa_threaded_mainloop_lock]++, P_pa_threaded_mainloop_lock(__VA_ARGS__))
#define pa_threaded_mainloop_new(...) (cuenta48[K_pa_threaded_mainloop_new]++, P_pa_threaded_mainloop_new(__VA_ARGS__))
#define pa_threaded_mainloop_signal(...) (cuenta48[K_pa_threaded_mainloop_signal]++, P_pa_threaded_mainloop_signal(__VA_ARGS__))
#define pa_threaded_mainloop_start(...) (cuenta48[K_pa_threaded_mainloop_start]++, P_pa_threaded_mainloop_start(__VA_ARGS__))
#define pa_threaded_mainloop_stop(...) (cuenta48[K_pa_threaded_mainloop_stop]++, P_pa_threaded_mainloop_stop(__VA_ARGS__))
#define pa_threaded_mainloop_unlock(...) (cuenta48[K_pa_threaded_mainloop_unlock]++, P_pa_threaded_mainloop_unlock(__VA_ARGS__))
#define pa_threaded_mainloop_wait(...) (cuenta48[K_pa_threaded_mainloop_wait]++, P_pa_threaded_mainloop_wait(__VA_ARGS__))
#define pa_timeval_diff(...) (cuenta48[K_pa_timeval_diff]++, P_pa_timeval_diff(__VA_ARGS__))
#define pa_usec_to_bytes(...) (cuenta48[K_pa_usec_to_bytes]++, P_pa_usec_to_bytes(__VA_ARGS__))
#endif
