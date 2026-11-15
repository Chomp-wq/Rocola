/* SPDX-License-Identifier: MIT
 * Prueba (5b) de rocola-pulso: la ABI. Se compila dos veces, con las
 * cabeceras PUBLICAS de libpulse (las del sistema de pruebas, o las de
 * PULSO_CABECERAS; solo para comparar) y con pulso_abi.h (-DNUESTRO), y
 * prueba.sh compara las dos salidas: el tamano y el desplazamiento de CADA
 * campo de CADA estructura de pulso_abi.h y el valor de CADA constante PA_*
 * que define (revision abi-y-semantica: antes era una muestra). */
#include <stddef.h>
#include <stdio.h>
#ifdef NUESTRO
#include "../pulso_abi.h"
#else
#include <pulse/pulseaudio.h>
#endif

#define T(t) printf("sizeof %s = %zu\n", #t, sizeof(struct t))
#define O(t, c) printf("offsetof %s.%s = %zu\n", #t, #c, offsetof(struct t, c))
#define V(v) printf("%s = %lld\n", #v, (long long)(v))

int main(void)
{
    T(pa_sample_spec);
    O(pa_sample_spec, format); O(pa_sample_spec, rate); O(pa_sample_spec, channels);
    T(pa_channel_map);
    O(pa_channel_map, channels); O(pa_channel_map, map);
    T(pa_cvolume);
    O(pa_cvolume, channels); O(pa_cvolume, values);
    T(pa_buffer_attr);
    O(pa_buffer_attr, maxlength); O(pa_buffer_attr, tlength); O(pa_buffer_attr, prebuf); O(pa_buffer_attr, minreq);
    O(pa_buffer_attr, fragsize);
    T(pa_timing_info);
    O(pa_timing_info, timestamp); O(pa_timing_info, synchronized_clocks); O(pa_timing_info, sink_usec); O(pa_timing_info, source_usec);
    O(pa_timing_info, transport_usec); O(pa_timing_info, playing); O(pa_timing_info, write_index_corrupt); O(pa_timing_info, write_index);
    O(pa_timing_info, read_index_corrupt); O(pa_timing_info, read_index); O(pa_timing_info, configured_sink_usec); O(pa_timing_info, configured_source_usec);
    O(pa_timing_info, since_underrun);
    T(pa_format_info);
    O(pa_format_info, encoding); O(pa_format_info, plist);
    T(pa_mainloop_api);
    O(pa_mainloop_api, userdata); O(pa_mainloop_api, io_new); O(pa_mainloop_api, io_enable); O(pa_mainloop_api, io_free);
    O(pa_mainloop_api, io_set_destroy); O(pa_mainloop_api, time_new); O(pa_mainloop_api, time_restart); O(pa_mainloop_api, time_free);
    O(pa_mainloop_api, time_set_destroy); O(pa_mainloop_api, defer_new); O(pa_mainloop_api, defer_enable); O(pa_mainloop_api, defer_free);
    O(pa_mainloop_api, defer_set_destroy); O(pa_mainloop_api, quit);
    T(pa_sink_input_info);
    O(pa_sink_input_info, index); O(pa_sink_input_info, name); O(pa_sink_input_info, owner_module); O(pa_sink_input_info, client);
    O(pa_sink_input_info, sink); O(pa_sink_input_info, sample_spec); O(pa_sink_input_info, channel_map); O(pa_sink_input_info, volume);
    O(pa_sink_input_info, buffer_usec); O(pa_sink_input_info, sink_usec); O(pa_sink_input_info, resample_method); O(pa_sink_input_info, driver);
    O(pa_sink_input_info, mute); O(pa_sink_input_info, proplist); O(pa_sink_input_info, corked); O(pa_sink_input_info, has_volume);
    O(pa_sink_input_info, volume_writable); O(pa_sink_input_info, format);
    T(pa_sink_port_info);
    O(pa_sink_port_info, name); O(pa_sink_port_info, description); O(pa_sink_port_info, priority); O(pa_sink_port_info, available);
    O(pa_sink_port_info, availability_group); O(pa_sink_port_info, type);
    T(pa_sink_info);
    O(pa_sink_info, name); O(pa_sink_info, index); O(pa_sink_info, description); O(pa_sink_info, sample_spec);
    O(pa_sink_info, channel_map); O(pa_sink_info, owner_module); O(pa_sink_info, volume); O(pa_sink_info, mute);
    O(pa_sink_info, monitor_source); O(pa_sink_info, monitor_source_name); O(pa_sink_info, latency); O(pa_sink_info, driver);
    O(pa_sink_info, flags); O(pa_sink_info, proplist); O(pa_sink_info, configured_latency); O(pa_sink_info, base_volume);
    O(pa_sink_info, state); O(pa_sink_info, n_volume_steps); O(pa_sink_info, card); O(pa_sink_info, n_ports);
    O(pa_sink_info, ports); O(pa_sink_info, active_port); O(pa_sink_info, n_formats); O(pa_sink_info, formats);
    T(pa_server_info);
    O(pa_server_info, user_name); O(pa_server_info, host_name); O(pa_server_info, server_version); O(pa_server_info, server_name);
    O(pa_server_info, sample_spec); O(pa_server_info, default_sink_name); O(pa_server_info, default_source_name); O(pa_server_info, cookie);
    O(pa_server_info, channel_map);
    V(PA_CHANNELS_MAX); V(PA_CHANNEL_MAP_AIFF); V(PA_CHANNEL_MAP_ALSA);
    V(PA_CHANNEL_MAP_AUX); V(PA_CHANNEL_MAP_DEFAULT); V(PA_CHANNEL_MAP_DEF_MAX);
    V(PA_CHANNEL_MAP_OSS); V(PA_CHANNEL_MAP_WAVEEX); V(PA_CHANNEL_POSITION_AUX0);
    V(PA_CHANNEL_POSITION_CENTER); V(PA_CHANNEL_POSITION_FRONT_CENTER); V(PA_CHANNEL_POSITION_FRONT_LEFT);
    V(PA_CHANNEL_POSITION_FRONT_LEFT_OF_CENTER); V(PA_CHANNEL_POSITION_FRONT_RIGHT); V(PA_CHANNEL_POSITION_FRONT_RIGHT_OF_CENTER);
    V(PA_CHANNEL_POSITION_INVALID); V(PA_CHANNEL_POSITION_LEFT); V(PA_CHANNEL_POSITION_LFE);
    V(PA_CHANNEL_POSITION_MAX); V(PA_CHANNEL_POSITION_MONO); V(PA_CHANNEL_POSITION_REAR_CENTER);
    V(PA_CHANNEL_POSITION_REAR_LEFT); V(PA_CHANNEL_POSITION_REAR_RIGHT); V(PA_CHANNEL_POSITION_RIGHT);
    V(PA_CHANNEL_POSITION_SIDE_LEFT); V(PA_CHANNEL_POSITION_SIDE_RIGHT); V(PA_CHANNEL_POSITION_SUBWOOFER);
    V(PA_CHANNEL_POSITION_TOP_CENTER); V(PA_CHANNEL_POSITION_TOP_FRONT_CENTER); V(PA_CHANNEL_POSITION_TOP_FRONT_LEFT);
    V(PA_CHANNEL_POSITION_TOP_FRONT_RIGHT); V(PA_CHANNEL_POSITION_TOP_REAR_CENTER); V(PA_CHANNEL_POSITION_TOP_REAR_LEFT);
    V(PA_CHANNEL_POSITION_TOP_REAR_RIGHT); V(PA_CONTEXT_AUTHORIZING); V(PA_CONTEXT_CONNECTING);
    V(PA_CONTEXT_FAILED); V(PA_CONTEXT_NOAUTOSPAWN); V(PA_CONTEXT_NOFAIL);
    V(PA_CONTEXT_NOFLAGS); V(PA_CONTEXT_READY); V(PA_CONTEXT_SETTING_NAME);
    V(PA_CONTEXT_TERMINATED); V(PA_CONTEXT_UNCONNECTED); V(PA_ENCODING_AC3_IEC61937);
    V(PA_ENCODING_ANY); V(PA_ENCODING_DTSHD_IEC61937); V(PA_ENCODING_DTS_IEC61937);
    V(PA_ENCODING_EAC3_IEC61937); V(PA_ENCODING_INVALID); V(PA_ENCODING_MAX);
    V(PA_ENCODING_MPEG2_AAC_IEC61937); V(PA_ENCODING_MPEG_IEC61937); V(PA_ENCODING_PCM);
    V(PA_ENCODING_TRUEHD_IEC61937); V(PA_ERR_ACCESS); V(PA_ERR_AUTHKEY);
    V(PA_ERR_BADSTATE); V(PA_ERR_BUSY); V(PA_ERR_COMMAND);
    V(PA_ERR_CONNECTIONREFUSED); V(PA_ERR_CONNECTIONTERMINATED); V(PA_ERR_EXIST);
    V(PA_ERR_FORKED); V(PA_ERR_INTERNAL); V(PA_ERR_INVALID);
    V(PA_ERR_INVALIDSERVER); V(PA_ERR_IO); V(PA_ERR_KILLED);
    V(PA_ERR_MAX); V(PA_ERR_MODINITFAILED); V(PA_ERR_NODATA);
    V(PA_ERR_NOENTITY); V(PA_ERR_NOEXTENSION); V(PA_ERR_NOTIMPLEMENTED);
    V(PA_ERR_NOTSUPPORTED); V(PA_ERR_OBSOLETE); V(PA_ERR_PROTOCOL);
    V(PA_ERR_TIMEOUT); V(PA_ERR_TOOLARGE); V(PA_ERR_UNKNOWN);
    V(PA_ERR_VERSION); V(PA_INVALID_INDEX); V(PA_IO_EVENT_ERROR);
    V(PA_IO_EVENT_HANGUP); V(PA_IO_EVENT_INPUT); V(PA_IO_EVENT_NULL);
    V(PA_IO_EVENT_OUTPUT); V(PA_OK); V(PA_OPERATION_CANCELLED);
    V(PA_OPERATION_DONE); V(PA_OPERATION_RUNNING); V(PA_SAMPLE_ALAW);
    V(PA_SAMPLE_FLOAT32BE); V(PA_SAMPLE_FLOAT32LE); V(PA_SAMPLE_INVALID);
    V(PA_SAMPLE_MAX); V(PA_SAMPLE_S16BE); V(PA_SAMPLE_S16LE);
    V(PA_SAMPLE_S24BE); V(PA_SAMPLE_S24LE); V(PA_SAMPLE_S24_32BE);
    V(PA_SAMPLE_S24_32LE); V(PA_SAMPLE_S32BE); V(PA_SAMPLE_S32LE);
    V(PA_SAMPLE_U8); V(PA_SAMPLE_ULAW); V(PA_SEEK_ABSOLUTE);
    V(PA_SEEK_RELATIVE); V(PA_SEEK_RELATIVE_END); V(PA_SEEK_RELATIVE_ON_READ);
    V(PA_SINK_IDLE); V(PA_SINK_INVALID_STATE); V(PA_SINK_RUNNING);
    V(PA_SINK_SUSPENDED); V(PA_STREAM_ADJUST_LATENCY); V(PA_STREAM_AUTO_TIMING_UPDATE);
    V(PA_STREAM_CREATING); V(PA_STREAM_DONT_INHIBIT_AUTO_SUSPEND); V(PA_STREAM_DONT_MOVE);
    V(PA_STREAM_EARLY_REQUESTS); V(PA_STREAM_FAILED); V(PA_STREAM_FAIL_ON_SUSPEND);
    V(PA_STREAM_FIX_CHANNELS); V(PA_STREAM_FIX_FORMAT); V(PA_STREAM_FIX_RATE);
    V(PA_STREAM_INTERPOLATE_TIMING); V(PA_STREAM_NODIRECTION); V(PA_STREAM_NOFLAGS);
    V(PA_STREAM_NOT_MONOTONIC); V(PA_STREAM_NO_REMAP_CHANNELS); V(PA_STREAM_NO_REMIX_CHANNELS);
    V(PA_STREAM_PASSTHROUGH); V(PA_STREAM_PEAK_DETECT); V(PA_STREAM_PLAYBACK);
    V(PA_STREAM_READY); V(PA_STREAM_RECORD); V(PA_STREAM_RELATIVE_VOLUME);
    V(PA_STREAM_START_CORKED); V(PA_STREAM_START_MUTED); V(PA_STREAM_START_UNMUTED);
    V(PA_STREAM_TERMINATED); V(PA_STREAM_UNCONNECTED); V(PA_STREAM_UPLOAD);
    V(PA_STREAM_VARIABLE_RATE); V(PA_SUBSCRIPTION_EVENT_AUTOLOAD); V(PA_SUBSCRIPTION_EVENT_CARD);
    V(PA_SUBSCRIPTION_EVENT_CHANGE); V(PA_SUBSCRIPTION_EVENT_CLIENT); V(PA_SUBSCRIPTION_EVENT_FACILITY_MASK);
    V(PA_SUBSCRIPTION_EVENT_MODULE); V(PA_SUBSCRIPTION_EVENT_NEW); V(PA_SUBSCRIPTION_EVENT_REMOVE);
    V(PA_SUBSCRIPTION_EVENT_SAMPLE_CACHE); V(PA_SUBSCRIPTION_EVENT_SERVER); V(PA_SUBSCRIPTION_EVENT_SINK);
    V(PA_SUBSCRIPTION_EVENT_SINK_INPUT); V(PA_SUBSCRIPTION_EVENT_SOURCE); V(PA_SUBSCRIPTION_EVENT_SOURCE_OUTPUT);
    V(PA_SUBSCRIPTION_EVENT_TYPE_MASK); V(PA_SUBSCRIPTION_MASK_ALL); V(PA_SUBSCRIPTION_MASK_AUTOLOAD);
    V(PA_SUBSCRIPTION_MASK_CARD); V(PA_SUBSCRIPTION_MASK_CLIENT); V(PA_SUBSCRIPTION_MASK_MODULE);
    V(PA_SUBSCRIPTION_MASK_NULL); V(PA_SUBSCRIPTION_MASK_SAMPLE_CACHE); V(PA_SUBSCRIPTION_MASK_SERVER);
    V(PA_SUBSCRIPTION_MASK_SINK); V(PA_SUBSCRIPTION_MASK_SINK_INPUT); V(PA_SUBSCRIPTION_MASK_SOURCE);
    V(PA_SUBSCRIPTION_MASK_SOURCE_OUTPUT); V(PA_USEC_INVALID); V(PA_USEC_PER_MSEC);
    V(PA_USEC_PER_SEC); V(PA_VOLUME_INVALID); V(PA_VOLUME_MAX);
    V(PA_VOLUME_MUTED); V(PA_VOLUME_NORM);
    /* PA_RATE_MAX: libpulse 17 la subio a 48000 * 16 (la 16 decia 48000 * 8):
     * con cabeceras de antes de la 17 se compara con la de la 17. La linea
     * «#» dice que cabeceras eran (prueba.sh no la compara). */
#ifdef NUESTRO
    V(PA_RATE_MAX);
    printf("# cabeceras: pulso_abi.h\n");
#else
#if PA_MAJOR < 17
    printf("PA_RATE_MAX = %lld\n", PA_RATE_MAX == 48000U * 8U ? (long long)(48000U * 16U) : -1LL);
#else
    V(PA_RATE_MAX);
#endif
    printf("# cabeceras: libpulse %s\n", pa_get_headers_version());
#endif
    return 0;
}
