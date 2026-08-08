package mx.tianguis.cuentas;

import android.content.ContentProvider;
import android.content.ContentValues;
import android.database.Cursor;
import android.database.MatrixCursor;
import android.net.Uri;
import android.os.ParcelFileDescriptor;
import android.provider.OpenableColumns;

import java.io.File;
import java.io.FileNotFoundException;
import java.io.IOException;

/**
 * Entrega los archivos generados (PNG, PDF, TXT) a la app con la que se
 * comparte. Es un FileProvider mínimo escrito a mano para que la aplicación
 * no dependa de ninguna librería externa.
 *
 * Sólo sirve archivos que estén dentro de la carpeta de caché "compartir"; el
 * camino se normaliza antes de abrirlo para que nadie pueda pedir otra cosa.
 */
public class ArchivosProvider extends ContentProvider {

    public static final String AUTORIDAD = "mx.tianguis.cuentas.archivos";

    public static Uri uriDe(File archivo) {
        return new Uri.Builder()
                .scheme("content")
                .authority(AUTORIDAD)
                .appendPath(archivo.getName())
                .build();
    }

    @Override
    public boolean onCreate() {
        return true;
    }

    /** Resuelve la Uri a un archivo real dentro de la carpeta permitida. */
    private File resolver(Uri uri) throws FileNotFoundException {
        String nombre = uri.getLastPathSegment();
        if (nombre == null || nombre.length() == 0) {
            throw new FileNotFoundException("Uri sin nombre de archivo: " + uri);
        }
        try {
            File carpeta = Archivos.carpeta(getContext()).getCanonicalFile();
            File archivo = new File(carpeta, nombre).getCanonicalFile();
            if (!archivo.getPath().startsWith(carpeta.getPath() + File.separator)) {
                throw new FileNotFoundException("Ruta fuera de la carpeta compartida");
            }
            if (!archivo.exists()) {
                throw new FileNotFoundException("No existe " + nombre);
            }
            return archivo;
        } catch (IOException e) {
            throw new FileNotFoundException(e.getMessage());
        }
    }

    @Override
    public ParcelFileDescriptor openFile(Uri uri, String modo) throws FileNotFoundException {
        return ParcelFileDescriptor.open(resolver(uri), ParcelFileDescriptor.MODE_READ_ONLY);
    }

    /**
     * Las apps que reciben el archivo preguntan por su nombre y su tamaño
     * antes de adjuntarlo.
     */
    @Override
    public Cursor query(Uri uri, String[] columnas, String seleccion, String[] args,
                        String orden) {
        File archivo;
        try {
            archivo = resolver(uri);
        } catch (FileNotFoundException e) {
            return null;
        }
        String[] pedidas = columnas != null ? columnas
                : new String[]{OpenableColumns.DISPLAY_NAME, OpenableColumns.SIZE};

        Object[] valores = new Object[pedidas.length];
        for (int i = 0; i < pedidas.length; i++) {
            if (OpenableColumns.DISPLAY_NAME.equals(pedidas[i])) {
                valores[i] = archivo.getName();
            } else if (OpenableColumns.SIZE.equals(pedidas[i])) {
                valores[i] = archivo.length();
            } else {
                valores[i] = null;
            }
        }
        MatrixCursor cursor = new MatrixCursor(pedidas, 1);
        cursor.addRow(valores);
        return cursor;
    }

    @Override
    public String getType(Uri uri) {
        String nombre = uri.getLastPathSegment();
        if (nombre == null) {
            return "application/octet-stream";
        }
        String bajo = nombre.toLowerCase(Fechas.MX);
        if (bajo.endsWith(".pdf")) {
            return "application/pdf";
        }
        if (bajo.endsWith(".png")) {
            return "image/png";
        }
        if (bajo.endsWith(".txt")) {
            return "text/plain";
        }
        return "application/octet-stream";
    }

    // La app nunca escribe a través del proveedor.

    @Override
    public Uri insert(Uri uri, ContentValues valores) {
        throw new UnsupportedOperationException("Sólo lectura");
    }

    @Override
    public int delete(Uri uri, String seleccion, String[] args) {
        throw new UnsupportedOperationException("Sólo lectura");
    }

    @Override
    public int update(Uri uri, ContentValues valores, String seleccion, String[] args) {
        throw new UnsupportedOperationException("Sólo lectura");
    }
}
