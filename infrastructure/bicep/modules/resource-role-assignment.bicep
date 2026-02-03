// modules/resource-role-assignment.bicep
// Creates a role assignment at a specific resource scope.

param resourceName string
param principalId string
param principalName string
param roleDefinitionId string
param principalType string = 'ServicePrincipal'

resource targetResource 'Microsoft.Maps/accounts@2023-06-01' existing = {
  name: resourceName
}

resource roleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(targetResource.id, principalName, roleDefinitionId)
  scope: targetResource
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleDefinitionId)
    principalId: principalId
    principalType: principalType
  }
}
