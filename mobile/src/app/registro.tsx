import { Link } from 'expo-router';
import { useState } from 'react';
import { Aviso, Boton, Campo, colores, Pantalla, Suave, Tarjeta } from '../components/ui';
import { useSesion } from '../lib/sesion';
import { mensajeDe } from '../lib/useCarga';

export default function Registro() {
  const { registrar } = useSesion();
  const [email, setEmail] = useState('');
  const [clave, setClave] = useState('');
  const [confirmacion, setConfirmacion] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [ocupado, setOcupado] = useState(false);
  const [pendiente, setPendiente] = useState(false);

  const enviar = async () => {
    setError(null);
    if (!email.trim() || !clave) return setError('Escribe tu correo y una clave.');
    if (clave !== confirmacion) return setError('Las claves no coinciden.');
    setOcupado(true);
    try {
      if ((await registrar(email, clave)) === 'pendiente') {
        setPendiente(true);
        setOcupado(false);
      }
    } catch (e) {
      setError(mensajeDe(e));
      setOcupado(false);
    }
  };

  if (pendiente)
    return (
      <Pantalla>
        <Tarjeta titulo="Cuenta creada">
          <Aviso tipo="exito">
            Tu cuenta quedó pendiente de aprobación. Cuando un administrador la apruebe, ingresa con tu correo y tu
            clave.
          </Aviso>
          <Link href="/ingresar" style={{ color: colores.primario, textAlign: 'center', padding: 8 }}>
            Volver a ingresar
          </Link>
        </Tarjeta>
      </Pantalla>
    );

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
