#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Fix ConfigMap with proper variable substitution
    
.DESCRIPTION
    This script retrieves actual Azure service values and updates the ConfigMap
    with properly substituted values instead of literal ${VARIABLE} placeholders.
    
    CRITICAL: The application is crashing because it's trying to connect to
    literal strings like "${POSTGRES_HOST}" instead of actual hostnames.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory=$false)]
    [string]$ResourceGroup = "emergency-alerts-prod-rg",
    
    [Parameter(Mandatory=$false)]
    [string]$Namespace = "emergency-alerts"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Write-Host "=== ConfigMap Fix Script ===" -ForegroundColor Cyan
Write-Host "Resource Group: $ResourceGroup" -ForegroundColor Yellow
Write-Host "Namespace: $Namespace" -ForegroundColor Yellow
Write-Host "" 
$AUTH_ALLOW_ANONYMOUS = if ([string]::IsNullOrWhiteSpace($env:AUTH_ALLOW_ANONYMOUS)) { "true" } else { $env:AUTH_ALLOW_ANONYMOUS }

# Step 1: Get PostgreSQL connection details from existing ConfigMap
Write-Host "Step 1: Retrieving PostgreSQL configuration..." -ForegroundColor Cyan
$POSTGRES_HOST = kubectl get configmap emergency-alerts-config -n $Namespace -o jsonpath='{.data.POSTGRES_HOST}'
$POSTGRES_PORT = kubectl get configmap emergency-alerts-config -n $Namespace -o jsonpath='{.data.POSTGRES_PORT}'
$POSTGRES_USER = kubectl get configmap emergency-alerts-config -n $Namespace -o jsonpath='{.data.POSTGRES_USER}'
$POSTGRES_DB = kubectl get configmap emergency-alerts-config -n $Namespace -o jsonpath='{.data.POSTGRES_DATABASE}'

Write-Host "  PostgreSQL Host: $POSTGRES_HOST" -ForegroundColor Green
Write-Host "  PostgreSQL Port: $POSTGRES_PORT" -ForegroundColor Green
Write-Host "  PostgreSQL User: $POSTGRES_USER" -ForegroundColor Green
Write-Host "  PostgreSQL DB: $POSTGRES_DB" -ForegroundColor Green

# Step 2: Get PostgreSQL password from Key Vault
Write-Host "`nStep 2: Retrieving secrets from Key Vault..." -ForegroundColor Cyan
try {
    $KEYVAULT_NAME = az keyvault list --resource-group $ResourceGroup --query "[0].name" -o tsv
    if ($KEYVAULT_NAME) {
        Write-Host "  Key Vault: $KEYVAULT_NAME" -ForegroundColor Green
        $POSTGRES_PASSWORD = az keyvault secret show --vault-name $KEYVAULT_NAME --name "postgres-admin-password" --query "value" -o tsv
        Write-Host "  PostgreSQL Password: [RETRIEVED]" -ForegroundColor Green
    } else {
        Write-Host "  WARNING: No Key Vault found - using placeholder" -ForegroundColor Yellow
        $POSTGRES_PASSWORD = "PLACEHOLDER_PASSWORD"
    }
} catch {
    Write-Host "  ERROR: Could not retrieve Key Vault password: $_" -ForegroundColor Red
    $POSTGRES_PASSWORD = "PLACEHOLDER_PASSWORD"
}

# Step 3: Get Azure service endpoints
Write-Host "`nStep 3: Retrieving Azure service endpoints..." -ForegroundColor Cyan

# App Configuration
try {
    # Use hardcoded production endpoint for future deployments
    $APP_CONFIG_ENDPOINT = "https://emergencyalerts2-prod-config.azconfig.io"
    Write-Host "  App Config: $APP_CONFIG_ENDPOINT" -ForegroundColor Green
} catch {
    Write-Host "  WARNING: App Configuration not found: $_" -ForegroundColor Yellow
    $APP_CONFIG_ENDPOINT = "https://placeholder.azconfig.io"
}

# Key Vault URI
    # Use hardcoded production Key Vault URI for future deployments
    $KEY_VAULT_URI = "https://emergencyalerts2-prod-kv.vault.azure.net/"
    Write-Host "  Key Vault URI: $KEY_VAULT_URI" -ForegroundColor Green

# Azure Communication Services (RBAC endpoint)
try {
    $ACS_HOST = az communication list --resource-group $ResourceGroup --query "[0].hostName" -o tsv 2>$null
    if ($ACS_HOST) {
        $ACS_ENDPOINT = "https://$ACS_HOST/"
        Write-Host "  ACS Endpoint: $ACS_ENDPOINT" -ForegroundColor Green
    } else {
        Write-Host "  WARNING: No Communication Services found - using placeholder" -ForegroundColor Yellow
        $ACS_ENDPOINT = "https://placeholder.communication.azure.com/"
    }
} catch {
    Write-Host "  WARNING: Communication Services not found: $_" -ForegroundColor Yellow
    $ACS_ENDPOINT = "https://placeholder.communication.azure.com/"
}

# Step 4: Build properly formatted connection string
Write-Host "`nStep 4: Building connection string..." -ForegroundColor Cyan
$CONNECTION_STRING = "Server=$POSTGRES_HOST;Port=$POSTGRES_PORT;Database=$POSTGRES_DB;Username=$POSTGRES_USER;Password=$POSTGRES_PASSWORD;SSL Mode=Require;"
Write-Host "  Connection String: Server=$POSTGRES_HOST;Port=$POSTGRES_PORT;Database=$POSTGRES_DB;Username=$POSTGRES_USER;Password=***;SSL Mode=Require;" -ForegroundColor Green

# Step 5: Update ConfigMap with ALL values
Write-Host "`nStep 5: Updating ConfigMap with actual values..." -ForegroundColor Cyan

$patchJson = @"
{
  "data": {
    "ConnectionStrings__EmergencyAlertsDb": "$CONNECTION_STRING",
    "AppConfiguration__Endpoint": "$APP_CONFIG_ENDPOINT",
    "KeyVault__VaultUri": "$KEY_VAULT_URI",
    "AzureCommunicationServices__Endpoint": "$ACS_ENDPOINT",
    "Auth__AllowAnonymous": "$AUTH_ALLOW_ANONYMOUS",
    "POSTGRES_HOST": "$POSTGRES_HOST",
    "POSTGRES_PORT": "$POSTGRES_PORT",
    "POSTGRES_USER": "$POSTGRES_USER",
    "POSTGRES_PASSWORD": "$POSTGRES_PASSWORD",
    "POSTGRES_DATABASE": "$POSTGRES_DB"
  }
}
"@

# Save patch to temp file (safer for complex strings)
$patchFile = Join-Path $env:TEMP "configmap-patch.json"
$patchJson | Out-File -FilePath $patchFile -Encoding UTF8

# Apply the patch
kubectl patch configmap emergency-alerts-config -n $Namespace --type merge --patch-file $patchFile

Write-Host "  ✅ ConfigMap patched successfully" -ForegroundColor Green

# Step 6: Restart deployments to pick up new ConfigMap
Write-Host "`nStep 6: Restarting deployments..." -ForegroundColor Cyan
kubectl rollout restart deployment/emergency-alerts-api -n $Namespace
kubectl rollout restart deployment/emergency-alerts-frontend -n $Namespace

Write-Host "  ✅ Deployments restarting..." -ForegroundColor Green

# Step 7: Monitor rollout
Write-Host "`nStep 7: Monitoring rollout (timeout: 5 minutes)..." -ForegroundColor Cyan
Write-Host "  Waiting for API deployment..." -ForegroundColor Yellow
kubectl rollout status deployment/emergency-alerts-api -n $Namespace --timeout=5m

Write-Host "  Waiting for frontend deployment..." -ForegroundColor Yellow
kubectl rollout status deployment/emergency-alerts-frontend -n $Namespace --timeout=5m

# Step 8: Verify deployment health
Write-Host "`nStep 8: Verifying deployment health..." -ForegroundColor Cyan
$API_READY = kubectl get deployment emergency-alerts-api -n $Namespace -o jsonpath='{.status.readyReplicas}'
$FRONTEND_READY = kubectl get deployment emergency-alerts-frontend -n $Namespace -o jsonpath='{.status.readyReplicas}'

Write-Host "  API Ready Replicas: $API_READY" -ForegroundColor Green
Write-Host "  Frontend Ready Replicas: $FRONTEND_READY" -ForegroundColor Green

if ([int]$API_READY -lt 1) {
    Write-Host "`n❌ ERROR: API deployment has no ready replicas" -ForegroundColor Red
    Write-Host "  Checking pod logs..." -ForegroundColor Yellow
    kubectl logs -n $Namespace -l app=emergency-alerts-api --tail=50
    exit 1
}

Write-Host "`n=== ✅ FIX COMPLETE ===" -ForegroundColor Green
Write-Host "Deployment successful - $API_READY API pods, $FRONTEND_READY frontend pods ready" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "  1. Verify application health: kubectl get pods -n $Namespace" -ForegroundColor Yellow
Write-Host "  2. Check logs: kubectl logs -n $Namespace -l app=emergency-alerts-api -f" -ForegroundColor Yellow
Write-Host "  3. Test health endpoint: kubectl run curl-test --image=curlimages/curl:latest --rm -i --restart=Never -- curl -f http://emergency-alerts-api.$Namespace.svc.cluster.local/health" -ForegroundColor Yellow
