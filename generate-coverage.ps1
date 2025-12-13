#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Generates code coverage reports for SonarQube analysis
.DESCRIPTION
    This script runs tests with code coverage collection and generates reports in OpenCover XML format
    that can be consumed by SonarQube for code coverage analysis.
.PARAMETER OutputDir
    Directory where coverage reports will be saved. Default: ./TestResults
.PARAMETER Format
    Coverage report format. Default: opencover (for SonarQube)
.EXAMPLE
    .\generate-coverage.ps1
.EXAMPLE
    .\generate-coverage.ps1 -OutputDir "./coverage" -Format "opencover"
#>

param(
    [string]$OutputDir = "./TestResults",
    [string]$Format = "opencover"
)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Code Coverage Report Generation" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Ensure output directory exists
if (!(Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
    Write-Host "Created output directory: $OutputDir" -ForegroundColor Green
}

# Clean previous coverage results
Write-Host "Cleaning previous coverage results..." -ForegroundColor Yellow
Get-ChildItem -Path $OutputDir -Filter "coverage.*" -Recurse | Remove-Item -Force
Write-Host "Previous results cleaned." -ForegroundColor Green
Write-Host ""

# Run tests with coverage
Write-Host "Running tests with code coverage collection..." -ForegroundColor Yellow
Write-Host ""

# Use relative path for coverlet
$coverageFileName = "coverage.$Format.xml"
$relativeCoveragePath = "$OutputDir/$coverageFileName"

$testCommand = @(
    "test",
    "Squirrel.Wiki.Core.Tests/Squirrel.Wiki.Core.Tests.csproj",
    "/p:CollectCoverage=true",
    "/p:CoverletOutputFormat=$Format",
    "/p:CoverletOutput=`"../$relativeCoveragePath`"",
    "/p:ExcludeByFile=`"**/*Designer.cs`"",
    "/p:Exclude=`"[xunit.*]*,[*.Tests]*`"",
    "--verbosity", "minimal"
)

$coverageFile = $relativeCoveragePath

& dotnet $testCommand

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "Tests failed or coverage collection encountered an error." -ForegroundColor Red
    exit $LASTEXITCODE
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Coverage Report Generated Successfully!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Coverage file location:" -ForegroundColor Yellow
Write-Host "  $coverageFile" -ForegroundColor White
Write-Host ""
Write-Host "To use with SonarQube, add this to your sonar-project.properties:" -ForegroundColor Yellow
Write-Host "  sonar.cs.opencover.reportsPaths=$coverageFile" -ForegroundColor White
Write-Host ""
Write-Host "Or pass it as a parameter to the SonarScanner:" -ForegroundColor Yellow
Write-Host "  dotnet sonarscanner begin /k:`"project-key`" /d:sonar.cs.opencover.reportsPaths=`"$coverageFile`"" -ForegroundColor White
Write-Host ""
