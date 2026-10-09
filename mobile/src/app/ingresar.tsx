import { Link } from 'expo-router';
import { useState } from 'react';
import { Text, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { Aviso, Boton, Campo, colores, Pantalla, Suave, Tarjeta } from '../components/ui';
import { useSesion } from '../lib/sesion';
import { mensajeDe } from '../lib/useCarga';

export default function Ingresar() {
  const { ingresar, servidor } = useSesion();
  const [email, setEmail] = useState('');
  const [clave, setClave] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [ocupado, setOcupado] = useState(false);

  const enviar = async () => {
    setError(null);
    if (!email.trim() || !clave) return setError('Escribe tu correo y tu clave.');
    setOcupado(true);
    try {
      await ingresar(email, clave);
    } catch (e) {
      setError(mensajeDe(e));
    } finally {
      setOcupado(false);
    }
  };

  return (
    <SafeAreaView style={{ flex: 1, backgroundColor: colores.fondo }}>
      <Pantalla>
        <View style={{ alignItems: 'center', marginVertical: 24, gap: 4 }}>
          <Text style={{ fontSize: 28, fontWeight: '700', color: colores.texto }}>Horas Extras</Text>
          <Suave>Registro y liquidación quincenal</Suave>
        </View>
        <Tarjeta titulo="Ingresar">
          <Campo
            etiqueta="Correo"
            value={email}
            onChangeText={setEmail}
            autoCapitalize="none"
            autoComplete="email"
            keyboardType="email-address"
            textContentType="username"
          />
          <Campo
            etiqueta="Clave"
            value={clave}
            onChangeText={setClave}
            secureTextEntry
            autoComplete="password"
            textContentType="password"
            onSubmitEditing={enviar}
          />
          {error ? <Aviso tipo="error">{error}</Aviso> : null}
          <Boton titulo="Ingresar" onPress={enviar} ocupado={ocupado} />
          <Link href="/registro" style={{ color: colores.primario, textAlign: 'center', padding: 8 }}>
            ¿No tienes cuenta? Crear una
          </Link>
        </Tarjeta>
        <Link href="/servidor" style={{ textAlign: 'center', padding: 8 }}>
          <Suave>Servidor: {servidor} · Cambiar</Suave>
        </Link>
      </Pantalla>
    </SafeAreaView>
  );
}
