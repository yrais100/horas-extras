import { useState } from 'react';
import { Aviso, Boton, Campo, Pantalla, Suave, Tarjeta } from '../components/ui';
import { useSesion } from '../lib/sesion';
import { mensajeDe } from '../lib/useCarga';

export default function Registro() {
  const { registrar } = useSesion();
  const [email, setEmail] = useState('');
  const [clave, setClave] = useState('');
  const [confirmacion, setConfirmacion] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [ocupado, setOcupado] = useState(false);

  const enviar = async () => {
    setError(null);
    if (!email.trim() || !clave) return setError('Escribe tu correo y una clave.');
    if (clave !== confirmacion) return setError('Las claves no coinciden.');
    setOcupado(true);
    try {
      await registrar(email, clave);
    } catch (e) {
      setError(mensajeDe(e));
      setOcupado(false);
    }
  };

  return (
    <Pantalla>
      <Tarjeta titulo="Crear cuenta">
        <Campo
          etiqueta="Correo"
          value={email}
          onChangeText={setEmail}
          autoCapitalize="none"
          autoComplete="email"
          keyboardType="email-address"
          textContentType="username"
        />
        <Campo etiqueta="Clave" value={clave} onChangeText={setClave} secureTextEntry textContentType="newPassword" />
        <Campo
          etiqueta="Confirmar clave"
          value={confirmacion}
          onChangeText={setConfirmacion}
          secureTextEntry
          textContentType="newPassword"
          onSubmitEditing={enviar}
        />
        <Suave>Mínimo 6 caracteres, con una mayúscula, un número y un símbolo.</Suave>
        {error ? <Aviso tipo="error">{error}</Aviso> : null}
        <Boton titulo="Crear cuenta" onPress={enviar} ocupado={ocupado} />
      </Tarjeta>
    </Pantalla>
  );
}
