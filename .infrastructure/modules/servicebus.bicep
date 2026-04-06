@description('Location for all resources.')
param location string

@description('Name prefix for all resources.')
param namePrefix string

@description('Key Vault resource ID for storing secrets.')
param keyVaultName string

var namespaceName = '${namePrefix}-sb'
var topicName = 'EmailDeliveryReportReceived'
var subscriptionName = 'acsdemo-api'

resource serviceBusNamespace 'Microsoft.ServiceBus/namespaces@2022-10-01-preview' = {
  name: namespaceName
  location: location
  sku: {
    name: 'Standard'
    tier: 'Standard'
  }
}

resource topic 'Microsoft.ServiceBus/namespaces/topics@2022-10-01-preview' = {
  name: topicName
  parent: serviceBusNamespace
}

resource subscription 'Microsoft.ServiceBus/namespaces/topics/subscriptions@2022-10-01-preview' = {
  name: subscriptionName
  parent: topic
  properties: {
    lockDuration: 'PT1M'
    maxDeliveryCount: 10
    defaultMessageTimeToLive: 'P14D'
  }
}

resource sbConnectionStringSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  name: '${keyVaultName}/ServiceBus--ConnectionString'
  properties: {
    value: serviceBusNamespace.listKeys().primaryConnectionString
  }
}

output serviceBusNamespaceName string = serviceBusNamespace.name
output topicName string = topic.name
