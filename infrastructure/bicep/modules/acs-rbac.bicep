// modules/acs-rbac.bicep
// Assign RBAC role to managed identity at ACS resource scope.

param acsName string
param principalId string
param principalName string
param roleDefinitionId string

resource acsResource 'Microsoft.Communication/communicationServices@2023-04-01' existing = {
  name: acsName
}

resource acsRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(resourceGroup().id, principalName, roleDefinitionId, acsName)
  scope: acsResource
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleDefinitionId)
    principalId: principalId
    principalType: 'ServicePrincipal'
  }
}
