// modules/managed-identity.bicep
// User-Assigned Managed Identity for AKS

param location string
param identityName string
param tags object

resource managedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: identityName
  location: location
  tags: tags
}

output identityId string = managedIdentity.id
output identityName string = managedIdentity.name
output identityPrincipalId string = managedIdentity.properties.principalId
output identityClientId string = managedIdentity.properties.clientId
