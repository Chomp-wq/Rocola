package mx.tianguis.cuentas;

import android.content.Context;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;
import android.widget.BaseAdapter;
import android.widget.TextView;

import java.util.ArrayList;
import java.util.List;

/** Lista de lo vendido en el día. */
public class VentaAdapter extends BaseAdapter {

    private final Context ctx;
    private final List<Venta> ventas = new ArrayList<Venta>();

    public VentaAdapter(Context ctx) {
        this.ctx = ctx;
    }

    public void reemplazar(List<Venta> nuevas) {
        ventas.clear();
        ventas.addAll(nuevas);
        notifyDataSetChanged();
    }

    @Override
    public int getCount() {
        return ventas.size();
    }

    @Override
    public Venta getItem(int posicion) {
        return ventas.get(posicion);
    }

    @Override
    public long getItemId(int posicion) {
        return ventas.get(posicion).id;
    }

    @Override
    public boolean hasStableIds() {
        return true;
    }

    @Override
    public View getView(int posicion, View reciclada, ViewGroup padre) {
        View fila = reciclada;
        if (fila == null) {
            fila = LayoutInflater.from(ctx).inflate(R.layout.item_venta, padre, false);
        }
        Venta v = ventas.get(posicion);

        TextView insignia = (TextView) fila.findViewById(R.id.insignia);
        insignia.setText(v.artesano);
        insignia.setBackgroundResource(v.esDeA() ? R.drawable.insignia_a : R.drawable.insignia_b);

        ((TextView) fila.findViewById(R.id.txt_descripcion)).setText(v.descripcion);
        ((TextView) fila.findViewById(R.id.txt_detalle)).setText(
                Prefs.nombre(ctx, v.artesano) + " · " + v.cantidad + " × " + Dinero.formato(v.precio));
        ((TextView) fila.findViewById(R.id.txt_monto)).setText(Dinero.formato(v.total()));
        return fila;
    }
}
