# GitHub Actions CI/CD Pipeline - Updated Implementation

## Architecture Overview

This updated implementation follows **infrastructure-as-code best practices** by moving all Azure resource creation to Bicep templates and limiting PowerShell to Kubernetes-only operations.

### Deployment Flow

```
┌─────────────────────────────────────────────────────────┐
│ 1. DEPLOY INFRASTRUCTURE (Bicep)                        │
│    - Create AKS cluster                                 │
│    - Enable OIDC issuer & workload identity on AKS     │
│    - Create managed identity                            │
│    - Create workload identity federation link          │
│    - Configure PostgreSQL firewall (specific IP)        │
│    - Attach ACR to AKS                                  │
│    - Create Key Vault, App Config, etc.               │
└─────────────────────────────────────────────────────────┘
                         ↓
┌─────────────────────────────────────────────────────────┐
│ 2. BUILD & PUSH IMAGES                                 │
│    - Build Docker image                                 │
│    - Push to ACR                                        │
└─────────────────────────────────────────────────────────┘
                         ↓
┌─────────────────────────────────────────────────────────┐
│ 3. DEPLOY TO KUBERNETES                                │
│    - Apply ServiceAccount + RBAC                        │
│    - Apply Deployment + Services                        │
│    - Apply Ingress + network policies                   │
└─────────────────────────────────────────────────────────┘
                         ↓
┌─────────────────────────────────────────────────────────┐
│ 4. POST-DEPLOYMENT: KUBERNETES CONFIGURATION            │
│    - Install cert-manager (via Helm)                    │
│    - Apply network policies                             │
│    - Verify health                                      │
└─────────────────────────────────────────────────────────┘
```

---

## GitHub Actions Workflow Configuration

### Step 1: Deploy Infrastructure with Bicep

```yaml
- name: Deploy Infrastructure with Bicep
  uses: azure/arm-deploy@v2
  with:
    scope: subscription
    subscriptionId: ${{ secrets.AZURE_SUBSCRIPTION_ID }}
    template: infrastructure/bicep/main.bicep
    parameters: |
      location=australiaeast
      environment=${{ matrix.environment }}
      projectName=emergency-alerts
      databaseAdminPassword=${{ secrets.DB_ADMIN_PASSWORD }}
      createSchema=true
      # NEW: Provide AKS outbound IP for production firewall rules
      aksOutboundIpAddress=${{ secrets.AKS_OUTBOUND_IP }}
      # NEW: Allow all Azure services only in development
      allowAllAzureServicesPostgres=${{ matrix.environment == 'dev' }}
      kubernetesNamespace=emergency-alerts
      kubernetesServiceAccountName=emergency-alerts-sa
    deploymentName: emergency-alerts-${{ matrix.environment }}-${{ github.run_id }}
    failOnStdErr: false
```

**Key Changes from Previous Approach:**
- ✅ All Azure resources created by Bicep (no post-deployment scripts for control plane)
- ✅ Workload identity federation created during infrastructure deployment
- ✅ PostgreSQL firewall rules configured with specific AKS IP (production) or Azure range (dev)
- ✅ ACR attachment handled by Bicep
- ✅ Managed identity configured with proper RBAC roles

### Step 2: Build and Push Images

```yaml
- name: Build and Push API Image to ACR
  run: |
    az acr build \
      --registry ${{ env.ACR_NAME }} \
      --image emergency-alerts-api:${{ github.sha }} \
      --image emergency-alerts-api:latest \
      --file src/EmergencyAlerts.Api/Dockerfile \
      backend/
    
- name: Build and Push Frontend Image to ACR
  run: |
    az acr build \
      --registry ${{ env.ACR_NAME }} \
      --image emergency-alerts-frontend:${{ github.sha }} \
      --image emergency-alerts-frontend:latest \
      --build-arg VITE_API_URL=${{ env.API_URL }} \
      frontend/Dockerfile \
      frontend/
```

### Step 3: Deploy Kubernetes Manifests

```yaml
- name: Get AKS Credentials
  run: |
    az aks get-credentials \
      --resource-group ${{ env.RESOURCE_GROUP }} \
      --name ${{ env.AKS_CLUSTER_NAME }} \
      --overwrite-existing

- name: Create Namespace and ServiceAccount
  run: |
    # Create namespace if not exists
    kubectl create namespace emergency-alerts --dry-run=client -o yaml | kubectl apply -f -
    
    # Apply ServiceAccount with workload identity annotation
    cat <<EOF | kubectl apply -f -
    apiVersion: v1
    kind: ServiceAccount
    metadata:
      name: emergency-alerts-sa
      namespace: emergency-alerts
      annotations:
        azure.workload.identity/client-id: ${{ env.MANAGED_IDENTITY_CLIENT_ID }}
    EOF

- name: Apply Deployment and Services
  run: |
    # Substitute image names and deploy
    export ACR_NAME=${{ env.ACR_NAME }}
    export IMAGE_TAG=${{ github.sha }}
    envsubst < infrastructure/k8s/deployment.yaml | kubectl apply -f -
    
    # Apply ingress
    kubectl apply -f infrastructure/k8s/ingress.yaml
    
    # Apply RBAC
    kubectl apply -f infrastructure/k8s/rbac.yaml
```

**Important:** Kubernetes manifests should include:

```yaml
# In deployment.yaml
spec:
  serviceAccountName: emergency-alerts-sa
  containers:
  - name: api
    image: ${ACR_NAME}.azurecr.io/emergency-alerts-api:${IMAGE_TAG}
    # Workload identity will automatically inject AZURE_FEDERATED_TOKEN_FILE
    # and other required environment variables
    env:
    - name: MANAGED_IDENTITY_CLIENT_ID
      value: ${{ env.MANAGED_IDENTITY_CLIENT_ID }}
```

### Step 4: Post-Deployment Kubernetes Configuration

```yaml
- name: Post-Deployment Setup (cert-manager + network policies)
  run: |
    pwsh infrastructure/scripts/setup-aks-post-deployment.ps1 `
      -AksClusterName ${{ env.AKS_CLUSTER_NAME }} `
      -Environment ${{ matrix.environment }} `
      -Namespace emergency-alerts `
      -SkipCertManager $false `
      -SkipNetworkPolicy $false
```

**What This Script Does (NEW: Kubernetes-Only):**
- ✅ Installs cert-manager v1.14.x via Helm
- ✅ Applies network policies with Azure CIDR ranges (40.0.0.0/8)
- ✅ Verifies all pods are running and healthy
- ✅ **NO Azure resource management** (all handled by Bicep)

**What This Script NO LONGER Does (Moved to Bicep):**
- ❌ Workload identity federation (now in Bicep module)
- ❌ PostgreSQL firewall rules (now in Bicep module)
- ❌ ACR attachment (now in Bicep module)
- ❌ Managed identity creation (now in Bicep module)

---

## Network Policy Security: 40.0.0.0/8 CIDR Range

### What is 40.0.0.0/8?

Microsoft's public CIDR block containing **all Azure services globally**:
- All Azure data centers and services
- All regional Azure infrastructure
- All Azure-hosted resources

### Egress Rules (Updated)

```yaml
egress:
  # Kubernetes internal communication (pod-to-pod)
  - to:
    - namespaceSelector: {}
    - podSelector: {}
    ports:
    - protocol: TCP
      port: 53           # DNS
    - protocol: UDP
      port: 53           # DNS
    - protocol: TCP
      port: 80           # HTTP
    - protocol: TCP
      port: 443          # HTTPS
  
  # Azure services ONLY (40.0.0.0/8)
  # This allows: PostgreSQL, App Config, Key Vault, ACR, etc.
  - to:
    - ipBlock:
        cidr: 40.0.0.0/8
    ports:
    - protocol: TCP
      port: 5432         # PostgreSQL
    - protocol: TCP
      port: 443          # HTTPS (App Config, Key Vault, ACR)
    - protocol: UDP
      port: 53           # DNS
```

### Why 40.0.0.0/8 vs 0.0.0.0/0?

| Aspect | 0.0.0.0/0 | 40.0.0.0/8 |
|--------|-----------|-----------|
| **Security** | Allows ANY internet host | Allows only Microsoft Azure services |
| **Risk** | Accidental data exfiltration to internet | Contained to Azure ecosystem |
| **Compliance** | Fails audits | Passes security review |
| **Azure services** | ✅ Works | ✅ Works |
| **Internet sites** | ✅ Works (risky) | ❌ Blocked (good) |
| **Private Endpoints** | ✅ Works | ✅ Works (recommended) |

### Production Hardening: Private Endpoints (Optional)

For maximum security, use **Azure Private Endpoints** to connect to Azure services over private links instead of public IPs:

```bash
# Example: PostgreSQL Private Endpoint
az network private-endpoint create \
  --resource-group emergency-alerts-prod-rg \
  --name emergency-alerts-pgfs-endpoint \
  --vnet-name emergency-alerts-vnet \
  --subnet aks-subnet \
  --private-connection-resource-id /subscriptions/.../providers/Microsoft.DBforPostgreSQL/serverGroupsv2/emergency-alerts-prod-cluster \
  --group-ids coordinator \
  --connection-name emergency-alerts-pgfs-connection
```

With Private Endpoints, network policy can restrict to `10.0.0.0/8` (Azure VNet range) for even greater security.

---

## Bicep Parameters for Different Environments

### Development (allow all Azure services)

```bash
az deployment sub create \
  --template-file infrastructure/bicep/main.bicep \
  --parameters \
    location=australiaeast \
    environment=dev \
    projectName=emergency-alerts \
    databaseAdminPassword=$DB_PASS \
    # Allow all Azure services (no specific IP)
    aksOutboundIpAddress="" \
    allowAllAzureServicesPostgres=true
```

### Production (restrict to AKS outbound IP)

```bash
az deployment sub create \
  --template-file infrastructure/bicep/main.bicep \
  --parameters \
    location=australiaeast \
    environment=prod \
    projectName=emergency-alerts \
    databaseAdminPassword=$DB_PASS \
    # Specific AKS outbound IP (more secure)
    aksOutboundIpAddress="51.132.213.103" \
    allowAllAzureServicesPostgres=false
```

---

## Complete GitHub Actions Workflow Example

```yaml
name: Deploy Emergency Alerts

on:
  push:
    branches: [main]
  workflow_dispatch:
    inputs:
      environment:
        description: 'Environment (dev/prod)'
        required: true
        default: 'dev'

jobs:
  deploy:
    runs-on: ubuntu-latest
    strategy:
      matrix:
        environment: [dev, prod]
    environment: ${{ matrix.environment }}
    
    steps:
      - uses: actions/checkout@v4
      
      - name: Azure Login
        uses: azure/login@v1
        with:
          creds: ${{ secrets.AZURE_CREDENTIALS }}
      
      # 1. Deploy Infrastructure (Bicep)
      - name: Deploy Infrastructure with Bicep
        uses: azure/arm-deploy@v2
        with:
          scope: subscription
          subscriptionId: ${{ secrets.AZURE_SUBSCRIPTION_ID }}
          template: infrastructure/bicep/main.bicep
          parameters: |
            location=australiaeast
            environment=${{ matrix.environment }}
            projectName=emergency-alerts
            databaseAdminPassword=${{ secrets.DB_ADMIN_PASSWORD }}
            createSchema=true
            aksOutboundIpAddress=${{ matrix.environment == 'prod' && secrets.AKS_OUTBOUND_IP || '' }}
            allowAllAzureServicesPostgres=${{ matrix.environment == 'dev' }}
            kubernetesNamespace=emergency-alerts
            kubernetesServiceAccountName=emergency-alerts-sa
          deploymentName: emergency-alerts-${{ matrix.environment }}-${{ github.run_id }}
      
      # 2. Build & Push Images
      - name: Build and Push Images to ACR
        run: |
          ACR_NAME=$(az deployment sub show \
            --name emergency-alerts-${{ matrix.environment }}-${{ github.run_id }} \
            --query properties.outputs.acrName.value -o tsv)
          
          az acr build \
            --registry $ACR_NAME \
            --image emergency-alerts-api:${{ github.sha }} \
            --file backend/src/EmergencyAlerts.Api/Dockerfile \
            backend/
      
      # 3. Deploy to Kubernetes
      - name: Get AKS Credentials
        run: |
          RESOURCE_GROUP=$(az deployment sub show \
            --name emergency-alerts-${{ matrix.environment }}-${{ github.run_id }} \
            --query properties.outputs.resourceGroupName.value -o tsv)
          AKS_NAME=$(az deployment sub show \
            --name emergency-alerts-${{ matrix.environment }}-${{ github.run_id }} \
            --query properties.outputs.aksClusterName.value -o tsv | sed 's/-//')
          
          az aks get-credentials \
            --resource-group $RESOURCE_GROUP \
            --name emergency-alerts-${{ matrix.environment }}-aks \
            --overwrite-existing
      
      - name: Deploy Kubernetes Manifests
        run: |
          kubectl create namespace emergency-alerts --dry-run=client -o yaml | kubectl apply -f -
          kubectl apply -f infrastructure/k8s/deployment.yaml
          kubectl apply -f infrastructure/k8s/ingress.yaml
          kubectl apply -f infrastructure/k8s/rbac.yaml
      
      # 4. Post-Deployment (cert-manager + network policies)
      - name: Post-Deployment Setup
        run: |
          pwsh infrastructure/scripts/setup-aks-post-deployment.ps1 `
            -AksClusterName emergency-alerts-${{ matrix.environment }}-aks `
            -Environment ${{ matrix.environment }}
      
      - name: Verify Deployment
        run: |
          kubectl get pods -n emergency-alerts
          kubectl get svc -n emergency-alerts
          kubectl get networkpolicy -n emergency-alerts
```

---

## Migration Guide: Old → New Approach

### What Changed?

| Aspect | Old Approach | New Approach |
|--------|------------|------------|
| **Workload Identity** | Post-deploy script | Bicep module (infrastructure phase) |
| **PostgreSQL Firewall** | Post-deploy script | Bicep module (infrastructure phase) |
| **ACR Attachment** | Post-deploy script | Bicep (infrastructure phase) |
| **Managed Identity** | Bicep | Bicep (enhanced with federation) |
| **cert-manager** | Post-deploy script | PowerShell K8s-only script |
| **Network Policies** | Post-deploy script | PowerShell K8s-only script + improved CIDR |
| **Network Policy CIDR** | 0.0.0.0/0 | 40.0.0.0/8 (Azure services only) |

### Why This Matters

1. **Infrastructure Idempotency**: Bicep deployments can be re-run safely (all Azure resources are declarative)
2. **Faster Deployments**: No waiting for post-deployment scripts to create resources
3. **Better Security**: Network policies restrict to Azure CIDR instead of open internet
4. **Auditability**: All infrastructure changes show in `az deployment` history
5. **Separation of Concerns**: Azure resources ≠ Kubernetes configuration
6. **Easier Rollback**: Just re-deploy previous Bicep version (no manual cleanup)

---

## Verification Checklist

After deployment, verify each phase:

### ✅ Phase 1: Infrastructure Deployment
```bash
# Check resource group
az group show --name emergency-alerts-prod-rg

# Verify AKS
az aks show --resource-group emergency-alerts-prod-rg --name emergency-alerts-prod-aks

# Verify workload identity federation
az identity federated-credential list \
  --identity-name emergency-alerts-prod-identity \
  --resource-group emergency-alerts-prod-rg

# Verify PostgreSQL firewall rules
az postgres flexible-server firewall-rule list \
  --resource-group emergency-alerts-prod-rg \
  --name emergency-alerts-prod-cluster
```

### ✅ Phase 2: Images Built
```bash
# List ACR images
az acr repository list --name emergencyalertsprodacr
az acr repository show-tags --name emergencyalertsprodacr --repository emergency-alerts-api
```

### ✅ Phase 3: Kubernetes Deployed
```bash
# Check pods
kubectl get pods -n emergency-alerts

# Check services
kubectl get svc -n emergency-alerts

# Check ingress
kubectl get ingress -n emergency-alerts
```

### ✅ Phase 4: Post-Deployment Complete
```bash
# Verify cert-manager
kubectl get pods -n cert-manager
kubectl get certificate -n emergency-alerts

# Verify network policies
kubectl get networkpolicy -n emergency-alerts

# Verify health endpoints
kubectl exec -it <pod-name> -n emergency-alerts -- \
  curl http://localhost:5000/health/ready
```

---

## Troubleshooting Guide

### Pods Not Starting?

**Check 1: Workload Identity Federation**
```bash
# Verify federated credential exists
az identity federated-credential list \
  --identity-name emergency-alerts-prod-identity \
  --resource-group emergency-alerts-prod-rg

# Check ServiceAccount annotation
kubectl get sa emergency-alerts-sa -n emergency-alerts -o yaml | grep azure.workload
```

**Check 2: PostgreSQL Firewall**
```bash
# Verify firewall rule includes AKS IP
az postgres flexible-server firewall-rule list \
  --resource-group emergency-alerts-prod-rg \
  --name emergency-alerts-prod-cluster \
  --output table
```

**Check 3: Network Policy**
```bash
# Test connectivity from pod
kubectl exec -it <pod> -n emergency-alerts -- \
  nslookup emergency-alerts-prod-pgfs.postgres.database.azure.com
```

### Firewall Failure?

If PostgreSQL returns "IP not in firewall rules":
```bash
# Get actual AKS outbound IP
az deployment sub show \
  --name emergency-alerts-prod-$RUN_ID \
  --query outputs.aksOutboundIp.value -o tsv

# Verify it matches Bicep parameter
cat infrastructure/bicep/main.bicep | grep aksOutboundIpAddress
```

---

## Security Best Practices Implemented

✅ **Workload Identity (Zero-Trust)**: No credentials stored in cluster
✅ **Restricted Network Policy**: Azure CIDR only (not 0.0.0.0/0)
✅ **Managed Identity RBAC**: Least privilege for each service
✅ **Secrets in Key Vault**: Not in code or ConfigMaps
✅ **Automated Infrastructure**: Bicep ensures consistency and auditability
✅ **TLS Certificates**: cert-manager + Let's Encrypt

---

**Documentation Files:**
- [AUTOMATION_IMPLEMENTATION_GUIDE.md](../AUTOMATION_IMPLEMENTATION_GUIDE.md) - Step-by-step setup
- [AKS_POST_DEPLOYMENT_SETUP.md](AKS_POST_DEPLOYMENT_SETUP.md) - Technical deep-dive
- [infrastructure/README.md](README.md) - Infrastructure overview
