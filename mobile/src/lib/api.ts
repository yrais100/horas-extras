import type {
  Liquidacion,
  LiquidacionGuardada,
  Periodo,
  Recargos,
  Salario,
  SalarioInput,
  Sesion,
  Tokens,
  ValoresLey,
} from './tipos';

/** Error de la API con un mensaje listo para mostrar. status 0 = no hubo respuesta del servidor. */
export class ApiError extends Error {
  constructor(
    public readonly status: number,
    message: string,
  ) {
    super(message);
    this.name = 'ApiError';
  }
}

/** Tokens guardados junto con el instante (ms) en que vence el de acceso. */
export interface TokensGuardados {
  accessToken: string;
  refreshToken: string;
  venceEn: number;
}

/** Dónde se guardan los tokens; en la app es expo-secure-store. */
export interface AlmacenTokens {
  leer(): Promise<TokensGuardados | null>;
  guardar(tokens: TokensGuardados | null): Promise<void>;
}

export interface OpcionesCliente {
  baseUrl: string;
  almacen: AlmacenTokens;
  fetch?: typeof fetch;
  ahora?: () => number;
  /** Se llama cuando el token de renovación ya no sirve y hay que volver a ingresar. */
  alExpirar?: () => void;
  timeoutMs?: number;
}

/** Quita la barra final y valida que sea una URL http(s). Devuelve null si no es válida. */
export function normalizarBaseUrl(url: string): string | null {
  const u = url.trim().replace(/\/+$/, '');
  return /^https?:\/\/[^\s/]+(\/[^\s]*)?$/i.test(u) ? u : null;
}

const MARGEN_RENOVACION_MS = 60_000;

/** Traduce la respuesta de error (ProblemDetails de ASP.NET Core) a un mensaje para el usuario. */
export async function mensajeDeError(r: Response, ruta: string): Promise<string> {
  let cuerpo: { detail?: string; title?: string; errors?: Record<string, string[]> } | null = null;
  if ((r.headers.get('content-type') ?? '').includes('json')) {
    try {
      cuerpo = await r.json();
    } catch {
      cuerpo = null;
    }
  }
  if (ruta === '/api/auth/login' && r.status === 401) {
    if (cuerpo?.detail === 'LockedOut') return 'La cuenta está bloqueada por varios intentos fallidos. Intenta más tarde.';
    if (cuerpo?.detail === 'NotAllowed') return 'Esta cuenta todavía no puede iniciar sesión.';
    return 'Correo o clave incorrectos.';
  }
  if (cuerpo?.errors) {
    const mensajes = Object.values(cuerpo.errors).flat();
    if (mensajes.length) return mensajes.join('\n');
  }
  if (cuerpo?.detail) return cuerpo.detail;
  if (r.status === 401) return 'Tu sesión expiró. Vuelve a ingresar.';
  if (r.status === 404) return 'No se encontró el recurso en el servidor. Revisa la dirección del servidor.';
  return `El servidor respondió con un error (${r.status}).`;
}

export class ClienteApi {
  readonly baseUrl: string;
  private readonly almacen: AlmacenTokens;
  private readonly fetch: typeof fetch;
  private readonly ahora: () => number;
  private readonly alExpirar?: () => void;
  private readonly timeoutMs: number;
  private renovando: Promise<TokensGuardados | null> | null = null;

  constructor(o: OpcionesCliente) {
    const base = normalizarBaseUrl(o.baseUrl);
    if (!base) throw new Error(`Dirección de servidor inválida: ${o.baseUrl}`);
    this.baseUrl = base;
    this.almacen = o.almacen;
    this.fetch = o.fetch ?? ((...args) => fetch(...args));
    this.ahora = o.ahora ?? Date.now;
    this.alExpirar = o.alExpirar;
    this.timeoutMs = o.timeoutMs ?? 30_000;
  }

  // ---- Autenticación (MapIdentityApi en /api/auth) ----

  async registrar(email: string, password: string): Promise<void> {
    await this.pedir('POST', '/api/auth/register', { email: email.trim(), password }, false);
  }

  async ingresar(email: string, password: string): Promise<void> {
    const t = await this.pedir<Tokens>('POST', '/api/auth/login', { email: email.trim(), password }, false);
    await this.guardarTokens(t);
  }

  async salir(): Promise<void> {
    await this.almacen.guardar(null);
  }

  async tieneSesion(): Promise<boolean> {
    return (await this.almacen.leer()) !== null;
  }

  // ---- Sesiones de horas extra ----

  estado = () => this.pedir<Sesion | null>('GET', '/api/estado');
  iniciar = (nota?: string) => this.pedir<Sesion>('POST', '/api/sesiones/iniciar', { nota: nota || null });
  pausar = () => this.pedir<Sesion>('POST', '/api/sesiones/pausar');
  continuar = () => this.pedir<Sesion>('POST', '/api/sesiones/continuar');
  finalizar = () => this.pedir<Sesion>('POST', '/api/sesiones/finalizar');
  registrarManual = (inicioUtc: Date, finUtc: Date, nota?: string) =>
    this.pedir<Sesion>('POST', '/api/sesiones', {
      inicioUtc: inicioUtc.toISOString(),
      finUtc: finUtc.toISOString(),
      nota: nota || null,
    });
  sesiones = (p?: Periodo) => this.pedir<Sesion[]>('GET', `/api/sesiones${consulta(p)}`);
  eliminarSesion = (id: number) => this.pedir<void>('DELETE', `/api/sesiones/${id}`);

  // ---- Liquidación ----

  liquidacion = (p?: Periodo) => this.pedir<Liquidacion>('GET', `/api/liquidacion${consulta(p)}`);
  guardarLiquidacion = (p?: Periodo) => this.pedir<LiquidacionGuardada>('POST', '/api/liquidaciones', p ?? {});
  liquidaciones = () => this.pedir<LiquidacionGuardada[]>('GET', '/api/liquidaciones');
  liquidacionGuardada = (id: number) => this.pedir<Liquidacion>('GET', `/api/liquidaciones/${id}`);

  // ---- Perfil ----

  salarios = () => this.pedir<Salario[]>('GET', '/api/perfil/salarios');
  guardarSalario = (s: SalarioInput) => this.pedir<Salario>('POST', '/api/perfil/salarios', s);
  eliminarSalario = (id: number) => this.pedir<void>('DELETE', `/api/perfil/salarios/${id}`);
  recargos = () => this.pedir<Recargos>('GET', '/api/perfil/recargos');
  guardarRecargos = (r: Recargos) => this.pedir<Recargos>('PUT', '/api/perfil/recargos', r);
  ley = (fecha?: string) => this.pedir<ValoresLey>('GET', `/api/referencia/ley${fecha ? `?fecha=${fecha}` : ''}`);

  // ---- Infraestructura ----

  private async guardarTokens(t: Tokens): Promise<TokensGuardados> {
    const g = { accessToken: t.accessToken, refreshToken: t.refreshToken, venceEn: this.ahora() + t.expiresIn * 1000 };
    await this.almacen.guardar(g);
    return g;
  }

  /** Renueva los tokens una sola vez aunque varias peticiones lo pidan a la vez. */
  private renovar(refreshToken: string): Promise<TokensGuardados | null> {
    this.renovando ??= (async () => {
      try {
        const t = await this.pedir<Tokens>('POST', '/api/auth/refresh', { refreshToken }, false);
        return await this.guardarTokens(t);
      } catch (e) {
        // 400: versiones de la API anteriores a este cambio respondían 400 a un token de renovación inválido.
        if (e instanceof ApiError && (e.status === 401 || e.status === 400)) {
          await this.almacen.guardar(null);
          this.alExpirar?.();
          return null;
        }
        throw e;
      } finally {
        this.renovando = null;
      }
    })();
    return this.renovando;
  }

  private async tokenVigente(): Promise<string> {
    let t = await this.almacen.leer();
    if (!t) {
      this.alExpirar?.();
      throw new ApiError(401, 'Tu sesión expiró. Vuelve a ingresar.');
    }
    if (t.venceEn - MARGEN_RENOVACION_MS <= this.ahora()) t = await this.renovar(t.refreshToken);
    if (!t) throw new ApiError(401, 'Tu sesión expiró. Vuelve a ingresar.');
    return t.accessToken;
  }

  private async pedir<T>(metodo: string, ruta: string, cuerpo?: unknown, autenticado = true): Promise<T> {
    let token = autenticado ? await this.tokenVigente() : null;
    let r = await this.enviar(metodo, ruta, cuerpo, token);

    // El token pudo vencer o revocarse en el servidor aunque aquí pareciera vigente: renovar y reintentar una vez.
    if (autenticado && r.status === 401) {
      const t = await this.almacen.leer();
      const nuevo = t ? await this.renovar(t.refreshToken) : null;
      if (!nuevo) throw new ApiError(401, 'Tu sesión expiró. Vuelve a ingresar.');
      token = nuevo.accessToken;
      r = await this.enviar(metodo, ruta, cuerpo, token);
    }

    if (!r.ok) throw new ApiError(r.status, await mensajeDeError(r, ruta));
    if (r.status === 204) return undefined as T;
    const texto = await r.text();
    return (texto ? JSON.parse(texto) : undefined) as T;
  }

  private async enviar(metodo: string, ruta: string, cuerpo: unknown, token: string | null): Promise<Response> {
    const headers: Record<string, string> = { Accept: 'application/json' };
    if (cuerpo !== undefined) headers['Content-Type'] = 'application/json';
    if (token) headers.Authorization = `Bearer ${token}`;
    const control = new AbortController();
    const reloj = setTimeout(() => control.abort(), this.timeoutMs);
    try {
      return await this.fetch(this.baseUrl + ruta, {
        method: metodo,
        headers,
        body: cuerpo === undefined ? undefined : JSON.stringify(cuerpo),
        signal: control.signal,
      });
    } catch {
      if (control.signal.aborted)
        throw new ApiError(
          0,
          `${this.baseUrl} tardó demasiado en responder. Si el servidor estaba dormido, espera un momento e intenta de nuevo.`,
        );
      throw new ApiError(
        0,
        `No se pudo conectar con ${this.baseUrl}. Revisa tu conexión a internet o la dirección del servidor.`,
      );
    } finally {
      clearTimeout(reloj);
    }
  }
}

function consulta(p?: Periodo): string {
  return p ? `?desde=${p.desde}&hasta=${p.hasta}` : '';
}
