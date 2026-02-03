// modules/acs.bicep
// Azure Communication Services Configuration

param acsName string
param tags object

resource acs 'Microsoft.Communication/communicationServices@2023-04-01' = {
  name: acsName
  location: 'Global'
  tags: tags
  properties: {
    dataLocation: 'europe'
    linkedDomains: [
      emailDomain.id
    ]
  }
}

// Email Service and Domain
resource emailService 'Microsoft.Communication/emailServices@2023-06-01-preview' = {
  name: '${acsName}-email'
  location: 'Global'
  tags: tags
  properties: {
    dataLocation: 'europe'
  }
}

resource emailDomain 'Microsoft.Communication/emailServices/domains@2023-06-01-preview' = {
  name: 'AzureManagedDomain'
  parent: emailService
  location: 'Global'
  tags: tags
  properties: {
    domainManagement: 'AzureManaged'
  }
}

output acsId string = acs.id
output emailServiceId string = emailService.id
output emailDomainId string = emailDomain.id
