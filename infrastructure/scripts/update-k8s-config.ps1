#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Update Kubernetes ConfigMap with actual Azure resource values.
.DESCRIPTION
    Gathers Azure resource values and updates the Kubernetes ConfigMap
    with proper configuration for the Emergency Alerts application.
#>
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Write-Host "Gathering Azure resource values..." -ForegroundColor Cyan

# Resource naming (override via env vars)
$projectName = if ([string]::IsNullOrWhiteSpace($env:PROJECT_NAME)) { "emergencyalerts2" } else { $env:PROJECT_NAME }
$environment = if ([string]::IsNullOrWhiteSpace($env:ENVIRONMENT)) { "prod" } else { $env:ENVIRONMENT }
$resourceGroup = "$projectName-$environment-rg"
$appConfigName = "$projectName-$environment-config"
$keyVaultName = "$projectName-$environment-kv"
$managedIdentityName = "$projectName-$environment-identity"
$authAllowAnonymous = if ([string]::IsNullOrWhiteSpace($env:AUTH_ALLOW_ANONYMOUS)) { "true" } else { $env:AUTH_ALLOW_ANONYMOUS }

# Get App Configuration endpoint
$appConfigEndpoint = az appconfig show `
    --resource-group $resourceGroup `
    --name $appConfigName `
    --query endpoint `
    --output tsv

Write-Host "✓ App Config Endpoint: $appConfigEndpoint" -ForegroundColor Green

# Get PostgreSQL details
$postgresHost = az postgres flexible-server list `
    --resource-group $resourceGroup `
    --query "[0].fullyQualifiedDomainName" `
    --output tsv

Write-Host "✓ PostgreSQL Host: $postgresHost" -ForegroundColor Green

# Get Key Vault URI
$keyVaultUri = az keyvault show `
    --resource-group $resourceGroup `
    --name $keyVaultName `
    --query properties.vaultUri `
    --output tsv

Write-Host "✓ Key Vault URI: $keyVaultUri" -ForegroundColor Green

# Get PostgreSQL admin password from Key Vault
$postgresPassword = az keyvault secret show `
    --vault-name $keyVaultName `
    --name postgres-admin-password `
    --query value `
    --output tsv

Write-Host "✓ PostgreSQL password retrieved" -ForegroundColor Green

# Get ACS endpoint (resource-based, no secrets)
$acsEndpoint = az communication list `
    --resource-group $resourceGroup `
    --query "[0].hostName" `
    --output tsv 2>$null

if (-not $acsEndpoint) {
    Write-Host "! ACS endpoint not found, using placeholder" -ForegroundColor Yellow
    $acsEndpoint = "NOT_CONFIGURED"
} else {
    $acsEndpoint = "https://$acsEndpoint/"
}

# Get Managed Identity Client ID
$managedIdentityClientId = az identity show `
    --resource-group $resourceGroup `
    --name $managedIdentityName `
    --query clientId `
    --output tsv

Write-Host "✓ Managed Identity Client ID: $managedIdentityClientId" -ForegroundColor Green

# Create ConfigMap YAML with actual values
Write-Host "`nCreating ConfigMap with actual values..." -ForegroundColor Cyan

$configMapYaml = @"
apiVersion: v1
kind: ConfigMap
metadata:
  name: emergency-alerts-config
  namespace: emergency-alerts
data:
  ASPNETCORE_ENVIRONMENT: "Production"
  ConnectionStrings__EmergencyAlertsDb: "Server=$postgresHost;Port=5432;Database=postgres;Username=pgadmin;Password=$postgresPassword;SSL Mode=Require;"
  AppConfiguration__Endpoint: "$appConfigEndpoint"
  KeyVault__VaultUri: "$keyVaultUri"
  AzureCommunicationServices__Endpoint: "$acsEndpoint"
  Auth__AllowAnonymous: "$authAllowAnonymous"
  Logging__LogLevel__Default: "Information"
  Logging__LogLevel__Microsoft: "Warning"
  Logging__LogLevel__Microsoft.AspNetCore: "Information"
  Cors__AllowedOrigins: "https://emergency-alerts.example.com,http://localhost:5173,http://localhost:3000"
  POSTGRES_HOST: "$postgresHost"
  POSTGRES_PORT: "5432"
  POSTGRES_USER: "pgadmin"
  POSTGRES_PASSWORD: "$postgresPassword"
  POSTGRES_DATABASE: "postgres"
"@

# Save to temp file
$tempFile = [System.IO.Path]::GetTempFileName() + ".yaml"
$configMapYaml | Out-File -FilePath $tempFile -Encoding UTF8

Write-Host "ConfigMap saved to: $tempFile" -ForegroundColor Gray

# Apply the ConfigMap
Write-Host "`nApplying ConfigMap to Kubernetes..." -ForegroundColor Cyan
kubectl apply -f $tempFile

if ($LASTEXITCODE -eq 0) {
    Write-Host "✓ ConfigMap updated successfully" -ForegroundColor Green
    
    # Restart deployments to pick up new config
    Write-Host "`nRestarting deployments to apply new configuration..." -ForegroundColor Cyan
    kubectl rollout restart deployment emergency-alerts-api -n emergency-alerts
    kubectl rollout restart deployment emergency-alerts-frontend -n emergency-alerts
    
    Write-Host "✓ Deployments restarted" -ForegroundColor Green
    
    Write-Host "`nWaiting for rollout to complete..." -ForegroundColor Cyan
    kubectl rollout status deployment emergency-alerts-api -n emergency-alerts --timeout=5m
    
    Write-Host "`n✅ Configuration updated and applied!" -ForegroundColor Green
} else {
    Write-Host "❌ Failed to apply ConfigMap" -ForegroundColor Red
    exit 1
}

# Cleanup temp file
Remove-Item -Path $tempFile -Force
