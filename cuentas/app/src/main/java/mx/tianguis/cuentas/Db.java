package mx.tianguis.cuentas;

import android.content.ContentValues;
import android.content.Context;
import android.database.Cursor;
import android.database.sqlite.SQLiteDatabase;
import android.database.sqlite.SQLiteOpenHelper;

import java.util.ArrayList;
import java.util.List;

/**
 * Base de datos local de las ventas. Todo vive en el telefono, sin internet
 * y sin cuentas de usuario.
 */
public class Db extends SQLiteOpenHelper {

    private static final String ARCHIVO = "cuentas.db";
    private static final int VERSION = 1;

    private static final String TABLA = "ventas";
    private static final String COL_ID = "id";
    private static final String COL_FECHA = "fecha";
    private static final String COL_ARTESANO = "artesano";
    private static final String COL_DESCRIPCION = "descripcion";
    private static final String COL_CANTIDAD = "cantidad";
    private static final String COL_PRECIO = "precio";
    private static final String COL_CREADO = "creado";

    private static Db instancia;

    public static synchronized Db obtener(Context contexto) {
        if (instancia == null) {
            instancia = new Db(contexto.getApplicationContext());
        }
        return instancia;
    }

    private Db(Context contexto) {
        super(contexto, ARCHIVO, null, VERSION);
    }

    @Override
    public void onCreate(SQLiteDatabase db) {
        db.execSQL("CREATE TABLE " + TABLA + " ("
                + COL_ID + " INTEGER PRIMARY KEY AUTOINCREMENT, "
                + COL_FECHA + " TEXT NOT NULL, "
                + COL_ARTESANO + " TEXT NOT NULL, "
                + COL_DESCRIPCION + " TEXT NOT NULL, "
                + COL_CANTIDAD + " INTEGER NOT NULL, "
                + COL_PRECIO + " INTEGER NOT NULL, "
                + COL_CREADO + " INTEGER NOT NULL)");
        db.execSQL("CREATE INDEX idx_ventas_fecha ON " + TABLA + "(" + COL_FECHA + ")");
    }

    @Override
    public void onUpgrade(SQLiteDatabase db, int anterior, int nueva) {
        // Version 1: todavia no hay migraciones que aplicar.
    }

    // ---------------------------------------------------------------- escribir

    /** Guarda una venta nueva y devuelve su id. */
    public long insertar(Venta v) {
        ContentValues cv = new ContentValues();
        cv.put(COL_FECHA, v.fecha);
        cv.put(COL_ARTESANO, v.artesano);
        cv.put(COL_DESCRIPCION, v.descripcion);
        cv.put(COL_CANTIDAD, v.cantidad);
        cv.put(COL_PRECIO, v.precio);
        cv.put(COL_CREADO, v.creado == 0 ? System.currentTimeMillis() : v.creado);
        v.id = getWritableDatabase().insert(TABLA, null, cv);
        return v.id;
    }

    public void actualizar(Venta v) {
        ContentValues cv = new ContentValues();
        cv.put(COL_FECHA, v.fecha);
        cv.put(COL_ARTESANO, v.artesano);
        cv.put(COL_DESCRIPCION, v.descripcion);
        cv.put(COL_CANTIDAD, v.cantidad);
        cv.put(COL_PRECIO, v.precio);
        getWritableDatabase().update(TABLA, cv, COL_ID + " = ?",
                new String[]{String.valueOf(v.id)});
    }

    public void borrar(long id) {
        getWritableDatabase().delete(TABLA, COL_ID + " = ?", new String[]{String.valueOf(id)});
    }

    // ------------------------------------------------------------------- leer

    /** Ventas de un dia, en el orden en que se fueron anotando. */
    public List<Venta> ventasDelDia(String fecha) {
        return consultar(COL_FECHA + " = ?", new String[]{fecha});
    }

    /** Ventas de un rango de dias, ambos extremos incluidos. */
    public List<Venta> ventasDelRango(String desde, String hasta) {
        return consultar(COL_FECHA + " BETWEEN ? AND ?", new String[]{desde, hasta});
    }

    private List<Venta> consultar(String donde, String[] args) {
        List<Venta> lista = new ArrayList<Venta>();
        Cursor c = getReadableDatabase().query(TABLA, null, donde, args, null, null,
                COL_FECHA + " ASC, " + COL_CREADO + " ASC, " + COL_ID + " ASC");
        try {
            int iId = c.getColumnIndexOrThrow(COL_ID);
            int iFecha = c.getColumnIndexOrThrow(COL_FECHA);
            int iArtesano = c.getColumnIndexOrThrow(COL_ARTESANO);
            int iDesc = c.getColumnIndexOrThrow(COL_DESCRIPCION);
            int iCant = c.getColumnIndexOrThrow(COL_CANTIDAD);
            int iPrecio = c.getColumnIndexOrThrow(COL_PRECIO);
            int iCreado = c.getColumnIndexOrThrow(COL_CREADO);
            while (c.moveToNext()) {
                Venta v = new Venta();
                v.id = c.getLong(iId);
                v.fecha = c.getString(iFecha);
                v.artesano = c.getString(iArtesano);
                v.descripcion = c.getString(iDesc);
                v.cantidad = c.getInt(iCant);
                v.precio = c.getLong(iPrecio);
                v.creado = c.getLong(iCreado);
                lista.add(v);
            }
        } finally {
            c.close();
        }
        return lista;
    }

    /** Totales de un dia sin traer todas las filas. */
    public Totales totalesDelDia(String fecha) {
        Totales t = new Totales();
        Cursor c = getReadableDatabase().rawQuery(
                "SELECT " + COL_ARTESANO + ", SUM(" + COL_CANTIDAD + " * " + COL_PRECIO + "), "
                        + "SUM(" + COL_CANTIDAD + ") FROM " + TABLA
                        + " WHERE " + COL_FECHA + " = ? GROUP BY " + COL_ARTESANO,
                new String[]{fecha});
        try {
            while (c.moveToNext()) {
                boolean esA = Venta.ARTESANO_A.equals(c.getString(0));
                if (esA) {
                    t.a = c.getLong(1);
                    t.piezasA = c.getInt(2);
                } else {
                    t.b = c.getLong(1);
                    t.piezasB = c.getInt(2);
                }
            }
        } finally {
            c.close();
        }
        return t;
    }

    /**
     * Sugerencias para el campo de descripcion: lo mas vendido primero, para
     * que la vendedora no tenga que teclear de nuevo lo de siempre.
     */
    public List<String> descripcionesUsadas() {
        List<String> lista = new ArrayList<String>();
        Cursor c = getReadableDatabase().rawQuery(
                "SELECT " + COL_DESCRIPCION + ", COUNT(*) AS veces FROM " + TABLA
                        + " GROUP BY " + COL_DESCRIPCION + " ORDER BY veces DESC, "
                        + COL_DESCRIPCION + " ASC LIMIT 60", null);
        try {
            while (c.moveToNext()) {
                lista.add(c.getString(0));
            }
        } finally {
            c.close();
        }
        return lista;
    }

    /**
     * Ultimo precio con el que se vendio una descripcion, para rellenarlo solo.
     * Devuelve -1 si esa pieza nunca se ha vendido.
     */
    public long ultimoPrecioDe(String descripcion) {
        Cursor c = getReadableDatabase().rawQuery(
                "SELECT " + COL_PRECIO + " FROM " + TABLA + " WHERE " + COL_DESCRIPCION
                        + " = ? ORDER BY " + COL_CREADO + " DESC LIMIT 1",
                new String[]{descripcion});
        try {
            if (c.moveToFirst()) {
                return c.getLong(0);
            }
        } finally {
            c.close();
        }
        return -1;
    }
}
