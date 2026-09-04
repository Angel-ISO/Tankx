param(
    [Parameter(Mandatory=$false)]
    [string]$BaseUrl = "http://localhost:5074",
    [Parameter(Mandatory=$false)]
    [int]$Requests = 1000,
    [Parameter(Mandatory=$false)]
    [int]$Concurrency = 10,
    [Parameter(Mandatory=$false)]
    [string]$AuthToken = ""
)

$endpoint = "$BaseUrl/tankx/RedisCache/leaderboard/top?count=10"
Write-Host "Running Apache Bench (ab) test" -ForegroundColor Green
Write-Host "Endpoint: $endpoint" -ForegroundColor Gray
Write-Host "Requests: $Requests" -ForegroundColor Gray
Write-Host "Concurrency: $Concurrency" -ForegroundColor Gray
Write-Host ""

$headers = @{}
if ($AuthToken) {
    $headers["Authorization"] = "Bearer $AuthToken"
}

$abAvailable = Get-Command ab -ErrorAction SilentlyContinue

if ($abAvailable) {
    $headerArgs = @()
    foreach ($key in $headers.Keys) {
        $headerArgs += "-H"
        $headerArgs += "$($key): $($headers[$key])"
    }
    & ab -n $Requests -c $Concurrency @headerArgs $endpoint
} else {
    Write-Host "Apache Bench not found. Using PowerShell alternative..." -ForegroundColor Yellow
    Write-Host ""
    
    $results = @()
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    
    for ($i = 0; $i -lt $Requests; $i++) {
        try {
            $response = Invoke-WebRequest -Uri $endpoint -Headers $headers -UseBasicParsing -TimeoutSec 30
            $results += @{ Success = $true; StatusCode = $response.StatusCode; Duration = $response.Headers["X-Request-Time"] }
        } catch {
            $results += @{ Success = $false; StatusCode = $_.Exception.Response.StatusCode.value__; Duration = -1 }
        }
        
        if (($i + 1) % 100 -eq 0) {
            Write-Host "  Completed $($i + 1)/$Requests requests..." -ForegroundColor Gray
        }
    }
    
    $stopwatch.Stop()
    
    $successful = ($results | Where-Object { $_.Success -eq $true }).Count
    $failed = ($results | Where-Object { $_.Success -eq $false }).Count
    $avgDuration = ($results | Where-Object { $_.Duration -gt 0 } | Measure-Object -Property Duration -Average).Average
    
    Write-Host ""
    Write-Host "Test completed in $([math]::Round($stopwatch.Elapsed.TotalSeconds, 2)) seconds" -ForegroundColor Green
    Write-Host "Total requests: $Requests" -ForegroundColor Gray
    Write-Host "Successful: $successful" -ForegroundColor Green
    Write-Host "Failed: $failed" -ForegroundColor $(if ($failed -gt 0) { "Red" } else { "Gray" })
    Write-Host "Average duration: $([math]::Round($avgDuration, 2))ms" -ForegroundColor Gray
    Write-Host "Requests per second: $([math]::Round($Requests / $stopwatch.Elapsed.TotalSeconds, 2))" -ForegroundColor Gray
}
