# Runbook: Rollback Procedures

**Purpose**: Safely roll back deployments when issues are detected in production  
**Audience**: On-call engineers, deployment team  
**Severity**: Critical (prevents further impact from bad deployments)  
**Target Execution Time**: <5 minutes

---

## Database Connection

> **Note:** PostgreSQL is Azure Database Flexible Server (managed service), not a K8s deployment.
> See [README.md#database-access](./README.md#database-access) for connection setup.

```bash
# Set environment variables first (get values from Azure Portal or Bicep outputs)
export PG_HOST="<your-server>.postgres.database.azure.com"  # From: postgresCoordinatorFqdn output
export PG_USER="pgadmin"
export PG_DB="postgres"
```

---

## When to Rollback

Initiate rollback if any of the following conditions are met:

- ✅ **P0/SEV-1 Incident:** Service unavailable, data loss, critical security issue
- ✅ **SLA Breach:** >10% error rate for >5 minutes
- ✅ **Failed Health Checks:** Readiness/liveness probes failing on >50% of pods
- ✅ **Data Corruption:** Database integrity violations detected
- ✅ **Regression Detected:** Critical functionality broken (alert creation, approval, delivery)

**DO NOT rollback for:**
- Minor UI glitches (cosmetic issues)
- Non-critical feature bugs (if workaround exists)
- Performance degradation <10%
- Single pod failures (if HPA compensates)

---

## Pre-Rollback Checklist

Before executing rollback:

1. [ ] **Verify Issue:** Confirm problem exists in logs/metrics (not transient)
2. [ ] **Check Deployment History:** Identify stable version to roll back to
3. [ ] **Notify Stakeholders:** Post in `#emergency-alerts-incidents` channel
4. [ ] **Capture State:** Take snapshots of logs, metrics, database state
5. [ ] **Stop CI/CD:** Pause GitHub Actions workflow to prevent auto-deploy

---

## Rollback Methods

### Method 1: Kubernetes Rollout Undo (Fastest)

**Use when:** Deployment was made via `kubectl set image` or `kubectl apply`  
**Execution time:** ~2 minutes

```bash
# Step 1: Check deployment history
kubectl rollout history deployment/emergency-alerts-api -n emergency-alerts

# Output example:
# REVISION  CHANGE-CAUSE
# 1         Initial deployment
# 2         Image update: emergency-alerts-api:abc123
# 3         Image update: emergency-alerts-api:def456  <- Current (broken)

# Step 2: Roll back to previous revision
kubectl rollout undo deployment/emergency-alerts-api -n emergency-alerts --to-revision=2

# Step 3: Monitor rollout status
kubectl rollout status deployment/emergency-alerts-api -n emergency-alerts --timeout=5m

# Step 4: Verify pods are healthy
kubectl get pods -n emergency-alerts -l app=emergency-alerts-api

# Step 5: Run smoke test
kubectl run curl-test --image=curlimages/curl:latest --rm -i --restart=Never -- \
  curl -f http://emergency-alerts-api.emergency-alerts.svc.cluster.local/health
```

**Success criteria:**
- All pods show `Running` status
- Health check returns `200 OK`
- Error rate drops below 1%

---

### Method 2: Re-deploy Known-Good Image (Most Reliable)

**Use when:** Need to roll back multiple revisions or deployment history unclear  
**Execution time:** ~3 minutes

```bash
# Step 1: Identify known-good image tag
GOOD_IMAGE_TAG="abc123"  # From Git commit or ACR tags

# Step 2: Update deployment with specific image
kubectl set image deployment/emergency-alerts-api \
  emergency-alerts-api=$ACR_NAME.azurecr.io/emergency-alerts-api:$GOOD_IMAGE_TAG \
  -n emergency-alerts

# Step 3: Wait for rollout
kubectl rollout status deployment/emergency-alerts-api -n emergency-alerts --timeout=5m

# Step 4: Verify deployment
kubectl get deployment emergency-alerts-api -n emergency-alerts -o jsonpath='{.spec.template.spec.containers[0].image}'

# Expected output: <acr-name>.azurecr.io/emergency-alerts-api:abc123
```

---

### Method 3: GitOps Rollback (Infrastructure as Code)

**Use when:** Deployment managed by ArgoCD/Flux or Bicep files changed  
**Execution time:** ~5 minutes

```bash
# Step 1: Identify known-good Git commit
git log --oneline infrastructure/k8s/deployment.yaml
# Output: 
# def456 (HEAD -> main) Update deployment image to v1.2.3
# abc123 Update deployment image to v1.2.0  <- Known good

# Step 2: Revert commit
git revert def456 --no-commit
git commit -m "Rollback: Revert deployment to v1.2.0 (commit abc123)"

# Step 3: Push to main branch
git push origin main

# Step 4: Trigger CI/CD pipeline or manually apply
kubectl apply -f infrastructure/k8s/deployment.yaml

# Step 5: Monitor rollout
kubectl rollout status deployment/emergency-alerts-api -n emergency-alerts --timeout=5m
```

---

## Rollback Drasi Queries

**Use when:** New Drasi query causing performance degradation or incorrect correlations

```bash
# Step 1: Check query deployment history
kubectl get continuousqueries -n drasi-system -o yaml > drasi-queries-backup.yaml

# Step 2: Roll back to previous query version
git checkout <previous-commit> infrastructure/drasi/queries/emergency-alerts.yaml

# Step 3: Apply previous version
kubectl apply -f infrastructure/drasi/queries/emergency-alerts.yaml

# Step 4: Verify queries are running
kubectl get continuousqueries -n drasi-system

# Step 5: Check query performance metrics
kubectl port-forward -n drasi-system svc/drasi-prometheus 9090:9090
# Query: drasi_query_execution_duration_seconds{query="geographic-correlation"}
```

---

## Database Rollback (Rare - High Risk)

**⚠️ WARNING:** Database rollbacks are complex and risky. Only perform if:
- Data corruption is confirmed
- Backups are recent (<1 hour old)
- Incident commander approval obtained

**DO NOT** perform database rollback for:
- Application logic bugs (fix forward instead)
- Missing indexes (add them incrementally)
- Slow queries (optimize queries, don't rollback)

### Database Point-in-Time Restore

```bash
# Step 1: Identify restore point (timestamp before corruption)
RESTORE_POINT="2026-01-25T14:30:00Z"

# Step 2: Create new Cosmos DB PostgreSQL instance from backup
az postgres flexible-server restore \
  --resource-group $RESOURCE_GROUP \
  --name emergency-alerts-db-restored \
  --source-server emergency-alerts-db \
  --restore-time $RESTORE_POINT

# Step 3: Update connection string in App Config
az appconfig kv set \
  --name $APPCONFIG_NAME \
  --key ConnectionStrings:Postgres \
  --value "Host=emergency-alerts-db-restored.postgres.database.azure.com;..." \
  --yes

# Step 4: Restart API pods to pick up new connection string
kubectl rollout restart deployment/emergency-alerts-api -n emergency-alerts

# Step 5: Verify data integrity (run from debug pod)
kubectl run psql-verify --rm -it --image=postgres:17-alpine --restart=Never -n emergency-alerts -- \
  psql "host=$PG_HOST dbname=$PG_DB user=$PG_USER password=<password> sslmode=require" -c \
  "SELECT count(*) FROM alerts WHERE created_at > '$RESTORE_POINT';"

# Expected: 0 (no alerts created after restore point)
```

**Post-Restore Actions:**
- Manually re-create any valid alerts created between restore point and now
- Notify operators of data loss window
- Document root cause to prevent recurrence

---

## Frontend Rollback

```bash
# Rollback frontend deployment
kubectl set image deployment/emergency-alerts-frontend \
  emergency-alerts-frontend=$ACR_NAME.azurecr.io/emergency-alerts-frontend:$GOOD_IMAGE_TAG \
  -n emergency-alerts

# Wait for rollout
kubectl rollout status deployment/emergency-alerts-frontend -n emergency-alerts --timeout=3m

# Clear browser cache for users (if needed)
# Update Cache-Control headers to force refresh
```

---

## Post-Rollback Verification

After rollback, verify system health:

### 1. Functional Tests

```bash
# Create test alert
curl -X POST https://api.emergency-alerts.example.com/api/v1/alerts \
  -H "Authorization: Bearer $TEST_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "headline": "Rollback Verification Test",
    "description": "Testing alert creation after rollback",
    "severity": "Moderate",
    "expiresAt": "'$(date -u -d "+2 hours" +"%Y-%m-%dT%H:%M:%SZ")'",
    "area": {
      "polygon": "POLYGON((-122.4 47.6, -122.3 47.6, -122.3 47.7, -122.4 47.7, -122.4 47.6))"
    }
  }'

# Verify alert appears in dashboard
curl https://api.emergency-alerts.example.com/api/v1/dashboard/summary \
  -H "Authorization: Bearer $TEST_TOKEN"
```

### 2. Metrics Check

```bash
# Check error rate (should be <1%)
kubectl port-forward -n drasi-system svc/drasi-prometheus 9090:9090
# Query: sum(rate(http_requests_total{status=~"5.."}[5m])) / sum(rate(http_requests_total[5m])) * 100

# Check latency (P95 should be <500ms)
# Query: histogram_quantile(0.95, http_request_duration_seconds_bucket{job="emergency-alerts-api"})
```

### 3. Drasi Health

```bash
# Verify all 8 queries running
kubectl get continuousqueries -n drasi-system --no-headers | wc -l
# Expected: 8

# Check query execution times
drasi query list
```

---

## Communication Template

**Post to #emergency-alerts-incidents:**

```markdown
🔄 **ROLLBACK EXECUTED**

**Deployment:** Backend API v1.2.3 → v1.2.0  
**Reason:** [Describe issue: e.g., "500 errors on /api/v1/alerts POST endpoint"]  
**Rollback Method:** kubectl rollout undo  
**Execution Time:** 2 minutes  
**Status:** ✅ Successful - all pods healthy, error rate <1%

**Next Steps:**
- Root cause analysis scheduled for [timestamp]
- Fix deployed to staging for validation
- Production re-deployment planned after approval

**Incident Commander:** @username
```

---

## Rollback Decision Matrix

| Condition | Severity | Action | Method |
|-----------|----------|--------|--------|
| Error rate >50% for >5min | SEV-1 | **Immediate rollback** | Method 1 (kubectl undo) |
| Error rate 10-50% for >10min | SEV-2 | Rollback after confirming no transient issue | Method 1 or 2 |
| Error rate <10%, UX broken | SEV-3 | Evaluate fix-forward vs rollback | Method 3 (GitOps) |
| Performance degradation >50% | SEV-2 | Rollback if no mitigation (scale up) | Method 2 |
| Data corruption detected | SEV-1 | Database PITR restore **only** with approval | Database restore |

---

## Prevention Best Practices

To reduce rollback frequency:

1. **Staged Rollouts:** Deploy to dev → staging → 10% prod → 100% prod
2. **Automated Canary Analysis:** Use Flagger/Argo Rollouts with metrics
3. **Load Testing:** Run T084 load tests before production deploy
4. **Feature Flags:** Use App Config to toggle new features without redeploy
5. **Comprehensive E2E Tests:** Run T081-T083 in staging before production

---

## Related Runbooks

- [Delivery Failure Troubleshooting](./delivery-failure-troubleshooting.md)
- [Latency Investigation](./latency-investigation.md)
- [Dashboard Troubleshooting](./dashboard-troubleshooting.md)

---

## Emergency Contacts

> **ACTION REQUIRED:** Update these contacts for your organization before using this runbook.
> See issue tracking for customization: Configure runbook contacts before production use.

- **Incident Commander:** (designated in PagerDuty rotation)
- **Database Admin:** `<CONFIGURE: db-team@your-org.com>`
- **Platform Team:** `<CONFIGURE: platform-oncall@your-org.com>`
- **Product Owner:** `<CONFIGURE: alerts-team-lead@your-org.com>`
