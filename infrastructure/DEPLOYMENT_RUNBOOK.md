# Deployment Runbook

## Pre-Deploy Checklist

```bash
# Right subscription?
az account show --query name -o tsv

# Lint passes?
az bicep build --file infrastructure/bicep/main.bicep --outdir /tmp
cd backend && dotnet build --configuration Release
cd frontend && npm run lint && npm run build

# What-if looks safe?
az deployment sub what-if -l australiaeast \
  -f infrastructure/bicep/main.json \
  -p infrastructure/bicep/main.bicepparam
```

## Deploy via CI/CD (normal path)

1. Push to feature branch
2. Create PR, wait for checks
3. Merge to main → pipeline deploys automatically

```bash
# Watch progress
gh run list --limit 1
gh run view <run-id> --log
```

## Manual Infrastructure Deploy (if CI fails)

```bash
az bicep build --file infrastructure/bicep/main.bicep --outdir infrastructure/bicep/generated

az deployment sub create -l australiaeast \
  -f infrastructure/bicep/generated/main.json \
  -p infrastructure/bicep/main.bicepparam \
  --name "emergency-alerts-$(date +%s)"
```

## Verify Deployment

```bash
# Pods running?
az aks get-credentials -g emergency-alerts-prod -n emergency-alerts-prod-aks --admin
kubectl get pods -n emergency-alerts

# Health OK?
kubectl port-forward -n emergency-alerts svc/emergency-alerts-api 5000:80 &
curl http://localhost:5000/health/live
curl http://localhost:5000/health/ready

# Logs clean?
kubectl logs -n emergency-alerts -l app=emergency-alerts-api --tail=50
```

## Rollback

```bash
# Quick: undo last deployment
kubectl rollout undo deployment/emergency-alerts-api -n emergency-alerts

# Safer: revert git commit
git revert <commit> --no-edit
git push origin main
```

## Common Issues

| Problem | Check | Fix |
|---------|-------|-----|
| CrashLoopBackOff | `kubectl logs <pod> --previous` | Fix config, redeploy |
| ImagePullBackOff | ACR creds, image exists | `kubectl describe pod`, fix ACR access |
| Health probe failing | App logs | Ensure `/health/*` bypasses auth |
| 502 on ingress | Pod status, ingress rules | Restart pods, check service ports |
| DB connection failed | App Config connection string | Verify firewall, managed identity |

## Post-Deploy

```bash
# Smoke test
curl -i https://emergency-alerts.nip.io/api/v1/health
curl -i https://emergency-alerts.nip.io/api/v1/alerts
```
