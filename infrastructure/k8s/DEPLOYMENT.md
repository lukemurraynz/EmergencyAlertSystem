# Kubernetes Deployment Guide

Emergency Alert Core System - AKS Deployment Instructions

## Prerequisites

1. **AKS Cluster**: 1.29.0+ with workload identity enabled
2. **cert-manager**: v1.13.0+ installed (for TLS certificates)
3. **Ingress Controller**: NGINX ingress controller v1.8.0+
4. **Azure CLI**: Latest version configured with subscription access
5. **kubectl**: v1.29.0+ configured to access your AKS cluster

## Environment Variables

Before deployment, set these environment variables:

```bash
export CLUSTER_NAME="emergency-alerts-aks"
export RESOURCE_GROUP="emergency-alerts-rg"
export REGISTRY_NAME="emergencyalerts"
export IMAGE_TAG="latest"

# Azure identities
export AZURE_SUBSCRIPTION_ID="<your-subscription-id>"
export AZURE_TENANT_ID="<your-tenant-id>"
export MANAGED_IDENTITY_CLIENT_ID="<managed-identity-client-id>"

# Service endpoints
export COSMOS_CONNECTION_STRING="<cosmos-db-connection-string>"
export APP_CONFIG_ENDPOINT="<app-config-endpoint>"
export KEY_VAULT_URI="<key-vault-uri>"
export ACS_ENDPOINT="https://<acs-resource-name>.communication.azure.com/"

# DNS
export DOMAIN_NAME="api.emergency-alerts.example.com"
export CORS_ALLOWED_ORIGINS="https://app.example.com,https://localhost:3000"
```

## Deployment Steps

### 1. Create Namespace and Service Account

```bash
kubectl apply -f deployment.yaml
# This creates the namespace, service account, and configmap
```


### 2. Apply RBAC and Network Policies

```bash
kubectl apply -f rbac.yaml
# Apply all required NetworkPolicies (critical for pod-to-pod and ingress/egress security)
kubectl apply -f network-policy-fixed.yaml
kubectl apply -f emergency-alerts-api-allow-frontend.yaml
```

> **Note:** Both `network-policy-fixed.yaml` and `emergency-alerts-api-allow-frontend.yaml` are required for correct frontend-to-backend connectivity and secure operation. If you add new services or pods, review and update network policies accordingly.

### 3. Deploy Ingress Controller (one-time)

```bash
helm repo add ingress-nginx https://kubernetes.github.io/ingress-nginx
helm repo update
helm install ingress-nginx ingress-nginx/ingress-nginx \
  --namespace ingress-nginx \
  --create-namespace \
  --set controller.service.type=LoadBalancer
```

### 4. Install cert-manager (one-time)

```bash
helm repo add jetstack https://charts.jetstack.io
helm repo update
helm install cert-manager jetstack/cert-manager \
  --namespace cert-manager \
  --create-namespace \
  --set installCRDs=true
```

### 5. Create ClusterIssuer and Ingress

```bash
# Substitute environment variables in ingress.yaml
envsubst < ingress.yaml | kubectl apply -f -
```

### 6. Verify Deployment

```bash
# Check pod status
kubectl get pods -n emergency-alerts

# Check services
kubectl get svc -n emergency-alerts

# Check HPA status
kubectl get hpa -n emergency-alerts

# View logs
kubectl logs -n emergency-alerts -l app=emergency-alerts-api -f
```

### 7. Test Connectivity

```bash
# Port-forward to test locally
kubectl port-forward -n emergency-alerts svc/emergency-alerts-api 5000:80

# Test health endpoint
curl http://localhost:5000/health

# Test live probe
curl http://localhost:5000/health/live

# Test ready probe
curl http://localhost:5000/health/ready
```

## Configuration Updates

To update configuration without redeployment:

```bash
# Edit ConfigMap
kubectl edit configmap emergency-alerts-config -n emergency-alerts

# Pods will automatically reload configuration (within 5 minutes via App config refresh)
```

## Scaling Decisions

The HPA is configured to scale based on:
- **CPU**: 70% utilization threshold
- **Memory**: 80% utilization threshold
- **Min replicas**: 3
- **Max replicas**: 10

To manually scale:

```bash
kubectl scale deployment emergency-alerts-api -n emergency-alerts --replicas=5
```

## Troubleshooting

### Pod fails to start

```bash
# Check pod status and reason
kubectl describe pod -n emergency-alerts <pod-name>

# Check logs
kubectl logs -n emergency-alerts <pod-name>
```

### Ingress not routing traffic

```bash
# Check ingress status
kubectl get ingress -n emergency-alerts -o wide

# Check certificate status
kubectl get certificate -n emergency-alerts
kubectl describe certificate emergency-alerts-cert -n emergency-alerts

# Check certificate secret
kubectl get secret emergency-alerts-tls -n emergency-alerts -o yaml
```

### Service connectivity issues

```bash
# Test service DNS within pod
kubectl run -it --rm test --image=busybox --restart=Never -- nslookup emergency-alerts-api.emergency-alerts.svc.cluster.local

# Test port connectivity
kubectl run -it --rm test --image=busybox --restart=Never -- wget -O- http://emergency-alerts-api.emergency-alerts.svc.cluster.local:80/health
```

### HPA not scaling

```bash
# Check HPA status
kubectl describe hpa -n emergency-alerts emergency-alerts-api-hpa

# Check metrics server (required for resource metrics)
kubectl get deployment -n kube-system | grep metrics-server
```

## Rollback

To rollback to previous deployment:

```bash
kubectl rollout history deployment emergency-alerts-api -n emergency-alerts

# Rollback to previous revision
kubectl rollout undo deployment emergency-alerts-api -n emergency-alerts

# Rollback to specific revision
kubectl rollout undo deployment emergency-alerts-api -n emergency-alerts --to-revision=2
```

## Security

- **Workload Identity**: Pods use managed identity via OIDC federation (no credentials in manifests)
- **Network Policies**: Restrict ingress/egress traffic per security requirements
- **Pod Security**: Non-root container, read-only root filesystem, dropped capabilities
- **RBAC**: Minimal permissions per principle of least privilege
- **TLS**: All traffic encrypted in transit via Let's Encrypt certificates

## Observability

### Metrics

```bash
# View CPU/memory metrics
kubectl top nodes
kubectl top pods -n emergency-alerts
```

### Logs

```bash
# Stream logs from all pods
kubectl logs -n emergency-alerts -l app=emergency-alerts-api -f --all-containers

# View logs from specific pod
kubectl logs -n emergency-alerts <pod-name> -c api
```

### Events

```bash
kubectl get events -n emergency-alerts --sort-by='.lastTimestamp' -w
```

## Cleanup

To remove all resources:

```bash
# Delete namespace (removes all resources within it)
kubectl delete namespace emergency-alerts

# Optionally delete Ingress controller
helm uninstall ingress-nginx -n ingress-nginx
kubectl delete namespace ingress-nginx

# Optionally delete cert-manager
helm uninstall cert-manager -n cert-manager
kubectl delete namespace cert-manager
```

## Next Steps

1. Configure monitoring (Prometheus/Grafana)
2. Set up centralized logging (ELK/Loki)
3. Configure alerts for pod failures, high resource usage
4. Establish backup and disaster recovery procedures
5. Document operational runbooks for team
