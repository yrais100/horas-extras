# Horas Extras · app móvil

App para iOS y Android hecha con **React Native, Expo (SDK 57) y TypeScript**. Usa la API REST de la
solución .NET de este repositorio, así que ve los mismos datos que la web.

| Pantalla | Qué hace |
|---|---|
| Ingresar / Crear cuenta | Correo y clave. Los tokens se guardan cifrados en el teléfono (Keychain en iOS, Keystore en Android) con `expo-secure-store` y se renuevan solos. |
| Hoy | Cronómetro con **Iniciar, Pausar, Continuar y Finalizar**, y el resumen de la quincena actual. |
| Liquidar | Liquidación de cualquier periodo (por defecto la quincena actual, con botón a la anterior), detalle por día y **Guardar** como histórico. |
| Historial | **Registro manual** (para olvidos), sesiones de la quincena con opción de eliminar y liquidaciones guardadas. |
| Salario | Salario con fecha de vigencia, y recargos según la ley o manuales. También cierra la sesión. |

Todas las fechas y horas se muestran en hora de Colombia, aunque el teléfono esté en otra zona horaria.

## Verla en tu celular con Expo Go (sin cuentas de pago)

1. Instala **Expo Go** en el celular (App Store o Google Play).
2. En tu computador, con [Node.js 20 o superior](https://nodejs.org):

   ```bash
   cd mobile
   npm install
   npx expo start
   ```

3. Escanea el código QR que aparece: en Android, desde la app Expo Go; en iPhone, con la cámara.
   El computador y el celular deben estar en la misma red Wi-Fi. Si no lo están (o la red lo bloquea),
   usa `npx expo start --tunnel`.

La app se conecta por defecto a producción, `https://horasextras.azurewebsites.net`. Puedes usar la misma
cuenta que en la web.

> El plan gratuito de Azure apaga el sitio tras unos minutos sin uso: el primer ingreso del día puede
> tardar algunos segundos.

### Contra la API en tu computador

```bash
# en la raíz del repositorio: escucha en toda la red local, no solo en localhost
dotnet run --project src/HorasExtras.Web --urls http://0.0.0.0:5228
```

En la pantalla de ingreso toca **Servidor · Cambiar**, escribe `http://<IP de tu computador>:5228` (por
ejemplo `http://192.168.1.20:5228`), pulsa **Probar conexión** y **Guardar**. Si no conecta, revisa que el
firewall del computador permita el puerto 5228. También puedes fijar la dirección al arrancar:
`EXPO_PUBLIC_API_URL=http://192.168.1.20:5228 npx expo start`.

El servidor por defecto está en `app.json` → `expo.extra.apiUrl`.

## Pruebas

```bash
npm run typecheck   # TypeScript
npm run lint        # ESLint (configuración de Expo)
npm test            # Lógica: fechas en hora de Colombia, quincenas, formatos, cliente de la API y tokens
```

Prueba de punta a punta contra una API real (crea un usuario de prueba, úsala solo contra tu entorno local):

```bash
HE_API_URL=http://localhost:5228 npm test -- api.integracion
```

`npx expo start --web` abre la app en el navegador para revisar pantallas durante el desarrollo, pero no es
un destino soportado: ahí no funcionan los selectores de fecha ni los diálogos de confirmación.

## Generar los instaladores con EAS Build

EAS compila la app en la nube de Expo, sin necesidad de Xcode ni Android Studio.

**Cuentas necesarias**

| Para… | Cuenta | Costo |
|---|---|---|
| Compilar con EAS | [Expo](https://expo.dev/signup) | Gratis (con cupo mensual de compilaciones) |
| Instalar en Android sin tienda (APK) | Ninguna más | Gratis |
| Publicar en Google Play | [Google Play Console](https://play.google.com/console) | Pago único de US$ 25 |
| Instalar en un iPhone fuera de Expo Go (TestFlight, App Store o instalación directa) | [Apple Developer Program](https://developer.apple.com/programs/) | US$ 99 al año |

Sin la cuenta de Apple de pago, en iPhone solo se puede usar con Expo Go.

**Pasos**

```bash
cd mobile
npx eas-cli@latest login
npx eas-cli@latest build:configure     # crea el proyecto en Expo y agrega su projectId a app.json
```

Antes de la primera compilación, revisa en `app.json` el identificador de la app
(`ios.bundleIdentifier` y `android.package`, hoy `com.yrais100.horasextras`): una vez publicado en una
tienda no se puede cambiar.

- **Android, APK para instalar directo:** `npx eas-cli@latest build -p android --profile preview`.
  Al terminar, EAS da un enlace y un QR para descargar el `.apk` en el teléfono (Android pedirá permitir
  instalar apps de origen desconocido).
- **Android, Google Play:** `npx eas-cli@latest build -p android --profile production` genera un `.aab`;
  súbelo con `npx eas-cli@latest submit -p android` (la primera vez Google pide subirlo a mano en Play Console).
- **iOS:** `npx eas-cli@latest build -p ios --profile production` (EAS pide entrar con la cuenta de Apple
  Developer y crea los certificados) y luego `npx eas-cli@latest submit -p ios` para enviarla a TestFlight.
  Para instalar directo en iPhones concretos sin TestFlight: registra cada teléfono con
  `npx eas-cli@latest device:create` y compila con `--profile preview`.

Los perfiles están en `eas.json`.

## Estructura

```
mobile/
├─ app.json, eas.json         Configuración de Expo y de EAS Build
└─ src/
   ├─ app/                    Pantallas (Expo Router): ingresar, registro, servidor y las pestañas en (tabs)/
   ├─ components/             Botones, campos, selectores de fecha y hora, resumen de liquidación
   └─ lib/
      ├─ api.ts               Cliente de la API: tokens, renovación automática y mensajes de error en español
      ├─ tiempo.ts            Hora de Colombia (UTC-5) y quincenas, igual que el servidor
      ├─ formato.ts           Pesos, horas, fechas
      ├─ sesion.tsx           Estado de la sesión y servidor elegido
      └─ __tests__/           Pruebas con Jest
```
