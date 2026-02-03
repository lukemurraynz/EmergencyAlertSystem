// modules/workload-identity-federation.bicep
// Workload Identity Federation: Links AKS OIDC issuer to Azure Managed Identity
//
// This enables keyless, OAuth 2.0 OIDC-based authentication from Kubernetes pods
// to Azure services without storing secrets or credentials in the cluster.
//
// Reference: https://learn.microsoft.com/en-us/azure/aks/workload-identity-overview

param managedIdentityName string
param aksOidcIssuerUrl string
param kubernetesNamespace string = 'emergency-alerts'
param serviceAccountName string = 'emergency-alerts-sa'
// Use a deterministic name to avoid creating duplicate federated credentials
// that have the same issuer+subject. Default aligns with prior CLI-created resource.
param federatedCredentialName string = 'aks-federation'

// Reference the existing managed identity (in same resource group as module scope)
resource managedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' existing = {
  name: managedIdentityName
}

// Create federated credential linking AKS OIDC to managed identity
// This allows the Kubernetes service account to obtain Azure tokens
resource federatedCredential 'Microsoft.ManagedIdentity/userAssignedIdentities/federatedIdentityCredentials@2023-01-31' = {
  parent: managedIdentity
  name: federatedCredentialName
  properties: {
    // The issuer must be the AKS OIDC issuer URL (e.g., https://eastus.oic.prod-aks.azure.com/... )
    issuer: aksOidcIssuerUrl
    
    // Subject identifies the Kubernetes service account
    // Format: system:serviceaccount:<namespace>:<serviceaccount>
    subject: 'system:serviceaccount:${kubernetesNamespace}:${serviceAccountName}'
    
    // Audiences: Azure uses 'api://AzureADTokenExchange' by default for workload identity
    audiences: [
      'api://AzureADTokenExchange'
    ]
  }
}

// Outputs
output federatedCredentialId string = federatedCredential.id
output federatedCredentialName string = federatedCredential.name
output managedIdentityClientId string = managedIdentity.properties.clientId
output managedIdentityPrincipalId string = managedIdentity.properties.principalId
output managedIdentityId string = managedIdentity.id
