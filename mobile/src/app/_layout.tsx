import { Stack } from 'expo-router';
import { StatusBar } from 'expo-status-bar';
import { ActivityIndicator, View } from 'react-native';
import { SafeAreaProvider } from 'react-native-safe-area-context';
import { colores } from '../components/ui';
import { ProveedorSesion, useSesion } from '../lib/sesion';

function Navegacion() {
  const { estado } = useSesion();
  if (estado === 'cargando') {
    return (
      <View style={{ flex: 1, alignItems: 'center', justifyContent: 'center', backgroundColor: colores.fondo }}>
        <ActivityIndicator size="large" />
      </View>
    );
  }
  const dentro = estado === 'dentro';
  return (
    <Stack screenOptions={{ headerBackTitle: 'Atrás' }}>
      <Stack.Protected guard={dentro}>
        <Stack.Screen name="(tabs)" options={{ headerShown: false }} />
      </Stack.Protected>
      <Stack.Protected guard={!dentro}>
        <Stack.Screen name="ingresar" options={{ title: 'Ingresar', headerShown: false }} />
        <Stack.Screen name="registro" options={{ title: 'Crear cuenta' }} />
        <Stack.Screen name="servidor" options={{ title: 'Servidor' }} />
      </Stack.Protected>
    </Stack>
  );
}

export default function Raiz() {
  return (
    <SafeAreaProvider>
      <ProveedorSesion>
        <StatusBar style="dark" />
        <Navegacion />
      </ProveedorSesion>
    </SafeAreaProvider>
  );
}
