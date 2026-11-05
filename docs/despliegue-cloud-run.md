# Despliegue en Google Cloud Run (pruebas)

Cloud Run es la segunda plataforma de pruebas, junto a [Render](despliegue-render.md). Las dos usan el
mismo `Dockerfile` y la misma base de datos de **Supabase**, así que pueden estar encendidas a la vez y
probarse lado a lado: cada push a `main` que pase el CI se despliega en ambas.

| | Render (plan Free) | Cloud Run (capa gratuita) |
|---|---|---|
| Tarjeta | No | Sí (cuenta de facturación) |
| Al despertar | ~1 minuto con página de espera | Unos segundos |
| Memoria | 512 MB, 0,1 CPU | 512 MB, 1 CPU (configurado así) |
| Límite gratis | 750 h/mes | 180.000 vCPU-s, 360.000 GiB-s y 2 millones de solicitudes al mes |
| Despliegue | `render.yaml` (Render construye) | `.github/workflows/desplegar-cloud-run.yml` (GitHub construye) |

## Archivos

- `scripts/desplegar-cloud-run.sh`: prepara el proyecto de Google Cloud una sola vez (`preparar`) y permite
  desplegar a mano (`desplegar`).
- `.github/workflows/desplegar-cloud-run.yml`: despliega solo después de que el CI pasa en `main`. Si la
  variable `GCP_PROJECT_ID` no existe en GitHub, se salta sin fallar.
- El `Dockerfile` no cambia: escucha en el puerto de `PORT` (Cloud Run define 8080) y lee la cadena de
  `ConnectionStrings__Default`, que aquí viene de **Secret Manager** en vez de una variable a la vista.

## Pasos (una sola vez)

1. **Ten a mano la cadena de Supabase** en formato Npgsql (la misma de Render):
   `Host=aws-0-<region>.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.<ref>;Password=<clave>;SSL Mode=Require;Maximum Pool Size=10`
   Con Render y Cloud Run encendidos a la vez, cada uno abre hasta 10 conexiones; el pooler de Supabase
   gratuito aguanta bien ese total.
2. **Crea el proyecto** en [console.cloud.google.com](https://console.cloud.google.com): *Seleccionar
   proyecto → Proyecto nuevo* (por ejemplo `horas-extras`). Anota el **ID del proyecto**.
3. **Vincula la facturación** (*Facturación → Vincular una cuenta*). Google pide tarjeta, pero con lo que
   configura el script no se sale de la capa gratuita. De todas formas, crea una **alerta de presupuesto**
   (*Facturación → Presupuestos y alertas*, por ejemplo de 1 USD) para enterarte si algo cambia.
4. **Abre Cloud Shell** (el ícono `>_` arriba a la derecha de la consola) y corre:
   ```bash
   git clone https://github.com/yrais100/horas-extras.git && cd horas-extras
   gcloud config set project <id-del-proyecto>
   ./scripts/desplegar-cloud-run.sh preparar
   ```
   El script activa las APIs, crea el repositorio de imágenes `horasextras` en `us-east4` (Virginia, el más
   cercano a Supabase `us-east-1`), guarda la cadena de Supabase en el secreto `horasextras-db` (te la pide
   sin mostrarla) y crea la cuenta de servicio `github-despliegue`. Si tu Supabase está en otra región,
   antepón `GCP_REGION=<región>` al comando y usa la misma región en el paso 5.
5. **Conecta GitHub.** En Cloud Shell crea la llave que imprime el script, ábrela con `cat llave-github.json`
   y cópiala completa. En GitHub, *Settings → Secrets and variables → Actions*:
   - secreto `GCP_CREDENTIALS`: el contenido de la llave;
   - variable `GCP_PROJECT_ID`: el ID del proyecto;
   - variable `GCP_REGION`: solo si no usas `us-east4`.

   Luego borra la llave de Cloud Shell con `rm llave-github.json`.
6. **Primer despliegue:** en GitHub, *Actions → Desplegar en Cloud Run → Run workflow* (o en Cloud Shell,
   `./scripts/desplegar-cloud-run.sh desplegar`). Tarda unos 5 minutos. El resumen del workflow muestra la
   URL, de la forma `https://horasextras-<algo>.us-east4.run.app`.
7. **Comprueba** `https://<tu-url>/salud`: debe responder `{"estado":"ok"}`. Si no, el mensaje dice qué
   falla de la base de datos. Las migraciones se aplican solas al arrancar.

Desde ahí, cada push a `main` se despliega solo en Cloud Run **y** en Render cuando el CI pasa.

## Cómo queda configurado el servicio

- **Mínimo 0 y máximo 1 instancia:** sin visitas no consume nada, y nunca hay más de una copia, así que
  el gasto queda acotado.
- **512 MiB y 1 CPU**, con *startup CPU boost* para que despierte más rápido.
- **Tiempo máximo por solicitud de 60 minutos y afinidad de sesión:** la web Blazor mantiene una conexión
  abierta (WebSocket) mientras está en pantalla; con el valor por defecto (5 minutos) Cloud Run la cortaría
  y la página se reconectaría a cada rato.
- **Acceso público** (`--allow-unauthenticated`): la app maneja su propio ingreso con usuario y clave.
- Las llaves de sesión se guardan en la base de datos (tabla `horasextras.DataProtectionKeys`), igual que
  en Render, así que las sesiones sirven en las dos plataformas y no se pierden al reiniciar.
- Artifact Registry conserva solo las 3 imágenes más recientes, para no pasar de los 0,5 GB gratis.

Ojo con el costo: mientras alguien tenga la web abierta, la conexión mantiene la instancia activa y eso
cuenta como uso. Para pruebas sobra la capa gratuita (equivale a unas 50 horas de CPU al mes), pero no
dejes pestañas abiertas días enteros.

## Tareas comunes

- **Cambiar la cadena de Supabase:** `./scripts/desplegar-cloud-run.sh cadena`. Pide la cadena sin
  mostrarla, revisa que no tenga partes del ejemplo (`<region>`, `<clave>`) ni esté pegada dos veces, la
  guarda como versión nueva del secreto y reinicia el servicio para que la lea.
- **Ver los registros:** *Cloud Run → horasextras → Registros* en la consola, o
  `gcloud run services logs read horasextras --region us-east4`.
- **Apagar Cloud Run y quedarse solo con Render:** borra la variable `GCP_PROJECT_ID` en GitHub (el
  workflow se salta) y, si quieres, el servicio con `gcloud run services delete horasextras --region us-east4`.
- **App móvil:** en la pantalla de ingreso puedes cambiar el servidor a la URL de Cloud Run o a la de Render
  para comparar. La que quede por defecto se define en `extra.apiUrl` de `mobile/app.json` y
  `SERVIDOR_PRODUCCION` de `mobile/src/lib/config.ts`.
- **Supabase gratuito pausa el proyecto tras 7 días sin actividad.** Si `/salud` dice que la base no
  responde, entra a Supabase y dale *Restore project*.
