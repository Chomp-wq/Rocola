package mx.tianguis.cuentas;

/**
 * El corte en texto plano, pensado para pegarse en WhatsApp o en un mensaje.
 *
 * No se usan columnas alineadas con espacios porque las apps de mensajes no
 * usan tipografía de ancho fijo; cada renglón se lee solo.
 */
public final class ExportTexto {

    private ExportTexto() {
    }

    public static String armar(Reporte r) {
        StringBuilder sb = new StringBuilder();

        sb.append(r.titulo).append('\n');
        sb.append(r.subtitulo).append('\n');
        if (r.negocio != null && r.negocio.length() > 0) {
            sb.append(r.negocio).append('\n');
        }

        for (int i = 0; i < r.secciones.size(); i++) {
            Reporte.Seccion s = r.secciones.get(i);
            sb.append('\n');
            sb.append("── ").append(s.titulo.toUpperCase(Fechas.MX)).append(" ──").append('\n');

            if (s.vacia()) {
                sb.append(s.mensajeVacio).append('\n');
            } else {
                for (int j = 0; j < s.filas.size(); j++) {
                    Reporte.Fila f = s.filas.get(j);
                    if (s.estilo == Reporte.Seccion.ESTILO_DIAS) {
                        sb.append("• ").append(f.c1)
                                .append(" — A: ").append(f.c2)
                                .append(" · B: ").append(f.c3)
                                .append(" = ").append(Dinero.formato(f.monto)).append('\n');
                    } else {
                        sb.append("• ").append(f.c2).append(" × ").append(f.c1)
                                .append(" (").append(f.c3).append(") = ")
                                .append(Dinero.formato(f.monto)).append('\n');
                    }
                }
            }
            sb.append(s.pieEtiqueta).append(": ")
                    .append(Dinero.formato(s.pieMonto)).append('\n');
        }

        sb.append('\n').append("━━━━━━━━━━━━━━━━━━━━").append('\n');
        for (int i = 0; i < r.totales.size(); i++) {
            Reporte.Linea l = r.totales.get(i);
            sb.append(l.etiqueta).append(": ").append(Dinero.formato(l.monto)).append('\n');
        }

        if (r.nota != null && r.nota.length() > 0) {
            sb.append('\n').append(r.nota).append('\n');
        }
        sb.append("Cuentas Tianguis · ").append(Fechas.selloDeTiempo()).append('\n');

        return sb.toString();
    }
}
