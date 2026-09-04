param(
    [Parameter(Mandatory=$false)]
    [string]$InfluxDBUrl = $env:INFLUXDB_URL,
    [Parameter(Mandatory=$false)]
    [string]$InfluxDBToken = $env:INFLUXDB_TOKEN,
    [Parameter(Mandatory=$false)]
    [string]$InfluxDBBucket = $env:INFLUXDB_BUCKET,
    [Parameter(Mandatory=$false)]
    [string]$InfluxDBOrg = $env:INFLUXDB_ORG
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
        }
    }
    Write-Host ""
}

$env:SUPABASE_URL = $env:Supabase__Url
$env:SUPABASE_ANON_KEY = $env:SUPABASE_ANON_KEY

Write-Host "Running Auth Load Test (100 concurrent users)" -ForegroundColor Green
Write-Host "Supabase URL: $env:SUPABASE_URL" -ForegroundColor Gray
Write-Host "InfluxDB URL: $env:INFLUXDB_URL" -ForegroundColor Gray
Write-Host "InfluxDB Bucket: $env:INFLUXDB_BUCKET" -ForegroundColor Gray
Write-Host "InfluxDB Org: $env:INFLUXDB_ORG" -ForegroundColor Gray
Write-Host ""

$k6Path = "C:\Program Files\k6\k6.exe"
$testPath = Join-Path $PSScriptRoot "k6\auth-load-test.js"

$outParams = @()
if ($InfluxDBUrl -and $InfluxDBToken -and $InfluxDBBucket -and $InfluxDBOrg) {
    $outParams = @("--out", "influxdb=$InfluxDBUrl?bucket=$InfluxDBBucket&org=$InfluxDBOrg&token=$InfluxDBToken")
    Write-Host "InfluxDB output enabled" -ForegroundColor Green
} else {
    Write-Host "InfluxDB output disabled (missing credentials)" -ForegroundColor Yellow
}

& $k6Path run $testPath @outParams