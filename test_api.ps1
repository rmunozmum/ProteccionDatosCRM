$job = Start-Job -ScriptBlock {
    Set-Location "d:\Proyectos\Umayor.Dynamics.DeletePoc"
    dotnet run --urls "http://localhost:5011"
}

Write-Host "Waiting for API to start..."
Start-Sleep -Seconds 10

try {
    Write-Host "--- TEST 1: CATALOG ---"
    $catalog = Invoke-RestMethod -Uri "http://localhost:5011/api/reports/catalog" -Method Get
    $catalog | ConvertTo-Json -Depth 5 | Write-Host

    Write-Host "--- TEST 2: EXECUTE ---"
    $body = @{
        reportCode = "LPD-R01"
        parameters = @{
            rut = "17175272"
            usuarioEjecutor = "Rogelio Muñoz"
            correoEjecutor = ""
            areaEjecutor = "DTI"
            motivo = "Prueba técnica MVP Reportería LPD"
        }
    }
    $headers = @{ "X-MS-CLIENT-PRINCIPAL-NAME" = "test@umayor.cl" }

    $execute = Invoke-RestMethod -Uri "http://localhost:5011/api/reports/execute" -Method Post -Body ($body | ConvertTo-Json) -ContentType "application/json" -Headers $headers -ErrorAction Stop
    $execute | ConvertTo-Json -Depth 10 | Write-Host
}
catch {
    Write-Host "HTTP Request failed!"
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $reader.BaseStream.Position = 0
        $reader.DiscardBufferedData()
        $responseBody = $reader.ReadToEnd()
        Write-Host "Error Body: $responseBody"
    } else {
        Write-Host $_.Exception.Message
    }
}
finally {
    Stop-Job $job
    Remove-Job $job
}
