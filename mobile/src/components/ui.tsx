import { Children, type ReactNode } from 'react';
import {
  ActivityIndicator,
  KeyboardAvoidingView,
  Platform,
  Pressable,
  RefreshControl,
  ScrollView,
  StyleSheet,
  Text,
  TextInput,
  View,
  type TextInputProps,
} from 'react-native';

export const colores = {
  fondo: '#f3f5f9',
  tarjeta: '#ffffff',
  texto: '#1b1f24',
  suave: '#5f6b7a',
  borde: '#d8dee6',
  primario: '#0d6efd',
  exito: '#198754',
  advertencia: '#f0ad00',
  peligro: '#dc3545',
  nocturno: '#3d3b8e',
};

export function Pantalla({
  children,
  cargando = false,
  alRecargar,
}: {
  children: ReactNode;
  cargando?: boolean;
  alRecargar?: () => void;
}) {
  return (
    <KeyboardAvoidingView style={{ flex: 1 }} behavior={Platform.OS === 'ios' ? 'padding' : undefined}>
      <ScrollView
        style={{ backgroundColor: colores.fondo }}
        contentContainerStyle={estilos.pantalla}
        keyboardShouldPersistTaps="handled"
        refreshControl={alRecargar ? <RefreshControl refreshing={cargando} onRefresh={alRecargar} /> : undefined}
      >
        {children}
      </ScrollView>
    </KeyboardAvoidingView>
  );
}

export function Tarjeta({ titulo, children }: { titulo?: string; children: ReactNode }) {
  return (
    <View style={estilos.tarjeta}>
      {titulo ? <Text style={estilos.tituloTarjeta}>{titulo}</Text> : null}
      {children}
    </View>
  );
}

export function Suave({ children, style }: { children: ReactNode; style?: object }) {
  return <Text style={[estilos.suave, style]}>{children}</Text>;
}

type Variante = 'primario' | 'secundario' | 'exito' | 'advertencia' | 'peligro' | 'peligroBorde';

export function Boton({
  titulo,
  onPress,
  variante = 'primario',
  deshabilitado = false,
  ocupado = false,
  grande = false,
}: {
  titulo: string;
  onPress: () => void;
  variante?: Variante;
  deshabilitado?: boolean;
  ocupado?: boolean;
  grande?: boolean;
}) {
  const borde = variante === 'secundario' || variante === 'peligroBorde';
  const color =
    variante === 'secundario'
      ? colores.suave
      : variante === 'peligroBorde'
        ? colores.peligro
        : colores[variante];
  const inactivo = deshabilitado || ocupado;
  return (
    <Pressable
      accessibilityRole="button"
      onPress={onPress}
      disabled={inactivo}
      style={({ pressed }) => [
        estilos.boton,
        grande && estilos.botonGrande,
        borde ? { borderColor: color, borderWidth: 1, backgroundColor: 'transparent' } : { backgroundColor: color },
        (pressed || inactivo) && { opacity: 0.6 },
      ]}
    >
      {ocupado ? (
        <ActivityIndicator color={borde ? color : '#fff'} />
      ) : (
        <Text
          style={[
            estilos.textoBoton,
            grande && { fontSize: 18 },
            { color: borde ? color : variante === 'advertencia' ? colores.texto : '#fff' },
          ]}
        >
          {titulo}
        </Text>
      )}
    </Pressable>
  );
}

export function Campo({ etiqueta, ...props }: { etiqueta: string } & TextInputProps) {
  return (
    <View style={estilos.campo}>
      <Text style={estilos.etiqueta}>{etiqueta}</Text>
      <TextInput placeholderTextColor="#98a2b3" style={estilos.entrada} {...props} />
    </View>
  );
}

export function Aviso({ tipo, children }: { tipo: 'error' | 'exito' | 'advertencia'; children: ReactNode }) {
  const color = tipo === 'error' ? colores.peligro : tipo === 'exito' ? colores.exito : colores.advertencia;
  return (
    <View style={[estilos.aviso, { borderLeftColor: color, backgroundColor: `${color}1a` }]}>
      <Text style={{ color: colores.texto }}>{children}</Text>
    </View>
  );
}

/** Elementos en fila que pasan a la siguiente línea si no caben. Con `campos`, cada hijo ocupa el mismo ancho. */
export function Fila({ children, style, campos = false }: { children: ReactNode; style?: object; campos?: boolean }) {
  return (
    <View style={[estilos.fila, style]}>
      {campos
        ? Children.map(children, (c) => (c ? <View style={{ flexGrow: 1, flexBasis: 140 }}>{c}</View> : null))
        : children}
    </View>
  );
}

export const estilos = StyleSheet.create({
  pantalla: { padding: 16, gap: 12, paddingBottom: 32 },
  tarjeta: {
    backgroundColor: colores.tarjeta,
    borderRadius: 12,
    padding: 16,
    gap: 10,
    borderWidth: StyleSheet.hairlineWidth,
    borderColor: colores.borde,
  },
  tituloTarjeta: { fontSize: 16, fontWeight: '600', color: colores.texto },
  suave: { color: colores.suave, fontSize: 13 },
  boton: { borderRadius: 8, paddingVertical: 10, paddingHorizontal: 14, alignItems: 'center', justifyContent: 'center', minHeight: 44 },
  botonGrande: { paddingVertical: 14, paddingHorizontal: 22 },
  textoBoton: { fontSize: 15, fontWeight: '600' },
  campo: { gap: 4 },
  etiqueta: { fontSize: 13, color: colores.suave },
  entrada: {
    borderWidth: 1,
    borderColor: colores.borde,
    borderRadius: 8,
    paddingHorizontal: 12,
    paddingVertical: 10,
    fontSize: 16,
    color: colores.texto,
    backgroundColor: '#fff',
    minHeight: 44,
  },
  aviso: { borderLeftWidth: 4, borderRadius: 6, padding: 10 },
  fila: { flexDirection: 'row', flexWrap: 'wrap', gap: 8, alignItems: 'flex-end' },
});
