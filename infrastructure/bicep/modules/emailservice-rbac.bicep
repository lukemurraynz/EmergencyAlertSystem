// modules/emailservice-rbac.bicep
// Assign RBAC role to managed identity at Email Service resource scope.

param emailServiceName string
param principalId string
param principalName string
param roleDefinitionId string

resource emailService 'Microsoft.Communication/emailServices@2023-04-01' existing = {
  name: emailServiceName
}

resource emailServiceRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(resourceGroup().id, principalName, roleDefinitionId, emailServiceName)
  scope: emailService
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleDefinitionId)
    principalId: principalId
    principalType: 'ServicePrincipal'
  }
}
