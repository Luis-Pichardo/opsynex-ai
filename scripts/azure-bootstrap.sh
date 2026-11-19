#!/usr/bin/env bash
# Preparación única de Azure para Opsynex AI (etapa 14). Requiere Azure CLI con sesión iniciada
# (`az login`) con permisos de Owner (o Contributor + User Access Administrator) en la suscripción.
#
# Crea (si no existen):
#   1. El grupo de recursos.
#   2. Key Vault (RBAC, purge protection) con los secretos de producción GENERADOS aquí:
#      sql-admin-password, jwt-secret, ai-service-api-key, seed-admin-password y anthropic-api-key.
#   3. Una aplicación de Entra ID con credencial federada (OIDC) para GitHub Actions: sin
#      contraseñas guardadas en GitHub. Roles en el grupo: Contributor y Role Based Access
#      Control Administrator (para asignar AcrPull / Key Vault Secrets User a la identidad de las apps).
# Al final muestra (o configura con `gh`) las variables que necesita deploy.yml.
#
# Uso:
#   GITHUB_REPO=owner/opsynex-ai SEED_ADMIN_EMAIL=admin@tuempresa.com ALERT_EMAIL=ops@tuempresa.com \
#   ANTHROPIC_API_KEY=sk-ant-... bash scripts/azure-bootstrap.sh
# Es idempotente: se puede volver a ejecutar; nunca sobrescribe un secreto existente.

set -euo pipefail

: "${GITHUB_REPO:?Indica GITHUB_REPO (owner/repo)}"
: "${SEED_ADMIN_EMAIL:?Indica SEED_ADMIN_EMAIL (correo del primer Admin)}"
: "${ALERT_EMAIL:?Indica ALERT_EMAIL (correo de las alertas)}"
LOCATION="${AZURE_LOCATION:-eastus2}"
RESOURCE_GROUP="${AZURE_RESOURCE_GROUP:-rg-opsynex-prod}"
APP_NAME="${AZURE_APP_NAME:-github-opsynex-deploy}"

log() { printf '\033[1;34m==> %s\033[0m\n' "$*"; }
secret() { openssl rand -base64 "$1" | tr -d '\n/+=' | cut -c1-"$2"; }

SUBSCRIPTION_ID="$(az account show --query id -o tsv)"
TENANT_ID="$(az account show --query tenantId -o tsv)"
ME="$(az ad signed-in-user show --query id -o tsv)"
log "Suscripción $SUBSCRIPTION_ID · región $LOCATION · grupo $RESOURCE_GROUP"

for provider in Microsoft.App Microsoft.OperationalInsights Microsoft.Insights Microsoft.Sql \
  Microsoft.ContainerRegistry Microsoft.KeyVault Microsoft.ManagedIdentity; do
  az provider register --namespace "$provider" -o none
done

log "Grupo de recursos"
az group create -n "$RESOURCE_GROUP" -l "$LOCATION" --tags app=opsynex-ai environment=production -o none
RG_ID="$(az group show -n "$RESOURCE_GROUP" --query id -o tsv)"

log "Key Vault"
KEY_VAULT="${AZURE_KEY_VAULT:-$(az keyvault list -g "$RESOURCE_GROUP" --query '[0].name' -o tsv)}"
if [[ -z "$KEY_VAULT" ]]; then
  KEY_VAULT="kv-opsynex-$(secret 12 6 | tr '[:upper:]' '[:lower:]')"
  az keyvault create -n "$KEY_VAULT" -g "$RESOURCE_GROUP" -l "$LOCATION" \
    --enable-rbac-authorization true --enable-purge-protection true \
    --enabled-for-template-deployment true --retention-days 90 -o none
fi
KV_ID="$(az keyvault show -n "$KEY_VAULT" --query id -o tsv)"
az role assignment create --assignee-object-id "$ME" --assignee-principal-type User \
  --role "Key Vault Secrets Officer" --scope "$KV_ID" -o none 2>/dev/null || true
sleep 20 # propagación del rol

set_secret() { # nombre valor — solo si no existe
  if ! az keyvault secret show --vault-name "$KEY_VAULT" -n "$1" -o none 2>/dev/null; then
    az keyvault secret set --vault-name "$KEY_VAULT" -n "$1" --value "$2" -o none
    echo "   secreto $1 creado"
  else
    echo "   secreto $1 ya existe (no se cambia)"
  fi
}
# La contraseña de SQL debe tener mayúsculas, minúsculas, dígitos y símbolos.
set_secret sql-admin-password "Op$(secret 48 28)#9a"
set_secret jwt-secret "$(secret 96 64)"
set_secret ai-service-api-key "$(secret 48 40)"
set_secret seed-admin-password "Admin$(secret 24 16)7"
set_secret anthropic-api-key "${ANTHROPIC_API_KEY:-disabled}"

log "Identidad de GitHub Actions (OIDC)"
APP_ID="$(az ad app list --display-name "$APP_NAME" --query '[0].appId' -o tsv)"
if [[ -z "$APP_ID" ]]; then
  APP_ID="$(az ad app create --display-name "$APP_NAME" --query appId -o tsv)"
fi
SP_ID="$(az ad sp show --id "$APP_ID" --query id -o tsv 2>/dev/null || az ad sp create --id "$APP_ID" --query id -o tsv)"
for subject in "repo:${GITHUB_REPO}:ref:refs/heads/main" "repo:${GITHUB_REPO}:environment:production"; do
  name="$(echo "$subject" | tr -c '[:alnum:]' '-' | cut -c1-120)"
  if ! az ad app federated-credential list --id "$APP_ID" --query "[?subject=='$subject']" -o tsv | grep -q .; then
    az ad app federated-credential create --id "$APP_ID" --parameters "{\"name\":\"$name\",\"issuer\":\"https://token.actions.githubusercontent.com\",\"subject\":\"$subject\",\"audiences\":[\"api://AzureADTokenExchange\"]}" -o none
  fi
done
for role in "Contributor" "Role Based Access Control Administrator"; do
  az role assignment create --assignee-object-id "$SP_ID" --assignee-principal-type ServicePrincipal \
    --role "$role" --scope "$RG_ID" -o none 2>/dev/null || true
done

log "Variables de GitHub Actions"
declare -A VARS=(
  [AZURE_CLIENT_ID]="$APP_ID"
  [AZURE_TENANT_ID]="$TENANT_ID"
  [AZURE_SUBSCRIPTION_ID]="$SUBSCRIPTION_ID"
  [AZURE_RESOURCE_GROUP]="$RESOURCE_GROUP"
  [AZURE_KEY_VAULT]="$KEY_VAULT"
  [SEED_ADMIN_EMAIL]="$SEED_ADMIN_EMAIL"
  [ALERT_EMAIL]="$ALERT_EMAIL"
)
for key in "${!VARS[@]}"; do
  if command -v gh >/dev/null && gh auth status >/dev/null 2>&1; then
    gh variable set "$key" --repo "$GITHUB_REPO" --body "${VARS[$key]}"
  fi
  printf '   %-24s %s\n' "$key" "${VARS[$key]}"
done
cat <<MSG

Listo. Si no se configuraron solas, agrega esas variables en GitHub: Settings → Secrets and
variables → Actions → Variables. Ninguna es secreta (los secretos viven en Key Vault).
La contraseña del primer Admin está en Key Vault (secreto seed-admin-password):
  az keyvault secret show --vault-name $KEY_VAULT -n seed-admin-password --query value -o tsv
Después, ejecuta el workflow "Deploy" (o haz merge a main).
MSG
