# AKS Post-Deployment Setup Integration

This document explains how to integrate all the manual fixes into your Bicep/CI/CD deployment pipeline to ensure they run automatically.

## Overview

The `setup-aks-post-deployment.ps1` script automates the following post-deployment tasks:
1. Installs cert-manager for TLS certificate management
2. Attaches ACR to AKS cluster for image pulling  
3. Creates workload identity federation between AKS and Azure Managed Identity
4. Configures PostgreSQL firewall rules for AKS outbound IP
5. Updates Kubernetes ConfigMap with actual Azure resource values
6. Applies network policy allowing external Azure service access

## Option 1: CI/CD Pipeline Integration (Recommended)

### Add to GitHub Actions Workflow

In `.github/workflows/ci-cd.yml`, add this step after the "Create Kubernetes namespace and resources" step:

```yaml
      - name: Run AKS post-deployment configuration
        if: github.event_name == 'push' && github.ref == 'refs/heads/main'
        env:
          ACR_NAME: ${{ needs.deploy-infrastructure.outputs.acr_name }}
          RESOURCE_GROUP: ${{ needs.deploy-infrastructure.outputs.rg_name }}
          ENVIRONMENT: prod
        run: |
          # Get cluster details
          AKS_NAME=$(az aks list --resource-group $RESOURCE_GROUP --query "[0].name" -o tsv)
          IDENTITY_NAME="emergency-alerts-prod-identity"
          
          # Run post-deployment setup
          pwsh infrastructure/scripts/setup-aks-post-deployment.ps1 `
            -ResourceGroupName "$RESOURCE_GROUP" `
            -AksClusterName "$AKS_NAME" `
            -ManagedIdentityName "$IDENTITY_NAME" `
            -AcrName "$ACR_NAME" `
            -Environment "$ENVIRONMENT"
```

### Complete CI/CD Step Example

```yaml
  post-deploy-aks:
    name: Post-Deployment AKS Configuration
    runs-on: ubuntu-latest
    needs: [deploy, deploy-infrastructure]
    if: github.event_name == 'push' && github.ref == 'refs/heads/main'
    
    steps:
      - name: Checkout code
        uses: actions/checkout@v6

      - name: Azure Login (OIDC)
        uses: azure/login@v2
        with:
          client-id: ${{ env.AZURE_CLIENT_ID }}
          tenant-id: ${{ env.AZURE_TENANT_ID }}
          subscription-id: ${{ env.AZURE_SUBSCRIPTION_ID }}

      - name: Get AKS credentials
        run: |
          az aks get-credentials \
            --resource-group ${{ needs.deploy-infrastructure.outputs.rg_name }} \
            --name "emergency-alerts-prod-aks" \
            --overwrite-existing

      - name: Run AKS post-deployment configuration
        run: |
          pwsh infrastructure/scripts/setup-aks-post-deployment.ps1 `
            -ResourceGroupName "${{ needs.deploy-infrastructure.outputs.rg_name }}" `
            -AksClusterName "emergency-alerts-prod-aks" `
            -ManagedIdentityName "emergency-alerts-prod-identity" `
            -AcrName "${{ needs.deploy-infrastructure.outputs.acr_name }}" `
            -Environment "prod"

      - name: Verify post-deployment setup
        run: |
          echo "Checking pod status..."
          kubectl get pods -n emergency-alerts
          
          echo ""
          echo "Rolling restart deployments to apply configuration..."
          kubectl rollout restart deployment emergency-alerts-api -n emergency-alerts
          
          echo ""
          echo "Waiting for rollout to complete..."
          kubectl rollout status deployment emergency-alerts-api -n emergency-alerts --timeout=5m
          
          echo ""
          echo "Checking logs..."
          kubectl logs -l app=emergency-alerts-api -n emergency-alerts --tail=20
```

## Option 2: Manual Execution

If you prefer to run the post-deployment setup manually (useful for testing or one-time fixes):

```powershell
# Connect to Azure
az login

# Run the setup script
pwsh infrastructure/scripts/setup-aks-post-deployment.ps1 `
  -ResourceGroupName "emergency-alerts-prod-rg" `
  -AksClusterName "emergency-alerts-prod-aks" `
  -ManagedIdentityName "emergency-alerts-prod-identity" `
  -AcrName "emergencyalertsprodacr" `
  -Environment "prod"

# Restart deployments
kubectl rollout restart deployment emergency-alerts-api -n emergency-alerts
kubectl rollout restart deployment emergency-alerts-frontend -n emergency-alerts

# Monitor rollout
kubectl rollout status deployment emergency-alerts-api -n emergency-alerts --timeout=5m
kubectl rollout status deployment emergency-alerts-frontend -n emergency-alerts --timeout=5m

# Verify health
kubectl get pods -n emergency-alerts
kubectl logs -l app=emergency-alerts-api -n emergency-alerts --tail=50
```

## What Gets Fixed Automatically

### 1. cert-manager Installation
- **Issue**: Missing CRDs for TLS certificate management
- **Fix**: Installs cert-manager v1.14.2 and waits for readiness
- **Status**: ClusterIssuer and Certificate resources can now be deployed

### 2. ACR-AKS Integration
- **Issue**: Pods cannot pull images (authentication failure)
- **Fix**: Runs `az aks update --attach-acr` to grant AKS pull permissions
- **Status**: Pods can pull from container registry

### 3. Workload Identity Federation
- **Issue**: No OIDC token for managed identity authentication
- **Fix**: Creates federated credential linking ServiceAccount to Managed Identity
- **Status**: Pods can authenticate to Azure services using workload identity

### 4. PostgreSQL Firewall
- **Issue**: AKS pods cannot reach PostgreSQL (connection timeout)
- **Fix**: Adds AKS outbound IP to PostgreSQL firewall rules
- **Status**: Database connectivity works

### 5. Kubernetes ConfigMap
- **Issue**: Environment variables contain Azure resource placeholders
- **Fix**: Patches ConfigMap with actual endpoint URLs and secrets
- **Status**: Application can connect to App Config, Key Vault, Database

### 6. Authorization Middleware
- **Issue**: Health endpoints return 401 (authorization blocking)
- **Fix**: Updated `/health/*` to bypass authorization middleware
- **Status**: Kubernetes liveness/readiness probes work

## Bicep Module Enhancements

Consider adding these resources to your Bicep modules:

### Enable Workload Identity in AKS Module
```bicep
// In modules/aks.bicep, add:
param workloadIdentityEnabled bool = true

// In the resource definition:
oidcIssuerProfile: {
  enabled: true
}

securityProfile: {
  workloadIdentity: {
    enabled: workloadIdentityEnabled
  }
}
```

### PostgreSQL Firewall Rule in Database Module
```bicep
// In modules/elastic-cluster.bicep or new postgres firewall module:
resource postgresFirewall 'Microsoft.DBforPostgreSQL/flexibleServers/firewallRules@2023-12-01-preview' = {
  name: '${clusterName}/AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '255.255.255.255'
  }
}
```

## Troubleshooting

### Script Fails: "ACR name not found"
**Solution**: Ensure ACR was deployed by Bicep. Check `deploy-infrastructure` job outputs.

### Script Fails: "Workload identity federation already exists"
**Solution**: This is a warning - you can safely re-run the script. If there's a mismatch, delete and recreate:
```powershell
az identity federated-credential delete `
  --name "aks-federation" `
  --identity-name "emergency-alerts-prod-identity" `
  --resource-group "emergency-alerts-prod-rg"
```

### Pods Still Crashing After Script
**Issue**: Script completed but pods aren't running
**Debug**:
```bash
# Check if rollout was triggered
kubectl rollout restart deployment emergency-alerts-api -n emergency-alerts

# Monitor logs
kubectl logs -f -l app=emergency-alerts-api -n emergency-alerts

# Check pod events
kubectl describe pod <pod-name> -n emergency-alerts
```

### Health Endpoints Still Returning 401
**Issue**: Authorization code wasn't redeployed
**Solution**: Rebuild and push new image with fixed authorization middleware:
```powershell
cd backend
az acr build --registry emergencyalertsprodacr `
  --image emergency-alerts-api:latest `
  --file src/EmergencyAlerts.Api/Dockerfile `
  .

# Then restart the deployment
kubectl rollout restart deployment emergency-alerts-api -n emergency-alerts
```

## Security Considerations

### Network Policy
The included `network-policy-fixed.yaml` allows:
- ✅ DNS queries (port 53)
- ✅ HTTPS to external services (port 443)
- ✅ HTTP for internal services (port 80)
- ✅ PostgreSQL connections (port 5432)
- ❌ Blocks AWS metadata endpoint (169.254.169.254) as safety measure

**Production Recommendation**: Replace with more restrictive rules once verified working:
```yaml
egress:
  # Only allow specific Azure service IPs
  - to:
    - ipBlock:
        cidr: 40.0.0.0/8  # Azure region CIDR
    ports:
    - protocol: TCP
      port: 443
```

### Managed Identity Permissions
Verify the managed identity has required roles:
```powershell
# Check App Configuration access
$identity = az identity show `
  -g emergency-alerts-prod-rg `
  -n emergency-alerts-prod-identity `
  --query principalId -o tsv

az role assignment list --assignee $identity --output table
```

## Monitoring & Validation

After running the post-deployment setup:

```bash
# Verify cert-manager
kubectl get pods -n cert-manager
kubectl get clusterissuer

# Verify workload identity
kubectl get sa emergency-alerts-sa -n emergency-alerts -o yaml | grep azure.workload

# Verify pod can reach Azure services
kubectl exec <pod-name> -n emergency-alerts -- \
  curl -I https://emergency-alerts-prod-config.azconfig.io

# Check pod health
kubectl exec <pod-name> -n emergency-alerts -- \
  curl http://localhost:5000/health/ready

# Monitor logs for managed identity auth
kubectl logs <pod-name> -n emergency-alerts | grep -i "token\|auth\|azure"
```

## Future Improvements

1. **Automate in Bicep**: Move more logic into Bicep modules instead of post-deployment script
2. **Helm Charts**: Package Kubernetes manifests as Helm charts for better version control
3. **GitOps**: Use ArgoCD or Flux for declarative deployment management
4. **Service Principal Rotation**: Implement automatic credential rotation for managed identities
5. **Observability**: Define Prometheus/Grafana rules for monitoring post-deployment health

## References

- [AKS Workload Identity](https://learn.microsoft.com/en-us/azure/aks/workload-identity-overview?tabs=powershell)
- [cert-manager Installation](https://cert-manager.io/docs/installation/)
- [ACR Pull via AKS](https://learn.microsoft.com/en-us/azure/container-registry/container-registry-auth-kubernetes)
- [Kubernetes Network Policies](https://kubernetes.io/docs/concepts/services-networking/network-policies/)
