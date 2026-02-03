// modules/aks-acr-pull.bicep
// Grants AKS kubelet identity AcrPull on the ACR registry

param aksClusterId string
param acrName string

var aksRef = reference(aksClusterId, '2025-05-01', 'full')
var kubeletObjectId = aksRef.properties.identityProfile.kubeletidentity.objectId

resource acr 'Microsoft.ContainerRegistry/registries@2025-04-01' existing = {
  name: acrName
}

resource acrPullAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(aksClusterId, acr.id, 'AcrPull')
  scope: acr
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7f951dda-4ed3-4680-a7ca-43fe172d538d') // AcrPull
    principalId: kubeletObjectId
    principalType: 'ServicePrincipal'
  }
}
