// Relé para las partidas en línea de Punkartuchera.
// Crea: Azure SignalR Service (Free, modo sin servidor), una Function App en el plan Flex Consumption,
// su cuenta de almacenamiento (paquete de despliegue, temporizador y tabla de partidas),
// Application Insights para ver lo que pasa y un aviso de gasto.
// Nada usa claves: la Function App entra en el almacenamiento y en SignalR con una identidad administrada.

targetScope = 'resourceGroup'

@description('Región de los recursos. Tiene que admitir el plan Flex Consumption.')
param location string = resourceGroup().location

@description('Prefijo corto para los nombres (minúsculas y números).')
@minLength(3)
@maxLength(12)
param prefix string = 'punkrele'

@description('Orígenes que pueden llamar al relé: la web publicada y, si quieres, la de pruebas en el PC.')
param allowedOrigins array = [
  'https://yametekds.github.io'
]

@description('Correo para el aviso de gasto. Vacío = sin aviso.')
param alertEmail string = ''

@description('Gasto mensual (en la moneda de la suscripción) a partir del cual se avisa.')
param monthlyBudget int = 5

@description('Primer día del mes en curso; lo rellena Azure solo.')
param budgetStartDate string = utcNow('yyyy-MM-01')

var token = toLower(uniqueString(resourceGroup().id, prefix))
var appName = '${prefix}-${token}'
var deploymentContainer = 'despliegue'

// Roles integrados de Azure (identificadores fijos).
var roles = {
  storageBlobDataOwner: 'b7e6dc6d-f1e8-4753-8033-0f276bb0955b'
  storageQueueDataContributor: '974c5e8b-45b9-4653-ba55-5f855dd0fb88'
  storageTableDataContributor: '0a9a7e1f-b9d0-4cc4-a60d-0319b160aaa3'
  monitoringMetricsPublisher: '3913510d-42f4-4e42-8a64-420c390055eb'
  signalRAppServer: '420fcaa2-552c-430f-98ca-3264be4806c7'
}

resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: 'id-${appName}'
  location: location
}

resource logs 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: 'log-${appName}'
  location: location
  properties: {
    retentionInDays: 30
    sku: { name: 'PerGB2018' }
    workspaceCapping: { dailyQuotaGb: 1 }
  }
}

resource insights 'Microsoft.Insights/components@2020-02-02' = {
  name: 'appi-${appName}'
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logs.id
    DisableLocalAuth: true
  }
}

resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: take('st${replace(prefix, '-', '')}${token}', 24)
  location: location
  kind: 'StorageV2'
  sku: { name: 'Standard_LRS' }
  properties: {
    accessTier: 'Hot'
    allowBlobPublicAccess: false
    allowSharedKeyAccess: false
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
    publicNetworkAccess: 'Enabled'
  }

  resource blobs 'blobServices' = {
    name: 'default'
    resource container 'containers' = {
      name: deploymentContainer
      properties: { publicAccess: 'None' }
    }
  }

  resource tables 'tableServices' = {
    name: 'default'
    resource matches 'tables' = {
      name: 'partidas'
    }
  }
}

resource signalR 'Microsoft.SignalRService/signalR@2024-03-01' = {
  name: 'sigr-${appName}'
  location: location
  kind: 'SignalR'
  sku: {
    name: 'Free_F1'
    tier: 'Free'
    capacity: 1
  }
  properties: {
    features: [
      { flag: 'ServiceMode', value: 'Serverless' }
    ]
    cors: { allowedOrigins: allowedOrigins }
    disableLocalAuth: true
    publicNetworkAccess: 'Enabled'
  }
}

resource plan 'Microsoft.Web/serverfarms@2024-04-01' = {
  name: 'plan-${appName}'
  location: location
  kind: 'functionapp'
  sku: {
    tier: 'FlexConsumption'
    name: 'FC1'
  }
  properties: { reserved: true }
}

resource app 'Microsoft.Web/sites@2024-04-01' = {
  name: appName
  location: location
  kind: 'functionapp,linux'
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: { '${identity.id}': {} }
  }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    siteConfig: {
      minTlsVersion: '1.2'
      ftpsState: 'Disabled'
      http20Enabled: true
      cors: {
        allowedOrigins: allowedOrigins
        supportCredentials: false
      }
    }
    functionAppConfig: {
      deployment: {
        storage: {
          type: 'blobContainer'
          value: '${storage.properties.primaryEndpoints.blob}${deploymentContainer}'
          authentication: {
            type: 'UserAssignedIdentity'
            userAssignedIdentityResourceId: identity.id
          }
        }
      }
      scaleAndConcurrency: {
        // El mínimo que admite el plan. Para un grupo de colegas sobra con una instancia.
        maximumInstanceCount: 40
        instanceMemoryMB: 2048
      }
      runtime: {
        name: 'dotnet-isolated'
        version: '10.0'
      }
    }
  }

  resource settings 'config' = {
    name: 'appsettings'
    properties: {
      AzureWebJobsStorage__accountName: storage.name
      AzureWebJobsStorage__credential: 'managedidentity'
      AzureWebJobsStorage__clientId: identity.properties.clientId
      APPLICATIONINSIGHTS_CONNECTION_STRING: insights.properties.ConnectionString
      APPLICATIONINSIGHTS_AUTHENTICATION_STRING: 'ClientId=${identity.properties.clientId};Authorization=AAD'
      // DefaultAzureCredential usa esta identidad para la tabla y para SignalR.
      AZURE_CLIENT_ID: identity.properties.clientId
      Relay__TablesEndpoint: storage.properties.primaryEndpoints.table
      Relay__SignalREndpoint: 'https://${signalR.properties.hostName}'
    }
  }
}

// Permisos de la identidad: solo lo que necesita y solo sobre su recurso.
resource blobOwner 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storage.id, identity.id, roles.storageBlobDataOwner)
  scope: storage
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.storageBlobDataOwner)
    principalId: identity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource queueContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storage.id, identity.id, roles.storageQueueDataContributor)
  scope: storage
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.storageQueueDataContributor)
    principalId: identity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource tableContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storage.id, identity.id, roles.storageTableDataContributor)
  scope: storage
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.storageTableDataContributor)
    principalId: identity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource metricsPublisher 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(insights.id, identity.id, roles.monitoringMetricsPublisher)
  scope: insights
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.monitoringMetricsPublisher)
    principalId: identity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource signalRServer 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(signalR.id, identity.id, roles.signalRAppServer)
  scope: signalR
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.signalRAppServer)
    principalId: identity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource budget 'Microsoft.Consumption/budgets@2023-11-01' = if (!empty(alertEmail)) {
  name: 'presupuesto-${appName}'
  properties: {
    category: 'Cost'
    amount: monthlyBudget
    timeGrain: 'Monthly'
    timePeriod: { startDate: budgetStartDate }
    notifications: {
      gastoReal: {
        enabled: true
        operator: 'GreaterThan'
        threshold: 50
        contactEmails: [ alertEmail ]
        thresholdType: 'Actual'
      }
      previsto: {
        enabled: true
        operator: 'GreaterThan'
        threshold: 100
        contactEmails: [ alertEmail ]
        thresholdType: 'Forecasted'
      }
    }
  }
}

@description('Nombre de la Function App, para publicar el código.')
output functionAppName string = app.name

@description('Dirección del relé: va en Relay:BaseUrl de la app (variable RELAY_URL del repositorio).')
output relayUrl string = 'https://${app.properties.defaultHostName}/'
