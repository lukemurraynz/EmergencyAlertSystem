# Infrastructure

Azure infrastructure and Kubernetes configs for the Emergency Alert system.

```
infrastructure/
├── bicep/           # Azure IaC (AKS, PostgreSQL, App Config, Key Vault)
├── drasi/           # CDC queries and reactions
├── k8s/             # Kubernetes manifests
├── docs/            # Runbooks and migration guides
└── scripts/         # Setup helpers
```

## Prerequisites

- Azure CLI 2.60+
- kubectl 1.29+
- Drasi CLI
- Azure subscription with Contributor access

## Deploy

See [DEPLOYMENT_RUNBOOK.md](./DEPLOYMENT_RUNBOOK.md) for step-by-step instructions.
