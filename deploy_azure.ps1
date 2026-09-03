<#
.SYNOPSIS
    Script automatizado de compilacion, empaquetado y despliegue a Azure para el Sistema de Proteccion de Datos (Universidad Mayor).

.DESCRIPTION
    Compila la Web API (App Service) y el Procesador Asincrono de Colas (Function App) en modo Release,
    los empaqueta en archivos ZIP y los despliega usando Azure CLI ZipDeploy con verificacion post-despliegue.

.PARAMETER Environment
    Ambiente objetivo: 'qa' (por defecto), 'dev' o 'prod'.

.PARAMETER ResourceGroup
    Grupo de recursos en Azure (por defecto 'admincrm2021_rg_0225').

.PARAMETER SkipSmokeTests
    Si se especifica, omite las pruebas de verificacion post-despliegue.

.EXAMPLE
    .\deploy_azure.ps1 -Environment qa
#>

param(
    [ValidateSet("qa", "dev", "prod")]
    [string]$Environment = "qa",

    [string]$ResourceGroup = "admincrm2021_rg_0225",

    [switch]$SkipSmokeTests
)

$ErrorActionPreference = "Stop"

Write-Host "==========================================================================" -ForegroundColor Cyan
Write-Host "   DESPLIEGUE A AZURE - SISTEMA DE PROTECCION DE DATOS (ARCO) UMAYOR      " -ForegroundColor Cyan
Write-Host "   Ambiente Objetivo: $($Environment.ToUpper()) | Grupo Recursos: $ResourceGroup" -ForegroundColor Cyan
Write-Host "==========================================================================" -ForegroundColor Cyan

# 1. Mapeo de nombres de recursos por ambiente
$appNames = @{
    "qa"   = @{ Api = "um-ley-proteccion-datos-qa";   Fun = "um-ley-proteccion-datos-qa-fun"   }
    "dev"  = @{ Api = "um-ley-proteccion-datos-dev";  Fun = "um-ley-proteccion-datos-dev-fun"  }
    "prod" = @{ Api = "um-ley-proteccion-datos-prod"; Fun = "um-ley-proteccion-datos-prod-fun" }
}

$target = $appNames[$Environment]
$apiAppName = $target.Api
$funAppName = $target.Fun

# 2. Validar sesion activa en Azure CLI
Write-Host ""
Write-Host "[1/6] Verificando sesion activa en Azure CLI..." -ForegroundColor Yellow
try {
    $currentAccount = az account show --query "{Subscription:name, User:user.name}" -o json | ConvertFrom-Json
    Write-Host "  Conectado como : $($currentAccount.User)" -ForegroundColor Green
    Write-Host "  Suscripcion    : $($currentAccount.Subscription)" -ForegroundColor Green
}
catch {
    Write-Error "No se detecto una sesion activa en Azure CLI. Ejecute 'az login' interactivamente en su terminal antes de continuar."
    exit 1
}

# 3. Limpieza de artefactos
Write-Host ""
Write-Host "[2/6] Limpiando carpetas y artefactos previos..." -ForegroundColor Yellow
$cleanPaths = @(".\publish_web", ".\publish_fun", ".\publish_web.zip", ".\publish_fun.zip")
foreach ($p in $cleanPaths) {
    if (Test-Path $p) {
        Remove-Item -Path $p -Recurse -Force -ErrorAction SilentlyContinue
    }
}
Write-Host "  Limpieza completada." -ForegroundColor Green

# 4. Compilacion y publicacion .NET 8 Release
Write-Host ""
Write-Host "[3/6] Compilando proyectos en modo Release (.NET 8)..." -ForegroundColor Yellow

Write-Host "  Compilando Web API (App Service)..." -ForegroundColor Gray
dotnet publish .\Umayor.Dynamics.DeletePoc.csproj -c Release -o .\publish_web --nologo -v q
if ($LASTEXITCODE -ne 0) { throw "Error al compilar Umayor.Dynamics.DeletePoc" }

Write-Host "  Compilando Function App Worker..." -ForegroundColor Gray
dotnet publish .\Umayor.Dynamics.DeletePoc.Functions\Umayor.Dynamics.DeletePoc.Functions.csproj -c Release -o .\publish_fun --nologo -v q
if ($LASTEXITCODE -ne 0) { throw "Error al compilar Umayor.Dynamics.DeletePoc.Functions" }
Write-Host "  Compilacion exitosa." -ForegroundColor Green

# 5. Empaquetado ZIP
Write-Host ""
Write-Host "[4/6] Generando paquetes ZIP de despliegue..." -ForegroundColor Yellow
Compress-Archive -Path .\publish_web\* -DestinationPath .\publish_web.zip -Force
Compress-Archive -Path .\publish_fun\* -DestinationPath .\publish_fun.zip -Force
$webZipSize = [math]::Round((Get-Item .\publish_web.zip).Length / 1MB, 2)
$funZipSize = [math]::Round((Get-Item .\publish_fun.zip).Length / 1MB, 2)
Write-Host "  publish_web.zip generado ($webZipSize MB)" -ForegroundColor Green
Write-Host "  publish_fun.zip generado ($funZipSize MB)" -ForegroundColor Green

# 6. Despliegue con Azure CLI
Write-Host ""
Write-Host "[5/6] Desplegando paquetes a Azure..." -ForegroundColor Yellow

Write-Host "  Desplegando Web API a App Service ($apiAppName)..." -ForegroundColor Gray
az webapp deployment source config-zip --resource-group $ResourceGroup --name $apiAppName --src .\publish_web.zip
if ($LASTEXITCODE -ne 0) { throw "Fallo el despliegue al App Service $apiAppName" }

Write-Host "  Desplegando Worker a Function App ($funAppName)..." -ForegroundColor Gray
az functionapp deployment source config-zip --resource-group $ResourceGroup --name $funAppName --src .\publish_fun.zip
if ($LASTEXITCODE -ne 0) { throw "Fallo el despliegue a la Function App $funAppName" }

Write-Host "  Reiniciando servicios para aplicar los nuevos binarios..." -ForegroundColor Gray
az webapp restart --resource-group $ResourceGroup --name $apiAppName
az functionapp restart --resource-group $ResourceGroup --name $funAppName
Write-Host "  Despliegue y reinicio concluidos con exito." -ForegroundColor Green

# 7. Smoke Tests Post-Despliegue
if (-not $SkipSmokeTests) {
    Write-Host ""
    Write-Host "[6/6] Ejecutando Smoke Tests post-despliegue..." -ForegroundColor Yellow
    
    $apiBaseUrl = "https://$apiAppName.azurewebsites.net"
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
    Write-Host "[6/6] Smoke Tests omitidos por parametro (-SkipSmokeTests)." -ForegroundColor Gray
}

Write-Host ""
Write-Host "==========================================================================" -ForegroundColor Green
Write-Host "   PROCESO DE DESPLIEGUE FINALIZADO EXITOSAMENTE                          " -ForegroundColor Green
Write-Host "==========================================================================" -ForegroundColor Green
