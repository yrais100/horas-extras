# Despliegue gratuito en Render (pruebas)

La aplicación (web Blazor + API) corre en un **servicio web gratuito de Render** construido con el
`Dockerfile` del repositorio. La base de datos sigue en **Supabase**, sin cambios. Render no pide tarjeta
para el plan gratuito.

## Lo que hay que saber del plan gratuito

- **Se duerme tras 15 minutos sin visitas.** La siguiente visita lo despierta en alrededor de un minuto
  (Render muestra una página de espera). Mientras alguien tenga la web abierta no se duerme.
- **512 MB de RAM y 0,1 CPU.** Alcanza para pruebas; la primera generación de un PDF puede tardar un poco.
- **750 horas al mes por cuenta.** Un solo servicio encendido todo el mes usa 744, así que alcanza.
- **El disco se borra en cada reinicio.** Por eso las llaves que firman la sesión, los tokens de la app y
  los enlaces de los PDF se guardan en la base de datos (tabla `horasextras.DataProtectionKeys`); así nadie
  pierde la sesión cuando el servicio se duerme o se vuelve a desplegar.
- Render puede reiniciar el servicio cuando quiera y no lo recomienda para producción.

## Pasos (una sola vez)

1. **Ten a mano la cadena de Supabase** en formato Npgsql (la misma que tenías en Azure, en *Cadenas de
   conexión → Default*). Si no la tienes: en Supabase, *Connect → Session pooler*, y pásala a este formato:
   `Host=aws-0-<region>.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.<ref>;Password=<clave>;SSL Mode=Require;Maximum Pool Size=10`
2. **Crea la cuenta** en [render.com](https://render.com) con *Sign in with GitHub* y autoriza el
   repositorio `yrais100/horas-extras`.
3. En el panel de Render: **New → Blueprint**, elige el repositorio y la rama `main`. Render lee
   `render.yaml` y propone el servicio `horasextras` (Docker, plan Free, región Virginia).
4. Cuando pida **`ConnectionStrings__Default`**, pega la cadena del paso 1 y confirma con *Apply*.
5. Espera el primer build (unos 5 a 8 minutos). Al terminar, el servicio queda en
   `https://horasextras.onrender.com` (o con un sufijo si ese nombre ya existe; la URL exacta sale arriba en
   la página del servicio).
6. **Comprueba** `https://<tu-url>.onrender.com/salud`: debe responder `{"estado":"ok"}`. Si responde un
   error, el mensaje dice qué falla de la base de datos (clave, usuario del pooler, proyecto pausado).
   Las migraciones se aplican solas al arrancar.

Desde ahí, cada push a `main` se despliega solo **cuando el CI de GitHub pasa**.

## Después del cambio

- **App móvil:** en la pantalla de ingreso cambia el servidor a la nueva URL. Para que quede por defecto,
  actualiza `extra.apiUrl` en `mobile/app.json` y `SERVIDOR_PRODUCCION` en `mobile/src/lib/config.ts`.
- **Azure:** si ya no lo vas a usar, borra la variable `AZURE_WEBAPP_NAME` en GitHub (*Settings → Secrets
  and variables → Actions → Variables*). Así el workflow *Desplegar en Azure* se salta y no falla en cada
  push.
- **Supabase gratuito pausa el proyecto tras 7 días sin actividad.** Si `/salud` dice que la base no
  responde, entra a Supabase y dale *Restore project*.
- Opcional, para evitar la espera al despertar: un monitor gratuito (por ejemplo UptimeRobot) que visite
  `/salud` cada 10 minutos lo mantiene despierto y, de paso, evita que Supabase se pause. Con un solo
  servicio las 750 horas alcanzan.

## Google Cloud Run (segunda plataforma)

El mismo `Dockerfile` también se despliega en Google Cloud Run, que despierta en segundos pero pide
tarjeta. Las dos plataformas pueden estar encendidas a la vez con la misma base de datos. Pasos en
[despliegue-cloud-run.md](despliegue-cloud-run.md).
