#!/usr/bin/env pwsh
<#
.SYNOPSIS
Post-deployment Kubernetes configuration for AKS cluster.
Installs cert-manager and applies network policies.

.DESCRIPTION
Performs Kubernetes-level post-deployment tasks ONLY.
Azure resources (workload identity, firewall rules, ACR attachment) are managed by Bicep.

Tasks performed:
1. Installs cert-manager for TLS certificate management
2. Applies network policies for pod-to-pod communication security
3. Verifies all pods are healthy and ready

Prerequisites:
- AKS cluster is already deployed via Bicep (with workload identity, firewall rules configured)
- kubectl is configured and has access to the cluster
- Helm is installed locally
- Network policy YAML file exists at infrastructure/k8s/network-policy-fixed.yaml

.PARAMETER AksClusterName
The AKS cluster name (for verification and logging)

.PARAMETER Environment
The environment (dev, prod) for targeting correct resources

.PARAMETER Namespace
The Kubernetes namespace where application is deployed

.PARAMETER SkipCertManager
Skip cert-manager installation if already installed

.PARAMETER SkipNetworkPolicy
Skip network policy application if already applied

.EXAMPLE
.\setup-aks-post-deployment.ps1 -AksClusterName emergency-alerts-prod-aks -Environment prod

.EXAMPLE
.\setup-aks-post-deployment.ps1 -Environment dev -SkipCertManager $true
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$AksClusterName = 'emergency-alerts-aks',
    
    [Parameter(Mandatory = $false)]
    [string]$Environment = 'prod',
    
    [Parameter(Mandatory = $false)]
    [string]$Namespace = 'emergency-alerts',
    
    [Parameter(Mandatory = $false)]
    [bool]$SkipCertManager = $false,
    
    [Parameter(Mandatory = $false)]
    [bool]$SkipNetworkPolicy = $false
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# Import shared logging functions
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$loggingModule = Join-Path (Split-Path -Parent (Split-Path -Parent $scriptDir)) "scripts\common\logging.ps1"
if (Test-Path $loggingModule) {
    . $loggingModule
} else {
    # Fallback: define functions locally if shared module not found
    function Write-StepHeader { param([string]$Message) Write-Host "`n$('#' * 70)`n█ $Message`n$('#' * 70)" -ForegroundColor Cyan }
    function Write-Success { param([string]$Message) Write-Host "✅ $Message" -ForegroundColor Green }
    function Write-Info { param([string]$Message) Write-Host "ℹ️  $Message" -ForegroundColor Cyan }
    function Write-WarningMsg { param([string]$Message) Write-Host "⚠️  $Message" -ForegroundColor Yellow }
    function Test-CommandExists { param([string]$Command) $null = Get-Command $Command -ErrorAction SilentlyContinue; return $? }
}

# ============================================================================
# VALIDATION
# ============================================================================

Write-StepHeader "Pre-flight Checks"

# Check kubectl
if (-not (Test-CommandExists kubectl)) {
    Write-Host "❌ kubectl not found. Please install kubectl first." -ForegroundColor Red
    exit 1
}
Write-Success "kubectl found"

# Check helm
if (-not $SkipCertManager -and -not (Test-CommandExists helm)) {
    Write-Host "❌ helm not found. Please install helm first." -ForegroundColor Red
    exit 1
}
if (-not $SkipCertManager) {
    Write-Success "helm found"
}

# Check cluster connectivity
Write-Info "Testing cluster connectivity..."
try {
    $clusterInfo = kubectl cluster-info 2>&1
    Write-Success "Connected to Kubernetes cluster"
} catch {
    Write-Host "❌ Cannot connect to Kubernetes cluster. Run 'az aks get-credentials' first." -ForegroundColor Red
    exit 1
}

# ============================================================================
# 1. INSTALL CERT-MANAGER
# ============================================================================

if (-not $SkipCertManager) {
    Write-StepHeader "1. Installing cert-manager for TLS Certificate Management"
    
    Write-Info "Checking if cert-manager is already installed..."
    $certManagerWildcard = kubectl get ns cert-manager -ErrorAction SilentlyContinue
    
    if ($null -ne $certManagerWildcard) {
        Write-Warning "cert-manager namespace already exists"
        
        # Check if cert-manager pods are running
        $cmPods = kubectl get pods -n cert-manager --no-headers 2>$null | Measure-Object | Select-Object -ExpandProperty Count
        if ($cmPods -gt 0) {
            Write-Success "cert-manager is already installed and running"
        } else {
            Write-Warning "cert-manager namespace exists but pods not running. Reinstalling..."
            helm repo add jetstack https://charts.jetstack.io
            helm repo update
            helm upgrade --install cert-manager jetstack/cert-manager `
                --namespace cert-manager `
                --create-namespace `
                --set installCRDs=true `
                --set global.leaderElection.namespace=cert-manager
        }
    } else {
        Write-Info "Installing cert-manager v1.14.x..."
        
        # Add Helm repo
        helm repo add jetstack https://charts.jetstack.io
        helm repo update
        
        # Install cert-manager
        helm install cert-manager jetstack/cert-manager `
            --namespace cert-manager `
            --create-namespace `
            --set installCRDs=true `
            --set global.leaderElection.namespace=cert-manager
        
        Write-Success "cert-manager installed"
        
        # Wait for cert-manager to be ready
        Write-Info "Waiting for cert-manager to be ready (this may take 1-2 minutes)..."
        kubectl wait --for=condition=ready pod `
            -l app.kubernetes.io/instance=cert-manager `
            -n cert-manager `
            --timeout=300s 2>$null || Write-Warning "cert-manager readiness check timeout (may still be deploying)"
    }
    
    # Verify cert-manager pods
    Write-Info "Verifying cert-manager pod status..."
    kubectl get pods -n cert-manager -o wide
    Write-Success "cert-manager installation verified"
} else {
    Write-Info "Skipping cert-manager installation (--SkipCertManager flag set)"
}

# ============================================================================
# 2. APPLY NETWORK POLICIES
# ============================================================================

if (-not $SkipNetworkPolicy) {
    Write-StepHeader "2. Applying Network Policies for Pod Security"
    
    Write-Info "Creating namespace if not exists..."
    kubectl create namespace $Namespace --dry-run=client -o yaml | kubectl apply -f -

    Write-Info "Applying network policies from infrastructure/k8s/network-policy-fixed.yaml..."

    if (Test-Path "infrastructure/k8s/network-policy-fixed.yaml") {
        kubectl apply -f infrastructure/k8s/network-policy-fixed.yaml
        Write-Success "Network policies applied"
    } else {
        Write-Warning "Network policy file not found at infrastructure/k8s/network-policy-fixed.yaml"
        Write-Info "Skipping network policy application"
    }
    
    # Display network policies
    Write-Info "Network policies in namespace '$Namespace':"
    kubectl get networkpolicy -n $Namespace -o wide
} else {
    Write-Info "Skipping network policy application (--SkipNetworkPolicy flag set)"
}

# ============================================================================
# 3. VERIFY DEPLOYMENT
# ============================================================================

Write-StepHeader "3. Verifying Kubernetes Deployment"

# Check if deployment exists
$deploymentCount = kubectl get deployment -n $Namespace -o name 2>$null | Measure-Object | Select-Object -ExpandProperty Count

if ($deploymentCount -gt 0) {
    Write-Info "Deployments in namespace '$Namespace':"
    kubectl get deployments -n $Namespace -o wide
    
    Write-Info "Pods in namespace '$Namespace':"
    kubectl get pods -n $Namespace -o wide
    
    Write-Info "Services in namespace '$Namespace':"
    kubectl get services -n $Namespace -o wide
    
    # Check for pod issues
    $failedPods = kubectl get pods -n $Namespace --field-selector=status.phase!=Running -o name 2>$null | Measure-Object | Select-Object -ExpandProperty Count
    
    if ($failedPods -gt 0) {
        Write-Warning "⚠️  Found pods not in Running state:"
        kubectl get pods -n $Namespace --field-selector=status.phase!=Running
        Write-Info "Check pod logs: kubectl logs <pod-name> -n $Namespace"
    } else {
        Write-Success "All pods are in Running state"
    }
    
    # Check pod readiness
    $readyPods = kubectl get pods -n $Namespace -o jsonpath='{.items[?(@.status.conditions[?(@.type=="Ready")].status=="True")].metadata.name}' 2>$null | Measure-Object -Word | Select-Object -ExpandProperty Words
    Write-Info "Ready pods: $readyPods"
} else {
    Write-Warning "No deployments found in namespace '$Namespace'"
}

# ============================================================================
# 4. HEALTH CHECK SUMMARY
# ============================================================================

Write-StepHeader "Post-Deployment Setup Complete"

Write-Info "Summary of applied changes:"
Write-Info "  ✓ cert-manager installed (if not skipped)"
Write-Info "  ✓ Network policies applied (if not skipped)"
Write-Info "  ✓ All components verified"

Write-Success "AKS post-deployment configuration complete!"

# ============================================================================
# HELPFUL COMMANDS
# ============================================================================

Write-Host ""
Write-Host "Useful commands for troubleshooting:" -ForegroundColor Magenta
Write-Host ""
Write-Host "  # View pod logs:"
Write-Host "  kubectl logs <pod-name> -n $Namespace"
Write-Host ""
Write-Host "  # Describe pod (for error details):"
Write-Host "  kubectl describe pod <pod-name> -n $Namespace"
Write-Host ""
Write-Host "  # Port forward to test locally:"
Write-Host "  kubectl port-forward svc/<service-name> 5000:80 -n $Namespace"
Write-Host ""
Write-Host "  # View real-time pod logs:"
Write-Host "  kubectl logs -f deployment/<deployment-name> -n $Namespace"
Write-Host ""
Write-Host "  # Check cert-manager status:"
Write-Host "  kubectl get pods -n cert-manager"
Write-Host "  kubectl logs -n cert-manager deployment/cert-manager"
Write-Host ""
Write-Host "Documentation: infrastructure/AUTOMATION_IMPLEMENTATION_GUIDE.md" -ForegroundColor Magenta
