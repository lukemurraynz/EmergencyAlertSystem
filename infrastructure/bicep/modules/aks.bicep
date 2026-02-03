// modules/aks.bicep
// Azure Kubernetes Service (AKS) Cluster Configuration

param location string
param clusterName string
param managedIdentityResourceId string
param tags object

// AKS Cluster
resource aksCluster 'Microsoft.ContainerService/managedClusters@2025-05-01' = {
  name: clusterName
  location: location
  tags: tags
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${managedIdentityResourceId}': {}
    }
  }
  properties: {
    dnsPrefix: clusterName
    kubernetesVersion: '1.33'
    
    // Enable workload identity
    oidcIssuerProfile: {
      enabled: true
    }
    
    agentPoolProfiles: [
      {
        name: 'default'
        count: 3
        vmSize: 'Standard_D4s_v3'
        maxCount: 10
        minCount: 1
        enableAutoScaling: true
        mode: 'System'
        osSKU: 'Ubuntu'
        type: 'VirtualMachineScaleSets'
        maxPods: 30
      }
    ]
    
    networkProfile: {
      networkPlugin: 'azure'
      networkPolicy: 'azure'
      serviceCidrs: ['10.0.0.0/16']
      dnsServiceIP: '10.0.0.10'
    }
    
    addonProfiles: {
      httpApplicationRouting: {
        enabled: false
      }
      omsagent: {
        enabled: false
      }
    }
    
    securityProfile: {
      defender: {
        securityMonitoring: {
          enabled: false
        }
      }
      workloadIdentity: {
        enabled: true
      }
    }
  }
}

// Outputs
output clusterId string = aksCluster.id
output clusterName string = aksCluster.name
output oidcIssuerUrl string = aksCluster.properties.oidcIssuerProfile.issuerURL ?? ''
output fqdn string = aksCluster.properties.fqdn
output dnsPrefix string = aksCluster.properties.dnsPrefix
