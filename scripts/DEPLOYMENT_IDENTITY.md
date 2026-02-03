# GitHub Actions Deployment Identity Setup

This project uses **Azure Workload Identity Federation** with a **user-assigned managed identity** for secure, keyless authentication from GitHub Actions to Azure.

## Key Benefits

✅ **No secrets needed** - Uses OIDC token exchange  
✅ **Automatic renewal** - Tokens managed by Azure  
✅ **Auditability** - Full tracking of CI/CD operations  
✅ **Least privilege** - Identity only has required permissions  

## Architecture

```
GitHub Actions Workflow
         ↓
GitHub OIDC Token (JWT)
         ↓
Azure AD Token Endpoint
         ↓
Federated Credential Validation
         ↓
User-Assigned Managed Identity
         ↓
Azure Resources (AKS, ACR, etc.)
```

## Setup Instructions

### 1. Run Setup Script

```powershell
cd d:\GitHub\probable-octo-rotary-phone

# Run the setup script
.\scripts\setup-deployment-identity.ps1 `
    -SubscriptionId "your-subscription-id" `
    -ResourceGroupName "emergency-alerts-prod-rg" `
    -GitHubOwner "lukemurraynz" `
    -GitHubRepo "probable-octo-rotary-phone"
```

**Parameters:**
- `SubscriptionId` - Your Azure subscription ID (required)
- `ResourceGroupName` - Where to create the managed identity (required)
- `IdentityName` - Name of the managed identity (default: `emergency-alerts-github-actions`)
- `GitHubOwner` - Your GitHub username or organization (required)
- `GitHubRepo` - Your repository name (default: `probable-octo-rotary-phone`)

### 2. Add GitHub Repository Secrets

The script outputs values to add to your GitHub repository:

**Go to:** Repository Settings → Secrets and variables → Actions

Add these **Repository Secrets**:

| Secret Name | Value |
|---|---|
| `AZURE_SUBSCRIPTION_ID` | Output from script |
| `AZURE_TENANT_ID` | Output from script |
| `AZURE_CLIENT_ID` | Output from script |
| `ACR_NAME` | `emergencyalertsacr` |
| `AKS_CLUSTER_NAME` | `emergency-alerts-prod-aks` |
| `AKS_RESOURCE_GROUP` | Your resource group name |
| `VITE_API_URL` | Azure ingress domain (e.g., `https://emergency-alerts.eastus.cloudapp.azure.com`) - get after AKS deployment |

### 3. Verify Setup

```powershell
# Show the managed identity
az identity show `
    --resource-group "emergency-alerts-prod-rg" `
    --name "emergency-alerts-github-actions"

# List federated credentials
az identity federated-credential list `
    --resource-group "emergency-alerts-prod-rg" `
    --identity-name "emergency-alerts-github-actions"

# Show role assignments
az role assignment list `
    --assignee-object-id $(az identity show `
        --resource-group "emergency-alerts-prod-rg" `
        --name "emergency-alerts-github-actions" `
        --query principalId -o tsv) `
    --output table
```

## How GitHub Actions Uses This

In the CI/CD workflow (`.github/workflows/ci-cd.yml`):

```yaml
- name: Azure Login (OIDC)
  uses: azure/login@v2
  with:
    client-id: ${{ secrets.AZURE_CLIENT_ID }}
    tenant-id: ${{ secrets.AZURE_TENANT_ID }}
    subscription-id: ${{ secrets.AZURE_SUBSCRIPTION_ID }}
```

**This process:**
1. GitHub generates OIDC token (claims: repo, branch, commit SHA)
2. Token sent to Azure AD token endpoint
3. Azure validates token signature using GitHub's OIDC key
4. Azure matches token subject to federated credential
5. Azure issues access token for managed identity
6. Workflow authenticates to Azure without any secrets

## Federated Credentials

Two credentials are created automatically:

| Name | Subject | Branch | Purpose |
|---|---|---|---|
| `github-main` | `repo:owner/repo:ref:refs/heads/main` | main | Production deployments |
| `github-develop` | `repo:owner/repo:ref:refs/heads/develop` | develop | Development/staging deployments |

Each credential is **branch-specific** - tokens are only accepted for PRs/pushes on that branch.

## Managed Identity Roles

The identity receives these roles at **subscription scope**:

```
Contributor - Manage all Azure resources
User Access Administrator - Manage role assignments
AcrPush - Push/pull images to Azure Container Registry
```

### Scoping Down (Optional)

For more security, you can replace `Contributor` with specific role assignments:

```powershell
$resourceGroupName = "emergency-alerts-prod-rg"
$identityObjectId = $(az identity show -g $resourceGroupName -n "emergency-alerts-github-actions" --query principalId -o tsv)

# Assign roles at resource group level instead
az role assignment create `
    --role "Kubernetes Service Admin" `
    --assignee-object-id $identityObjectId `
    --resource-group $resourceGroupName

az role assignment create `
    --role "AcrPush" `
    --assignee-object-id $identityObjectId `
    --resource-group $resourceGroupName
```

## Troubleshooting

### "Invalid token" error in workflow

**Cause:** Federated credential doesn't match token subject  
**Solution:** Verify branch name matches federated credential subject

```powershell
# Check what subjects are registered
az identity federated-credential list `
    --resource-group "emergency-alerts-prod-rg" `
    --identity-name "emergency-alerts-github-actions" `
    --query "[].subject"
```

### "Insufficient permissions" error

**Cause:** Managed identity doesn't have required role  
**Solution:** Check role assignments

```powershell
az role assignment list `
    --assignee-object-id $(az identity show `
        --resource-group "emergency-alerts-prod-rg" `
        --name "emergency-alerts-github-actions" `
        --query principalId -o tsv)
```

### AKS deployment fails

**Cause:** Identity not assigned to AKS cluster identity  
**Solution:** Update AKS cluster (via Bicep) to use managed identity

```bicep
param managedIdentityId string = '/subscriptions/.../.../emergency-alerts-github-actions'

resource aksCluster 'Microsoft.ContainerService/managedClusters@2024-01-01' = {
  ...
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${managedIdentityId}': {}
    }
  }
  ...
}
```

## Reference

- [Azure Workload Identity Federation](https://docs.microsoft.com/en-us/azure/active-directory/workload-identities/workload-identity-federation)
- [GitHub Actions OIDC Provider](https://docs.github.com/en/actions/deployment/security-hardening-your-deployments/about-security-hardening-with-openid-connect)
- [Azure AD Federated Credentials](https://learn.microsoft.com/en-us/entra/workload-id/workload-identity-federation-create-trust?tabs=windows)

## Getting Azure Ingress Domain (for VITE_API_URL)

After your AKS infrastructure is deployed and the backend API is running:

```powershell
# Get the public IP of the ingress
az network public-ip list \
  --resource-group "emergency-alerts-prod-rg" \
  --query "[?tags.service=='emergency-alerts-ingress'].ipAddress" -o tsv

# If using Kubernetes ingress, get the external IP
kubectl get ingress -n emergency-alerts

# Azure will assign a domain like: XX-XX-XX-XX.eastus.cloudapp.azure.com
# Use this as VITE_API_URL: https://XX-XX-XX-XX.eastus.cloudapp.azure.com
```

**Add to GitHub Secrets once you have the domain:**

```powershell
# Using GitHub CLI
gh secret set VITE_API_URL --body "https://your-ingress-domain.cloudapp.azure.com"
```

Or manually in GitHub UI: Settings → Secrets and variables → Actions → New repository secret

## Cleanup

To remove the deployment identity:

```powershell
az identity delete `
    --resource-group "emergency-alerts-prod-rg" `
    --name "emergency-alerts-github-actions"
```
