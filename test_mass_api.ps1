param(
    [string]$BaseUrl = "http://localhost:5000",
    [Parameter(Mandatory = $true)]
    [string]$TestRut
)

$ErrorActionPreference = "Stop"

$body = @{
    ruts = @($TestRut)
    pasaportes = @()
    mode = "Consultar"
    confirmationText = ""
    instructionReference = "SMOKE-QAS-$([DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss'))"
} | ConvertTo-Json

Write-Host "Creando simulación masiva..."
$execution = Invoke-RestMethod `
    -Method Post `
    -Uri "$BaseUrl/api/mass-executions" `
    -ContentType "application/json" `
    -Body $body

Write-Host "ExecutionId: $($execution.executionId)"

$deadline = [DateTime]::UtcNow.AddMinutes(5)
do {
    Start-Sleep -Seconds 2
    $status = Invoke-RestMethod `
        -Method Get `
        -Uri "$BaseUrl/api/mass-executions/$($execution.executionId)"
    Write-Host "Estado: $($status.state) | Pendientes: $($status.pending) | Completados: $($status.completed) | Fallidos: $($status.failed)"
} while ($status.state -in @("Created", "Running", "Paused") -and [DateTime]::UtcNow -lt $deadline)

$items = Invoke-RestMethod `
    -Method Get `
    -Uri "$BaseUrl/api/mass-executions/$($execution.executionId)/items?skip=0&take=100"

Write-Host "Resultado final:"
$status | ConvertTo-Json -Depth 6
$items | ConvertTo-Json -Depth 10

if ($status.state -notin @("Completed", "PartiallyCompleted", "Failed")) {
    throw "La ejecución no terminó dentro del tiempo esperado."
}
