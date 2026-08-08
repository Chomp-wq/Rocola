package mx.tianguis.cuentas;

import android.app.Activity;
import android.app.AlertDialog;
import android.content.Intent;
import android.net.Uri;
import android.os.Handler;
import android.os.Looper;
import android.view.LayoutInflater;
import android.view.View;
import android.widget.Toast;

import java.io.File;
import java.io.FileOutputStream;
import java.io.IOException;
import java.io.OutputStreamWriter;
import java.io.Writer;

/**
 * Todo lo relacionado con mandar el corte a otra app: el menú de "¿cómo
 * quieres compartir?" y la generación de cada formato.
 */
public final class Compartir {

    private Compartir() {
    }

    /** Menú con las tres formas de compartir. */
    public static void menu(final Activity act, final Reporte reporte) {
        View vista = LayoutInflater.from(act).inflate(R.layout.dialogo_compartir, null);
        final AlertDialog dialogo = new AlertDialog.Builder(act)
                .setTitle(R.string.compartir_titulo)
                .setView(vista)
                .setNegativeButton(R.string.cancelar, null)
                .create();

        vista.findViewById(R.id.op_texto).setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View v) {
                dialogo.dismiss();
                texto(act, reporte);
            }
        });
        vista.findViewById(R.id.op_imagen).setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View v) {
                dialogo.dismiss();
                archivo(act, reporte, "image/png", true);
            }
        });
        vista.findViewById(R.id.op_pdf).setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View v) {
                dialogo.dismiss();
                archivo(act, reporte, "application/pdf", false);
            }
        });

        dialogo.show();
    }

    // ------------------------------------------------------------------ texto

    /** Manda el corte como texto plano, sin archivos de por medio. */
    public static void texto(Activity act, Reporte reporte) {
        String cuerpo = ExportTexto.armar(reporte);
        Intent i = new Intent(Intent.ACTION_SEND);
        i.setType("text/plain");
        i.putExtra(Intent.EXTRA_SUBJECT, reporte.titulo + " · " + reporte.subtitulo);
        i.putExtra(Intent.EXTRA_TEXT, cuerpo);
        act.startActivity(Intent.createChooser(i, act.getString(R.string.menu_compartir)));
    }

    // -------------------------------------------------- imagen, PDF y respaldo

    /**
     * Genera el archivo en segundo plano (dibujar el PDF o la imagen tarda un
     * momento) y luego abre el menú de compartir del sistema.
     */
    private static void archivo(final Activity act, final Reporte reporte,
                                final String tipo, final boolean esImagen) {
        Toast.makeText(act, "Preparando…", Toast.LENGTH_SHORT).show();
        final Handler handler = new Handler(Looper.getMainLooper());

        new Thread(new Runnable() {
            @Override
            public void run() {
                File generado = null;
                String error = null;
                try {
                    generado = esImagen
                            ? ExportImagen.generar(act, reporte)
                            : ExportPdf.generar(act, reporte);
                } catch (Throwable e) {
                    // Incluye OutOfMemoryError: un corte larguísimo no debe
                    // tumbar la app, sólo avisar que no se pudo.
                    error = e.getMessage() != null ? e.getMessage() : e.getClass().getSimpleName();
                }

                final File archivo = generado;
                final String fallo = error;
                handler.post(new Runnable() {
                    @Override
                    public void run() {
                        if (act.isFinishing()) {
                            return;
                        }
                        if (archivo == null) {
                            Toast.makeText(act, "No se pudo generar el archivo: " + fallo,
                                    Toast.LENGTH_LONG).show();
                            return;
                        }
                        enviar(act, reporte, archivo, tipo);
                    }
                });
            }
        }).start();
    }

    private static void enviar(Activity act, Reporte reporte, File archivo, String tipo) {
        Uri uri = ArchivosProvider.uriDe(archivo);
        Intent i = new Intent(Intent.ACTION_SEND);
        i.setType(tipo);
        i.putExtra(Intent.EXTRA_STREAM, uri);
        i.putExtra(Intent.EXTRA_SUBJECT, reporte.titulo + " · " + reporte.subtitulo);
        i.putExtra(Intent.EXTRA_TEXT, reporte.titulo + " — " + reporte.subtitulo
                + "\nTotal: " + Dinero.formato(reporte.granTotal()));
        i.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION);
        act.startActivity(Intent.createChooser(i, act.getString(R.string.menu_compartir)));
    }

    /** Guarda el texto como archivo .txt, por si se quiere mandar como adjunto. */
    public static File guardarTexto(Activity act, Reporte reporte) throws IOException {
        File destino = Archivos.nuevo(act, reporte.nombreArchivo + ".txt");
        Writer w = new OutputStreamWriter(new FileOutputStream(destino), "UTF-8");
        try {
            w.write(ExportTexto.armar(reporte));
        } finally {
            w.close();
        }
        return destino;
    }
}
