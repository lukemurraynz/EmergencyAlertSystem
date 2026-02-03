# Dashboard Troubleshooting

## Quick Checks

| Symptom | Check |
|---------|-------|
| Loading forever | API pods running? CORS configured? |
| Counts wrong | Query database directly, compare |
| No real-time updates | SignalR connected? WebSocket timeout? |
| "Reconnecting..." | Ingress timeout, sticky sessions |

## Dashboard won't load

```bash
# API up?
kubectl get pods -n emergency-alerts -l app=emergency-alerts-api

# Health OK?
curl https://api.emergency-alerts.example.com/api/v1/health

# CORS blocking?
# Browser console will show CORS error
az appconfig kv show --name $APPCONFIG_NAME --key Cors:AllowedOrigins
```

Fix CORS:
```bash
az appconfig kv set --name $APPCONFIG_NAME --key Cors:AllowedOrigins \
  --value "https://your-frontend-domain.com" --yes
```

## Real-time updates broken

```bash
# WebSocket timeout too short?
kubectl get ingress emergency-alerts-ingress -n emergency-alerts -o yaml | grep proxy-read-timeout

# Sticky sessions enabled?
kubectl get svc emergency-alerts-signalr -n emergency-alerts -o yaml | grep sessionAffinity
```

Fix:
```bash
kubectl annotate ingress emergency-alerts-ingress -n emergency-alerts \
  nginx.ingress.kubernetes.io/proxy-read-timeout="3600" \
  nginx.ingress.kubernetes.io/proxy-send-timeout="3600" --overwrite

kubectl patch svc emergency-alerts-signalr -n emergency-alerts -p '{"spec":{"sessionAffinity":"ClientIP"}}'
```

## Counts don't match

> **Prerequisites:** Set up database connection per [README.md#database-access](./README.md#database-access)

```bash
kubectl run psql-debug --rm -it --image=postgres:17-alpine --restart=Never -n emergency-alerts -- \
  psql "host=$PG_HOST dbname=$PG_DB user=$PG_USER password=<password> sslmode=require" -c \
  "SELECT status, count(*) FROM alerts GROUP BY status;"
```

Compare with API response. If mismatch, check aggregation query logic in code.

## Correlation events missing

```bash
# Drasi running?
kubectl get continuousqueries -n drasi-system

# Events in DB? (see README.md#database-access for setup)
kubectl run psql-debug --rm -it --image=postgres:17-alpine --restart=Never -n emergency-alerts -- \
  psql "host=$PG_HOST dbname=$PG_DB user=$PG_USER password=<password> sslmode=require" -c \
  "SELECT * FROM correlation_events ORDER BY created_at DESC LIMIT 5;"
```

Fix: Restart Drasi query pod
```bash
kubectl delete pod -n drasi-system -l query=geographic-correlation
```

## User FAQ

- **Old data?** Hard refresh (Ctrl+Shift+R)
- **"Reconnecting..."?** Check network, try refresh
- **Counts look wrong?** Check your filters in the UI
