package mx.tianguis.cuentas;

import android.app.Activity;
import android.content.Intent;
import android.os.Bundle;
import android.view.View;
import android.widget.AdapterView;
import android.widget.ListView;
import android.widget.TextView;

import java.util.ArrayList;
import java.util.List;

/**
 * Corte de la semana: cuánto se vendió cada día de lunes a domingo y los
 * totales de cada artesano al cierre de la semana.
 */
public class SemanaActivity extends Activity {

    public static final String EXTRA_FECHA = "fecha";
    private static final String ESTADO_LUNES = "lunes";

    /** Lunes de la semana que se está viendo. */
    private String lunes;

    private Db db;
    private DiaAdapter adaptador;

    private TextView txtRango;
    private TextView txtAnio;
    private TextView lblA;
    private TextView lblB;
    private TextView txtA;
    private TextView txtB;
    private TextView txtTotal;
    private TextView txtVacio;
    private ListView lista;

    @Override
    protected void onCreate(Bundle estado) {
        super.onCreate(estado);
        setContentView(R.layout.activity_semana);

        db = Db.obtener(this);
        if (getActionBar() != null) {
            getActionBar().setDisplayHomeAsUpEnabled(true);
        }

        String base = null;
        if (estado != null) {
            base = estado.getString(ESTADO_LUNES);
        }
        if (base == null) {
            base = getIntent().getStringExtra(EXTRA_FECHA);
        }
        if (base == null) {
            base = Fechas.hoy();
        }
        lunes = Fechas.lunesDeLaSemana(base);

        txtRango = (TextView) findViewById(R.id.txt_rango);
        txtAnio = (TextView) findViewById(R.id.txt_anio);
        lblA = (TextView) findViewById(R.id.lbl_sem_a);
        lblB = (TextView) findViewById(R.id.lbl_sem_b);
        txtA = (TextView) findViewById(R.id.txt_sem_a);
        txtB = (TextView) findViewById(R.id.txt_sem_b);
        txtTotal = (TextView) findViewById(R.id.txt_sem_total);
        txtVacio = (TextView) findViewById(R.id.txt_vacio_semana);
        lista = (ListView) findViewById(R.id.lista_dias);

        adaptador = new DiaAdapter(this);
        lista.setAdapter(adaptador);
        // Tocar un día lleva a la cuenta de ese día.
        lista.setOnItemClickListener(new AdapterView.OnItemClickListener() {
            @Override
            public void onItemClick(AdapterView<?> padre, View v, int posicion, long id) {
                Intent i = new Intent(SemanaActivity.this, MainActivity.class);
                i.putExtra(EXTRA_FECHA, adaptador.getItem(posicion).fecha);
                i.addFlags(Intent.FLAG_ACTIVITY_CLEAR_TOP);
                startActivity(i);
            }
        });

        findViewById(R.id.btn_semana_anterior).setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View v) {
                lunes = Fechas.sumarDias(lunes, -7);
                refrescar();
            }
        });
        findViewById(R.id.btn_semana_siguiente).setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View v) {
                lunes = Fechas.sumarDias(lunes, 7);
                refrescar();
            }
        });
        findViewById(R.id.btn_esta_semana).setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View v) {
                lunes = Fechas.lunesDeLaSemana(Fechas.hoy());
                refrescar();
            }
        });
        findViewById(R.id.btn_compartir_semana).setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View v) {
                Compartir.menu(SemanaActivity.this,
                        Reportes.deLaSemana(SemanaActivity.this, lunes));
            }
        });

        refrescar();
    }

    @Override
    protected void onSaveInstanceState(Bundle estado) {
        super.onSaveInstanceState(estado);
        estado.putString(ESTADO_LUNES, lunes);
    }

    @Override
    public boolean onOptionsItemSelected(android.view.MenuItem item) {
        if (item.getItemId() == android.R.id.home) {
            finish();
            return true;
        }
        return super.onOptionsItemSelected(item);
    }

    @Override
    protected void onResume() {
        super.onResume();
        refrescar();
    }

    private void refrescar() {
        txtRango.setText(Fechas.rangoSemana(lunes));
        txtAnio.setText(String.valueOf(Fechas.anio(lunes)));
        findViewById(R.id.btn_esta_semana).setVisibility(
                Fechas.lunesDeLaSemana(Fechas.hoy()).equals(lunes) ? View.GONE : View.VISIBLE);

        String domingo = Fechas.sumarDias(lunes, 6);
        List<Venta> ventas = db.ventasDelRango(lunes, domingo);

        List<DiaAdapter.Dia> dias = new ArrayList<DiaAdapter.Dia>();
        Totales semana = new Totales();
        for (int d = 0; d < 7; d++) {
            String fecha = Fechas.sumarDias(lunes, d);
            Totales t = Reportes.totalesDe(ventas, fecha);
            semana.sumar(t);
            if (t.vacio()) {
                continue;
            }
            DiaAdapter.Dia dia = new DiaAdapter.Dia();
            dia.fecha = fecha;
            dia.totales = t;
            dias.add(dia);
        }
        adaptador.reemplazar(dias);
        txtVacio.setVisibility(dias.isEmpty() ? View.VISIBLE : View.GONE);

        lblA.setText(Prefs.nombreA(this).toUpperCase(Fechas.MX) + " (A)");
        lblB.setText(Prefs.nombreB(this).toUpperCase(Fechas.MX) + " (B)");
        txtA.setText(Dinero.formato(semana.a));
        txtB.setText(Dinero.formato(semana.b));
        txtTotal.setText(Dinero.formato(semana.total()));
    }
}
