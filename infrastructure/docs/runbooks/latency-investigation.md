# Latency Investigation

## SLA Targets

- API P95: <500ms
- Dashboard updates: <2s
- Drasi correlations: <5s
- Alert creation e2e: <3 min

## Quick Check

```bash
kubectl top pods -n emergency-alerts
kubectl top nodes
kubectl port-forward -n drasi-system svc/drasi-grafana 3000:3000
# http://localhost:3000
```

## By Layer

### API slow

```bash
# HPA keeping up?
kubectl get hpa -n emergency-alerts

# CPU/memory OK?
kubectl top pods -n emergency-alerts -l app=emergency-alerts-api
```

Fix: Scale up manually if HPA is lagging
```bash
kubectl scale deployment/emergency-alerts-api --replicas=10 -n emergency-alerts
```

### Database slow

> **Prerequisites:** Set up database connection per [README.md#database-access](./README.md#database-access)

```bash
# Active queries
kubectl run psql-debug --rm -it --image=postgres:17-alpine --restart=Never -n emergency-alerts -- \
  psql "host=$PG_HOST dbname=$PG_DB user=$PG_USER password=<password> sslmode=require" -c \
  "SELECT pid, now() - query_start AS duration, query FROM pg_stat_activity WHERE state='active' ORDER BY duration DESC LIMIT 5;"

# Missing indexes
kubectl run psql-idx --rm -it --image=postgres:17-alpine --restart=Never -n emergency-alerts -- \
  psql "host=$PG_HOST dbname=$PG_DB user=$PG_USER password=<password> sslmode=require" -c \
  "SELECT tablename, indexname, idx_scan FROM pg_stat_user_indexes WHERE idx_scan=0;"
```

Fix: Add missing indexes, kill long queries

### Drasi queries slow

```bash
kubectl top pods -n drasi-system -l app=drasi-query
drasi source describe postgres-alerts
```

Fix: Scale query replicas, increase timeout

### SignalR dropping

```bash
kubectl get ingress emergency-alerts-ingress -n emergency-alerts -o yaml | grep proxy-read-timeout
```

Fix: Set timeout to 3600s
```bash
kubectl annotate ingress emergency-alerts-ingress -n emergency-alerts \
  nginx.ingress.kubernetes.io/proxy-read-timeout="3600" --overwrite
```

## Escalate If

- Latency persists >30 min
- P95 >2s affecting users
