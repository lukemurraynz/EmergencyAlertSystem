// Schema initialization using Deployment Scripts (resource-group scope)
// Creates the application schema in the 'postgres' database of the PostgreSQL server
// NOTE: This performs a data-plane operation; keep it optional and idempotent.

param location string
param dbHost string
param schemaName string = 'emergency_alerts'
param identityId string
param keyVaultName string
param dbAdminUser string = 'pgadmin'

resource createSchema 'Microsoft.Resources/deploymentScripts@2023-08-01' = {
  name: 'create-schema-${uniqueString(resourceGroup().id, schemaName)}'
  location: location
  kind: 'AzureCLI'
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${identityId}': {}
    }
  }
  properties: {
    azCliVersion: '2.51.0'
    timeout: 'PT15M'
    cleanupPreference: 'OnSuccess'
    retentionInterval: 'P1D'
    scriptContent: '''#!/usr/bin/env bash
set -euo pipefail
if ! command -v psql >/dev/null 2>&1; then
  echo "Installing PostgreSQL client..."
  if command -v apt-get >/dev/null 2>&1; then
    apt-get update -y
    DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends postgresql-client
  elif command -v apk >/dev/null 2>&1; then
    apk add --no-cache postgresql15-client || apk add --no-cache postgresql14-client
  fi
fi
echo "Fetching PostgreSQL admin password from Key Vault (using Managed Identity)..."
export PGPASSWORD="$(az keyvault secret show --vault-name "${KV_NAME}" --name postgres-admin-password --query value -o tsv)"
psql "host=${DB_HOST} port=5432 user=${DB_ADMIN_USER} dbname=postgres sslmode=require" -v ON_ERROR_STOP=1 \
  -c "CREATE SCHEMA IF NOT EXISTS ${SCHEMA_NAME};"
echo "Schema ${SCHEMA_NAME} created on ${DB_HOST}."'''
    environmentVariables: [
      {
        name: 'DB_HOST'
        value: dbHost
      }
      {
        name: 'SCHEMA_NAME'
        value: schemaName
      }
      {
        name: 'KV_NAME'
        value: keyVaultName
      }
      {
        name: 'DB_ADMIN_USER'
        value: dbAdminUser
      }
    ]
  }
}
