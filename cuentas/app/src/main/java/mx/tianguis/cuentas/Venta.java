package mx.tianguis.cuentas;

/**
 * Una venta anotada en el dia: a que artesano pertenece la pieza, que fue,
 * cuantas piezas y a que precio.
 *
 * El dinero se guarda siempre en centavos (long) para que las sumas sean
 * exactas; nunca en double.
 */
public class Venta {

    public static final String ARTESANO_A = "A";
    public static final String ARTESANO_B = "B";

    public long id;
    /** Fecha del dia de tianguis en formato yyyy-MM-dd. */
    public String fecha;
    /** "A" o "B". */
    public String artesano;
    public String descripcion;
    public int cantidad;
    /** Precio por pieza, en centavos. */
    public long precio;
    /** Momento en que se anoto, en milisegundos. */
    public long creado;

    public Venta() {
        this.cantidad = 1;
        this.artesano = ARTESANO_A;
        this.descripcion = "";
    }

    /** Importe total de la linea, en centavos. */
    public long total() {
        return cantidad * precio;
    }

    public boolean esDeA() {
        return ARTESANO_A.equals(artesano);
    }
}
