#!/usr/bin/env pwsh
<#
.SYNOPSIS
  One-command deployment without GitHub Actions.

.DESCRIPTION
  Deploys infrastructure (Bicep), builds and pushes images, deploys Kubernetes
  manifests, installs cert-manager, applies ingress, and updates the ConfigMap.
  Designed to run locally or on a self-hosted runner.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$Environment = 'prod',

    [Parameter(Mandatory = $false)]
    [string]$Location = 'australiaeast',

    [Parameter(Mandatory = $false)]
    [string]$ProjectName = 'emergencyalerts2',

    [Parameter(Mandatory = $false)]
    [string]$SubscriptionId = '',

    [Parameter(Mandatory = $false)]
    [string]$Namespace = 'emergency-alerts',

    [Parameter(Mandatory = $false)]
    [string]$ImageTag = '',

    [Parameter(Mandatory = $false)]
    [string]$DeploymentName = '',

    [Parameter(Mandatory = $false)]
    [string]$MapsAadAppId = '',

    [Parameter(Mandatory = $false)]
    [string]$ApiUrl = '',

    [Parameter(Mandatory = $false)]
    [string]$ApiDnsLabel = '',

    [Parameter(Mandatory = $false)]
    [string]$FrontendDnsLabel = '',

    [Parameter(Mandatory = $false)]
    [string]$SignalrDnsLabel = '',

    [Parameter(Mandatory = $false)]
    [string]$AuthAllowAnonymous = 'true',

    [switch]$SkipMigrations,

    [switch]$SkipInfrastructure,
    [switch]$SkipBuild,
    [switch]$SkipKubernetes,
    [switch]$UseAcrBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not $PSScriptRoot) {
    $PSScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
}
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')

function Test-CommandExists {
    param([string]$Command)
    $null = Get-Command $Command -ErrorAction SilentlyContinue
    return $?
}

function Require-Command {
    param([string]$Command)
    if (-not (Test-CommandExists $Command)) {
        throw "Required command not found: $Command"
    }
}

function Test-DockerAvailable {
    if (-not (Test-CommandExists 'docker')) {
        return $false
    }
    $null = docker info 2>$null
    return $LASTEXITCODE -eq 0
}

function Write-Step {
    param([string]$Message)
    Write-Host ("\n== {0} ==" -f $Message) -ForegroundColor Cyan
}

# Pre-flight checks
Require-Command 'az'
if (-not $SkipBuild -and -not $UseAcrBuild) {
    Require-Command 'docker'
}
if (-not $SkipKubernetes) {
    Require-Command 'kubectl'
}

# Ensure Azure CLI logged in
try {
    $null = az account show --query id -o tsv 2>$null
} catch {
    throw 'Azure CLI is not logged in. Run: az login'
}

Push-Location $repoRoot
try {
    if (-not [string]::IsNullOrWhiteSpace($SubscriptionId)) {
        Write-Step 'Setting subscription context'
        az account set --subscription $SubscriptionId | Out-Null
    }

    $tenantId = az account show --query tenantId -o tsv
    $subscriptionId = az account show --query id -o tsv
    if ([string]::IsNullOrWhiteSpace($tenantId)) {
        throw 'Unable to determine tenant ID from Azure CLI.'
    }
    $dnsSuffix = ''
    if (-not [string]::IsNullOrWhiteSpace($subscriptionId)) {
        $dnsSuffix = ($subscriptionId -replace '-', '').Substring(0, 6)
    }

    $resourceGroupName = "${ProjectName}-${Environment}-rg"
    $identityName = "${ProjectName}-${Environment}-identity"

    # Deploy infrastructure (Bicep)
    $deploymentOutputs = $null
    if (-not $SkipInfrastructure) {
        Write-Step 'Deploying infrastructure (Bicep)'
        if ([string]::IsNullOrWhiteSpace($MapsAadAppId)) {
            throw 'MapsAadAppId is required to deploy infrastructure (Azure Maps AAD App ID).'
        }
        $deploymentName = "infra-${Environment}-$(Get-Date -Format 'yyyyMMddHHmmss')"
        $outputsJson = az deployment sub create `
            --name $deploymentName `
            --location $Location `
            --template-file infrastructure/bicep/main.bicep `
            --parameters `
              location=$Location `
              environment=$Environment `
              projectName=$ProjectName `
              mapsAadAppId=$MapsAadAppId `
            --query 'properties.outputs' `
            --output json

        $deploymentOutputs = $outputsJson | ConvertFrom-Json
    } elseif ([string]::IsNullOrWhiteSpace($DeploymentName)) {
        $DeploymentName = az deployment sub list `
            --query "[?starts_with(name, 'infra-${Environment}-')]|sort_by(@, &properties.timestamp)[-1].name" `
            -o tsv
    }

    if ($SkipInfrastructure -and -not [string]::IsNullOrWhiteSpace($DeploymentName)) {
        Write-Step "Loading deployment outputs ($DeploymentName)"
        $deploymentOutputs = az deployment sub show `
            --name $DeploymentName `
            --query 'properties.outputs' `
            -o json | ConvertFrom-Json
    }

    function Get-OutputValue {
        param([string]$Name)
        if ($null -ne $deploymentOutputs -and $null -ne $deploymentOutputs.$Name) {
            return $deploymentOutputs.$Name.value
        }
        return ''
    }

    function Get-ImageTag {
        param([string]$Image)
        if ([string]::IsNullOrWhiteSpace($Image)) {
            return ''
        }

        if ($Image -match '@') {
            return ''
        }

        $parts = $Image.Split(':')
        if ($parts.Length -lt 2) {
            return ''
        }

        return $parts[-1]
    }

    function Resolve-ApiUrlFromCluster {
        param(
            [string]$Namespace,
            [string]$Location
        )

        try {
            $dnsLabel = kubectl get svc emergency-alerts-api -n $Namespace -o jsonpath='{.metadata.annotations.service\.beta\.kubernetes\.io/azure-dns-label-name}' 2>$null
            if (-not [string]::IsNullOrWhiteSpace($dnsLabel)) {
                return "http://${dnsLabel}.${Location}.cloudapp.azure.com"
            }
        } catch {
            return ''
        }

        return ''
    }

    function Test-FrontendBundleApiUrl {
        param(
            [string]$FrontendUrl,
            [string]$ExpectedApiUrl
        )

        if ([string]::IsNullOrWhiteSpace($FrontendUrl) -or [string]::IsNullOrWhiteSpace($ExpectedApiUrl)) {
            return
        }

        try {
            $indexResponse = Invoke-WebRequest -Uri $FrontendUrl -UseBasicParsing -TimeoutSec 10
            
            # Try to extract script URLs from Content directly via regex
            $scriptMatches = [regex]::Matches($indexResponse.Content, 'assets/index-[a-zA-Z0-9]+\.js')
            if ($scriptMatches.Count -eq 0) {
                Write-Warning "No frontend bundle scripts found in index.html, skipping API URL validation."
                return
            }

            $scriptPath = $scriptMatches[0].Value
            $scriptUrl = "${FrontendUrl}/${scriptPath}"
            $bundleResponse = Invoke-WebRequest -Uri $scriptUrl -UseBasicParsing -TimeoutSec 10

            if (-not $bundleResponse.Content.Contains($ExpectedApiUrl)) {
                Write-Warning "Frontend bundle does not contain expected API URL: $ExpectedApiUrl"
                return
            }
            
            Write-Host "✅ Frontend bundle validated: contains API URL $ExpectedApiUrl"
        } catch {
            Write-Warning "Frontend bundle validation skipped: $($_.Exception.Message)"
        }
    }

    $acrName = Get-OutputValue 'acrName'
    if ([string]::IsNullOrWhiteSpace($acrName)) {
        $acrName = az acr list --resource-group $resourceGroupName --query '[0].name' -o tsv
    }
    if ([string]::IsNullOrWhiteSpace($acrName)) {
        throw 'ACR name not found. Ensure infrastructure deployment succeeded.'
    }

    $acrLoginServer = "${acrName}.azurecr.io"

    $managedIdentityClientId = Get-OutputValue 'managedIdentityClientId'
    if ([string]::IsNullOrWhiteSpace($managedIdentityClientId)) {
        $managedIdentityClientId = az identity show `
            --resource-group $resourceGroupName `
            --name $identityName `
            --query clientId -o tsv
    }
    if ([string]::IsNullOrWhiteSpace($managedIdentityClientId)) {
        throw 'Managed identity client ID not found.'
    }

    $aksName = az aks list --resource-group $resourceGroupName --query '[0].name' -o tsv
    if ([string]::IsNullOrWhiteSpace($aksName)) {
        throw 'AKS cluster name not found.'
    }

    $apiUrl = if (-not [string]::IsNullOrWhiteSpace($ApiUrl)) { $ApiUrl } else { Get-OutputValue 'apiUrl' }
    if ([string]::IsNullOrWhiteSpace($apiUrl) -and $SkipInfrastructure) {
        Write-Host 'Resolving API URL from existing AKS service...' -ForegroundColor Yellow
        az aks get-credentials `
            --resource-group $resourceGroupName `
            --name $aksName `
            --overwrite-existing | Out-Null

        $apiUrl = Resolve-ApiUrlFromCluster -Namespace $Namespace -Location $Location
    }
    if ([string]::IsNullOrWhiteSpace($apiUrl)) { $apiUrl = 'http://localhost:5000' }

    # Determine image tag
    if ([string]::IsNullOrWhiteSpace($ImageTag)) {
        if ($SkipBuild) {
            $ImageTag = "${Environment}-latest"
        } else {
            try {
                $ImageTag = (git rev-parse --short HEAD).Trim()
            } catch {
                $ImageTag = (Get-Date -Format 'yyyyMMddHHmmss')
            }
        }
    }

    if (-not $SkipBuild) {
        $currentFrontendImage = kubectl get deployment/emergency-alerts-frontend -n $Namespace -o jsonpath='{.spec.template.spec.containers[0].image}' 2>$null
        $currentFrontendTag = Get-ImageTag -Image $currentFrontendImage
        if (-not [string]::IsNullOrWhiteSpace($currentFrontendTag) -and $currentFrontendTag -eq $ImageTag) {
            throw "Frontend image tag '$ImageTag' matches currently deployed image. Use a new tag or set -SkipBuild."
        }
    }

    # Build and push images
    if (-not $SkipBuild) {
        Write-Step 'Building and pushing images'

        $useAcrBuild = $UseAcrBuild.IsPresent -or -not (Test-DockerAvailable)
        if ($useAcrBuild) {
            Write-Host 'Docker not available. Using ACR build...' -ForegroundColor Yellow

            az acr build -r $acrName `
                -t emergency-alerts-api:${ImageTag} `
                -t emergency-alerts-api:${Environment}-latest `
                -f backend/src/EmergencyAlerts.Api/Dockerfile `
                backend/

            az acr build -r $acrName `
                -t emergency-alerts-frontend:${ImageTag} `
                -t emergency-alerts-frontend:${Environment}-latest `
                --build-arg VITE_API_URL=$apiUrl `
                -f frontend/Dockerfile `
                frontend/
        } else {
            az acr login --name $acrName | Out-Null

            Write-Host 'Building backend image...'
            docker build `
                -t ${acrLoginServer}/emergency-alerts-api:${ImageTag} `
                -t ${acrLoginServer}/emergency-alerts-api:${Environment}-latest `
                -f backend/src/EmergencyAlerts.Api/Dockerfile `
                backend/

            Write-Host 'Pushing backend image...'
            docker push ${acrLoginServer}/emergency-alerts-api:${ImageTag}
            docker push ${acrLoginServer}/emergency-alerts-api:${Environment}-latest

            Write-Host 'Building frontend image...'
            docker build `
                --build-arg VITE_API_URL="$apiUrl" `
                -t ${acrLoginServer}/emergency-alerts-frontend:${ImageTag} `
                -t ${acrLoginServer}/emergency-alerts-frontend:${Environment}-latest `
                -f frontend/Dockerfile `
                frontend/

            Write-Host 'Pushing frontend image...'
            docker push ${acrLoginServer}/emergency-alerts-frontend:${ImageTag}
            docker push ${acrLoginServer}/emergency-alerts-frontend:${Environment}-latest
        }
    }

    # Deploy to AKS
    if (-not $SkipKubernetes) {
        Write-Step 'Deploying to AKS'

        az aks get-credentials `
            --resource-group $resourceGroupName `
            --name $aksName `
            --overwrite-existing | Out-Null

        # Apply deployment manifest with substitutions
        $apiDnsLabel = if (-not [string]::IsNullOrWhiteSpace($ApiDnsLabel)) { $ApiDnsLabel } elseif ([string]::IsNullOrWhiteSpace($dnsSuffix)) { "${ProjectName}-${Environment}-api" } else { "${ProjectName}-${Environment}-api-${dnsSuffix}" }
        $frontendDnsLabel = if (-not [string]::IsNullOrWhiteSpace($FrontendDnsLabel)) { $FrontendDnsLabel } elseif ([string]::IsNullOrWhiteSpace($dnsSuffix)) { "${ProjectName}-${Environment}-frontend" } else { "${ProjectName}-${Environment}-frontend-${dnsSuffix}" }
        $signalrDnsLabel = if (-not [string]::IsNullOrWhiteSpace($SignalrDnsLabel)) { $SignalrDnsLabel } elseif ([string]::IsNullOrWhiteSpace($dnsSuffix)) { "${ProjectName}-${Environment}-signalr" } else { "${ProjectName}-${Environment}-signalr-${dnsSuffix}" }
        $deploymentTemplate = Get-Content infrastructure/k8s/deployment.yaml -Raw
        $deploymentRendered = $deploymentTemplate.Replace('${ACR_NAME}', $acrName)
        $deploymentRendered = $deploymentRendered.Replace('${IMAGE_TAG}', $ImageTag)
        $deploymentRendered = $deploymentRendered.Replace('${MANAGED_IDENTITY_CLIENT_ID}', $managedIdentityClientId)
        $deploymentRendered = $deploymentRendered.Replace('${AZURE_TENANT_ID}', $tenantId)
        $deploymentRendered = $deploymentRendered.Replace('${API_DNS_LABEL}', $apiDnsLabel)
        $deploymentRendered = $deploymentRendered.Replace('${FRONTEND_DNS_LABEL}', $frontendDnsLabel)
        $deploymentRendered = $deploymentRendered.Replace('${SIGNALR_DNS_LABEL}', $signalrDnsLabel)

        $deploymentFile = Join-Path $env:TEMP 'emergency-alerts-deployment.yaml'
        $deploymentRendered | Set-Content -Path $deploymentFile -Encoding utf8

        Write-Host 'Applying base manifests...'
        kubectl apply -f $deploymentFile
        kubectl apply -f infrastructure/k8s/rbac.yaml

        # Install cert-manager (CRDs + controller) and verify
        Write-Host 'Installing cert-manager...'
        ./infrastructure/scripts/setup-aks-post-deployment.ps1 `
            -AksClusterName $aksName `
            -Environment $Environment `
            -Namespace $Namespace

        # Apply ingress (cert-manager resources included)
        Write-Host 'Applying ingress...'
        kubectl apply -f infrastructure/k8s/ingress.yaml

        # Update ConfigMap with actual Azure values
        Write-Host 'Updating ConfigMap with Azure outputs...'

        $postgresFqdn = Get-OutputValue 'postgresCoordinatorFqdn'
        $postgresDb = Get-OutputValue 'postgresDatabaseName'
        $postgresAdmin = Get-OutputValue 'postgresAdminLogin'
        if ([string]::IsNullOrWhiteSpace($postgresAdmin)) {
            $postgresAdmin = 'pgadmin'
        }
        if ([string]::IsNullOrWhiteSpace($postgresFqdn) -or [string]::IsNullOrWhiteSpace($postgresDb)) {
            throw 'PostgreSQL outputs not found. Provide -DeploymentName or run with -SkipInfrastructure:$false.'
        }
        $postgresServerName = Get-OutputValue 'postgresClusterName'
        if ([string]::IsNullOrWhiteSpace($postgresServerName)) {
            $postgresServerName = az postgres flexible-server list --resource-group $resourceGroupName --query '[0].name' -o tsv
        }

        $keyVaultName = az keyvault list --resource-group $resourceGroupName --query '[0].name' -o tsv
        $keyVaultUri = "https://${keyVaultName}.vault.azure.net/"
        $postgresPassword = az keyvault secret show --vault-name $keyVaultName --name 'postgres-admin-password' --query 'value' -o tsv

        $appConfigName = az appconfig list --resource-group $resourceGroupName --query '[0].name' -o tsv
        if ([string]::IsNullOrWhiteSpace($appConfigName)) {
            $appConfigEndpoint = 'https://placeholder.azconfig.io'
        } else {
            $appConfigEndpoint = az appconfig show --name $appConfigName --resource-group $resourceGroupName --query 'endpoint' -o tsv
        }

        $acsHost = az communication list --resource-group $resourceGroupName --query '[0].hostName' -o tsv 2>$null
        if ([string]::IsNullOrWhiteSpace($acsHost)) {
            $acsEndpoint = 'https://placeholder.communication.azure.com/'
        } else {
            $acsEndpoint = "https://${acsHost}/"
        }

        $connectionString = "Server=${postgresFqdn};Port=5432;Database=${postgresDb};Username=${postgresAdmin};Password=${postgresPassword};SSL Mode=Require;"
        $frontendUrl = "http://${frontendDnsLabel}.${Location}.cloudapp.azure.com"
        $corsAllowedOrigins = "${frontendUrl},http://localhost:5173,http://localhost:3000"

        $patchJson = @{
            data = @{
                'ConnectionStrings__EmergencyAlertsDb' = $connectionString
                'AppConfiguration__Endpoint' = $appConfigEndpoint
                'KeyVault__VaultUri' = $keyVaultUri
                'AzureCommunicationServices__Endpoint' = $acsEndpoint
                'POSTGRES_HOST' = $postgresFqdn
                'POSTGRES_PORT' = '5432'
                'POSTGRES_USER' = $postgresAdmin
                'POSTGRES_PASSWORD' = $postgresPassword
                'POSTGRES_DATABASE' = $postgresDb
                'Cors__AllowedOrigins' = $corsAllowedOrigins
                'Auth__AllowAnonymous' = $AuthAllowAnonymous
            }
        } | ConvertTo-Json -Depth 4

        $patchFile = Join-Path $env:TEMP 'configmap-patch.json'
        $patchJson | Set-Content -Path $patchFile -Encoding utf8

        kubectl patch configmap emergency-alerts-config `
            -n $Namespace `
            --type merge `
            --patch-file $patchFile

        Write-Host 'Restarting API deployment to pick up ConfigMap changes...'
        kubectl rollout restart deployment/emergency-alerts-api -n $Namespace

        if (-not $SkipMigrations) {
            Write-Host 'Preparing PostgreSQL for migrations...'
            if (-not [string]::IsNullOrWhiteSpace($postgresServerName)) {
                az postgres flexible-server parameter set `
                    --resource-group $resourceGroupName `
                    --server-name $postgresServerName `
                    --name azure.extensions `
                    --value 'postgis' | Out-Null

                $aksOutboundIpId = az aks show `
                    --resource-group $resourceGroupName `
                    --name $aksName `
                    --query "networkProfile.loadBalancerProfile.effectiveOutboundIPs[0].id" `
                    -o tsv
                if (-not [string]::IsNullOrWhiteSpace($aksOutboundIpId)) {
                    $aksOutboundIp = az network public-ip show --ids $aksOutboundIpId --query ipAddress -o tsv
                    if (-not [string]::IsNullOrWhiteSpace($aksOutboundIp)) {
                        try {
                            az postgres flexible-server firewall-rule create `
                                --resource-group $resourceGroupName `
                                --name $postgresServerName `
                                --rule-name AksOutboundIp `
                                --start-ip-address $aksOutboundIp `
                                --end-ip-address $aksOutboundIp | Out-Null
                        } catch {
                            az postgres flexible-server firewall-rule update `
                                --resource-group $resourceGroupName `
                                --name $postgresServerName `
                                --rule-name AksOutboundIp `
                                --start-ip-address $aksOutboundIp `
                                --end-ip-address $aksOutboundIp | Out-Null
                        }
                    }
                }

                $publicIp = ''
                try {
                    $publicIp = (Invoke-RestMethod -Uri 'https://api.ipify.org')
                } catch {
                    $publicIp = ''
                }
                if (-not [string]::IsNullOrWhiteSpace($publicIp)) {
                    try {
                        az postgres flexible-server firewall-rule create `
                            --resource-group $resourceGroupName `
                            --name $postgresServerName `
                            --rule-name LocalDeploy `
                            --start-ip-address $publicIp `
                            --end-ip-address $publicIp | Out-Null
                    } catch {
                        az postgres flexible-server firewall-rule update `
                            --resource-group $resourceGroupName `
                            --name $postgresServerName `
                            --rule-name LocalDeploy `
                            --start-ip-address $publicIp `
                            --end-ip-address $publicIp | Out-Null
                    }
                }
            }

            Write-Host 'Running EF Core migrations...'
            Require-Command 'dotnet'
            $migrationConnectionString = "Host=${postgresFqdn};Port=5432;Database=${postgresDb};Username=${postgresAdmin};Password=${postgresPassword};SSL Mode=Require;Trust Server Certificate=true"
            dotnet ef database update `
                --project backend/src/EmergencyAlerts.Infrastructure `
                --startup-project backend/src/EmergencyAlerts.Api `
                --connection "$migrationConnectionString"
        }

        # Deploy Drasi resources (sources → queries → reactions)
        Write-Host 'Deploying Drasi resources...'
        if (-not (Get-Command drasi -ErrorAction SilentlyContinue)) {
            throw 'Drasi CLI not found. Install Drasi CLI before running local deployment.'
        }

        drasi env kube | Out-Null

        $drasiNamespace = 'drasi-system'
        $installDrasi = $false
        if (-not (kubectl get ns $drasiNamespace -o name 2>$null)) {
            Write-Host 'Drasi namespace not found; will install Drasi.'
            $installDrasi = $true
        } else {
            $deployCount = (kubectl get deployment -n $drasiNamespace --no-headers 2>$null | Measure-Object).Count
            if ($deployCount -eq 0) {
                Write-Host 'Drasi namespace exists but no deployments found; will install Drasi.'
                $installDrasi = $true
            } else {
                drasi list source -n $drasiNamespace | Out-Null
                if ($LASTEXITCODE -ne 0) {
                    Write-Host 'Drasi API not available; will install Drasi.'
                    $installDrasi = $true
                }
            }
        }
        if ($installDrasi) {
            for ($i = 1; $i -le 3; $i++) {
                Write-Host "drasi init attempt $i/3..."
                drasi init -n $drasiNamespace
                if ($LASTEXITCODE -eq 0) {
                    break
                }
                if ($i -lt 3) {
                    Write-Host 'drasi init failed; waiting 30s before retry...'
                    Start-Sleep -Seconds 30
                }
            }
        }

        $deployCount = (kubectl get deployment -n $drasiNamespace --no-headers 2>$null | Measure-Object).Count
        if ($deployCount -eq 0) {
            for ($i = 1; $i -le 18; $i++) {
                $deployCount = (kubectl get deployment -n $drasiNamespace --no-headers 2>$null | Measure-Object).Count
                if ($deployCount -gt 0) {
                    break
                }
                Write-Host "Waiting for Drasi deployments to appear... ($i/18)"
                Start-Sleep -Seconds 10
            }
        }
        if ($deployCount -gt 0) {
            kubectl wait --for=condition=available --timeout=900s deployment --all -n $drasiNamespace | Out-Null
        } else {
            throw 'ERROR: No Drasi deployments found after waiting for control plane.'
        }

        $apiReady = $false
        for ($i = 1; $i -le 6; $i++) {
            drasi list source -n $drasiNamespace | Out-Null
            if ($LASTEXITCODE -eq 0) {
                $apiReady = $true
                break
            }
            Write-Host "Waiting for Drasi API... ($i/6)"
            Start-Sleep -Seconds 10
        }
        if (-not $apiReady) {
            throw 'ERROR: Drasi API still unavailable after init.'
        }
        $drasiBaseUrl = "http://emergency-alerts-api.${Namespace}.svc.cluster.local/api/v1/drasi/reactions"
        $drasiReactionToken = if ([string]::IsNullOrWhiteSpace($env:DRASI_REACTION_TOKEN)) { 'cluster-local' } else { $env:DRASI_REACTION_TOKEN }

        kubectl create secret generic drasi-reaction-auth `
            --from-literal=token="$drasiReactionToken" `
            -n $drasiNamespace `
            --dry-run=client -o yaml | kubectl apply -f - | Out-Null

        $sourceTemplate = Get-Content infrastructure/drasi/sources/postgres-cdc.yaml -Raw
        $sourceRendered = $sourceTemplate.Replace('${POSTGRES_HOST}', $postgresFqdn)
        $sourceRendered = $sourceRendered.Replace('${POSTGRES_PORT}', '5432')
        $sourceRendered = $sourceRendered.Replace('${POSTGRES_USER}', $postgresAdmin)
        $sourceRendered = $sourceRendered.Replace('${POSTGRES_PASSWORD}', $postgresPassword)
        $sourceRendered = $sourceRendered.Replace('${POSTGRES_DATABASE}', $postgresDb)
        $sourceFile = Join-Path $env:TEMP 'postgres-cdc-resolved.yaml'
        $sourceRendered | Set-Content -Path $sourceFile -Encoding utf8

        drasi apply -f $sourceFile -n $drasiNamespace | Out-Null

        $queryFile = 'infrastructure/drasi/queries/emergency-alerts.yaml'
        $queryLines = Get-Content -Path $queryFile
        $queryNames = @()
        for ($i = 0; $i -lt $queryLines.Count; $i++) {
            if ($queryLines[$i] -match '^\s*kind:\s*ContinuousQuery\s*$') {
                for ($j = $i + 1; $j -lt [Math]::Min($i + 6, $queryLines.Count); $j++) {
                    if ($queryLines[$j] -match '^\s*name:\s*(.+)\s*$') {
                        $queryNames += $matches[1].Trim()
                        break
                    }
                }
            }
        }
        $queryNames = $queryNames | Sort-Object -Unique

        if ($queryNames.Count -gt 0) {
            Write-Host 'Refreshing Drasi queries (delete -> apply)...'
            foreach ($queryName in $queryNames) {
                try {
                    drasi delete query $queryName -n $drasiNamespace | Out-Null
                } catch {
                    Write-Host "Warning: failed to delete Drasi query $queryName; continuing."
                }
            }
        }

        drasi apply -f $queryFile -n $drasiNamespace | Out-Null

        $reactionTemplate = Get-Content infrastructure/drasi/reactions/emergency-alerts-http.yaml -Raw
        $reactionRendered = $reactionTemplate.Replace('${DRASI_HTTP_REACTION_BASE_URL}', $drasiBaseUrl)
        $reactionRendered = $reactionRendered.Replace('${DRASI_REACTION_TOKEN}', $drasiReactionToken)
        $reactionFile = Join-Path $env:TEMP 'drasi-reactions-resolved.yaml'
        $reactionRendered | Set-Content -Path $reactionFile -Encoding utf8

        drasi apply -f $reactionFile -n $drasiNamespace | Out-Null
        Write-Host '✅ Drasi sources, queries, and reactions applied'

        # Update deployment images (idempotent)
        Write-Host 'Updating deployment images...'
        kubectl set image deployment/emergency-alerts-api `
            api=${acrLoginServer}/emergency-alerts-api:${ImageTag} `
            -n $Namespace

        if (kubectl get deployment/emergency-alerts-frontend -n $Namespace 2>$null) {
            kubectl set image deployment/emergency-alerts-frontend `
                frontend=${acrLoginServer}/emergency-alerts-frontend:${ImageTag} `
                -n $Namespace
        }

        Write-Host 'Waiting for rollout...'
        kubectl rollout status deployment/emergency-alerts-api -n $Namespace --timeout=5m

        if (kubectl get deployment/emergency-alerts-frontend -n $Namespace 2>$null) {
            kubectl rollout status deployment/emergency-alerts-frontend -n $Namespace --timeout=5m
        }

        if (kubectl get deployment/emergency-alerts-frontend -n $Namespace 2>$null) {
            Test-FrontendBundleApiUrl -FrontendUrl $frontendUrl -ExpectedApiUrl $apiUrl
        }
    }

    Write-Host "\nDeployment complete."
    Write-Host "Resource group: $resourceGroupName"
    Write-Host "AKS cluster: $aksName"
    Write-Host "ACR: $acrName"
    Write-Host "Image tag: $ImageTag"
} finally {
    Pop-Location
}
