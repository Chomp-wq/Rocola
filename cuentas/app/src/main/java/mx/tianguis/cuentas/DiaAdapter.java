package mx.tianguis.cuentas;

import android.content.Context;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;
import android.widget.BaseAdapter;
import android.widget.TextView;

import java.util.ArrayList;
import java.util.List;

/** Lista de los días de la semana con lo que se vendió en cada uno. */
public class DiaAdapter extends BaseAdapter {

    /** Un renglón: la fecha y sus totales. */
    public static class Dia {
        public String fecha;
        public Totales totales;
    }

    private final Context ctx;
    private final List<Dia> dias = new ArrayList<Dia>();

    public DiaAdapter(Context ctx) {
        this.ctx = ctx;
    }

    public void reemplazar(List<Dia> nuevos) {
        dias.clear();
        dias.addAll(nuevos);
        notifyDataSetChanged();
    }

    @Override
    public int getCount() {
        return dias.size();
    }

    @Override
    public Dia getItem(int posicion) {
        return dias.get(posicion);
    }

    @Override
    public long getItemId(int posicion) {
        return posicion;
    }

    @Override
    public View getView(int posicion, View reciclada, ViewGroup padre) {
        View fila = reciclada;
        if (fila == null) {
            fila = LayoutInflater.from(ctx).inflate(R.layout.item_dia, padre, false);
        }
        Dia d = dias.get(posicion);

        ((TextView) fila.findViewById(R.id.txt_dia)).setText(Fechas.nombreDia(d.fecha));
        ((TextView) fila.findViewById(R.id.txt_fecha_corta)).setText(
                Fechas.diaYMes(d.fecha) + " · " + Reportes.piezas(d.totales.piezas()));
        ((TextView) fila.findViewById(R.id.txt_dia_a)).setText(
                "A " + Dinero.formato(d.totales.a));
        ((TextView) fila.findViewById(R.id.txt_dia_b)).setText(
                "B " + Dinero.formato(d.totales.b));
        ((TextView) fila.findViewById(R.id.txt_dia_total)).setText(
                Dinero.formato(d.totales.total()));
        return fila;
    }
}
