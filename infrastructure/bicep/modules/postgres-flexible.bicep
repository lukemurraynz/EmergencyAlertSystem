// modules/postgres-flexible.bicep
// PostgreSQL Flexible Server for Drasi CDC and app workloads.
// Note: Flexible Server supports logical replication (required by Drasi).

param location string
param serverName string
param administratorLogin string = 'pgadmin'
@secure()
param administratorLoginPassword string
param allowAllAzureServices bool = false
param aksOutboundIpAddress string = ''
param tags object

resource postgresServer 'Microsoft.DBforPostgreSQL/flexibleServers@2024-08-01' = {
  name: serverName
  location: location
  tags: tags
  sku: {
    name: 'Standard_B1ms'
    tier: 'Burstable'
  }
  properties: {
    administratorLogin: administratorLogin
    administratorLoginPassword: administratorLoginPassword
    authConfig: {
      activeDirectoryAuth: 'Enabled'
      passwordAuth: 'Enabled'
    }
    storage: {
      storageSizeGB: 32
    }
    version: '17'
    network: {
      delegatedSubnetResourceId: null
      privateDnsZoneArmResourceId: null
    }
    maintenanceWindow: {
      customWindow: 'Enabled'
      dayOfWeek: 6
      startHour: 2
      startMinute: 0
    }
    backup: {
      backupRetentionDays: 7
      geoRedundantBackup: 'Enabled'
    }
  }
}

resource allowAzureServices 'Microsoft.DBforPostgreSQL/flexibleServers/firewallRules@2024-08-01' = if (allowAllAzureServices) {
  parent: postgresServer
  name: 'AllowAllAzureIps'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '255.255.255.255'
  }
}

resource aksOutboundFirewallRule 'Microsoft.DBforPostgreSQL/flexibleServers/firewallRules@2024-08-01' = if (!empty(aksOutboundIpAddress)) {
  parent: postgresServer
  name: 'AksOutboundIp'
  properties: {
    startIpAddress: aksOutboundIpAddress
    endIpAddress: aksOutboundIpAddress
  }
}

// Allow PostGIS extension for spatial queries (required by EF migrations)
resource allowPostgisExtension 'Microsoft.DBforPostgreSQL/flexibleServers/configurations@2024-08-01' = {
  parent: postgresServer
  name: 'azure.extensions'
  properties: {
    value: 'postgis'
  }
}

// Enable logical replication for Drasi CDC (requires server restart)
resource walLevelLogical 'Microsoft.DBforPostgreSQL/flexibleServers/configurations@2024-08-01' = {
  parent: postgresServer
  name: 'wal_level'
  properties: {
    value: 'logical'
  }
}

// Keep sensible defaults explicit for CDC slots and WAL senders.
resource maxReplicationSlots 'Microsoft.DBforPostgreSQL/flexibleServers/configurations@2024-08-01' = {
  parent: postgresServer
  name: 'max_replication_slots'
  properties: {
    value: '10'
  }
}

resource maxWalSenders 'Microsoft.DBforPostgreSQL/flexibleServers/configurations@2024-08-01' = {
  parent: postgresServer
  name: 'max_wal_senders'
  properties: {
    value: '10'
  }
}

output serverId string = postgresServer.id
output serverName string = postgresServer.name
output fullyQualifiedDomainName string = postgresServer.properties.fullyQualifiedDomainName
output databaseName string = 'postgres'
output administratorLogin string = administratorLogin
