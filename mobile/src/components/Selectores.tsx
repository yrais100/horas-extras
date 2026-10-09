import DateTimePicker, { DateTimePickerAndroid } from '@react-native-community/datetimepicker';
import { Platform, Pressable, Text, View } from 'react-native';
import { fechaHoraLocal, fechaLarga, horaDelDia } from '../lib/formato';
import { aColombia, desdeColombia, fechaDe, horaDeMinutos, instanteDeFecha, ZONA } from '../lib/tiempo';
import type { Fecha } from '../lib/tipos';
import { estilos } from './ui';

// Los selectores muestran siempre la hora de Colombia, aunque el teléfono esté en otra zona horaria,
// igual que la web. Por eso se pasa timeZoneName en vez de usar la zona del dispositivo.

type Modo = 'date' | 'time';

function Envoltura({ etiqueta, children }: { etiqueta: string; children: React.ReactNode }) {
  return (
    <View style={estilos.campo}>
      <Text style={estilos.etiqueta}>{etiqueta}</Text>
      {children}
    </View>
  );
}

function BotonAndroid({ texto, onPress }: { texto: string; onPress: () => void }) {
  return (
    <Pressable accessibilityRole="button" onPress={onPress} style={estilos.entrada}>
      <Text style={{ fontSize: 16 }}>{texto}</Text>
    </Pressable>
  );
}

function abrirAndroid(valor: Date, modo: Modo, alElegir: (d: Date) => void, maximo?: Date) {
  DateTimePickerAndroid.open({
    value: valor,
    mode: modo,
    timeZoneName: ZONA,
    is24Hour: false,
    maximumDate: modo === 'date' ? maximo : undefined,
    onValueChange: (_e, d) => alElegir(d),
  });
}

function SelectorIos({ valor, modo, alCambiar, maximo }: { valor: Date; modo: Modo | 'datetime'; alCambiar: (d: Date) => void; maximo?: Date }) {
  return (
    <View style={{ alignItems: 'flex-start' }}>
      <DateTimePicker
        value={valor}
        mode={modo}
        display="compact"
        locale="es-CO"
        timeZoneName={ZONA}
        maximumDate={maximo}
        onValueChange={(_e, d) => alCambiar(d)}
      />
    </View>
  );
}

/** Fecha sin hora ("2026-10-08"). */
export function SelectorFecha({ etiqueta, valor, alCambiar }: { etiqueta: string; valor: Fecha; alCambiar: (f: Fecha) => void }) {
  const instante = instanteDeFecha(valor);
  return (
    <Envoltura etiqueta={etiqueta}>
      {Platform.OS === 'ios' ? (
        <SelectorIos valor={instante} modo="date" alCambiar={(d) => alCambiar(fechaDe(d))} />
      ) : (
        <BotonAndroid texto={fechaLarga(valor)} onPress={() => abrirAndroid(instante, 'date', (d) => alCambiar(fechaDe(d)))} />
      )}
    </Envoltura>
  );
}

/** Fecha y hora (instante). En Android se elige primero el día y luego la hora. */
export function SelectorFechaHora({
  etiqueta,
  valor,
  alCambiar,
}: {
  etiqueta: string;
  valor: Date;
  alCambiar: (d: Date) => void;
}) {
  const ahora = new Date();
  return (
    <Envoltura etiqueta={etiqueta}>
      {Platform.OS === 'ios' ? (
        <SelectorIos valor={valor} modo="datetime" alCambiar={alCambiar} maximo={ahora} />
      ) : (
        <BotonAndroid
          texto={fechaHoraLocal(valor.toISOString())}
          onPress={() => abrirAndroid(valor, 'date', (dia) => abrirAndroid(dia, 'time', alCambiar), ahora)}
        />
      )}
    </Envoltura>
  );
}

/** Hora del día en minutos desde la medianoche (para la jornada nocturna). */
export function SelectorHora({ etiqueta, minutos, alCambiar }: { etiqueta: string; minutos: number; alCambiar: (m: number) => void }) {
  const instante = desdeColombia(2026, 1, 1, Math.floor(minutos / 60), minutos % 60);
  const leer = (d: Date) => {
    const p = aColombia(d);
    alCambiar(p.hora * 60 + p.minuto);
  };
  return (
    <Envoltura etiqueta={etiqueta}>
      {Platform.OS === 'ios' ? (
        <SelectorIos valor={instante} modo="time" alCambiar={leer} />
      ) : (
        <BotonAndroid texto={horaDelDia(horaDeMinutos(minutos))} onPress={() => abrirAndroid(instante, 'time', leer)} />
      )}
    </Envoltura>
  );
}
