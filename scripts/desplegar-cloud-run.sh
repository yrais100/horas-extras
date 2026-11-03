#!/usr/bin/env bash
# Despliegue en Google Cloud Run (pruebas). Pasos completos en docs/despliegue-cloud-run.md.
# Se puede correr en Cloud Shell (ya trae gcloud y el repositorio se clona ahí) o en cualquier equipo con gcloud.
#
#   ./scripts/desplegar-cloud-run.sh preparar   # una sola vez: APIs, repositorio de imágenes, secreto y cuenta para GitHub
#   ./scripts/desplegar-cloud-run.sh desplegar  # construye el Dockerfile en Cloud Build y despliega
#
# Variables opcionales: GCP_PROJECT_ID (por defecto, el proyecto activo de gcloud), GCP_REGION (us-east4),
# SERVICIO (horasextras).
set -euo pipefail

PROYECTO="${GCP_PROJECT_ID:-$(gcloud config get-value project 2>/dev/null)}"
REGION="${GCP_REGION:-us-east4}"
SERVICIO="${SERVICIO:-horasextras}"
REPOSITORIO="horasextras"          # Artifact Registry (imágenes Docker)
SECRETO="horasextras-db"           # Secret Manager: cadena de conexión de Supabase
CUENTA_GITHUB="github-despliegue"  # cuenta de servicio que usa el workflow de GitHub Actions

if [ -z "$PROYECTO" ]; then
  echo "Define el proyecto: gcloud config set project <id-del-proyecto>  (o GCP_PROJECT_ID=<id>)" >&2
  exit 1
fi
gcloud config set project "$PROYECTO" >/dev/null
NUMERO=$(gcloud projects describe "$PROYECTO" --format='value(projectNumber)')
# Cloud Run ejecuta el contenedor con la cuenta de servicio predeterminada de Compute Engine.
CUENTA_EJECUCION="${NUMERO}-compute@developer.gserviceaccount.com"

preparar() {
  echo "==> Activando APIs (Cloud Run, Artifact Registry, Cloud Build, Secret Manager)"
  gcloud services enable run.googleapis.com artifactregistry.googleapis.com \
    cloudbuild.googleapis.com secretmanager.googleapis.com

  echo "==> Repositorio de imágenes $REPOSITORIO en $REGION"
  if ! gcloud artifacts repositories describe "$REPOSITORIO" --location "$REGION" >/dev/null 2>&1; then
    gcloud artifacts repositories create "$REPOSITORIO" --repository-format docker --location "$REGION" \
      --description "Imágenes de Horas Extras"
  fi
  # La capa gratuita de Artifact Registry es de 0,5 GB: se conservan solo las 3 imágenes más recientes.
  local politica
  politica=$(mktemp)
  cat > "$politica" <<'JSON'
[
  {"name": "conservar-recientes", "action": {"type": "Keep"}, "mostRecentVersions": {"keepCount": 3}},
  {"name": "borrar-viejas", "action": {"type": "Delete"}, "condition": {"tagState": "any", "olderThan": "86400s"}}
]
JSON
  gcloud artifacts repositories set-cleanup-policies "$REPOSITORIO" --location "$REGION" \
    --policy "$politica" --no-dry-run
  rm -f "$politica"

  echo "==> Secreto $SECRETO (cadena de conexión de Supabase)"
  if ! gcloud secrets describe "$SECRETO" >/dev/null 2>&1; then
    echo "Pega la cadena de Supabase en formato Npgsql y presiona Enter (no se muestra):"
    read -rs CADENA
    printf '%s' "$CADENA" | gcloud secrets create "$SECRETO" --replication-policy automatic --data-file=-
  else
    echo "Ya existe. Para cambiarla: printf '%s' '<cadena>' | gcloud secrets versions add $SECRETO --data-file=-"
  fi
  gcloud secrets add-iam-policy-binding "$SECRETO" \
    --member "serviceAccount:$CUENTA_EJECUCION" --role roles/secretmanager.secretAccessor >/dev/null

  echo "==> Cuenta de servicio $CUENTA_GITHUB para GitHub Actions"
  local correo="$CUENTA_GITHUB@$PROYECTO.iam.gserviceaccount.com"
  if ! gcloud iam service-accounts describe "$correo" >/dev/null 2>&1; then
    gcloud iam service-accounts create "$CUENTA_GITHUB" --display-name "Despliegue desde GitHub Actions"
  fi
  for rol in roles/run.admin roles/artifactregistry.writer; do
    gcloud projects add-iam-policy-binding "$PROYECTO" --member "serviceAccount:$correo" \
      --role "$rol" --condition=None >/dev/null
  done
  # Permite que el despliegue asigne la cuenta de ejecución al servicio de Cloud Run.
  gcloud iam service-accounts add-iam-policy-binding "$CUENTA_EJECUCION" \
    --member "serviceAccount:$correo" --role roles/iam.serviceAccountUser >/dev/null

  cat <<TXT

Listo. Para que GitHub despliegue solo en cada push a main:
  1. Crea la llave:  gcloud iam service-accounts keys create llave-github.json --iam-account $correo
  2. En GitHub (Settings → Secrets and variables → Actions):
       secreto   GCP_CREDENTIALS = contenido completo de llave-github.json
       variable  GCP_PROJECT_ID  = $PROYECTO
       variable  GCP_REGION      = $REGION   (opcional; por defecto us-east4)
  3. Borra el archivo:  rm llave-github.json
O despliega a mano ahora con:  ./scripts/desplegar-cloud-run.sh desplegar
TXT
}

desplegar() {
  cd "$(dirname "$0")/.."
  echo "==> Construyendo en Cloud Build y desplegando $SERVICIO en $REGION"
  local imagen="$REGION-docker.pkg.dev/$PROYECTO/$REPOSITORIO/web:manual-$(date +%Y%m%d%H%M%S)"
  gcloud builds submit --tag "$imagen" .
  # Mismos parámetros que .github/workflows/desplegar-cloud-run.yml: si cambias uno, cambia el otro.
  gcloud run deploy "$SERVICIO" --image "$imagen" --region "$REGION" \
    --allow-unauthenticated --port 8080 --memory 512Mi --cpu 1 \
    --min-instances 0 --max-instances 1 --timeout 3600 --session-affinity --cpu-boost \
    --set-secrets "ConnectionStrings__Default=$SECRETO:latest"
  local url
  url=$(gcloud run services describe "$SERVICIO" --region "$REGION" --format 'value(status.url)')
  echo
  echo "Servicio en: $url"
  echo "Comprueba:   $url/salud   (debe responder {\"estado\":\"ok\"})"
}

case "${1:-}" in
  preparar) preparar ;;
  desplegar) desplegar ;;
  *) echo "Uso: $0 preparar | desplegar" >&2; exit 1 ;;
esac
