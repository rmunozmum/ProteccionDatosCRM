$job = Start-Job -ScriptBlock {
    Set-Location "d:\Proyectos\Umayor.Dynamics.DeletePoc"
    dotnet run --urls "http://localhost:5012"
}

Write-Host "Waiting for API to start..."
Start-Sleep -Seconds 10

try {
    $swagger = Invoke-RestMethod -Uri "http://localhost:5012/swagger/v1/swagger.json" -Method Get
    $swagger | ConvertTo-Json -Depth 10 | Out-File -FilePath "d:\Proyectos\Umayor.Dynamics.DeletePoc\swagger_reports.json" -Encoding utf8
    Write-Host "Swagger exported successfully to swagger_reports.json"
}
catch {
    Write-Host "Failed to export swagger: $($_.Exception.Message)"
}
finally {
    Stop-Job $job
    Remove-Job $job
}
