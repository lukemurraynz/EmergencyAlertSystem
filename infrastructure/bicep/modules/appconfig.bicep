// modules/appconfig.bicep
// Azure App Configuration

param location string
param configName string
param managedIdentityObjectId string
param tags object

resource appConfig 'Microsoft.AppConfiguration/configurationStores@2023-03-01' = {
  name: configName
  location: location
  tags: tags
  sku: {
    name: 'standard'
  }
  identity: {
    type: 'None'
  }
  properties: {
    encryption: {
      keyVaultProperties: {
        keyIdentifier: null
      }
    }
    publicNetworkAccess: 'Enabled'
    disableLocalAuth: false
  }
}

// Role assignment for managed identity
resource roleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(managedIdentityObjectId, appConfig.id, 'App Configuration Data Reader')
  scope: appConfig
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '5ae67dd6-50cb-40e7-96ff-dc2bfa4b606b') // App Configuration Data Owner
    principalId: managedIdentityObjectId
    principalType: 'ServicePrincipal'
  }
}

output configId string = appConfig.id
output configName string = appConfig.name
output configEndpoint string = appConfig.properties.endpoint
