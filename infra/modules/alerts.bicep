// Alertas básicas por correo: API con errores 5xx, salud no disponible (prueba de
// disponibilidad cada 5 minutos desde 3 regiones) y base de datos con CPU o espacio altos.

param location string
param namePrefix string
param tags object
param alertEmail string
param insightsId string
param apiAppId string
param apiHealthUrl string
param sqlDatabaseId string

resource actionGroup 'Microsoft.Insights/actionGroups@2023-01-01' = {
  name: 'ag-${namePrefix}'
  location: 'global'
  tags: tags
  properties: {
    groupShortName: 'opsynex'
    enabled: true
    emailReceivers: [
      {
        name: 'operacion'
        emailAddress: alertEmail
        useCommonAlertSchema: true
      }
    ]
  }
}

var actions = [{ actionGroupId: actionGroup.id }]

resource api5xx 'Microsoft.Insights/metricAlerts@2018-03-01' = {
  name: 'alert-${namePrefix}-api-5xx'
  location: 'global'
  tags: tags
  properties: {
    description: 'El API respondió más de 10 errores 5xx en 5 minutos.'
    severity: 2
    enabled: true
    scopes: [apiAppId]
    evaluationFrequency: 'PT1M'
    windowSize: 'PT5M'
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [
        {
          name: 'errores5xx'
          criterionType: 'StaticThresholdCriterion'
          metricNamespace: 'Microsoft.App/containerApps'
          metricName: 'Requests'
          dimensions: [{ name: 'statusCodeCategory', operator: 'Include', values: ['5xx'] }]
          operator: 'GreaterThan'
          threshold: 10
          timeAggregation: 'Total'
        }
      ]
    }
    actions: actions
  }
}

resource availability 'Microsoft.Insights/webtests@2022-06-15' = {
  name: 'webtest-${namePrefix}-api-health'
  location: location
  tags: union(tags, { 'hidden-link:${insightsId}': 'Resource' })
  kind: 'standard'
  properties: {
    SyntheticMonitorId: 'webtest-${namePrefix}-api-health'
    Name: 'API /health/ready'
    Kind: 'standard'
    Enabled: true
    Frequency: 300
    Timeout: 30
    RetryEnabled: true
    Locations: [
      { Id: 'us-va-ash-azr' }
      { Id: 'us-fl-mia-edge' }
      { Id: 'us-tx-sn1-azr' }
    ]
    Request: {
      RequestUrl: apiHealthUrl
      HttpVerb: 'GET'
    }
    ValidationRules: {
      ExpectedHttpStatusCode: 200
      SSLCheck: true
      SSLCertRemainingLifetimeCheck: 7
    }
  }
}

resource availabilityAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = {
  name: 'alert-${namePrefix}-api-availability'
  location: 'global'
  tags: tags
  properties: {
    description: 'La salud del API falla desde 2 o más regiones.'
    severity: 1
    enabled: true
    scopes: [availability.id, insightsId]
    evaluationFrequency: 'PT1M'
    windowSize: 'PT5M'
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.WebtestLocationAvailabilityCriteria'
      webTestId: availability.id
      componentId: insightsId
      failedLocationCount: 2
    }
    actions: actions
  }
}

resource sqlCpu 'Microsoft.Insights/metricAlerts@2018-03-01' = {
  name: 'alert-${namePrefix}-sql-cpu'
  location: 'global'
  tags: tags
  properties: {
    description: 'La base de datos usa más del 80 % de CPU durante 15 minutos.'
    severity: 3
    enabled: true
    scopes: [sqlDatabaseId]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT15M'
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [
        {
          name: 'cpu'
          criterionType: 'StaticThresholdCriterion'
          metricName: 'cpu_percent'
          operator: 'GreaterThan'
          threshold: 80
          timeAggregation: 'Average'
        }
      ]
    }
    actions: actions
  }
}

resource sqlStorage 'Microsoft.Insights/metricAlerts@2018-03-01' = {
  name: 'alert-${namePrefix}-sql-storage'
  location: 'global'
  tags: tags
  properties: {
    description: 'La base de datos supera el 80 % de su tamaño máximo.'
    severity: 2
    enabled: true
    scopes: [sqlDatabaseId]
    evaluationFrequency: 'PT1H'
    windowSize: 'PT1H'
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [
        {
          name: 'espacio'
          criterionType: 'StaticThresholdCriterion'
          metricName: 'storage_percent'
          operator: 'GreaterThan'
          threshold: 80
          timeAggregation: 'Maximum'
        }
      ]
    }
    actions: actions
  }
}
