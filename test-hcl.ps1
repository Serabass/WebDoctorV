# PowerShell script for running HCL parser tests specifically
param(
    [string]$Verbosity = "normal"
)

Write-Host "Running HCL Parser tests..." -ForegroundColor Cyan

& docker compose --profile test run --rm tests dotnet test WebdoctorV.Tests/WebdoctorV.Tests.csproj --filter "FullyQualifiedName~HclParserTests" --verbosity $Verbosity

if ($LASTEXITCODE -eq 0) {
    Write-Host "`nHCL Parser tests completed successfully!" -ForegroundColor Green
} else {
    Write-Host "`nHCL Parser tests failed!" -ForegroundColor Red
    exit $LASTEXITCODE
}
