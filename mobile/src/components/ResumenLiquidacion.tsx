import { StyleSheet, Text, View } from 'react-native';
import { horas, nombreCategoria, pesos } from '../lib/formato';
import type { Liquidacion } from '../lib/tipos';
import { Aviso, colores } from './ui';

function Kpi({ etiqueta, valor, detalle, estilo }: { etiqueta: string; valor: string; detalle?: string; estilo?: object }) {
  return (
    <View style={[e.kpi, estilo]}>
      <Text style={e.etiqueta}>{etiqueta}</Text>
      <Text style={e.valor}>{valor}</Text>
      {detalle ? <Text style={e.detalle}>{detalle}</Text> : null}
    </View>
  );
}

export function ResumenLiquidacion({ resultado }: { resultado: Liquidacion }) {
  return (
    <View style={{ gap: 8 }}>
      <View style={e.rejilla}>
        {resultado.totales.map((t) => (
          <Kpi
            key={t.categoria}
            etiqueta={nombreCategoria(t.categoria)}
            valor={horas(t.horas)}
            detalle={pesos(t.valor)}
            estilo={t.categoria.endsWith('Nocturna') ? e.nocturno : undefined}
          />
        ))}
        <Kpi etiqueta="Días trabajados" valor={String(resultado.diasTrabajados)} />
        <Kpi etiqueta="Total horas" valor={horas(resultado.horasTotales)} />
      </View>
      <Kpi etiqueta="Valor liquidado hasta ahora" valor={pesos(resultado.valorTotal)} estilo={e.total} />
      {resultado.avisos.map((a) => (
        <Aviso key={a} tipo="advertencia">
          {a}
        </Aviso>
      ))}
    </View>
  );
}

const e = StyleSheet.create({
  rejilla: { flexDirection: 'row', flexWrap: 'wrap', gap: 8 },
  kpi: { flexGrow: 1, flexBasis: '45%', backgroundColor: colores.fondo, borderRadius: 10, padding: 10, gap: 2 },
  nocturno: { backgroundColor: '#e9e8f7' },
  total: { backgroundColor: '#e3f1e9', flexBasis: 'auto' },
  etiqueta: { fontSize: 12, color: colores.suave },
  valor: { fontSize: 18, fontWeight: '700', color: colores.texto },
  detalle: { fontSize: 13, color: colores.texto },
});
