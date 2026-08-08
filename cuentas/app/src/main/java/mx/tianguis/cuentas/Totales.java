package mx.tianguis.cuentas;

/** Sumas de un dia o de una semana, separadas por artesano. */
public class Totales {

    /** Importe de A, en centavos. */
    public long a;
    /** Importe de B, en centavos. */
    public long b;
    /** Piezas vendidas de A. */
    public int piezasA;
    /** Piezas vendidas de B. */
    public int piezasB;

    public long total() {
        return a + b;
    }

    public int piezas() {
        return piezasA + piezasB;
    }

    public boolean vacio() {
        return piezasA == 0 && piezasB == 0;
    }

    public void sumar(Totales otro) {
        a += otro.a;
        b += otro.b;
        piezasA += otro.piezasA;
        piezasB += otro.piezasB;
    }

    public void sumar(Venta v) {
        if (v.esDeA()) {
            a += v.total();
            piezasA += v.cantidad;
        } else {
            b += v.total();
            piezasB += v.cantidad;
        }
    }
}
