@description('Location for all resources.')
param location string = resourceGroup().location

@description('Name prefix for all resources.')
param namePrefix string = 'acsdemo'

@description('Principal ID of the User-Assigned Managed Identity for the Container App.')
param managedIdentityPrincipalId string

@description('Resource ID of the User-Assigned Managed Identity for the Container App.')
param managedIdentityResourceId string

module cosmos 'modules/cosmos.bicep' = {
  name: 'cosmos'
  params: {
    location: location
    namePrefix: namePrefix
  }
}

module keyVault 'modules/keyvault.bicep' = {
  name: 'keyVault'
  params: {
    location: location
    namePrefix: namePrefix
    managedIdentityPrincipalId: managedIdentityPrincipalId
  }
}

module serviceBus 'modules/servicebus.bicep' = {
  name: 'serviceBus'
  params: {
    location: location
    namePrefix: namePrefix
    keyVaultName: keyVault.outputs.keyVaultName
  }
  dependsOn: [keyVault]
}

module appConfig 'modules/appconfig.bicep' = {
  name: 'appConfig'
  params: {
    location: location
    namePrefix: namePrefix
    keyVaultUri: keyVault.outputs.keyVaultUri
    cosmosAccountEndpoint: cosmos.outputs.cosmosAccountEndpoint
  }
  dependsOn: [cosmos, keyVault]
}

module containerApp 'modules/containerapp.bicep' = {
  name: 'containerApp'
  params: {
    location: location
    namePrefix: namePrefix
    appConfigEndpoint: appConfig.outputs.appConfigEndpoint
    managedIdentityPrincipalId: managedIdentityPrincipalId
    managedIdentityResourceId: managedIdentityResourceId
  }
  dependsOn: [appConfig, serviceBus]
}

output containerAppFqdn string = containerApp.outputs.containerAppFqdn
output cosmosAccountEndpoint string = cosmos.outputs.cosmosAccountEndpoint
output appConfigEndpoint string = appConfig.outputs.appConfigEndpoint
