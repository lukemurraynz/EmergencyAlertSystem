# PostgreSQL Elastic Cluster Migration Guide

## Issue Resolution

**Problem:** Infrastructure deployment failed with error:
```
Value 'citus' is invalid for server parameter 'shared_preload_libraries'. 
Allowed values are: anon,auto_explain,azure_storage,...
```

**Root Cause:** The infrastructure code was attempting to enable Citus on Azure Database for PostgreSQL **Flexible Server**, but Citus is not available as a preloadable library on standard Flexible Server instances. Citus requires a different service offering.

## Migration Overview

### What Changed

| Aspect | Previous | Current |
|--------|----------|---------|
| **Service** | PostgreSQL Flexible Server | PostgreSQL Elastic Cluster |
| **Citus Support** | Manual configuration via shared_preload_libraries (❌ NOT SUPPORTED) | Automatic, built-in to Elastic Cluster (✅ FULLY SUPPORTED) |
| **Sharding** | Not possible | Transparent horizontal sharding via Citus |
| **Scalability** | Single node or traditional replication | Distributed, linearly scalable |
| **Replaced Service** | Azure Cosmos DB for PostgreSQL (deprecated) | — |

### Why Elastic Clusters?

According to [Microsoft Learn documentation](https://learn.microsoft.com/en-us/azure/postgresql/elastic-clusters/concepts-elastic-clusters):

> "Azure Cosmos DB for PostgreSQL is **no longer supported for new projects**. Use the **Elastic Clusters feature of Azure Database For PostgreSQL** for sharded PostgreSQL using the open-source Citus extension."

**Elastic Clusters** are the officially supported replacement for the deprecated Azure Cosmos DB for PostgreSQL service. They provide:

- Automatic Citus configuration (no manual shared_preload_libraries needed)
- Managed multi-node Citus clusters
- Automatic coordinator and worker node management
- Zone-redundant high availability by default
- Horizontal scaling out of the box
- **Microsoft Entra ID authentication automatically configured** (no manual `ad-admin` command needed)

### Entra ID Authentication (Auto-Configured)

**Status Update (2026-01-26):** Microsoft Entra ID authentication is automatically configured during cluster/server deployment via Bicep template parameters. Legacy setup scripts (`configure-postgres-entra-id.sh`, `Configure-PostgresEntraId.ps1`) have been **removed** as:
- The Azure CLI `ad-admin` command is deprecated
- Bicep deployment configures authentication automatically
- Manual post-deployment steps are no longer necessary

Your PostgreSQL resource already has:
- ✅ `authConfig.activeDirectoryAuth` set to "Enabled"
- ✅ `authConfig.tenantId` configured with your Azure AD tenant
- ✅ Ready for Entra ID-based connections (see [Connection via Entra ID](#connection-via-entra-id) below)

## Technical Details

### Citus in Elastic Clusters vs. Flexible Server

**PostgreSQL Flexible Server:**
- Single node or replicated node (not distributed)
- Citus extension is NOT available in shared_preload_libraries
- No automatic multi-node coordination
- Suitable for traditional PostgreSQL workloads

**PostgreSQL Elastic Clusters:**
- Multi-node distributed cluster by default
- Citus extension is **always present and automatically configured**
- Coordinator + worker nodes architecture
- Suitable for distributed, sharded workloads (e.g., multi-tenant SaaS, real-time analytics)

### Bicep Changes

**Deprecated Module:** `modules/postgres-flexible.bicep`
- Now marked as deprecated with migration notice
- Falls back to minimal single-node setup for non-Citus workloads only

**New Module:** `modules/elastic-cluster.bicep`
- Uses `Microsoft.DBforPostgreSQL/serverGroupsv2` API
- Automatically configures Citus at cluster creation time
- Default: 2 nodes for dev, 3+ for production (adjustable)
- Zone-redundant by default for HA

**Updated Entrypoint:** `main.bicep`
- Replaced `postgresFlexible` module with `elasticCluster` module
- Updated output names to reflect cluster terminology

### Node Configuration for Different Environments

```bicep
nodeCount: environment == 'prod' ? 3 : 2
```

- **prod:** 3+ nodes (1 coordinator + 2+ workers) for high availability and scale
- **dev:** 2 nodes (1 coordinator + 1 worker) for cost efficiency

## Deployment Instructions

### Prerequisites

1. Ensure your Azure subscription has availability for Elastic Clusters in your target region
2. Update any hardcoded references to the old PostgreSQL Flexible Server name/FQDN

### Redeploy Infrastructure

```powershell
# Navigate to infrastructure directory
cd infrastructure

# Deploy with updated Bicep (uses Elastic Cluster)
az deployment sub create `
  --template-file bicep/main.bicep `
  --location australiaeast `
  --parameters `
    environment=prod `
    projectName=emergency-alerts `
    databaseAdminPassword=$env:DB_ADMIN_PASSWORD
```

### Application Updates Required

Your application connection string and parameters may need updates:

**Connection String Format:**
```
Server=emergency-alerts-prod-cluster-c.postgres.database.azure.com,5432;
Database=postgres;
Port=5432;
User Id=pgadmin;
Password=...;
```

**Important:** Elastic Clusters use `postgres` as the default database name. The cluster does not support creating additional databases via `CREATE DATABASE`. Use schemas for logical separation:

```sql
-- Create schema for emergency alerts
CREATE SCHEMA emergency_alerts;

-- Create tables in schema
CREATE TABLE emergency_alerts.alerts (...);
CREATE TABLE emergency_alerts.areas (...);
```

**Citus-specific Configuration:**
- Distributed tables are now available via `SELECT create_distributed_table('emergency_alerts.alerts', 'alert_id');`
- Multi-tenant sharding is automatic if tables are properly distributed

## Connection via Entra ID (Recommended)

Since Entra ID authentication is automatically configured, you can connect using Azure AD credentials:

```bash
# Get access token for PostgreSQL (valid for 1 hour)
TOKEN=$(az account get-access-token --resource-type oss-rdbms --output json | jq -r .accessToken)

# Connect using the token as password
psql -h emergency-alerts-prod-pgfs.postgres.database.azure.com \
  -U '<your-email>@<tenant>.onmicrosoft.com' \
  -d postgres \
  --password
# When prompted for password, paste the token value above
```

**Benefits of Entra ID:**
- ✅ No hardcoded database passwords
- ✅ Credentials stored in Azure AD / Managed Identity
- ✅ Automatic token refresh for AKS pods
- ✅ Audit trail integrated with Azure AD logs
- ✅ Compliant with zero-trust security model

## Citus Extension Reference

Once connected to the Elastic Cluster, Citus functions are immediately available:

```sql
-- View Citus version
SELECT citus_version();

-- Create distributed table (if needed for sharding)
SELECT create_distributed_table('alerts', 'alert_id');

-- View active nodes in cluster
SELECT * FROM citus_get_active_worker_nodes();

-- List shards
SELECT * FROM pg_dist_placement;
```

## Limitations & Considerations

### Known Limitations (as per Microsoft docs)

1. **Single Database per Cluster:** Elastic Clusters support one `postgres` database by default
   - Additional `CREATE DATABASE` commands currently fail
   - This is a platform limitation, not a bug
   - Design your schema to use schemas instead of multiple databases if needed

2. **Unsupported Extensions:** 
   - TimescaleDB (conflicts with Citus)
   - PostGIS Topology
   - Query Store (pg_qs)
   - anon

3. **Feature Limitations:**
   - Major version upgrades not yet supported
   - Server log downloads not supported (use Log Analytics instead)

### Regional Availability

Elastic Clusters are available in the same regions as PostgreSQL Flexible Server. Verify availability in your target region:
```bash
az postgres flexible-server list-skus --location australiaeast --output table
```

## Monitoring & Observability

### Key Citus-Specific Metrics

Monitor these via Azure Portal or Log Analytics:
- **Distributed query latency:** Time for queries involving multiple shards
- **Shard rebalancing:** Data redistribution across nodes
- **Connection pool saturation:** PgBouncer (auto-provisioned) health

### Alerting

Set up alerts for:
- Coordinator CPU/Memory utilization > 80%
- Worker node failover events
- Shard rebalancing in progress

## Rollback Plan

If you need to revert to Flexible Server (not recommended):

1. Create a new PostgreSQL Flexible Server instance
2. Use `pg_dump` to export data from Elastic Cluster's `emergency-alerts` database
3. Import to new Flexible Server
4. Update connection strings in applications
5. Delete Elastic Cluster resource

However, **Elastic Clusters is the official Microsoft standard for Citus workloads**, so rollback is not recommended unless requirements change.

## Additional Resources

- [Elastic Clusters Documentation](https://learn.microsoft.com/en-us/azure/postgresql/elastic-clusters/concepts-elastic-clusters)
- [Elastic Clusters FAQ](https://learn.microsoft.com/en-us/azure/postgresql/elastic-clusters/concepts-elastic-clusters-limitations)
- [Citus Official Docs](https://docs.citusdata.com/)
- [Microsoft Azure Verified Modules for PostgreSQL](https://github.com/Azure/bicep-registry-modules)

---

**Status:** ✅ Migration complete and deployed
**Date:** 2026-01-26
**References:** Microsoft Learn, Azure Database for PostgreSQL documentation
