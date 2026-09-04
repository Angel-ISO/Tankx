# Script to run k6 tests with environment variables from .env file
# Usage: .\run-k6-test.ps1 [test-file]

param(
    [Parameter(Mandatory=$false)]
    [string]$TestFile = "k6\validation-test.js"
)

$envFile = Join-Path $PSScriptRoot ".env"
if (Test-Path $envFile) {
    Write-Host "Loading environment variables from .env file..." -ForegroundColor Yellow
    Get-Content $envFile | ForEach-Object {
        if ($_ -match '^([^=]+)=(.*)$') {
            $name = $matches[1].Trim()
            $value = $matches[2].Trim()
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
$env:INFLUXDB_URL = $env:INFLUXDB_URL
$env:INFLUXDB_TOKEN = $env:INFLUXDB_TOKEN
$env:INFLUXDB_BUCKET = $env:INFLUXDB_BUCKET
$env:INFLUXDB_ORG = $env:INFLUXDB_ORG
$env:K6_BASE_URL = $env:K6_BASE_URL

# Supabase credentials for k6 tests
$env:SUPABASE_URL = $env:Supabase__Url
$env:SUPABASE_ANON_KEY = $env:SUPABASE_ANON_KEY

Write-Host "Running k6 test: $TestFile" -ForegroundColor Green
Write-Host "Backend URL: $env:K6_BASE_URL" -ForegroundColor Gray
Write-Host "Supabase URL: $env:SUPABASE_URL" -ForegroundColor Gray
Write-Host "InfluxDB URL: $env:INFLUXDB_URL" -ForegroundColor Gray
Write-Host "InfluxDB Bucket: $env:INFLUXDB_BUCKET" -ForegroundColor Gray
Write-Host "InfluxDB Org: $env:INFLUXDB_ORG" -ForegroundColor Gray
Write-Host ""

$k6Path = "C:\Program Files\k6\k6.exe"
$testPath = Join-Path $PSScriptRoot $TestFile

& $k6Path run $testPath