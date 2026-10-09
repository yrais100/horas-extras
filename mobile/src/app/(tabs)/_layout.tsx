import Ionicons from '@expo/vector-icons/Ionicons';
import { Tabs } from 'expo-router';
import type { ColorValue } from 'react-native';
import { colores } from '../../components/ui';

type Icono = keyof typeof Ionicons.glyphMap;

const icono = (nombre: Icono) =>
  function IconoPestana({ color, size }: { color: ColorValue; size: number }) {
    return <Ionicons name={nombre} color={color} size={size} />;
  };

export default function Pestanas() {
  return (
    <Tabs screenOptions={{ tabBarActiveTintColor: colores.primario, headerTitleAlign: 'center' }}>
      <Tabs.Screen name="index" options={{ title: 'Hoy', tabBarIcon: icono('time-outline') }} />
      <Tabs.Screen name="liquidar" options={{ title: 'Liquidar', tabBarIcon: icono('calculator-outline') }} />
      <Tabs.Screen name="historial" options={{ title: 'Historial', tabBarIcon: icono('list-outline') }} />
      <Tabs.Screen name="perfil" options={{ title: 'Salario y recargos', tabBarLabel: 'Salario', tabBarIcon: icono('wallet-outline') }} />
    </Tabs>
  );
}
