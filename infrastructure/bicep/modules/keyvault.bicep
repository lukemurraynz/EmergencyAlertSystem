// modules/keyvault.bicep
// Azure Key Vault

param location string
param keyVaultName string
param managedIdentityObjectId string
@secure()
param adminPassword string = ''
param tags object

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
  location: location
  tags: tags
  properties: {
    enabledForDeployment: true
    enabledForTemplateDeployment: true
    enabledForDiskEncryption: false
    enableRbacAuthorization: true
    tenantId: subscription().tenantId
    sku: {
      family: 'A'
      name: 'standard'
    }
    accessPolicies: []
  }
}

// Role assignment for managed identity
resource roleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(managedIdentityObjectId, keyVault.id, 'Key Vault Secrets User')
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6') // Key Vault Secrets User
    principalId: managedIdentityObjectId
    principalType: 'ServicePrincipal'
  }
}

// Optionally create the admin password secret
resource adminPwdSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = if (!empty(adminPassword)) {
  parent: keyVault
  name: 'postgres-admin-password'
  properties: {
    value: adminPassword
    contentType: 'text/plain'
  }
}

output keyVaultId string = keyVault.id
output keyVaultName string = keyVault.name
output keyVaultUri string = keyVault.properties.vaultUri
