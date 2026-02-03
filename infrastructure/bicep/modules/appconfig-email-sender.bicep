// modules/appconfig-email-sender.bicep
// Deployment script to populate Email:SenderAddress in App Configuration

param location string
param resourceGroupName string
param appConfigName string
param acsName string
param identityId string
param emailDomainName string = 'AzureManagedDomain'
param senderUsername string = 'alerts'
param senderDisplayName string = 'Emergency Alerts'
@description('Comma-separated test recipient email addresses for alert delivery.')
param testRecipients string = 'luke@luke.geek.nz'
param mapsAccountName string
param mapsClientId string
param mapsAadAppId string
param mapsAadTenantId string
param mapsAuthMode string = 'sas'

var emailServiceName = '${acsName}-email'
var emailDomainResourceId = resourceId('Microsoft.Communication/emailServices/domains', emailServiceName, emailDomainName)
var fromSenderDomain = reference(emailDomainResourceId, '2023-03-31').fromSenderDomain

resource setEmailSender 'Microsoft.Resources/deploymentScripts@2023-08-01' = {
  name: 'set-email-sender-${uniqueString(resourceGroupName, appConfigName, acsName)}'
  location: location
  kind: 'AzureCLI'
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${identityId}': {}
    }
  }
  properties: {
    azCliVersion: '2.80.0'
    timeout: 'PT30M'
    cleanupPreference: 'OnSuccess'
    retentionInterval: 'P1D'
    environmentVariables: [
      {
        name: 'SUBSCRIPTION_ID'
        value: subscription().subscriptionId
      }
      {
        name: 'FROM_SENDER_DOMAIN'
        value: fromSenderDomain
      }
      {
        name: 'RESOURCE_GROUP'
        value: resourceGroupName
      }
      {
        name: 'APP_CONFIG_NAME'
        value: appConfigName
      }
      {
        name: 'ACS_NAME'
        value: acsName
      }
      {
        name: 'EMAIL_DOMAIN_NAME'
        value: emailDomainName
      }
      {
        name: 'SENDER_USERNAME'
        value: senderUsername
      }
      {
        name: 'SENDER_DISPLAY_NAME'
        value: senderDisplayName
      }
      {
        name: 'EMAIL_TEST_RECIPIENTS'
        value: testRecipients
      }
      {
        name: 'MAPS_ACCOUNT_NAME'
        value: mapsAccountName
      }
      {
        name: 'MAPS_CLIENT_ID'
        value: mapsClientId
      }
      {
        name: 'MAPS_AAD_APP_ID'
        value: mapsAadAppId
      }
      {
        name: 'MAPS_AAD_TENANT_ID'
        value: mapsAadTenantId
      }
      {
        name: 'MAPS_AUTH_MODE'
        value: mapsAuthMode
      }
    ]
    scriptContent: '''#!/usr/bin/env bash
set -euo pipefail

az config set extension.use_dynamic_install=yes_without_prompt >/dev/null
az config set extension.dynamic_install_allow_preview=true >/dev/null

echo "Ensuring Azure CLI communication extension..."
az extension add --name communication --version 1.14.0 --upgrade

EMAIL_SERVICE_NAME="${ACS_NAME}-email"

echo "Resolving Azure-managed email domain for ${EMAIL_SERVICE_NAME}..."
DOMAIN_RESOURCE_ID="/subscriptions/${SUBSCRIPTION_ID}/resourceGroups/${RESOURCE_GROUP}/providers/Microsoft.Communication/emailServices/${EMAIL_SERVICE_NAME}/domains/${EMAIL_DOMAIN_NAME}"
DOMAIN_NAME="${FROM_SENDER_DOMAIN:-}"
if [ -n "${DOMAIN_NAME}" ] && [ "${DOMAIN_NAME}" != "AzureManagedDomain" ]; then
  echo "Resolved domain from ARM: ${DOMAIN_NAME}"
else
  DOMAIN_NAME=""
  for i in {1..90}; do
    DOMAIN_JSON="$(az resource show --ids "${DOMAIN_RESOURCE_ID}" --api-version 2023-03-31 -o json 2>/dev/null || true)"

    if [ -n "${DOMAIN_JSON}" ]; then
      DOMAIN_NAME="$(python - <<'PY'
import json,sys
try:
    data=json.load(sys.stdin)
except Exception:
    print("")
    raise SystemExit(0)
props=data.get("properties") or {}
value = (
    props.get("fromSenderDomain")
    or props.get("mailFromSenderDomain")
    or data.get("fromSenderDomain")
    or data.get("mailFromSenderDomain")
    or props.get("domainName")
    or data.get("domainName")
    or ""
)
print(value)
PY
      <<<"${DOMAIN_JSON}")"
    fi

    if [ -n "${DOMAIN_NAME}" ] && [ "${DOMAIN_NAME}" != "AzureManagedDomain" ]; then
      echo "Resolved domain: ${DOMAIN_NAME}"
      break
    fi

    echo "Domain not ready yet, retrying (${i}/90)..."
    sleep 10
  done
fi

if [ -z "${DOMAIN_NAME}" ] || [ "${DOMAIN_NAME}" = "AzureManagedDomain" ]; then
  echo "ERROR: Azure-managed domain name not available."
  exit 1
fi

SENDER_ADDRESS="${SENDER_USERNAME}@${DOMAIN_NAME}"

echo "Ensuring email sender exists..."
az communication email domain sender-username create \
  --resource-group "${RESOURCE_GROUP}" \
  --email-service-name "${EMAIL_SERVICE_NAME}" \
  --domain-name "${EMAIL_DOMAIN_NAME}" \
  --sender-username "${SENDER_USERNAME}" \
  --username "${SENDER_USERNAME}" \
  --display-name "${SENDER_DISPLAY_NAME}" \
  -o none || {
    echo "Failed to create email sender. Listing existing senders for diagnostics..."
    az communication email domain sender-username list \
      --resource-group "${RESOURCE_GROUP}" \
      --email-service-name "${EMAIL_SERVICE_NAME}" \
      --domain-name "${EMAIL_DOMAIN_NAME}" \
      -o table || true
    exit 1
  }

echo "Setting Email:SenderAddress and Email:DeliveryMode in App Configuration..."
az appconfig kv set --name "${APP_CONFIG_NAME}" --key "Email:SenderAddress" --value "${SENDER_ADDRESS}" --auth-mode login --yes
az appconfig kv set --name "${APP_CONFIG_NAME}" --key "Email:DeliveryMode" --value "Acs" --auth-mode login --yes
az appconfig kv set --name "${APP_CONFIG_NAME}" --key "Email:TestRecipients" --value "${EMAIL_TEST_RECIPIENTS}" --auth-mode login --yes

echo "Setting Azure Communication Services endpoint in App Configuration..."
az appconfig kv set --name "${APP_CONFIG_NAME}" --key "AzureCommunicationServices:Endpoint" --value "https://${ACS_NAME}.communication.azure.com/" --auth-mode login --yes

echo "Setting Maps configuration in App Configuration..."
az appconfig kv set --name "${APP_CONFIG_NAME}" --key "Maps:AccountName" --value "${MAPS_ACCOUNT_NAME}" --auth-mode login --yes
az appconfig kv set --name "${APP_CONFIG_NAME}" --key "Maps:AadClientId" --value "${MAPS_CLIENT_ID}" --auth-mode login --yes
az appconfig kv set --name "${APP_CONFIG_NAME}" --key "Maps:AuthMode" --value "${MAPS_AUTH_MODE}" --auth-mode login --yes
az appconfig kv set --name "${APP_CONFIG_NAME}" --key "Maps:AadAppId" --value "${MAPS_AAD_APP_ID}" --auth-mode login --yes
az appconfig kv set --name "${APP_CONFIG_NAME}" --key "Maps:AadTenantId" --value "${MAPS_AAD_TENANT_ID}" --auth-mode login --yes

echo "Email sender address set to ${SENDER_ADDRESS}"
'''
  }
}
