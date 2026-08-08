package mx.tianguis.cuentas;

import android.app.Activity;
import android.app.AlertDialog;
import android.app.DatePickerDialog;
import android.content.DialogInterface;
import android.content.Intent;
import android.os.Bundle;
import android.view.LayoutInflater;
import android.view.Menu;
import android.view.MenuItem;
import android.view.View;
import android.widget.AdapterView;
import android.widget.ArrayAdapter;
import android.widget.AutoCompleteTextView;
import android.widget.DatePicker;
import android.widget.EditText;
import android.widget.ListView;
import android.widget.TextView;
import android.widget.Toast;

import java.util.Calendar;
import java.util.List;

/**
 * Pantalla principal: la cuenta de un día de tianguis.
 *
 * Arriba se elige el día, en medio se anota lo que se va vendiendo y abajo
 * siempre están a la vista los totales de cada artesano y el del día.
 */
public class MainActivity extends Activity {

    private static final String ESTADO_FECHA = "fecha";

    private String fecha;
    private String artesano = Venta.ARTESANO_A;

    private Db db;
    private VentaAdapter adaptador;

    private TextView txtDiaSemana;
    private TextView txtFecha;
    private TextView chipA;
    private TextView chipB;
    private AutoCompleteTextView campoDescripcion;
    private EditText campoCantidad;
    private EditText campoPrecio;
    private ListView lista;
    private TextView txtVacio;
    private TextView lblTotalA;
    private TextView lblTotalB;
    private TextView txtTotalA;
    private TextView txtTotalB;
    private TextView txtTotalDia;

    @Override
    protected void onCreate(Bundle estado) {
        super.onCreate(estado);
        setContentView(R.layout.activity_main);

        db = Db.obtener(this);
        fecha = estado != null ? estado.getString(ESTADO_FECHA) : null;
        if (fecha == null) {
            // Al volver desde la vista de la semana se abre el día que se tocó.
            fecha = getIntent().getStringExtra(SemanaActivity.EXTRA_FECHA);
        }
        if (fecha == null) {
            fecha = Fechas.hoy();
        }
        artesano = Prefs.ultimoArtesano(this);

        enlazarVistas();
        prepararBarraDeFecha();
        prepararFormulario();
        prepararLista();
        prepararBotones();

        pintarChips();
        refrescar();
    }

    @Override
    protected void onSaveInstanceState(Bundle estado) {
        super.onSaveInstanceState(estado);
        estado.putString(ESTADO_FECHA, fecha);
    }

    @Override
    protected void onResume() {
        super.onResume();
        refrescar();
    }

    private void enlazarVistas() {
        txtDiaSemana = (TextView) findViewById(R.id.txt_dia_semana);
        txtFecha = (TextView) findViewById(R.id.txt_fecha);
        chipA = (TextView) findViewById(R.id.chip_a);
        chipB = (TextView) findViewById(R.id.chip_b);
        campoDescripcion = (AutoCompleteTextView) findViewById(R.id.campo_descripcion);
        campoCantidad = (EditText) findViewById(R.id.campo_cantidad);
        campoPrecio = (EditText) findViewById(R.id.campo_precio);
        lista = (ListView) findViewById(R.id.lista);
        txtVacio = (TextView) findViewById(R.id.txt_vacio);
        lblTotalA = (TextView) findViewById(R.id.lbl_total_a);
        lblTotalB = (TextView) findViewById(R.id.lbl_total_b);
        txtTotalA = (TextView) findViewById(R.id.txt_total_a);
        txtTotalB = (TextView) findViewById(R.id.txt_total_b);
        txtTotalDia = (TextView) findViewById(R.id.txt_total_dia);
    }

    // ---------------------------------------------------------- barra de fecha

    private void prepararBarraDeFecha() {
        findViewById(R.id.btn_dia_anterior).setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View v) {
                irA(Fechas.sumarDias(fecha, -1));
            }
        });
        findViewById(R.id.btn_dia_siguiente).setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View v) {
                irA(Fechas.sumarDias(fecha, 1));
            }
        });
        findViewById(R.id.btn_hoy).setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View v) {
                irA(Fechas.hoy());
            }
        });
        findViewById(R.id.btn_fecha).setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View v) {
                elegirFecha();
            }
        });
    }

    private void elegirFecha() {
        Calendar c = Fechas.calendario(fecha);
        new DatePickerDialog(this, new DatePickerDialog.OnDateSetListener() {
            @Override
            public void onDateSet(DatePicker picker, int anio, int mes, int dia) {
                Calendar elegido = Fechas.calendarioHoy();
                elegido.set(anio, mes, dia);
                irA(Fechas.clave(elegido));
            }
        }, c.get(Calendar.YEAR), c.get(Calendar.MONTH), c.get(Calendar.DAY_OF_MONTH)).show();
    }

    private void irA(String nuevaFecha) {
        fecha = nuevaFecha;
        refrescar();
    }

    // ------------------------------------------------------------- formulario

    private void prepararFormulario() {
        chipA.setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View v) {
                artesano = Venta.ARTESANO_A;
                Prefs.guardarUltimoArtesano(MainActivity.this, artesano);
                pintarChips();
            }
        });
        chipB.setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View v) {
                artesano = Venta.ARTESANO_B;
                Prefs.guardarUltimoArtesano(MainActivity.this, artesano);
                pintarChips();
            }
        });

        campoDescripcion.setThreshold(1);
        // Al elegir una pieza ya vendida antes, se propone el mismo precio.
        campoDescripcion.setOnItemClickListener(new AdapterView.OnItemClickListener() {
            @Override
            public void onItemClick(AdapterView<?> padre, View v, int posicion, long id) {
                Object elegido = padre.getItemAtPosition(posicion);
                if (elegido == null) {
                    return;
                }
                long precio = db.ultimoPrecioDe(elegido.toString());
                if (precio >= 0 && campoPrecio.getText().toString().trim().length() == 0) {
                    campoPrecio.setText(Dinero.formatoSinSigno(precio));
                }
            }
        });

        findViewById(R.id.btn_agregar).setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View v) {
                agregar();
            }
        });
    }

    private void pintarChips() {
        chipA.setText("A · " + Prefs.nombreA(this));
        chipB.setText("B · " + Prefs.nombreB(this));
        DialogoVenta.pintarChips(chipA, chipB, artesano);
    }

    private void agregar() {
        String descripcion = campoDescripcion.getText().toString().trim();
        if (descripcion.length() == 0) {
            campoDescripcion.setError("Escribe qué se vendió");
            campoDescripcion.requestFocus();
            return;
        }
        int cantidad = DialogoVenta.enteroDe(campoCantidad.getText().toString());
        if (cantidad <= 0) {
            campoCantidad.setError("¿Cuántas piezas?");
            campoCantidad.requestFocus();
            return;
        }
        long precio = Dinero.aCentavos(campoPrecio.getText().toString());
        if (precio < 0) {
            campoPrecio.setError("Escribe el precio de cada pieza");
            campoPrecio.requestFocus();
            return;
        }

        Venta v = new Venta();
        v.fecha = fecha;
        v.artesano = artesano;
        v.descripcion = descripcion;
        v.cantidad = cantidad;
        v.precio = precio;
        v.creado = System.currentTimeMillis();
        db.insertar(v);

        campoDescripcion.setText("");
        campoPrecio.setText("");
        campoCantidad.setText("1");
        campoDescripcion.requestFocus();

        refrescar();
        lista.smoothScrollToPosition(Math.max(0, adaptador.getCount() - 1));
        Toast.makeText(this, "Anotado: " + Dinero.formato(v.total()), Toast.LENGTH_SHORT).show();
    }

    // ------------------------------------------------------------------ lista

    private void prepararLista() {
        adaptador = new VentaAdapter(this);
        lista.setAdapter(adaptador);
        lista.setEmptyView(txtVacio);
        lista.setOnItemClickListener(new AdapterView.OnItemClickListener() {
            @Override
            public void onItemClick(AdapterView<?> padre, View v, int posicion, long id) {
                DialogoVenta.editar(MainActivity.this, adaptador.getItem(posicion),
                        new DialogoVenta.AlCambiar() {
                            @Override
                            public void cambio() {
                                refrescar();
                            }
                        });
            }
        });
    }

    private void prepararBotones() {
        findViewById(R.id.btn_semana).setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View v) {
                Intent i = new Intent(MainActivity.this, SemanaActivity.class);
                i.putExtra(SemanaActivity.EXTRA_FECHA, fecha);
                startActivity(i);
            }
        });
        findViewById(R.id.btn_compartir).setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View v) {
                Compartir.menu(MainActivity.this, Reportes.delDia(MainActivity.this, fecha));
            }
        });
    }

    // ------------------------------------------------------------- refrescado

    private void refrescar() {
        txtDiaSemana.setText(Fechas.nombreDia(fecha).toUpperCase(Fechas.MX));
        txtFecha.setText(Fechas.diaYMes(fecha));
        findViewById(R.id.btn_hoy).setVisibility(
                Fechas.hoy().equals(fecha) ? View.GONE : View.VISIBLE);

        List<Venta> ventas = db.ventasDelDia(fecha);
        adaptador.reemplazar(ventas);

        Totales t = new Totales();
        for (int i = 0; i < ventas.size(); i++) {
            t.sumar(ventas.get(i));
        }
        lblTotalA.setText(Prefs.nombreA(this).toUpperCase(Fechas.MX) + " (A)");
        lblTotalB.setText(Prefs.nombreB(this).toUpperCase(Fechas.MX) + " (B)");
        txtTotalA.setText(Dinero.formato(t.a));
        txtTotalB.setText(Dinero.formato(t.b));
        txtTotalDia.setText(Dinero.formato(t.total()));

        campoDescripcion.setAdapter(new ArrayAdapter<String>(this,
                android.R.layout.simple_dropdown_item_1line, db.descripcionesUsadas()));
        pintarChips();
    }

    // ------------------------------------------------------------------- menú

    @Override
    public boolean onCreateOptionsMenu(Menu menu) {
        menu.add(0, 1, 0, R.string.menu_semana).setShowAsAction(MenuItem.SHOW_AS_ACTION_NEVER);
        menu.add(0, 2, 1, R.string.menu_compartir).setShowAsAction(MenuItem.SHOW_AS_ACTION_NEVER);
        menu.add(0, 3, 2, R.string.menu_nombres).setShowAsAction(MenuItem.SHOW_AS_ACTION_NEVER);
        return true;
    }

    @Override
    public boolean onOptionsItemSelected(MenuItem item) {
        switch (item.getItemId()) {
            case 1:
                Intent i = new Intent(this, SemanaActivity.class);
                i.putExtra(SemanaActivity.EXTRA_FECHA, fecha);
                startActivity(i);
                return true;
            case 2:
                Compartir.menu(this, Reportes.delDia(this, fecha));
                return true;
            case 3:
                editarNombres();
                return true;
            default:
                return super.onOptionsItemSelected(item);
        }
    }

    private void editarNombres() {
        View vista = LayoutInflater.from(this).inflate(R.layout.dialogo_nombres, null);
        final EditText campoA = (EditText) vista.findViewById(R.id.campo_a);
        final EditText campoB = (EditText) vista.findViewById(R.id.campo_b);
        campoA.setText(Prefs.nombreA(this));
        campoB.setText(Prefs.nombreB(this));

        new AlertDialog.Builder(this)
                .setTitle(R.string.menu_nombres)
                .setView(vista)
                .setNegativeButton(R.string.cancelar, null)
                .setPositiveButton(R.string.guardar, new DialogInterface.OnClickListener() {
                    @Override
                    public void onClick(DialogInterface d, int cual) {
                        Prefs.guardarNombres(MainActivity.this,
                                campoA.getText().toString(), campoB.getText().toString());
                        refrescar();
                    }
                })
                .show();
    }
}
