# Validation script for benchmarking tools installation
# Run: .\validate-tools.ps1

Write-Host "=== Benchmarking Tools Validation ===" -ForegroundColor Green
Write-Host ""

Write-Host "Checking k6 installation..." -ForegroundColor Yellow
try {
    $k6Version = & "C:\Program Files\k6\k6.exe" version 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✓ k6 is installed: $k6Version" -ForegroundColor Green
    } else {
        Write-Host "✗ k6 is not working properly" -ForegroundColor Red
    }
} catch {
    Write-Host "✗ k6 is not installed or not in PATH" -ForegroundColor Red
}
Write-Host ""

Write-Host "Checking Artillery installation..." -ForegroundColor Yellow
try {
    $artilleryVersion = & "C:\Users\angel\.bun\bin\artillery.exe" --version 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✓ Artillery is installed" -ForegroundColor Green
        Write-Host "$artilleryVersion" -ForegroundColor Gray
    } else {
        Write-Host "✗ Artillery is not working properly" -ForegroundColor Red
    }
} catch {
    Write-Host "✗ Artillery is not installed or not in PATH" -ForegroundColor Red
}
Write-Host ""

Write-Host "Checking InfluxDB environment variables..." -ForegroundColor Yellow
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
    Write-Host "✗ INFLUXDB_BUCKET is not set (will use default 'k6')" -ForegroundColor Yellow
}

if ($influxOrg) {
    Write-Host "✓ INFLUXDB_ORG is set: $influxOrg" -ForegroundColor Green
} else {
    Write-Host "✗ INFLUXDB_ORG is not set (will use default 'default')" -ForegroundColor Yellow
}
Write-Host ""

Write-Host "Checking test files..." -ForegroundColor Yellow
$k6Files = @("basic-test.js", "multiplayer-load-test.js", "signalr-test.js", "validation-test.js")
$artilleryFiles = @("tankx-load-test.yml")

foreach ($file in $k6Files) {
    $filePath = Join-Path $PSScriptRoot "k6\$file"
    if (Test-Path $filePath) {
        Write-Host "✓ Found k6 test file: $file" -ForegroundColor Green
    } else {
        Write-Host "✗ Missing k6 test file: $file" -ForegroundColor Red
    }
}

foreach ($file in $artilleryFiles) {
    $filePath = Join-Path $PSScriptRoot "artillery\$file"
    if (Test-Path $filePath) {
        Write-Host "✓ Found Artillery test file: $file" -ForegroundColor Green
    } else {
        Write-Host "✗ Missing Artillery test file: $file" -ForegroundColor Red
    }
}
Write-Host ""

Write-Host "=== Validation Complete ===" -ForegroundColor Green
Write-Host ""
Write-Host "To run a quick validation test:" -ForegroundColor Cyan
Write-Host "  k6 run k6\validation-test.js" -ForegroundColor Gray
Write-Host ""
Write-Host "To run the basic k6 test:" -ForegroundColor Cyan
Write-Host "  k6 run k6\basic-test.js" -ForegroundColor Gray
Write-Host ""
Write-Host "To run the Artillery test:" -ForegroundColor Cyan
Write-Host "  artillery run artillery\tankx-load-test.yml" -ForegroundColor Gray