Write-Host "=== Load Tests Validation ===" -ForegroundColor Green
Write-Host ""

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
        }
    }
    Write-Host ""
} else {
    Write-Host "Warning: .env file not found" -ForegroundColor Yellow
    Write-Host ""
}

Write-Host "Checking test files..." -ForegroundColor Yellow
$testFiles = @("k6\auth-load-test.js", "k6\signalr-load-test.js")

foreach ($file in $testFiles) {
    $filePath = Join-Path $PSScriptRoot $file
    if (Test-Path $filePath) {
        Write-Host "✓ Found test file: $file" -ForegroundColor Green
    } else {
        Write-Host "✗ Missing test file: $file" -ForegroundColor Red
    }
}
Write-Host ""

Write-Host "Checking InfluxDB configuration..." -ForegroundColor Yellow
$influxUrl = $env:INFLUXDB_URL
$influxToken = $env:INFLUXDB_TOKEN
$influxBucket = $env:INFLUXDB_BUCKET
$influxOrg = $env:INFLUXDB_ORG

if ($influxUrl) {
    Write-Host "✓ INFLUXDB_URL is set" -ForegroundColor Green
} else {
    Write-Host "✗ INFLUXDB_URL is not set" -ForegroundColor Red
}

if ($influxToken) {
    Write-Host "✓ INFLUXDB_TOKEN is set" -ForegroundColor Green
} else {
    Write-Host "✗ INFLUXDB_TOKEN is not set" -ForegroundColor Red
}

if ($influxBucket) {
    Write-Host "✓ INFLUXDB_BUCKET is set: $influxBucket" -ForegroundColor Green
} else {
    Write-Host "✗ INFLUXDB_BUCKET is not set" -ForegroundColor Red
}

if ($influxOrg) {
    Write-Host "✓ INFLUXDB_ORG is set: $influxOrg" -ForegroundColor Green
} else {
    Write-Host "✗ INFLUXDB_ORG is not set" -ForegroundColor Red
}
Write-Host ""

Write-Host "Checking Supabase configuration..." -ForegroundColor Yellow
$supabaseUrl = $env:Supabase__Url
$supabaseAnonKey = $env:SUPABASE_ANON_KEY

if ($supabaseUrl) {
    Write-Host "✓ Supabase__Url is set" -ForegroundColor Green
} else {
    Write-Host "✗ Supabase__Url is not set" -ForegroundColor Red
}

if ($supabaseAnonKey) {
    Write-Host "✓ SUPABASE_ANON_KEY is set" -ForegroundColor Green
} else {
    Write-Host "✗ SUPABASE_ANON_KEY is not set" -ForegroundColor Red
}
Write-Host ""

Write-Host "Checking k6 installation..." -ForegroundColor Yellow
$k6Check = & "C:\Program Files\k6\k6.exe" version 2>&1
if ($LASTEXITCODE -eq 0) {
    Write-Host "✓ k6 is installed: $k6Check" -ForegroundColor Green
} else {
    Write-Host "✗ k6 is not working properly" -ForegroundColor Red
}
Write-Host ""

Write-Host "=== Validation Complete ===" -ForegroundColor Green
Write-Host ""
Write-Host "To run auth load test (100 concurrent users):" -ForegroundColor Cyan
Write-Host "  .\run-auth-load-test.ps1" -ForegroundColor Gray
Write-Host ""
Write-Host "To run SignalR load test (20 players):" -ForegroundColor Cyan
Write-Host "  .\run-signalr-load-test.ps1" -ForegroundColor Gray
Write-Host ""
Write-Host "Note: Make sure your backend is running before executing load tests" -ForegroundColor Yellow