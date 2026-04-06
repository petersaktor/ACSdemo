@description('Location for all resources.')
param location string

@description('Name prefix for all resources.')
param namePrefix string

@description('Key Vault URI for Key Vault references.')
param keyVaultUri string

@description('Cosmos DB account endpoint.')
param cosmosAccountEndpoint string

var appConfigName = '${namePrefix}-appconfig'

resource appConfig 'Microsoft.AppConfiguration/configurationStores@2023-03-01' = {
  name: appConfigName
  location: location
  sku: {
    name: 'standard'
  }
}

resource cosmosEndpointSetting 'Microsoft.AppConfiguration/configurationStores/keyValues@2023-03-01' = {
  name: 'CosmosDb:AccountEndpoint'
  parent: appConfig
  properties: {
    value: cosmosAccountEndpoint
  }
}

resource cosmosDatabaseSetting 'Microsoft.AppConfiguration/configurationStores/keyValues@2023-03-01' = {
  name: 'CosmosDb:DatabaseName'
  parent: appConfig
  properties: {
    value: 'acsdemo'
  }
}

resource cosmosContainerSetting 'Microsoft.AppConfiguration/configurationStores/keyValues@2023-03-01' = {
  name: 'CosmosDb:ContainerName'
  parent: appConfig
  properties: {
    value: 'EmailDeliveryReports'
  }
}

resource daprPubSubSetting 'Microsoft.AppConfiguration/configurationStores/keyValues@2023-03-01' = {
  name: 'Dapr:PubSubName'
  parent: appConfig
  properties: {
    value: 'pubsub'
  }
}

resource daprTopicSetting 'Microsoft.AppConfiguration/configurationStores/keyValues@2023-03-01' = {
  name: 'Dapr:TopicName'
  parent: appConfig
  properties: {
    value: 'EmailDeliveryReportReceived'
  }
}

output appConfigEndpoint string = appConfig.properties.endpoint
