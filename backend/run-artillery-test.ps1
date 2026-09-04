# Script to run Artillery tests with environment variables from .env file
# Usage: .\run-artillery-test.ps1 [config-file]

param(
    [Parameter(Mandatory=$false)]
    [string]$ConfigFile = "artillery\tankx-load-test.yml"
)

# Load environment variables from .env file
$envFile = Join-Path $PSScriptRoot ".env"
if (Test-Path $envFile) {
    Write-Host "Loading environment variables from .env file..." -ForegroundColor Yellow
    Get-Content $envFile | ForEach-Object {
        if ($_ -match '^([^=]+)=(.*)$') {
            $name = $matches[1].Trim()
            $value = $matches[2].Trim()
            # Remove quotes if present
            if ($value -match '^"(.*)"$') {
                $value = $matches[1]
            }
            [Environment]::SetEnvironmentVariable($name, $value, "Process")
            Write-Host "  Loaded: $name" -ForegroundColor Gray
        }
    }
    Write-Host ""
} else {
    Write-Host "Warning: .env file not found. Using system environment variables." -ForegroundColor Yellow
    Write-Host ""
}

# Set up environment variables for Artillery
$env:INFLUXDB_URL = $env:INFLUXDB_URL
$env:INFLUXDB_TOKEN = $env:INFLUXDB_TOKEN
$env:INFLUXDB_BUCKET = $env:INFLUXDB_BUCKET
$env:INFLUXDB_ORG = $env:INFLUXDB_ORG

Write-Host "Running Artillery test: $ConfigFile" -ForegroundColor Green
Write-Host "InfluxDB URL: $env:INFLUXDB_URL" -ForegroundColor Gray
Write-Host "InfluxDB Bucket: $env:INFLUXDB_BUCKET" -ForegroundColor Gray
Write-Host "InfluxDB Org: $env:INFLUXDB_ORG" -ForegroundColor Gray
Write-Host ""

# Run Artillery with full path
$artilleryPath = "C:\Users\angel\.bun\bin\artillery.exe"
$configPath = Join-Path $PSScriptRoot $ConfigFile

& $artilleryPath run $configPath