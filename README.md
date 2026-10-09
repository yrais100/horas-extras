# Horas Extras

Registro y liquidación quincenal de horas extras para Colombia. Cada usuario inicia, pausa, continúa y
finaliza sus horas; la aplicación las clasifica en diurnas, nocturnas, dominicales y festivas según la
ley vigente en cada fecha, y las liquida con **el salario de cada usuario**.

- **Web:** Blazor (render interactivo en el servidor), con inicio de sesión y multiusuario.
- **API REST:** en la misma aplicación, para la futura app en React Native.
- **Base de datos:** PostgreSQL en producción; SQLite en desarrollo y pruebas, sin instalar nada.

## Estructura

```
HorasExtras.slnx
├─ src/
│  ├─ HorasExtras.Domain          Reglas puras: franjas, festivos, valores de ley, liquidación
│  ├─ HorasExtras.Application     Casos de uso: registro de horas, liquidación, perfil (salario y recargos)
│  ├─ HorasExtras.Infrastructure  EF Core (PostgreSQL / SQLite), Identity, migraciones
│  └─ HorasExtras.Web             Blazor + API REST (/api) + inicio de sesión
└─ tests/
   ├─ HorasExtras.Domain.Tests    Cálculo: cruces de medianoche, festivos, cambios de ley y de salario
   └─ HorasExtras.Web.Tests       API de punta a punta con dos usuarios
```

## Correrlo en tu equipo

Requiere el [SDK de .NET 10](https://dotnet.microsoft.com/download).

```bash
dotnet run --project src/HorasExtras.Web     # usa SQLite (horasextras.dev.db), sin instalar nada más
dotnet test                                  # 16 pruebas
```

Para probar con PostgreSQL, como en producción: `docker compose up --build` y abre http://localhost:8080.

**El primer usuario que se registra queda como administrador** y ve el resumen de todos en *Usuarios*.

## Cómo se calcula

1. Cada sesión guarda sus tramos (inicio y fin en UTC). Pausar cierra el tramo y continuar abre otro,
   así las pausas no se cuentan y la hora de inicio real queda guardada.
2. Cada tramo se parte por día y por franja en hora de Colombia (UTC-5). Un turno que cruza la medianoche
   queda repartido entre los dos días, y los domingos y festivos (Ley Emiliani y Semana Santa, calculados
   para cualquier año) se detectan solos.
3. Cada trozo se valora con **el salario vigente ese día** dividido por las horas mensuales, o con el valor
   hora manual si el usuario lo ingresó, más el recargo de su categoría.
4. Se puede liquidar en cualquier momento: la sesión abierta se cuenta hasta ese instante. Guardar una
   liquidación congela el resultado como histórico.

### Salario por usuario

Cada usuario registra su salario en *Salario y recargos* con la fecha desde la que rige. Si le suben el
salario a mitad de quincena, las horas anteriores se liquidan con el salario anterior y las nuevas con
el nuevo.

### Valores de ley (modo "Ley", el predeterminado)

| Concepto | Valor aplicado hoy (octubre 2026) |
|---|---|
| Jornada nocturna | 19:00 a 06:00 (desde el 25-dic-2025; antes desde las 21:00) |
| Hora extra diurna / nocturna | +25 % / +75 % |
| Recargo dominical o festivo | 90 % (80 % antes de jul-2026, 100 % desde jul-2027) |
| Extra dominical/festiva diurna / nocturna | +115 % / +165 % |
| Jornada semanal | 42 h desde el 15-jul-2026 (divisor 210 h/mes) |
| Límite de horas extra | 2 h diarias (la app avisa si se supera) |

> ⚠️ **Revisar con nómina o un abogado laboral.** Los valores salen del Código Sustantivo del Trabajo,
> la Ley 2101 de 2021 y la Ley 2466 de 2025, y están en un solo lugar:
> `src/HorasExtras.Domain/ValoresLey.cs`. Cada usuario puede pasar a modo "Manual" y fijar sus propios
> porcentajes y franjas.

## API REST (para la app móvil)

La app inicia sesión con correo y clave y recibe un token de acceso (1 hora) y uno de renovación.
Todas las rutas actúan sobre el usuario del token; nadie ve las horas de otro.

| Ruta | Qué hace |
|---|---|
| `POST /api/auth/register` · `POST /api/auth/login` · `POST /api/auth/refresh` | Registro, inicio de sesión y renovación del token |
| `GET /api/estado` | Sesión abierta y segundos acumulados |
| `POST /api/sesiones/iniciar` · `/pausar` · `/continuar` · `/finalizar` | Cronómetro |
| `POST /api/sesiones` · `GET /api/sesiones` · `DELETE /api/sesiones/{id}` | Registro manual, listado y borrado |
| `GET /api/liquidacion?desde=&hasta=` | Liquidación en vivo (por defecto, la quincena actual) |
| `POST /api/liquidaciones` · `GET /api/liquidaciones` · `GET /api/liquidaciones/{id}` | Guardar y consultar liquidaciones |
| `GET/POST /api/perfil/salarios` · `DELETE /api/perfil/salarios/{id}` | Salario con historial |
| `GET/PUT /api/perfil/recargos` | Modo ley o manual, franjas y porcentajes |
| `GET /api/referencia/ley?fecha=` · `GET /api/referencia/festivos?anio=` | Valores de ley y festivos |
| `GET /api/admin/usuarios` | Resumen de la quincena de todos (solo administrador) |

En desarrollo la especificación OpenAPI está en `/openapi/v1.json`. Los errores salen como
`ProblemDetails`: 400 datos inválidos, 404 no encontrado, 409 regla de negocio (por ejemplo, pausar sin
sesión abierta).

## Despliegue en Azure con Supabase

1. **Base de datos (Supabase):** en *Connect* copia la cadena del **Session pooler** (puerto 5432; la
   conexión directa solo funciona por IPv6 y App Service no lo soporta) y pásala a formato Npgsql:
   `Host=aws-0-<region>.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.<ref>;Password=<clave>;SSL Mode=Require;Maximum Pool Size=10`
2. **App Service:** Linux, pila **.NET 10**. En *Configuración → Cadenas de conexión* agrega una de
   nombre `Default` y tipo `PostgreSQL` con la cadena anterior (equivale a la variable
   `ConnectionStrings__Default`).
3. **Despliegue desde GitHub:** en el App Service activa *Configuración → Credenciales de publicación
   básica de SCM*, descarga el perfil de publicación y, en el repositorio de GitHub, crea la variable
   `AZURE_WEBAPP_NAME` y el secreto `AZURE_WEBAPP_PUBLISH_PROFILE`. Cada push a `main` corre las pruebas
   y despliega (`.github/workflows/desplegar-azure.yml`).
4. **Migraciones:** se aplican solas al arrancar la aplicación. Las tablas quedan en el esquema
   `horasextras`, no en `public`, porque Supabase expone `public` por su API REST.

Notas del plan gratuito: F1 apaga la aplicación tras ~20 minutos sin uso (la primera visita después tarda
unos segundos) y Supabase gratuito pausa el proyecto tras una semana sin actividad.

Alternativa: la imagen del `Dockerfile` corre en cualquier servicio de contenedores (Azure Container Apps,
Railway, un VPS) con la misma variable `ConnectionStrings__Default`.

Para la app móvil, agrega su origen en `Cors:Origenes` solo si la vas a usar desde un navegador (Expo
web); las apps nativas no lo necesitan.

## Migraciones

```bash
dotnet tool restore
ASPNETCORE_ENVIRONMENT=Production dotnet ef migrations add <Nombre> \
  -p src/HorasExtras.Infrastructure -s src/HorasExtras.Web -o Migrations
```

## Pendiente

- App móvil en React Native (Expo) contra esta API.
- Envío de correos (confirmación de cuenta y recuperación de clave); hoy el registro no exige confirmar el correo.
- Traducir al español las pantallas secundarias de la cuenta (2FA, passkeys, cambio de correo), que siguen en inglés.
- Exportar la liquidación a PDF o Excel.
