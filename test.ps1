# PowerShell script for running tests
param(
    [string]$Filter = "",
    [string]$Verbosity = "normal"
)

Write-Host "Running tests..." -ForegroundColor Cyan

$composeArgs = @("compose", "--profile", "test", "run", "--rm", "tests")

if ($Filter) {
    $composeArgs += "dotnet"
    $composeArgs += "test"
    $composeArgs += "WebdoctorV.Tests/WebdoctorV.Tests.csproj"
    $composeArgs += "--filter"
    $composeArgs += $Filter
    $composeArgs += "--verbosity"
    $composeArgs += $Verbosity
} else {
    $composeArgs += "dotnet"
    $composeArgs += "test"
    $composeArgs += "WebdoctorV.Tests/WebdoctorV.Tests.csproj"
    $composeArgs += "--verbosity"
    $composeArgs += $Verbosity
}

& docker $composeArgs

if ($LASTEXITCODE -eq 0) {
    Write-Host "`nTests completed successfully!" -ForegroundColor Green
} else {
    Write-Host "`nTests failed!" -ForegroundColor Red
    exit $LASTEXITCODE
}
