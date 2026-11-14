/* SPDX-License-Identifier: MIT
 * rocola-pulso: los tipos de la API publica de libpulse que usa un cliente
 * como Spotify Soloist (y pacat, paplay, mpv).
 *
 * Escritos a partir de la documentacion PUBLICA de PulseAudio 17 (def.h,
 * sample.h, channelmap.h, volume.h, introspect.h, mainloop-api.h...):
 * mismos nombres, tamanos, desplazamientos y valores, que es lo que exige la
 * ABI de libpulse.so.0. No hay codigo de PulseAudio. prueba/abi.c compara
 * cada tamano y cada desplazamiento con las cabeceras de verdad.
 */
#ifndef PULSO_ABI_H
#define PULSO_ABI_H

#include <stddef.h>
#include <stdint.h>
#include <poll.h>
#include <sys/time.h>
#include <time.h>

/* ---- numeros ---- */
#define PA_CHANNELS_MAX 32U
#define PA_RATE_MAX (48000U * 16U)
#define PA_INVALID_INDEX ((uint32_t)-1)
#define PA_USEC_PER_SEC ((pa_usec_t)1000000ULL)
#define PA_USEC_PER_MSEC ((pa_usec_t)1000ULL)
#define PA_USEC_INVALID ((pa_usec_t)-1)
#define PA_TIMEVAL_RTCLOCK ((time_t)(1LU << 30))

typedef uint64_t pa_usec_t;
typedef uint32_t pa_volume_t;
#define PA_VOLUME_MUTED ((pa_volume_t)0U)
#define PA_VOLUME_NORM ((pa_volume_t)0x10000U)
#define PA_VOLUME_MAX ((pa_volume_t)(UINT32_MAX / 2))
#define PA_VOLUME_INVALID ((pa_volume_t)UINT32_MAX)

/* ---- muestras ---- */
typedef enum pa_sample_format {
    PA_SAMPLE_U8, PA_SAMPLE_ALAW, PA_SAMPLE_ULAW,
    PA_SAMPLE_S16LE, PA_SAMPLE_S16BE,
    PA_SAMPLE_FLOAT32LE, PA_SAMPLE_FLOAT32BE,
    PA_SAMPLE_S32LE, PA_SAMPLE_S32BE,
    PA_SAMPLE_S24LE, PA_SAMPLE_S24BE,
    PA_SAMPLE_S24_32LE, PA_SAMPLE_S24_32BE,
    PA_SAMPLE_MAX,
    PA_SAMPLE_INVALID = -1
} pa_sample_format_t;

typedef struct pa_sample_spec {
    pa_sample_format_t format;
    uint32_t rate;
    uint8_t channels;
} pa_sample_spec;

typedef enum pa_channel_position {
    PA_CHANNEL_POSITION_INVALID = -1,
    PA_CHANNEL_POSITION_MONO = 0,
    PA_CHANNEL_POSITION_FRONT_LEFT, PA_CHANNEL_POSITION_FRONT_RIGHT, PA_CHANNEL_POSITION_FRONT_CENTER,
    PA_CHANNEL_POSITION_LEFT = PA_CHANNEL_POSITION_FRONT_LEFT,
    PA_CHANNEL_POSITION_RIGHT = PA_CHANNEL_POSITION_FRONT_RIGHT,
    PA_CHANNEL_POSITION_CENTER = PA_CHANNEL_POSITION_FRONT_CENTER,
    PA_CHANNEL_POSITION_REAR_CENTER, PA_CHANNEL_POSITION_REAR_LEFT, PA_CHANNEL_POSITION_REAR_RIGHT,
    PA_CHANNEL_POSITION_LFE,
    PA_CHANNEL_POSITION_SUBWOOFER = PA_CHANNEL_POSITION_LFE,
    PA_CHANNEL_POSITION_FRONT_LEFT_OF_CENTER, PA_CHANNEL_POSITION_FRONT_RIGHT_OF_CENTER,
    PA_CHANNEL_POSITION_SIDE_LEFT, PA_CHANNEL_POSITION_SIDE_RIGHT,
    PA_CHANNEL_POSITION_AUX0, /* ... AUX31 = AUX0 + 31 */
    PA_CHANNEL_POSITION_TOP_CENTER = PA_CHANNEL_POSITION_AUX0 + 32,
    PA_CHANNEL_POSITION_TOP_FRONT_LEFT, PA_CHANNEL_POSITION_TOP_FRONT_RIGHT, PA_CHANNEL_POSITION_TOP_FRONT_CENTER,
    PA_CHANNEL_POSITION_TOP_REAR_LEFT, PA_CHANNEL_POSITION_TOP_REAR_RIGHT, PA_CHANNEL_POSITION_TOP_REAR_CENTER,
    PA_CHANNEL_POSITION_MAX
} pa_channel_position_t;

typedef struct pa_channel_map {
    uint8_t channels;
    pa_channel_position_t map[PA_CHANNELS_MAX];
} pa_channel_map;

typedef enum pa_channel_map_def {
    PA_CHANNEL_MAP_AIFF, PA_CHANNEL_MAP_ALSA, PA_CHANNEL_MAP_AUX, PA_CHANNEL_MAP_WAVEEX,
    PA_CHANNEL_MAP_OSS, PA_CHANNEL_MAP_DEF_MAX,
    PA_CHANNEL_MAP_DEFAULT = PA_CHANNEL_MAP_AIFF
} pa_channel_map_def_t;

typedef struct pa_cvolume {
    uint8_t channels;
    pa_volume_t values[PA_CHANNELS_MAX];
} pa_cvolume;

/* ---- estados, banderas, errores ---- */
typedef enum pa_context_state {
    PA_CONTEXT_UNCONNECTED, PA_CONTEXT_CONNECTING, PA_CONTEXT_AUTHORIZING,
    PA_CONTEXT_SETTING_NAME, PA_CONTEXT_READY, PA_CONTEXT_FAILED, PA_CONTEXT_TERMINATED
} pa_context_state_t;

typedef enum pa_stream_state {
    PA_STREAM_UNCONNECTED, PA_STREAM_CREATING, PA_STREAM_READY, PA_STREAM_FAILED, PA_STREAM_TERMINATED
} pa_stream_state_t;

typedef enum pa_operation_state {
    PA_OPERATION_RUNNING, PA_OPERATION_DONE, PA_OPERATION_CANCELLED
} pa_operation_state_t;

typedef enum pa_context_flags {
    PA_CONTEXT_NOFLAGS = 0, PA_CONTEXT_NOAUTOSPAWN = 1, PA_CONTEXT_NOFAIL = 2
} pa_context_flags_t;

typedef enum pa_stream_direction {
    PA_STREAM_NODIRECTION, PA_STREAM_PLAYBACK, PA_STREAM_RECORD, PA_STREAM_UPLOAD
} pa_stream_direction_t;

typedef enum pa_stream_flags {
    PA_STREAM_NOFLAGS = 0x0000U,
    PA_STREAM_START_CORKED = 0x0001U,
    PA_STREAM_INTERPOLATE_TIMING = 0x0002U,
    PA_STREAM_NOT_MONOTONIC = 0x0004U,
    PA_STREAM_AUTO_TIMING_UPDATE = 0x0008U,
    PA_STREAM_NO_REMAP_CHANNELS = 0x0010U,
    PA_STREAM_NO_REMIX_CHANNELS = 0x0020U,
    PA_STREAM_FIX_FORMAT = 0x0040U,
    PA_STREAM_FIX_RATE = 0x0080U,
    PA_STREAM_FIX_CHANNELS = 0x0100,
    PA_STREAM_DONT_MOVE = 0x0200U,
    PA_STREAM_VARIABLE_RATE = 0x0400U,
    PA_STREAM_PEAK_DETECT = 0x0800U,
    PA_STREAM_START_MUTED = 0x1000U,
    PA_STREAM_ADJUST_LATENCY = 0x2000U,
    PA_STREAM_EARLY_REQUESTS = 0x4000U,
    PA_STREAM_DONT_INHIBIT_AUTO_SUSPEND = 0x8000U,
    PA_STREAM_START_UNMUTED = 0x10000U,
    PA_STREAM_FAIL_ON_SUSPEND = 0x20000U,
    PA_STREAM_RELATIVE_VOLUME = 0x40000U,
    PA_STREAM_PASSTHROUGH = 0x80000U
} pa_stream_flags_t;

typedef struct pa_buffer_attr {
    uint32_t maxlength;
    uint32_t tlength;
    uint32_t prebuf;
    uint32_t minreq;
    uint32_t fragsize;
} pa_buffer_attr;

enum {
    PA_OK = 0, PA_ERR_ACCESS, PA_ERR_COMMAND, PA_ERR_INVALID, PA_ERR_EXIST, PA_ERR_NOENTITY,
    PA_ERR_CONNECTIONREFUSED, PA_ERR_PROTOCOL, PA_ERR_TIMEOUT, PA_ERR_AUTHKEY, PA_ERR_INTERNAL,
    PA_ERR_CONNECTIONTERMINATED, PA_ERR_KILLED, PA_ERR_INVALIDSERVER, PA_ERR_MODINITFAILED,
    PA_ERR_BADSTATE, PA_ERR_NODATA, PA_ERR_VERSION, PA_ERR_TOOLARGE, PA_ERR_NOTSUPPORTED,
    PA_ERR_UNKNOWN, PA_ERR_NOEXTENSION, PA_ERR_OBSOLETE, PA_ERR_NOTIMPLEMENTED, PA_ERR_FORKED,
    PA_ERR_IO, PA_ERR_BUSY, PA_ERR_MAX
};

typedef enum pa_subscription_mask {
    PA_SUBSCRIPTION_MASK_NULL = 0x0000U, PA_SUBSCRIPTION_MASK_SINK = 0x0001U,
    PA_SUBSCRIPTION_MASK_SOURCE = 0x0002U, PA_SUBSCRIPTION_MASK_SINK_INPUT = 0x0004U,
    PA_SUBSCRIPTION_MASK_SOURCE_OUTPUT = 0x0008U, PA_SUBSCRIPTION_MASK_MODULE = 0x0010U,
    PA_SUBSCRIPTION_MASK_CLIENT = 0x0020U, PA_SUBSCRIPTION_MASK_SAMPLE_CACHE = 0x0040U,
    PA_SUBSCRIPTION_MASK_SERVER = 0x0080U, PA_SUBSCRIPTION_MASK_AUTOLOAD = 0x0100U,
    PA_SUBSCRIPTION_MASK_CARD = 0x0200U, PA_SUBSCRIPTION_MASK_ALL = 0x02ffU
} pa_subscription_mask_t;

typedef enum pa_subscription_event_type {
    PA_SUBSCRIPTION_EVENT_SINK = 0x0000U, PA_SUBSCRIPTION_EVENT_SOURCE = 0x0001U,
    PA_SUBSCRIPTION_EVENT_SINK_INPUT = 0x0002U, PA_SUBSCRIPTION_EVENT_SOURCE_OUTPUT = 0x0003U,
    PA_SUBSCRIPTION_EVENT_MODULE = 0x0004U, PA_SUBSCRIPTION_EVENT_CLIENT = 0x0005U,
    PA_SUBSCRIPTION_EVENT_SAMPLE_CACHE = 0x0006U, PA_SUBSCRIPTION_EVENT_SERVER = 0x0007U,
    PA_SUBSCRIPTION_EVENT_AUTOLOAD = 0x0008U, PA_SUBSCRIPTION_EVENT_CARD = 0x0009U,
    PA_SUBSCRIPTION_EVENT_FACILITY_MASK = 0x000FU,
    PA_SUBSCRIPTION_EVENT_NEW = 0x0000U, PA_SUBSCRIPTION_EVENT_CHANGE = 0x0010U,
    PA_SUBSCRIPTION_EVENT_REMOVE = 0x0020U, PA_SUBSCRIPTION_EVENT_TYPE_MASK = 0x0030U
} pa_subscription_event_type_t;

typedef enum pa_seek_mode {
    PA_SEEK_RELATIVE = 0, PA_SEEK_ABSOLUTE = 1, PA_SEEK_RELATIVE_ON_READ = 2, PA_SEEK_RELATIVE_END = 3
} pa_seek_mode_t;

typedef enum pa_sink_state {
    PA_SINK_INVALID_STATE = -1, PA_SINK_RUNNING = 0, PA_SINK_IDLE = 1, PA_SINK_SUSPENDED = 2
} pa_sink_state_t;

typedef enum pa_encoding {
    PA_ENCODING_ANY, PA_ENCODING_PCM, PA_ENCODING_AC3_IEC61937, PA_ENCODING_EAC3_IEC61937,
    PA_ENCODING_MPEG_IEC61937, PA_ENCODING_DTS_IEC61937, PA_ENCODING_MPEG2_AAC_IEC61937,
    PA_ENCODING_TRUEHD_IEC61937, PA_ENCODING_DTSHD_IEC61937, PA_ENCODING_MAX,
    PA_ENCODING_INVALID = -1
} pa_encoding_t;

typedef struct pa_timing_info {
    struct timeval timestamp;
    int synchronized_clocks;
    pa_usec_t sink_usec;
    pa_usec_t source_usec;
    pa_usec_t transport_usec;
    int playing;
    int write_index_corrupt;
    int64_t write_index;
    int read_index_corrupt;
    int64_t read_index;
    pa_usec_t configured_sink_usec;
    pa_usec_t configured_source_usec;
    int64_t since_underrun;
} pa_timing_info;

/* ---- objetos opacos ---- */
typedef struct pa_mainloop pa_mainloop;
typedef struct pa_threaded_mainloop pa_threaded_mainloop;
typedef struct pa_context pa_context;
typedef struct pa_stream pa_stream;
typedef struct pa_operation pa_operation;
typedef struct pa_proplist pa_proplist;
typedef struct pa_io_event pa_io_event;
typedef struct pa_time_event pa_time_event;
typedef struct pa_defer_event pa_defer_event;
typedef struct pa_signal_event pa_signal_event;
typedef struct pa_spawn_api pa_spawn_api;

typedef struct pa_format_info {
    pa_encoding_t encoding;
    pa_proplist *plist;
} pa_format_info;

/* ---- el bucle (mainloop-api.h) ---- */
typedef enum pa_io_event_flags {
    PA_IO_EVENT_NULL = 0, PA_IO_EVENT_INPUT = 1, PA_IO_EVENT_OUTPUT = 2,
    PA_IO_EVENT_HANGUP = 4, PA_IO_EVENT_ERROR = 8
} pa_io_event_flags_t;

typedef struct pa_mainloop_api pa_mainloop_api;
typedef void (*pa_io_event_cb_t)(pa_mainloop_api *ea, pa_io_event *e, int fd, pa_io_event_flags_t events, void *userdata);
typedef void (*pa_io_event_destroy_cb_t)(pa_mainloop_api *a, pa_io_event *e, void *userdata);
typedef void (*pa_time_event_cb_t)(pa_mainloop_api *a, pa_time_event *e, const struct timeval *tv, void *userdata);
typedef void (*pa_time_event_destroy_cb_t)(pa_mainloop_api *a, pa_time_event *e, void *userdata);
typedef void (*pa_defer_event_cb_t)(pa_mainloop_api *a, pa_defer_event *e, void *userdata);
typedef void (*pa_defer_event_destroy_cb_t)(pa_mainloop_api *a, pa_defer_event *e, void *userdata);

struct pa_mainloop_api {
    void *userdata;
    pa_io_event *(*io_new)(pa_mainloop_api *a, int fd, pa_io_event_flags_t events, pa_io_event_cb_t cb, void *userdata);
    void (*io_enable)(pa_io_event *e, pa_io_event_flags_t events);
    void (*io_free)(pa_io_event *e);
    void (*io_set_destroy)(pa_io_event *e, pa_io_event_destroy_cb_t cb);
    pa_time_event *(*time_new)(pa_mainloop_api *a, const struct timeval *tv, pa_time_event_cb_t cb, void *userdata);
    void (*time_restart)(pa_time_event *e, const struct timeval *tv);
    void (*time_free)(pa_time_event *e);
    void (*time_set_destroy)(pa_time_event *e, pa_time_event_destroy_cb_t cb);
    pa_defer_event *(*defer_new)(pa_mainloop_api *a, pa_defer_event_cb_t cb, void *userdata);
    void (*defer_enable)(pa_defer_event *e, int b);
    void (*defer_free)(pa_defer_event *e);
    void (*defer_set_destroy)(pa_defer_event *e, pa_defer_event_destroy_cb_t cb);
    void (*quit)(pa_mainloop_api *a, int retval);
};

/* ---- introspeccion (introspect.h) ---- */
typedef struct pa_sink_input_info {
    uint32_t index;
    const char *name;
    uint32_t owner_module;
    uint32_t client;
    uint32_t sink;
    pa_sample_spec sample_spec;
    pa_channel_map channel_map;
    pa_cvolume volume;
    pa_usec_t buffer_usec;
    pa_usec_t sink_usec;
    const char *resample_method;
    const char *driver;
    int mute;
    pa_proplist *proplist;
    int corked;
    int has_volume;
    int volume_writable;
    pa_format_info *format;
} pa_sink_input_info;

typedef struct pa_sink_port_info {
    const char *name;
    const char *description;
    uint32_t priority;
    int available;
    const char *availability_group;
    uint32_t type;
} pa_sink_port_info;

typedef struct pa_sink_info {
    const char *name;
    uint32_t index;
    const char *description;
    pa_sample_spec sample_spec;
    pa_channel_map channel_map;
    uint32_t owner_module;
    pa_cvolume volume;
    int mute;
    uint32_t monitor_source;
    const char *monitor_source_name;
    pa_usec_t latency;
    const char *driver;
    int flags;
    pa_proplist *proplist;
    pa_usec_t configured_latency;
    pa_volume_t base_volume;
    pa_sink_state_t state;
    uint32_t n_volume_steps;
    uint32_t card;
    uint32_t n_ports;
    pa_sink_port_info **ports;
    pa_sink_port_info *active_port;
    uint8_t n_formats;
    pa_format_info **formats;
} pa_sink_info;

typedef struct pa_server_info {
    const char *user_name;
    const char *host_name;
    const char *server_version;
    const char *server_name;
    pa_sample_spec sample_spec;
    const char *default_sink_name;
    const char *default_source_name;
    uint32_t cookie;
    pa_channel_map channel_map;
} pa_server_info;

/* ---- funciones de aviso ---- */
typedef void (*pa_context_notify_cb_t)(pa_context *c, void *userdata);
typedef void (*pa_context_success_cb_t)(pa_context *c, int success, void *userdata);
typedef void (*pa_context_subscribe_cb_t)(pa_context *c, pa_subscription_event_type_t t, uint32_t idx, void *userdata);
typedef void (*pa_sink_input_info_cb_t)(pa_context *c, const pa_sink_input_info *i, int eol, void *userdata);
typedef void (*pa_sink_info_cb_t)(pa_context *c, const pa_sink_info *i, int eol, void *userdata);
typedef void (*pa_server_info_cb_t)(pa_context *c, const pa_server_info *i, void *userdata);
typedef void (*pa_stream_notify_cb_t)(pa_stream *p, void *userdata);
typedef void (*pa_stream_request_cb_t)(pa_stream *p, size_t nbytes, void *userdata);
typedef void (*pa_stream_success_cb_t)(pa_stream *s, int success, void *userdata);
typedef void (*pa_stream_event_cb_t)(pa_stream *p, const char *name, pa_proplist *pl, void *userdata);
typedef void (*pa_operation_notify_cb_t)(pa_operation *o, void *userdata);
typedef void (*pa_free_cb_t)(void *p);
typedef void (*pa_time_event_cb_t)(pa_mainloop_api *a, pa_time_event *e, const struct timeval *tv, void *userdata);
typedef void (*pa_signal_cb_t)(pa_mainloop_api *api, pa_signal_event *e, int sig, void *userdata);
typedef void (*pa_signal_destroy_cb_t)(pa_mainloop_api *api, pa_signal_event *e, void *userdata);
typedef int (*pa_poll_func)(struct pollfd *ufds, unsigned long nfds, int timeout, void *userdata);

#endif
