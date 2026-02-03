# Delivery Failure Troubleshooting

## Symptoms

- Alerts stuck in `DeliveryInProgress` for >60s
- Emails not arriving
- SLA breach alerts firing

## Quick Check

```bash
# Recent delivery errors?
kubectl logs -n emergency-alerts deployment/emergency-alerts-api --tail=100 | grep -i "delivery\|failed\|error"

# ACS healthy?
az communication list-service-health --resource-group $RESOURCE_GROUP --name $ACS_NAME
```

## Common Causes

### Network blocked

```bash
kubectl run curl-test --image=curlimages/curl --rm -i --restart=Never -- \
  curl -v https://management.azure.com
```

Fix: Check NetworkPolicy allows egress to `communication.azure.com:443`

### Quota exceeded

```bash
az communication show-quota --resource-group $RESOURCE_GROUP --name $ACS_NAME
```

Fix: Request quota increase, or temporarily prioritize high-severity alerts only

### Managed identity broken

```bash
# Check workload identity setup
kubectl get sa emergency-alerts-sa -n emergency-alerts -o yaml | grep azure.workload.identity

# Check federated credential
az identity federated-credential show \
  --name emergency-alerts-federated-cred \
  --identity-name emergency-alerts-identity \
  --resource-group $RESOURCE_GROUP
```

Fix: Recreate federated credential, restart pods

### No recipients configured

```bash
az appconfig kv show --name $APPCONFIG_NAME --key Email:TestRecipients
```

Fix: Set `Email:TestRecipients` to comma-separated email list

### Retry exhausted

Query delivery attempts (see [README.md#database-access](./README.md#database-access) for setup):
```bash
kubectl run psql-debug --rm -it --image=postgres:17-alpine --restart=Never -n emergency-alerts -- \
  psql "host=$PG_HOST dbname=$PG_DB user=$PG_USER password=<password> sslmode=require" -c \
  "SELECT alert_id, status, error_message FROM delivery_attempts WHERE status='Failed' ORDER BY attempted_at DESC LIMIT 10;"
```

Fix: Trigger manual retry via admin endpoint or fix root cause and redeploy

## Escalate If

- >15 min without resolution
- Extreme/Severe alerts affected (life-safety risk)

Post in `#emergency-alerts-incidents`
