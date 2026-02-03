# Schema Creation for CI/CD

## Approach: Simple Post-Deployment Step

Instead of embedding schema creation in Bicep (which adds complexity with deployment scripts), use a simple one-liner in your CI/CD pipeline **after** infrastructure deployment.

### Why This Approach?

✅ **Simple** - One command, no deployment script overhead  
✅ **CI/CD Friendly** - Runs automatically after infrastructure deployment  
✅ **Passwordless** - Uses Key Vault (pipeline identity has access via RBAC)  
✅ **Idempotent** - `CREATE SCHEMA IF NOT EXISTS` means safe to re-run  
✅ **Follows Best Practices** - Control plane (Bicep) vs Data plane (pipeline) separation

### CI/CD Pipeline Steps

```yaml
# Example: Azure DevOps Pipeline
steps:
  # 1. Deploy infrastructure (Bicep)
  - task: AzureCLI@2
    displayName: 'Deploy Infrastructure'
    inputs:
      azureSubscription: '$(azureServiceConnection)'
      scriptType: 'bash'
      scriptLocation: 'inlineScript'
      inlineScript: |
        az deployment sub create \
          --location australiaeast \
          --template-file infrastructure/bicep/main.bicep \
          --parameters environment=$(environment)
  
  # 2. Create schema (data plane - one liner!)
  - task: AzureCLI@2
    displayName: 'Create Application Schema'
    inputs:
      azureSubscription: '$(azureServiceConnection)'
      scriptType: 'bash'
      scriptLocation: 'inlineScript'
      inlineScript: |
        DB_HOST=$(az deployment sub show \
          --name <deployment-name> \
          --query properties.outputs.postgresCoordinatorFqdn.value -o tsv)
        
        DB_PASSWORD=$(az keyvault secret show \
          --vault-name emergency-alerts-$(environment)-kv \
          --name postgres-admin-password \
          --query value -o tsv)
        
        PGPASSWORD="$DB_PASSWORD" psql \
          "host=$DB_HOST port=5432 user=pgadmin dbname=postgres sslmode=require" \
          -c "CREATE SCHEMA IF NOT EXISTS emergency_alerts;"
```

### PowerShell Version (GitHub Actions / Local)

```powershell
# Get outputs from deployment
$dbHost = az deployment sub show `
  --name emergency-alerts-prod-deploy `
  --query 'properties.outputs.postgresCoordinatorFqdn.value' -o tsv

# Get password from Key Vault
$env:PGPASSWORD = az keyvault secret show `
  --vault-name emergency-alerts-prod-kv `
  --name postgres-admin-password `
  --query value -o tsv

# Create schema (idempotent)
psql "host=$dbHost port=5432 user=pgadmin dbname=postgres sslmode=require" `
  -c "CREATE SCHEMA IF NOT EXISTS emergency_alerts;"
```

### Manual Execution (Once)

```bash
# For prod
cd infrastructure/bicep

DB_HOST=$(az deployment sub show \
  --name emergency-alerts-prod-deploy \
  --query properties.outputs.postgresCoordinatorFqdn.value -o tsv)

DB_PASSWORD=$(az keyvault secret show \
  --vault-name emergency-alerts-prod-kv \
  --name postgres-admin-password \
  --query value -o tsv)

PGPASSWORD="$DB_PASSWORD" psql \
  "host=$DB_HOST port=5432 user=pgadmin dbname=postgres sslmode=require" \
  -c "CREATE SCHEMA IF NOT EXISTS emergency_alerts;"
```

### What About RBAC for the Database?

The current setup uses the built-in `pgadmin` account with password stored in Key Vault. This is:
- ✅ Simple and works immediately 
- ✅ Password never in source code or CI/CD variables
- ✅ RBAC-protected via Key Vault

**Optional: Switch to Managed Identity authentication** (more complex, requires Azure Entra ID configuration on the cluster - portal-based, not easily automated in Bicep with current API).

---

## Summary

| Aspect | Status |
|--------|--------|
| Infrastructure (Bicep) | ✅ Complete - handles all Azure resources |
| Schema Creation | ✅ Simple one-liner in CI/CD post-deploy |
| Password Management | ✅ Key Vault with RBAC |
| CI/CD Complexity | ✅ Minimal - no deployment scripts needed |
| Idempotency | ✅ `CREATE SCHEMA IF NOT EXISTS` |

**Result:** Simple, secure, automatable schema creation without overcomplicating Bicep.
