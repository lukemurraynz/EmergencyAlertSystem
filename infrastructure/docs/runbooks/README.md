# Runbooks

| Runbook | When to Use |
|---------|-------------|
| [Delivery Failures](./delivery-failure-troubleshooting.md) | Emails not sending, SLA breaches |
| [Latency](./latency-investigation.md) | Slow API, delayed updates |
| [Rollback](./rollback-procedures.md) | Bad deployment |
| [Dashboard](./dashboard-troubleshooting.md) | UI broken, wrong data |

## First Response

```bash
kubectl get pods -n emergency-alerts
kubectl get pods -n drasi-system
kubectl logs -n emergency-alerts deployment/emergency-alerts-api --tail=100 | grep ERROR
```

## Database Access

PostgreSQL runs on Azure Flexible Server (not in K8s). Get creds from Key Vault.

```bash
export PG_HOST="<your-server>.postgres.database.azure.com"
export PG_USER="pgadmin"
export PG_DB="postgres"

# Interactive session via debug pod
kubectl run psql-debug --rm -it --image=postgres:17-alpine --restart=Never -n emergency-alerts -- \
  psql "host=$PG_HOST dbname=$PG_DB user=$PG_USER password=<password> sslmode=require"
```

## Quick Fixes

**Pods crashing?**
```bash
kubectl logs -n emergency-alerts deployment/emergency-alerts-api --previous
kubectl rollout restart deployment/emergency-alerts-api -n emergency-alerts
```

**Drasi queries stuck?**
```bash
kubectl get continuousqueries -n drasi-system
kubectl delete pod -n drasi-system -l query=geographic-correlation
```

## Severity Guide

- **SEV-1**: Service down or life-safety alerts failing. Page immediately, update Slack every 15 min.
- **SEV-2**: Major degradation (>10% errors, >5s latency). Respond in 15 min.
- **SEV-3**: Minor issues. Fix within 1 hour or next business day.

## Grafana

```bash
kubectl port-forward -n drasi-system svc/drasi-grafana 3000:3000
# http://localhost:3000
```
