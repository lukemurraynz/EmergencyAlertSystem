// main.bicep
// Emergency Alert Core System Infrastructure

targetScope = 'subscription'

// Parameters
@description('Azure region for deployment. Defaults to Australia East.')
@allowed([
  'australiaeast'
  'australiasoutheast'
  'eastus'
  'westus2'
  'westeurope'
  'northeurope'
  'uksouth'
  'ukwest'
])
param location string = 'australiaeast'

@description('Deployment environment name.')
@allowed([
  'dev'
  'staging'
  'prod'
])
param environment string = 'dev'

@description('Project name used for resource naming. Must be lowercase.')
@minLength(3)
@maxLength(20)
param projectName string = 'emergency-alerts'

@secure()
@description('PostgreSQL admin password. Auto-generated if not provided.')
param databaseAdminPassword string = ''
// Optional: create application schema during deployment (data-plane)
param createSchema bool = false
param schemaName string = 'emergency_alerts'
// AKS-related parameters
param aksOutboundIpAddress string = '' // Will be obtained from AKS LoadBalancer public IP
param allowAllAzureServicesPostgres bool = false // Set to false for production, true for development
param kubernetesNamespace string = 'emergency-alerts'
param kubernetesServiceAccountName string = 'emergency-alerts-sa'
param mapsAadAppId string
@description('Comma-separated test recipient email addresses for alert delivery.')
param emailTestRecipients string = 'luke@luke.geek.nz'
// Allow overriding the federated credential name to ensure idempotency
param federatedCredentialName string = '${projectName}-aks-federation'
// Skip ACR pull role assignment if it already exists (useful for existing clusters)
param skipAcrPullAssignment bool = false

// Metadata
metadata description = 'Emergency Alert Core System - Main Infrastructure Deployment'
metadata author = 'Emergency Alerts Team'

// Variables
var resourceGroupName = '${projectName}-${environment}-rg'
var tags = {
  environment: environment
  project: projectName
  createdBy: 'bicep'
}
var acsName = '${projectName}-${environment}-acs'
var managedIdentityName = '${projectName}-${environment}-identity'
var acsEmailSenderRoleId = '09976791-48a7-449e-bb21-39d1a415f350'
var subscriptionSuffix = take(replace(subscription().subscriptionId, '-', ''), 6)
var apiDnsLabel = '${projectName}-${environment}-api-${subscriptionSuffix}'
var apiHost = '${apiDnsLabel}.${location}.cloudapp.azure.com'
// Global-unique names for resources with global namespaces
var keyVaultName = toLower('${take(projectName, 6)}${take(environment, 3)}kv${uniqueString(rg.id)}')
var appConfigName = '${projectName}-${environment}-config-${uniqueString(rg.id)}'
// Azure Maps only supports specific regions: westcentralus, global, westus2, eastus, westeurope, northeurope
// Map the deployment location to a supported Maps region when needed
var mapsSupportedRegions = [
  'westcentralus'
  'global'
  'westus2'
  'eastus'
  'westeurope'
  'northeurope'
]
var mapsLocation = contains(mapsSupportedRegions, toLower(location)) ? toLower(location) : 'westeurope'
// Compute the admin password consistently for both module and deployment script
var adminPassword = empty(databaseAdminPassword) ? 'P@ssw0rd-${uniqueString(rg.id)}' : databaseAdminPassword

// Create Resource Group
resource rg 'Microsoft.Resources/resourceGroups@2023-07-01' = {
  name: resourceGroupName
  location: location
  tags: tags
}

// User-Assigned Managed Identity for AKS
module managedIdentity 'modules/managed-identity.bicep' = {
  scope: rg
  name: 'managedIdentity-${uniqueString(rg.id)}'
  params: {
    location: location
    identityName: managedIdentityName
    tags: tags
  }
}

// Key Vault
module keyVault 'modules/keyvault.bicep' = {
  scope: rg
  name: 'keyVault-${uniqueString(rg.id)}'
  params: {
    location: location
    keyVaultName: keyVaultName
    managedIdentityObjectId: managedIdentity.outputs.identityPrincipalId
    adminPassword: adminPassword
    tags: tags
  }
}

// Azure Maps Account
module mapsAccount 'modules/maps-account.bicep' = {
  scope: rg
  name: 'mapsAccount-${uniqueString(rg.id)}'
  params: {
    mapsLocation: mapsLocation
    accountName: '${projectName}-${environment}-maps'
    tags: tags
    userAssignedIdentityId: managedIdentity.outputs.identityId
  }
}

// Grant Azure Maps Data Reader to the managed identity (RBAC-based Maps access)
module mapsDataReaderRoleAssignment 'modules/resource-role-assignment.bicep' = {
  scope: rg
  name: 'mapsDataReaderRoleAssignment-${uniqueString(rg.id)}'
  params: {
    resourceName: mapsAccount.outputs.mapsAccountName
    principalId: managedIdentity.outputs.identityPrincipalId
    principalName: managedIdentityName
    roleDefinitionId: '423170ca-a8f6-4b0f-8487-9e4eb8f49bfa' // Azure Maps Data Reader
  }
}

// App Configuration
module appConfig 'modules/appconfig.bicep' = {
  scope: rg
  name: 'appConfig-${uniqueString(rg.id)}'
  params: {
    location: location
    configName: appConfigName
    managedIdentityObjectId: managedIdentity.outputs.identityPrincipalId
    tags: tags
  }
}

// Admin password will be stored as a secret by the Key Vault module when provided
// PostgreSQL Flexible Server for Drasi CDC and app workloads
module postgresFlexible 'modules/postgres-flexible.bicep' = {
  scope: rg
  name: 'postgresFlexible-${uniqueString(rg.id)}'
  params: {
    location: location
    serverName: '${projectName}-${environment}-pg'
    administratorLogin: 'pgadmin'
    // Password must be 8-256 chars with 3 types: lower, upper, number, symbol
    administratorLoginPassword: adminPassword
    // Use AKS outbound IP if provided (production), otherwise fallback to Azure services (development)
    aksOutboundIpAddress: !empty(aksOutboundIpAddress) ? aksOutboundIpAddress : ''
    allowAllAzureServices: empty(aksOutboundIpAddress) && allowAllAzureServicesPostgres
    tags: tags
  }
}

// Optional: Create application schema via module (resource-group scope)
module schemaInit 'modules/schema-init.bicep' = if (createSchema) {
  scope: rg
  name: 'schemaInit-${uniqueString(rg.id, schemaName)}'
  params: {
    location: location
    dbHost: postgresFlexible.outputs.fullyQualifiedDomainName
    schemaName: schemaName
    identityId: managedIdentity.outputs.identityId
    keyVaultName: keyVault.outputs.keyVaultName
    dbAdminUser: postgresFlexible.outputs.administratorLogin
  }
}

// Azure Communication Services
module acs 'modules/acs.bicep' = {
  scope: rg
  name: 'acs-${uniqueString(rg.id)}'
  params: {
    acsName: acsName
    tags: tags
  }
}

module acsEmailSenderRoleAssignment 'modules/acs-rbac.bicep' = {
  scope: rg
  name: 'acsEmailSenderRoleAssignment-${uniqueString(rg.id)}'
  params: {
    acsName: acsName
    principalId: managedIdentity.outputs.identityPrincipalId
    principalName: managedIdentityName
    roleDefinitionId: acsEmailSenderRoleId
  }
}

// Email Service scope role assignment for sender-username operations
module emailServiceRoleAssignment 'modules/emailservice-rbac.bicep' = {
  scope: rg
  name: 'emailServiceRoleAssignment-${uniqueString(rg.id)}'
  params: {
    emailServiceName: '${acsName}-email'
    principalId: managedIdentity.outputs.identityPrincipalId
    principalName: managedIdentityName
    roleDefinitionId: acsEmailSenderRoleId
  }
}

// Reader access for deployment scripts to query ACS resources
module managedIdentityReader 'modules/rg-role-assignment.bicep' = {
  scope: rg
  name: 'managedIdentityReader-${uniqueString(rg.id)}'
  params: {
    principalId: managedIdentity.outputs.identityPrincipalId
    principalName: managedIdentityName
    roleDefinitionId: 'acdd72a7-3385-48ef-bd42-f606fba81ae7' // Reader
  }
}

// Populate App Configuration with ACS sender address after domain provisioning
module appConfigEmailSender 'modules/appconfig-email-sender.bicep' = {
  scope: rg
  name: 'appConfigEmailSender-${uniqueString(rg.id)}'
  params: {
    location: location
    resourceGroupName: rg.name
    appConfigName: appConfig.outputs.configName
    acsName: acsName
    identityId: managedIdentity.outputs.identityId
    emailDomainName: 'AzureManagedDomain'
    senderUsername: 'alerts'
    senderDisplayName: 'Emergency Alerts'
    testRecipients: emailTestRecipients
    mapsAccountName: mapsAccount.outputs.mapsAccountName
    mapsClientId: mapsAccount.outputs.mapsAccountClientId
    mapsAadAppId: mapsAadAppId
    mapsAadTenantId: subscription().tenantId
    mapsAuthMode: 'sas'
  }
}

// Azure Container Registry (ACR)
module acr 'modules/acr.bicep' = {
  scope: rg
  name: 'acr-${uniqueString(rg.id)}'
  params: {
    location: location
    registryName: '${replace(projectName, '-', '')}${environment}acr'
    managedIdentityObjectId: managedIdentity.outputs.identityPrincipalId
    tags: tags
  }
}

// AKS Cluster
module aks 'modules/aks.bicep' = {
  scope: rg
  name: 'aks-${uniqueString(rg.id)}'
  params: {
    location: location
    clusterName: '${projectName}-${environment}-aks'
    managedIdentityResourceId: managedIdentity.outputs.identityId
    tags: tags
  }
}

// Allow AKS kubelet identity to pull images from ACR
module aksAcrPull 'modules/aks-acr-pull.bicep' = if (!skipAcrPullAssignment) {
  scope: rg
  name: 'aksAcrPull-${uniqueString(rg.id)}'
  params: {
    aksClusterId: aks.outputs.clusterId
    acrName: acr.outputs.registryName
  }
}

// Workload Identity Federation: Links AKS OIDC to Azure Managed Identity
// This enables keyless pod authentication to Azure services
module workloadIdentityFederation 'modules/workload-identity-federation.bicep' = {
  scope: rg
  name: 'workloadIdentityFederation-${uniqueString(rg.id)}'
  params: {
    managedIdentityName: managedIdentity.outputs.identityName
    aksOidcIssuerUrl: aks.outputs.oidcIssuerUrl
    kubernetesNamespace: kubernetesNamespace
    serviceAccountName: kubernetesServiceAccountName
    federatedCredentialName: federatedCredentialName
  }
}

// Outputs
output resourceGroupName string = rg.name
output resourceGroupId string = rg.id
output managedIdentityId string = managedIdentity.outputs.identityId
output managedIdentityClientId string = managedIdentity.outputs.identityClientId
output aksClusterId string = aks.outputs.clusterId
output aksClusterFqdn string = aks.outputs.fqdn
output acrName string = acr.outputs.registryName
output acrUrl string = acr.outputs.registryUrl
output apiUrl string = 'http://${apiHost}'
output postgresClusterName string = postgresFlexible.outputs.serverName
output postgresCoordinatorFqdn string = postgresFlexible.outputs.fullyQualifiedDomainName
output postgresDatabaseName string = postgresFlexible.outputs.databaseName
output postgresAdminLogin string = postgresFlexible.outputs.administratorLogin
