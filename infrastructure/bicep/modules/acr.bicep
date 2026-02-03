// modules/acr.bicep
// Azure Container Registry (ACR) for microservices

param location string
param registryName string
param managedIdentityObjectId string
param tags object = {}

// Azure Container Registry
// Standard SKU provides geo-replication, webhooks, and image scanning
resource acr 'Microsoft.ContainerRegistry/registries@2025-04-01' = {
  name: registryName
  location: location
  tags: tags
  sku: {
    name: 'Standard'
  }
  properties: {
    adminUserEnabled: false
    anonymousPullEnabled: false
    networkRuleBypassOptions: 'AzureServices'
    publicNetworkAccess: 'Enabled'
  }
}

// Assign AcrPush role to managed identity for push operations
resource acrPushRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(acr.id, 'AcrPush', managedIdentityObjectId)
  scope: acr
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '8311e382-0749-4cb8-b61a-304f252e45ec')
    principalId: managedIdentityObjectId
    principalType: 'ServicePrincipal'
  }
}

// Outputs
output registryId string = acr.id
output registryName string = acr.name
output registryUrl string = acr.properties.loginServer
