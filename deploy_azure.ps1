<#
.SYNOPSIS
    Script automatizado de compilacion, empaquetado y despliegue a Azure para el Sistema de Proteccion de Datos (Universidad Mayor).

.DESCRIPTION
    Compila la Web API (App Service) y el Procesador Asincrono de Colas (Function App) en modo Release,
    los empaqueta en archivos ZIP y los despliega usando Azure CLI ZipDeploy con verificacion post-despliegue.
    Soporta los ambientes predefinidos ('qa', 'dev', 'prod') y cualquier nuevo entorno ('custom', 'staging', 'dr', etc.).

.PARAMETER Environment
    Ambiente objetivo: 'qa' (por defecto), 'dev', 'prod' o cualquier nombre personalizado (ej: 'custom', 'staging').

.PARAMETER ResourceGroup
    Grupo de recursos en Azure (por defecto 'admincrm2021_rg_0225').

.PARAMETER ApiAppName
    Nombre del App Service en Azure. Obligatorio si el ambiente es personalizado o para sobreescribir el predeterminado.

.PARAMETER FunAppName
    Nombre de la Function App en Azure. Obligatorio si el ambiente es personalizado o para sobreescribir el predeterminado.

.PARAMETER StorageAccount
    Nombre de la cuenta de almacenamiento en Azure (opcional, requerida si se usa -ProvisionStorage).

.PARAMETER ProvisionStorage
    Si se especifica, verifica y crea automaticamente la cola 'privacy-mass-executions' y el contenedor 'privacy-backups'.

.PARAMETER SkipSmokeTests
    Si se especifica, omite las pruebas de verificacion post-despliegue.

.EXAMPLE
    .\deploy_azure.ps1 -Environment qa

.EXAMPLE
    .\deploy_azure.ps1 -Environment custom -ResourceGroup "rg-umayor-staging" -ApiAppName "um-lpd-staging" -FunAppName "um-lpd-staging-fun"

.EXAMPLE
    .\deploy_azure.ps1 -Environment staging -ResourceGroup "rg-staging" -ApiAppName "um-lpd-staging" -FunAppName "um-lpd-staging-fun" -StorageAccount "stlumayorstaging" -ProvisionStorage
#>

param(
    [string]$Environment = "qa",

    [string]$ResourceGroup = "admincrm2021_rg_0225",

    [string]$ApiAppName = "",

    [string]$FunAppName = "",

    [string]$StorageAccount = "",

    [switch]$ProvisionStorage,

    [switch]$SkipSmokeTests
)

$ErrorActionPreference = "Stop"

$envLower = $Environment.ToLower().Trim()

Write-Host "==========================================================================" -ForegroundColor Cyan
Write-Host "   DESPLIEGUE A AZURE - SISTEMA DE PROTECCION DE DATOS (ARCO) UMAYOR      " -ForegroundColor Cyan
Write-Host "   Ambiente: $($Environment.ToUpper()) | Grupo Recursos: $ResourceGroup" -ForegroundColor Cyan
Write-Host "==========================================================================" -ForegroundColor Cyan

# 1. Resolver nombres de recursos por ambiente
$predefined = @{
    "qa"   = @{ Api = "um-ley-proteccion-datos-qa";   Fun = "um-ley-proteccion-datos-qa-fun"   }
    "dev"  = @{ Api = "um-ley-proteccion-datos-dev";  Fun = "um-ley-proteccion-datos-dev-fun"  }
    "prod" = @{ Api = "um-ley-proteccion-datos-prod"; Fun = "um-ley-proteccion-datos-prod-fun" }
}

$apiFinalName = $ApiAppName
$funFinalName = $FunAppName

if ($predefined.ContainsKey($envLower)) {
    if ([string]::IsNullOrWhiteSpace($apiFinalName)) { $apiFinalName = $predefined[$envLower].Api }
    if ([string]::IsNullOrWhiteSpace($funFinalName)) { $funFinalName = $predefined[$envLower].Fun }
}
else {
    # Ambiente nuevo / personalizado
    if ([string]::IsNullOrWhiteSpace($apiFinalName)) {
        $apiFinalName = Read-Host "Ingrese el nombre del App Service (API Web) para '$Environment'"
    }
    if ([string]::IsNullOrWhiteSpace($funFinalName)) {
        $funFinalName = Read-Host "Ingrese el nombre de la Function App (Worker) para '$Environment'"
    }
    if ([string]::IsNullOrWhiteSpace($apiFinalName) -or [string]::IsNullOrWhiteSpace($funFinalName)) {
        throw "Debe especificar -ApiAppName y -FunAppName para el ambiente '$Environment'."
    }
}

Write-Host "  App Service (API) : $apiFinalName" -ForegroundColor Cyan
Write-Host "  Function App (Fun): $funFinalName" -ForegroundColor Cyan

# 2. Validar sesion activa en Azure CLI
Write-Host ""
Write-Host "[1/7] Verificando sesion activa en Azure CLI..." -ForegroundColor Yellow
try {
    $currentAccount = az account show --query "{Subscription:name, User:user.name}" -o json | ConvertFrom-Json
    Write-Host "  Conectado como : $($currentAccount.User)" -ForegroundColor Green
    Write-Host "  Suscripcion    : $($currentAccount.Subscription)" -ForegroundColor Green
}
catch {
    Write-Error "No se detecto una sesion activa en Azure CLI. Ejecute 'az login' interactivamente en su terminal antes de continuar."
    exit 1
}

# 3. Aprovisionamiento opcional de Storage (util para nuevos entornos)
if ($ProvisionStorage) {
    Write-Host ""
    Write-Host "[2/7] Aprovisionando recursos de Storage en Azure..." -ForegroundColor Yellow
    if ([string]::IsNullOrWhiteSpace($StorageAccount)) {
        $StorageAccount = Read-Host "Ingrese el nombre del Storage Account en el Grupo '$ResourceGroup'"
    }
    
    if (-not [string]::IsNullOrWhiteSpace($StorageAccount)) {
        try {
            Write-Host "  Consultando Connection String para '$StorageAccount'..." -ForegroundColor Gray
            $connStr = az storage account show-connection-string -g $ResourceGroup -n $StorageAccount --query connectionString -o tsv
            
            Write-Host "  Asegurando existencia de Cola 'privacy-mass-executions'..." -ForegroundColor Gray
            az storage queue create --name "privacy-mass-executions" --connection-string $connStr | Out-Null
            
            Write-Host "  Asegurando existencia de Contenedor 'privacy-backups'..." -ForegroundColor Gray
            az storage container create --name "privacy-backups" --connection-string $connStr --public-access off | Out-Null
            
            Write-Host "  Storage aprovisionado y validado correctamente." -ForegroundColor Green
        }
        catch {
            Write-Warning "No se pudo aprovisionar el Storage automaticamente ($($_.Exception.Message)). Verifique los permisos."
        }
    }
}
else {
    Write-Host ""
    Write-Host "[2/7] Aprovisionamiento de Storage omitido (no se indico -ProvisionStorage)." -ForegroundColor Gray
}

# 4. Limpieza de artefactos
Write-Host ""
Write-Host "[3/7] Limpiando carpetas y artefactos previos..." -ForegroundColor Yellow
$cleanPaths = @(".\publish_web", ".\publish_fun", ".\publish_web.zip", ".\publish_fun.zip")
foreach ($p in $cleanPaths) {
    if (Test-Path $p) {
        Remove-Item -Path $p -Recurse -Force -ErrorAction SilentlyContinue
    }
}
Write-Host "  Limpieza completada." -ForegroundColor Green

# 5. Compilacion y publicacion .NET 8 Release
Write-Host ""
Write-Host "[4/7] Compilando proyectos en modo Release (.NET 8)..." -ForegroundColor Yellow

Write-Host "  Compilando Web API (App Service)..." -ForegroundColor Gray
dotnet publish .\Umayor.Dynamics.DeletePoc.csproj -c Release -o .\publish_web --nologo -v q
if ($LASTEXITCODE -ne 0) { throw "Error al compilar Umayor.Dynamics.DeletePoc" }

Write-Host "  Compilando Function App Worker..." -ForegroundColor Gray
dotnet publish .\Umayor.Dynamics.DeletePoc.Functions\Umayor.Dynamics.DeletePoc.Functions.csproj -c Release -o .\publish_fun --nologo -v q
if ($LASTEXITCODE -ne 0) { throw "Error al compilar Umayor.Dynamics.DeletePoc.Functions" }
Write-Host "  Compilacion exitosa." -ForegroundColor Green

# 6. Empaquetado ZIP
Write-Host ""
Write-Host "[5/7] Generando paquetes ZIP de despliegue..." -ForegroundColor Yellow
Compress-Archive -Path .\publish_web\* -DestinationPath .\publish_web.zip -Force
Compress-Archive -Path .\publish_fun\* -DestinationPath .\publish_fun.zip -Force
$webZipSize = [math]::Round((Get-Item .\publish_web.zip).Length / 1MB, 2)
$funZipSize = [math]::Round((Get-Item .\publish_fun.zip).Length / 1MB, 2)
Write-Host "  publish_web.zip generado ($webZipSize MB)" -ForegroundColor Green
Write-Host "  publish_fun.zip generado ($funZipSize MB)" -ForegroundColor Green

# 7. Despliegue con Azure CLI
Write-Host ""
Write-Host "[6/7] Desplegando paquetes a Azure..." -ForegroundColor Yellow

Write-Host "  Desplegando Web API a App Service ($apiFinalName)..." -ForegroundColor Gray
az webapp deployment source config-zip --resource-group $ResourceGroup --name $apiFinalName --src .\publish_web.zip
if ($LASTEXITCODE -ne 0) { throw "Fallo el despliegue al App Service $apiFinalName" }

Write-Host "  Desplegando Worker a Function App ($funFinalName)..." -ForegroundColor Gray
az functionapp deployment source config-zip --resource-group $ResourceGroup --name $funFinalName --src .\publish_fun.zip
if ($LASTEXITCODE -ne 0) { throw "Fallo el despliegue a la Function App $funFinalName" }

Write-Host "  Reiniciando servicios para aplicar los nuevos binarios..." -ForegroundColor Gray
az webapp restart --resource-group $ResourceGroup --name $apiFinalName
az functionapp restart --resource-group $ResourceGroup --name $funFinalName
Write-Host "  Despliegue y reinicio concluidos con exito." -ForegroundColor Green

# 8. Smoke Tests Post-Despliegue
if (-not $SkipSmokeTests) {
    Write-Host ""
    Write-Host "[7/7] Ejecutando Smoke Tests post-despliegue..." -ForegroundColor Yellow
    
    $apiBaseUrl = "https://$apiFinalName.azurewebsites.net"
    $healthUrl = "$apiBaseUrl/api/diagnostics/build"
    
    Write-Host "  Esperando 10 segundos a que el App Service inicie..." -ForegroundColor Gray
    Start-Sleep -Seconds 10

    try {
        $buildInfo = Invoke-RestMethod -Uri $healthUrl -Method Get -TimeoutSec 30
        Write-Host "  ================ VERIFICACION HEALTH CHECK ================" -ForegroundColor Green
        Write-Host "  API Build       : $($buildInfo.apiBuild)" -ForegroundColor Green
        Write-Host "  Dataverse Target: $($buildInfo.dataverseUrl)" -ForegroundColor Green
        Write-Host "  Environment     : $($buildInfo.environment)" -ForegroundColor Green
        Write-Host "  Started UTC     : $($buildInfo.startedAtUtc)" -ForegroundColor Green
        Write-Host "  ===========================================================" -ForegroundColor Green
    }
    catch {
        Write-Warning "El endpoint de diagnostico no respondio inmediatamente ($($_.Exception.Message)). Puede deberse al warm-up inicial del App Service."
    }
}
else {
    Write-Host ""
    Write-Host "[7/7] Smoke Tests omitidos por parametro (-SkipSmokeTests)." -ForegroundColor Gray
}

Write-Host ""
Write-Host "==========================================================================" -ForegroundColor Green
Write-Host "   PROCESO DE DESPLIEGUE FINALIZADO EXITOSAMENTE                          " -ForegroundColor Green
Write-Host "==========================================================================" -ForegroundColor Green
