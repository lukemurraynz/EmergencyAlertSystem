# Managed Identity Authentication for Azure Cosmos DB for PostgreSQL

> **🎯 Key Change**: Microsoft Entra ID authentication is now **configured entirely in Bicep**! 
> Only the final step (adding managed identity as admin) requires manual Portal interaction. No passwords, no Key Vault needed.

## Overview

This infrastructure now uses **Microsoft Entra ID authentication with managed identities** instead of hardcoded passwords for Azure Cosmos DB for PostgreSQL. This approach provides:

✅ **Zero-trust security** - No password management needed  
✅ **Automatic credential rotation** - Azure manages identity lifecycle  
✅ **Audit trail** - All access logged via Azure AD  
✅ **Least privilege** - Managed identity has only necessary permissions  
✅ **Compliance** - Meets modern security standards (no secrets in code)

## Architecture

```
AKS Cluster (with System Assigned Identity)
    ↓
Managed Identity (User Assigned)
    ↓
Microsoft Entra ID
    ↓
Azure Cosmos DB for PostgreSQL (Entra ID Auth Enabled)
```

## Deployment Steps

### 1. Deploy Infrastructure (Automated)

```powershell
# Deploy Bicep infrastructure - automatically enables Entra ID auth!
az deployment subscription create \
  --name "infra-$(Get-Date -Format 'yyyyMMddHHmmss')" \
  --location australiaeast \
  --template-file infrastructure/bicep/main.bicep \
  --parameters environment=prod projectName=emergency-alerts
```

✅ **What's automated:**
- Entra ID authentication **enabled** at deployment time
- Password authentication **disabled** for security
- Database configured for token-based access only

### 2. Post-Deployment Configuration (Manual - 2 minutes only)

Only one step remains: **Add managed identity as Microsoft Entra ID admin**

#### Option A: Azure Portal (Easiest)

1. Go to **Azure Portal** → Search for your **PostgreSQL cluster** (e.g., `emergency-alerts-prod-db`)
2. Under **Cluster management**, select **Authentication**
3. You'll see ✓ **Microsoft Entra ID authentication** is already **enabled**
4. Click **Add Microsoft Entra ID admins**
5. Search for `emergency-alerts-prod-identity` (your managed identity name)
6. Select it and click **Select**
7. Click **Save**

#### Option B: Using Helper Scripts (Optional)

The provided scripts can guide you through getting the managed identity object ID:

```powershell
# Windows
& ".\infrastructure\scripts\Configure-PostgresEntraId.ps1" `
  -ResourceGroup "emergency-alerts-prod-rg" `
  -ClusterName "emergency-alerts-prod-db" `
  -ManagedIdentityName "emergency-alerts-prod-identity"

# Linux/macOS
bash ./infrastructure/scripts/configure-postgres-entra-id.sh \
  emergency-alerts-prod-rg \
  emergency-alerts-prod-db \
  emergency-alerts-prod-identity
```

### 3. Verify Configuration

```bash
# Check that Entra ID auth is enabled and get connection info
az resource show \
  --resource-group emergency-alerts-prod-rg \
  --resource-type "Microsoft.DBforPostgreSQL/serverGroupsv2" \
  --name emergency-alerts-prod-db \
  --query "properties.authConfig"

# Test connection using Azure credentials
az account get-access-token --resource https://token.postgres.cosmos.azure.com
# Use the token as password when connecting with psql
```

## Application Changes Required

### Connection String Format

**Before (Password-based):**
```
postgresql://citus:password@hostname:5432/database?sslmode=require
```

**After (Managed Identity):**
```csharp
// Use DefaultAzureCredential from Azure Identity SDK
// This automatically uses the pod's managed identity in AKS
var connection = new NpgsqlConnection(
    "Host=emergency-alerts-prod-db.postgres.cosmos.azure.com;" +
    "Database=emergency-alerts;" +
    "SslMode=Require;" +
    "Trust Server Certificate=true"
);

// With Azure Identity SDK:
var credential = new DefaultAzureCredential();
var token = credential.GetToken(
    new Azure.Core.TokenRequestContext(
        new[] { "https://token.postgres.cosmos.azure.com" }));

connection.Password = token.Token;
await connection.OpenAsync();
```

### Required NuGet Packages

```xml
<PackageReference Include="Azure.Identity" Version="1.11.0" />
<PackageReference Include="Npgsql" Version="8.0.0" />
```

## Security Best Practices

✅ **Do This:**
- Use `DefaultAzureCredential` for token acquisition
- Enable managed identity on AKS workloads
- Rotate tokens automatically (built-in, ~1 hour)
- Audit access via Azure Monitor
- Use separate managed identities per application

❌ **Don't Do This:**
- Store authentication tokens in config files
- Share credentials between applications
- Disable Entra ID authentication (when not needed for legacy support)
- Hardcode usernames or connection strings

## Troubleshooting

### "Principal with id […] does not exist"
The managed identity hasn't been added as an admin. Re-run Step 2 above.

### "role […] does not have permission"
The managed identity needs to be configured within the PostgreSQL cluster (not just Azure RBAC).
- Verify in Portal: Authentication → Microsoft Entra ID admins includes your managed identity

### "Role definition does not exist"
This is a transient Azure issue. Wait a few minutes and retry the deployment.

### Token Expiration Issues
Tokens expire after ~1 hour. `DefaultAzureCredential` automatically refreshes them - no action needed.

## Rollback (If Needed)

To revert to password-based auth:

1. **Azure Portal** → PostgreSQL cluster → Authentication
2. Uncheck ✓ **Microsoft Entra ID authentication**
3. Check ✓ **PostgreSQL authentication**
4. Update connection strings in application
5. Redeploy application

## References

- [Microsoft Entra ID and PostgreSQL authentication](https://learn.microsoft.com/en-us/azure/cosmos-db/postgresql/concepts-authentication)
- [Configure authentication methods](https://learn.microsoft.com/en-us/azure/cosmos-db/postgresql/how-to-configure-authentication)
- [Azure Identity SDK](https://learn.microsoft.com/en-us/dotnet/api/overview/azure/identity-readme)
- [Secretless authentication best practices](https://learn.microsoft.com/en-us/entra/architecture/zero-trust-architecture)

---

**Last Updated:** January 26, 2026  
**Status:** ✅ Recommended for Production
