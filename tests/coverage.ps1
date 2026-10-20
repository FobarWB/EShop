# Runs all tests with code coverage and builds one HTML report:
#   powershell -ExecutionPolicy Bypass -File tests/coverage.ps1
# Report: TestResults/coverage-report/index.html
$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')

# ReportGenerator merges every Cobertura file it finds, so files from earlier runs must go.
if (Test-Path TestResults) { Remove-Item TestResults -Recurse -Force }

dotnet test --coverage --coverage-output-format cobertura
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet tool restore
dotnet tool run reportgenerator `
    "-reports:TestResults/*.cobertura.xml" `
    "-targetdir:TestResults/coverage-report" `
    "-reporttypes:Html;TextSummary" `
    "-assemblyfilters:+EShop.*;-EShop.*.Tests.*;-EShop.Tests.Common" `
    "-classfilters:-EShop.Data.Migrations.*;-Microsoft.AspNetCore.OpenApi.Generated*;-System.Runtime.CompilerServices*"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Get-Content TestResults/coverage-report/Summary.txt -TotalCount 16
Write-Host "Report: $(Resolve-Path TestResults/coverage-report/index.html)"
