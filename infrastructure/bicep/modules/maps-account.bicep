// Azure Maps Account with User-Assigned Managed Identity
// API Version: 2023-06-01 (stable version with full Bicep type support)
// Note: Azure Maps only supports specific regions: westcentralus, global, westus2, eastus, westeurope, northeurope
// The mapsLocation parameter allows overriding the default region for Maps resources

param mapsLocation string
param accountName string
param tags object = {}
param userAssignedIdentityId string

resource mapsAccount 'Microsoft.Maps/accounts@2023-06-01' = {
  name: accountName
  location: mapsLocation
  tags: tags
  sku: {
    name: 'G2'
  }
  kind: 'Gen2'
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${userAssignedIdentityId}': {}
    }
  }
}

output mapsAccountName string = mapsAccount.name
output mapsAccountId string = mapsAccount.id
output mapsAccountClientId string = mapsAccount.properties.uniqueId
