param(
    [string]$BaseUrl = "http://localhost:5000",
    [string]$TestRut = "11111111"
)

$ErrorActionPreference = "Stop"

Write-Host "Ejecutando prueba automatizada de execute-batch para RUT: $TestRut..."

$body = @{
    ruts = @($TestRut)
    pasaportes = @()
    mode = "Consultar"
    confirmationText = ""
} | ConvertTo-Json

try {
    $response = Invoke-RestMethod `
        -Method Post `
        -Uri "$BaseUrl/api/execute-batch" `
        -ContentType "application/json" `
        -Body $body

    Write-Host "Respuesta de la API:"
    $response | ConvertTo-Json -Depth 10 | Write-Host

    $result = $response.results[0]
    $summary = $response.summary

    Write-Host "`n--- VERIFICACIONES ---"
    Write-Host "RUT: $($result.rut)"
    Write-Host "Status: $($result.status)"
    Write-Host "Found: $($result.data.found)"
    Write-Host "Summary Successful: $($summary.successful)"
    Write-Host "Summary NotFound: $($summary.notFound)"
    Write-Host "Summary Failed: $($summary.failed)"

    # Rule 1: Si data.found == true -> status = "Consultado"
    # Rule 2: Si data.found == false -> status = "NoEncontrado"
    # Rule 3: El objeto summary debe calcularse a partir de los status
    if ($result.data.found -eq $true) {
        if ($result.status -ne "Consultado") {
            throw "ERROR: Se esperaba status 'Consultado' porque found es true, pero se obtuvo '$($result.status)'"
        }
        if ($summary.successful -ne 1) {
            throw "ERROR: Se esperaba successful = 1 en el summary, pero se obtuvo $($summary.successful)"
        }
        Write-Output "RESULTADO: OK - found = true mapeado correctamente a Consultado / successful = 1"
    } else {
        if ($result.status -ne "NoEncontrado") {
            throw "ERROR: Se esperaba status 'NoEncontrado' porque found es false, pero se obtuvo '$($result.status)'"
        }
        if ($summary.notFound -ne 1) {
            throw "ERROR: Se esperaba notFound = 1 en el summary, pero se obtuvo $($summary.notFound)"
        }
        Write-Output "RESULTADO: OK - found = false mapeado correctamente a NoEncontrado / notFound = 1"
    }
}
catch {
    Write-Error "La prueba falló: $_"
    exit 1
}
