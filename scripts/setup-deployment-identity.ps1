#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Set up Azure Deployment Identity for GitHub Actions using Workload Identity Federation
.DESCRIPTION
    Creates a user-assigned managed identity with federated credentials for GitHub Actions OIDC
.PARAMETER SubscriptionId
    Azure Subscription ID
.PARAMETER ResourceGroupName
    Resource Group name for the managed identity
.PARAMETER IdentityName
    Name of the user-assigned managed identity
.PARAMETER GitHubOwner
    GitHub repository owner (username or org)
.PARAMETER GitHubRepo
    GitHub repository name (default: probable-octo-rotary-phone)
#>

param(
    [Parameter(Mandatory=$true)]
    [string]$SubscriptionId,
    
    [Parameter(Mandatory=$true)]
    [string]$ResourceGroupName,
    
    [Parameter(Mandatory=$false)]
    [string]$IdentityName = "emergency-alerts-github-actions",
    
    [Parameter(Mandatory=$true)]
    [string]$GitHubOwner,
    
    [Parameter(Mandatory=$false)]
    [string]$GitHubRepo = "probable-octo-rotary-phone"
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Emergency Alerts Deployment Identity Setup" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

# Set context
Write-Host "`n[1/6] Setting Azure subscription context..." -ForegroundColor Yellow
az account set --subscription $SubscriptionId
$TenantId = az account show --query tenantId -o tsv
Write-Host "✅ Subscription: $SubscriptionId" -ForegroundColor Green
Write-Host "✅ Tenant ID: $TenantId" -ForegroundColor Green

# Create resource group if it doesn't exist
Write-Host "`n[2/6] Ensuring resource group exists..." -ForegroundColor Yellow
az group create --name $ResourceGroupName --location uksouth 2>&1 | Out-Null
Write-Host "✅ Resource group: $ResourceGroupName" -ForegroundColor Green

# Create user-assigned managed identity
Write-Host "`n[3/6] Creating user-assigned managed identity..." -ForegroundColor Yellow
az identity create `
    --resource-group $ResourceGroupName `
    --name $IdentityName | Out-Null

$Identity = az identity show `
    --resource-group $ResourceGroupName `
    --name $IdentityName | ConvertFrom-Json

$ClientId = $Identity.clientId
$ObjectId = $Identity.principalId
$ResourceId = $Identity.id

Write-Host "✅ Managed Identity: $IdentityName" -ForegroundColor Green
Write-Host "   Client ID: $ClientId" -ForegroundColor Green
Write-Host "   Principal ID: $ObjectId" -ForegroundColor Green
Write-Host "   Resource ID: $ResourceId" -ForegroundColor Green

# Assign roles to managed identity
Write-Host "`n[4/6] Assigning roles to managed identity..." -ForegroundColor Yellow

$roles = @(
    "Contributor",
    "User Access Administrator",
    "AcrPush",
    "Key Vault Secrets User"
)

foreach ($role in $roles) {
    Write-Host "   Assigning role: $role" -ForegroundColor Cyan
    
    # Check if assignment already exists
    $existing = az role assignment list `
        --assignee-object-id $ObjectId `
        --role $role `
        --scope "/subscriptions/$SubscriptionId" `
        --query "length([0])" --output tsv
    
    if ($existing -eq "0") {
        az role assignment create `
            --role $role `
            --assignee-object-id $ObjectId `
            --scope "/subscriptions/$SubscriptionId" | Out-Null
        Write-Host "   ✅ Assigned: $role" -ForegroundColor Green
    } else {
        Write-Host "   ⚠️  Already assigned: $role" -ForegroundColor Yellow
    }
}

# Configure federated credentials for GitHub Actions
Write-Host "`n[5/6] Configuring federated credentials for GitHub Actions..." -ForegroundColor Yellow

$branches = @("main", "develop")

foreach ($branch in $branches) {
    $credentialName = "github-$branch"
    $subject = "repo:${GitHubOwner}/${GitHubRepo}:ref:refs/heads/${branch}"
    
    Write-Host "   Creating credential: $credentialName" -ForegroundColor Cyan
    
    # Check if credential already exists
    $existing = az identity federated-credential show `
        --resource-group $ResourceGroupName `
        --identity-name $IdentityName `
        --name $credentialName 2>&1
    
    if ($LASTEXITCODE -ne 0) {
        az identity federated-credential create `
            --resource-group $ResourceGroupName `
            --identity-name $IdentityName `
            --name $credentialName `
            --issuer "https://token.actions.githubusercontent.com" `
            --subject $subject `
            --audiences "api://AzureADTokenExchange" | Out-Null
        Write-Host "   ✅ Created: $credentialName (branch: $branch)" -ForegroundColor Green
    } else {
        Write-Host "   ⚠️  Already exists: $credentialName" -ForegroundColor Yellow
    }
}

# Output configuration
Write-Host "`n[6/6] Configuration complete!" -ForegroundColor Yellow

Write-Host "`n" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "GitHub Repository Secrets" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Add these secrets to your GitHub repository:" -ForegroundColor Yellow
Write-Host ""
Write-Host "AZURE_SUBSCRIPTION_ID=$SubscriptionId" -ForegroundColor White
Write-Host "AZURE_TENANT_ID=$TenantId" -ForegroundColor White
Write-Host "AZURE_CLIENT_ID=$ClientId" -ForegroundColor White

Write-Host ""
Write-Host "ACR and AKS Configuration:" -ForegroundColor Yellow
Write-Host "ACR_NAME=emergencyalertsacr" -ForegroundColor White
Write-Host "AKS_CLUSTER_NAME=emergency-alerts-prod-aks" -ForegroundColor White
Write-Host "AKS_RESOURCE_GROUP=$ResourceGroupName" -ForegroundColor White

Write-Host ""
Write-Host "Optional (set after AKS deployment)::" -ForegroundColor Yellow
Write-Host "VITE_API_URL=<azure-ingress-domain>" -ForegroundColor White
Write-Host "   Get this from AKS ingress: kubectl get ingress -n emergency-alerts" -ForegroundColor Cyan

Write-Host "`n" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Deployment Information" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Managed Identity Name: $IdentityName" -ForegroundColor White
Write-Host "Resource Group: $ResourceGroupName" -ForegroundColor White
Write-Host "Federated Credentials: main, develop" -ForegroundColor White

Write-Host "`n" -ForegroundColor Cyan
Write-Host "✅ Setup complete! Your GitHub Actions can now authenticate to Azure." -ForegroundColor Green
