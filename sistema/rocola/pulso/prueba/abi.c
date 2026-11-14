/* SPDX-License-Identifier: MIT
 * Prueba (5b) de rocola-pulso: la ABI. Se compila dos veces, con las
 * cabeceras PUBLICAS de libpulse (las del sistema de pruebas, solo para
 * comparar) y con pulso_abi.h (-DNUESTRO), y prueba.sh compara las dos
 * salidas: tamanos, desplazamientos y valores de lo que usa un cliente. */
#include <stddef.h>
#include <stdio.h>
#ifdef NUESTRO
#include "../pulso_abi.h"
#else
#include <pulse/pulseaudio.h>
#endif

#define T(t) printf("sizeof %s = %zu\n", #t, sizeof(t))
#define O(t, c) printf("offsetof %s.%s = %zu\n", #t, #c, offsetof(t, c))
#define V(v) printf("%s = %lld\n", #v, (long long)(v))

int main(void)
{
    T(pa_sample_spec); O(pa_sample_spec, format); O(pa_sample_spec, rate); O(pa_sample_spec, channels);
    T(pa_channel_map); O(pa_channel_map, map);
    T(pa_cvolume); O(pa_cvolume, values);
    T(pa_buffer_attr); O(pa_buffer_attr, tlength); O(pa_buffer_attr, prebuf); O(pa_buffer_attr, minreq); O(pa_buffer_attr, fragsize);
    T(pa_timing_info);
    O(pa_timing_info, timestamp); O(pa_timing_info, synchronized_clocks); O(pa_timing_info, sink_usec); O(pa_timing_info, source_usec);
    O(pa_timing_info, transport_usec); O(pa_timing_info, playing); O(pa_timing_info, write_index_corrupt); O(pa_timing_info, write_index);
    O(pa_timing_info, read_index_corrupt); O(pa_timing_info, read_index); O(pa_timing_info, configured_sink_usec);
    O(pa_timing_info, configured_source_usec); O(pa_timing_info, since_underrun);
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
    T(pa_sink_info); O(pa_sink_info, index); O(pa_sink_info, sample_spec); O(pa_sink_info, channel_map); O(pa_sink_info, owner_module);
    O(pa_sink_info, volume); O(pa_sink_info, latency); O(pa_sink_info, proplist); O(pa_sink_info, configured_latency);
    O(pa_sink_info, base_volume); O(pa_sink_info, state); O(pa_sink_info, card); O(pa_sink_info, ports); O(pa_sink_info, active_port);
    O(pa_sink_info, n_formats); O(pa_sink_info, formats);
    T(pa_sink_port_info);
    T(pa_server_info); O(pa_server_info, sample_spec); O(pa_server_info, default_sink_name); O(pa_server_info, cookie); O(pa_server_info, channel_map);
    T(pa_format_info); O(pa_format_info, plist);
    V(PA_SAMPLE_U8); V(PA_SAMPLE_S16LE); V(PA_SAMPLE_FLOAT32LE); V(PA_SAMPLE_S32LE); V(PA_SAMPLE_S24LE); V(PA_SAMPLE_S24_32LE);
    V(PA_SAMPLE_S24_32BE); V(PA_SAMPLE_MAX); V(PA_SAMPLE_INVALID);
    V(PA_CHANNEL_POSITION_FRONT_LEFT); V(PA_CHANNEL_POSITION_FRONT_RIGHT); V(PA_CHANNEL_POSITION_LFE); V(PA_CHANNEL_POSITION_SIDE_RIGHT);
    V(PA_CHANNEL_POSITION_AUX0); V(PA_CHANNEL_POSITION_TOP_CENTER); V(PA_CHANNEL_POSITION_TOP_REAR_CENTER); V(PA_CHANNEL_POSITION_MAX);
    V(PA_CONTEXT_READY); V(PA_CONTEXT_FAILED); V(PA_CONTEXT_TERMINATED); V(PA_STREAM_READY); V(PA_STREAM_FAILED); V(PA_STREAM_TERMINATED);
    V(PA_OPERATION_DONE); V(PA_OPERATION_CANCELLED); V(PA_CONTEXT_NOAUTOSPAWN);
    V(PA_STREAM_START_CORKED); V(PA_STREAM_INTERPOLATE_TIMING); V(PA_STREAM_AUTO_TIMING_UPDATE); V(PA_STREAM_ADJUST_LATENCY);
    V(PA_STREAM_PASSTHROUGH);
    V(PA_ERR_INVALID); V(PA_ERR_CONNECTIONREFUSED); V(PA_ERR_KILLED); V(PA_ERR_BADSTATE); V(PA_ERR_NODATA); V(PA_ERR_NOTSUPPORTED);
    V(PA_ERR_BUSY); V(PA_ERR_MAX);
    V(PA_SUBSCRIPTION_MASK_SINK_INPUT); V(PA_SUBSCRIPTION_MASK_ALL); V(PA_SUBSCRIPTION_EVENT_SINK_INPUT); V(PA_SUBSCRIPTION_EVENT_CHANGE);
    V(PA_SUBSCRIPTION_EVENT_REMOVE); V(PA_SUBSCRIPTION_EVENT_TYPE_MASK);
    V(PA_SEEK_RELATIVE); V(PA_SEEK_RELATIVE_END); V(PA_IO_EVENT_ERROR); V(PA_VOLUME_NORM); V(PA_VOLUME_MAX); V(PA_CHANNELS_MAX);
    V(PA_RATE_MAX); V(PA_ENCODING_PCM); V(PA_CHANNEL_MAP_AIFF); V(PA_CHANNEL_MAP_OSS); V(PA_SINK_SUSPENDED);
    return 0;
}
