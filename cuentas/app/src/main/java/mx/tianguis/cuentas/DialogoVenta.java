package mx.tianguis.cuentas;

import android.app.Activity;
import android.app.AlertDialog;
import android.content.DialogInterface;
import android.view.LayoutInflater;
import android.view.View;
import android.widget.ArrayAdapter;
import android.widget.AutoCompleteTextView;
import android.widget.EditText;
import android.widget.TextView;
import android.widget.Toast;

import java.util.List;

/** Ventana para corregir o borrar una venta ya anotada. */
public final class DialogoVenta {

    /** Aviso de que la lista cambió y hay que volver a pintarla. */
    public interface AlCambiar {
        void cambio();
    }

    private DialogoVenta() {
    }

    public static void editar(final Activity act, final Venta original, final AlCambiar aviso) {
        final Db db = Db.obtener(act);
        View vista = LayoutInflater.from(act).inflate(R.layout.dialogo_venta, null);

        final TextView chipA = (TextView) vista.findViewById(R.id.chip_a);
        final TextView chipB = (TextView) vista.findViewById(R.id.chip_b);
        final AutoCompleteTextView campoDesc =
                (AutoCompleteTextView) vista.findViewById(R.id.campo_descripcion);
        final EditText campoCant = (EditText) vista.findViewById(R.id.campo_cantidad);
        final EditText campoPrecio = (EditText) vista.findViewById(R.id.campo_precio);

        chipA.setText("A · " + Prefs.nombreA(act));
        chipB.setText("B · " + Prefs.nombreB(act));

        // El artesano seleccionado se guarda en el tag para leerlo al aceptar.
        final String[] artesano = {original.artesano};
        pintarChips(chipA, chipB, artesano[0]);
        chipA.setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View v) {
                artesano[0] = Venta.ARTESANO_A;
                pintarChips(chipA, chipB, artesano[0]);
            }
        });
        chipB.setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View v) {
                artesano[0] = Venta.ARTESANO_B;
                pintarChips(chipA, chipB, artesano[0]);
            }
        });

        List<String> sugerencias = db.descripcionesUsadas();
        campoDesc.setAdapter(new ArrayAdapter<String>(act,
                android.R.layout.simple_dropdown_item_1line, sugerencias));
        campoDesc.setThreshold(1);

        campoDesc.setText(original.descripcion);
        campoCant.setText(String.valueOf(original.cantidad));
        campoPrecio.setText(Dinero.formatoSinSigno(original.precio));

        AlertDialog dialogo = new AlertDialog.Builder(act)
                .setTitle(R.string.editar_venta)
                .setView(vista)
                .setPositiveButton(R.string.guardar, null)
                .setNegativeButton(R.string.cancelar, null)
                .setNeutralButton(R.string.eliminar, new DialogInterface.OnClickListener() {
                    @Override
                    public void onClick(DialogInterface d, int cual) {
                        confirmarBorrado(act, db, original, aviso);
                    }
                })
                .create();

        dialogo.show();
        // Se engancha después de show() para poder validar sin cerrar la ventana.
        dialogo.getButton(AlertDialog.BUTTON_POSITIVE).setOnClickListener(
                new GuardarListener(act, db, dialogo, original, artesano,
                        campoDesc, campoCant, campoPrecio, aviso));
    }

    private static class GuardarListener implements View.OnClickListener {
        private final Activity act;
        private final Db db;
        private final AlertDialog dialogo;
        private final Venta original;
        private final String[] artesano;
        private final AutoCompleteTextView campoDesc;
        private final EditText campoCant;
        private final EditText campoPrecio;
        private final AlCambiar aviso;

        GuardarListener(Activity act, Db db, AlertDialog dialogo, Venta original,
                        String[] artesano, AutoCompleteTextView campoDesc,
                        EditText campoCant, EditText campoPrecio, AlCambiar aviso) {
            this.act = act;
            this.db = db;
            this.dialogo = dialogo;
            this.original = original;
            this.artesano = artesano;
            this.campoDesc = campoDesc;
            this.campoCant = campoCant;
            this.campoPrecio = campoPrecio;
            this.aviso = aviso;
        }

        @Override
        public void onClick(View v) {
            String descripcion = campoDesc.getText().toString().trim();
            if (descripcion.length() == 0) {
                campoDesc.setError("Escribe qué se vendió");
                return;
            }
            int cantidad = enteroDe(campoCant.getText().toString());
            if (cantidad <= 0) {
                campoCant.setError("¿Cuántas piezas?");
                return;
            }
            long precio = Dinero.aCentavos(campoPrecio.getText().toString());
            if (precio < 0) {
                campoPrecio.setError("Revisa el precio");
                return;
            }

            original.artesano = artesano[0];
            original.descripcion = descripcion;
            original.cantidad = cantidad;
            original.precio = precio;
            db.actualizar(original);

            dialogo.dismiss();
            Toast.makeText(act, "Venta corregida", Toast.LENGTH_SHORT).show();
            aviso.cambio();
        }
    }

    private static void confirmarBorrado(final Activity act, final Db db, final Venta v,
                                         final AlCambiar aviso) {
        new AlertDialog.Builder(act)
                .setTitle(R.string.eliminar)
                .setMessage("¿Borrar «" + v.descripcion + "» de la cuenta del día?")
                .setNegativeButton(R.string.cancelar, null)
                .setPositiveButton(R.string.eliminar, new DialogInterface.OnClickListener() {
                    @Override
                    public void onClick(DialogInterface d, int cual) {
                        db.borrar(v.id);
                        Toast.makeText(act, "Venta borrada", Toast.LENGTH_SHORT).show();
                        aviso.cambio();
                    }
                })
                .show();
    }

    static void pintarChips(TextView chipA, TextView chipB, String artesano) {
        boolean esA = Venta.ARTESANO_A.equals(artesano);
        chipA.setSelected(esA);
        chipB.setSelected(!esA);
        chipA.setTextColor(esA ? 0xFFFFFFFF : Reportes.COLOR_A);
        chipB.setTextColor(!esA ? 0xFFFFFFFF : Reportes.COLOR_B);
    }

    static int enteroDe(String texto) {
        try {
            return Integer.parseInt(texto.trim());
        } catch (NumberFormatException e) {
            return 0;
        }
    }
}
