@description('Location for all resources.')
param location string

@description('Name prefix for all resources.')
param namePrefix string

@description('Azure App Configuration endpoint.')
param appConfigEndpoint string

@description('Principal ID of the User-Assigned Managed Identity.')
param managedIdentityPrincipalId string

@description('Resource ID of the User-Assigned Managed Identity.')
param managedIdentityResourceId string

var logAnalyticsName  = '${namePrefix}-logs'
var envName           = '${namePrefix}-env'
var appName           = '${namePrefix}-api'
var acrName           = replace('${namePrefix}acr', '-', '')

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2022-10-01' = {
  name: logAnalyticsName
  location: location
  properties: {
    sku: { name: 'PerGB2018' }
    retentionInDays: 30
  }
}

resource containerAppEnv 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: envName
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalytics.properties.customerId
        sharedKey: logAnalytics.listKeys().primarySharedKey
      }
    }
  }
}

resource daprPubSub 'Microsoft.App/managedEnvironments/daprComponents@2024-03-01' = {
  name: 'pubsub'
  parent: containerAppEnv
  properties: {
    componentType: 'pubsub.azure.servicebus.topics'
    version: 'v1'
    metadata: [
      { name: 'connectionString', secretRef: 'sb-conn' }
      { name: 'topic', value: 'EmailDeliveryReportReceived' }
    ]
    scopes: [ appName ]
  }
}

resource containerApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: appName
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${managedIdentityResourceId}': {}
    }
  }
  properties: {
    environmentId: containerAppEnv.id
    configuration: {
      ingress: {
        external: true
        targetPort: 8080
        transport: 'http'
      }
      dapr: {
        enabled: true
        appId: appName
        appPort: 8080
        appProtocol: 'http'
      }
      secrets: [
        { name: 'sb-conn', keyVaultUrl: '', identity: managedIdentityResourceId }
      ]
    }
    template: {
      containers: [
        {
          name: appName
          image: '${acrName}.azurecr.io/${appName}:latest'
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
          env: [
            {
              name: 'Azure__AppConfig__Endpoint'
              value: appConfigEndpoint
            }
            {
              name: 'ASPNETCORE_ENVIRONMENT'
              value: 'Production'
            }
          ]
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 5
        rules: [
          {
            name: 'http-scaling'
            http: { metadata: { concurrentRequests: '20' } }
          }
        ]
      }
    }
  }
}

output containerAppFqdn string = containerApp.properties.configuration.ingress.fqdn
