# Test script for WebdoctorV metrics
# Tests Prometheus metrics endpoint

$ErrorActionPreference = "Stop"

Write-Host "=== WebdoctorV Metrics Test ===" -ForegroundColor Cyan
Write-Host ""

$metricsUrl = "http://localhost:8080/metrics"

# Test 1: Check if metrics endpoint is accessible
Write-Host "1. Testing metrics endpoint accessibility..." -ForegroundColor Yellow
try {
    $response = Invoke-WebRequest -Uri $metricsUrl -UseBasicParsing
    Write-Host "   [OK] Metrics endpoint is accessible (Status: $($response.StatusCode))" -ForegroundColor Green
    Write-Host "   Response size: $($response.Content.Length) bytes" -ForegroundColor Gray
} catch {
    Write-Host "   [FAIL] Cannot access metrics endpoint: $_" -ForegroundColor Red
    exit 1
}

$metrics = $response.Content
$lines = $metrics -split "`n"

# Test 2: Check Prometheus format
Write-Host "`n2. Checking Prometheus format..." -ForegroundColor Yellow
$hasHelp = ($lines | Where-Object { $_ -match "^# HELP" }).Count -gt 0
$hasType = ($lines | Where-Object { $_ -match "^# TYPE" }).Count -gt 0
if ($hasHelp -and $hasType) {
    Write-Host "   [OK] Valid Prometheus format (HELP and TYPE comments present)" -ForegroundColor Green
} else {
    Write-Host "   [FAIL] Invalid Prometheus format" -ForegroundColor Red
    exit 1
}

# Test 3: Check webdoctor custom metrics
Write-Host "`n3. Checking webdoctor custom metrics..." -ForegroundColor Yellow
$requiredMetrics = @(
    "webdoctor_item_status",
    "webdoctor_item_last_duration",
    "webdoctor_item_last_check_date",
    "webdoctor_executable_item_count",
    "webdoctor_checks_total",
    "webdoctor_service_uptime_percent"
)

$foundMetrics = @()
foreach ($metric in $requiredMetrics) {
    $found = ($lines | Where-Object { $_ -match "^$metric" }).Count -gt 0
    if ($found) {
        $foundMetrics += $metric
        Write-Host "   [OK] Found: $metric" -ForegroundColor Green
    } else {
        Write-Host "   [FAIL] Missing: $metric" -ForegroundColor Red
    }
}

if ($foundMetrics.Count -eq $requiredMetrics.Count) {
    Write-Host "   [OK] All required webdoctor metrics present" -ForegroundColor Green
} else {
    Write-Host "   [WARN] Only $($foundMetrics.Count)/$($requiredMetrics.Count) metrics found" -ForegroundColor Yellow
}

# Test 4: Check metric values
Write-Host "`n4. Checking metric values..." -ForegroundColor Yellow
$statusLines = $lines | Where-Object { $_ -match "^webdoctor_item_status" }
$alive = ($statusLines | Where-Object { $_ -match '\s+1\s*$' }).Count
$dead = ($statusLines | Where-Object { $_ -match '\s+0\s*$' }).Count
$pending = ($statusLines | Where-Object { $_ -match '\s+-1\s*$' }).Count
$total = $statusLines.Count

Write-Host "   Total services: $total" -ForegroundColor Gray
Write-Host "   Alive: $alive" -ForegroundColor Green
Write-Host "   Dead: $dead" -ForegroundColor Red
Write-Host "   Pending: $pending" -ForegroundColor Yellow

$itemCountLine = $lines | Where-Object { $_ -match "^webdoctor_executable_item_count\s+" } | Select-Object -First 1
if ($itemCountLine -match '\s+(\d+)\s*$') {
    $itemCount = [int]$matches[1]
    Write-Host "   Executable items: $itemCount" -ForegroundColor Gray
    if ($itemCount -eq $total) {
        Write-Host "   [OK] Item count matches status metrics" -ForegroundColor Green
    } else {
        Write-Host "   [WARN] Item count ($itemCount) doesn't match status metrics ($total)" -ForegroundColor Yellow
    }
}

# Test 5: Check counters
Write-Host "`n5. Checking check counters..." -ForegroundColor Yellow
$checkLines = $lines | Where-Object { $_ -match "^webdoctor_checks_total" }
$totalChecks = 0
foreach ($line in $checkLines) {
    if ($line -match '\s+(\d+)\s*$') {
        $totalChecks += [int]$matches[1]
    }
}
Write-Host "   Total checks performed: $totalChecks" -ForegroundColor Gray
if ($totalChecks -gt 0) {
    Write-Host "   [OK] Check counters are working" -ForegroundColor Green
} else {
    Write-Host "   [WARN] No checks recorded yet" -ForegroundColor Yellow
}

# Test 6: Check durations
Write-Host "`n6. Checking duration metrics..." -ForegroundColor Yellow
$durationLines = $lines | Where-Object { $_ -match "^webdoctor_item_last_duration" }
$validDurations = ($durationLines | Where-Object { $_ -match '\s+[\d.]+\s*$' }).Count
Write-Host "   Services with duration data: $validDurations/$total" -ForegroundColor Gray
if ($validDurations -gt 0) {
    Write-Host "   [OK] Duration metrics are present" -ForegroundColor Green
} else {
    Write-Host "   [WARN] No duration data" -ForegroundColor Yellow
}

# Test 7: Check timestamps
Write-Host "`n7. Checking timestamp metrics..." -ForegroundColor Yellow
$timestampLines = $lines | Where-Object { $_ -match "^webdoctor_item_last_check_date" }
$validTimestamps = ($timestampLines | Where-Object { $_ -match '\s+\d+\s*$' }).Count
Write-Host "   Services with timestamp data: $validTimestamps/$total" -ForegroundColor Gray
if ($validTimestamps -gt 0) {
    Write-Host "   [OK] Timestamp metrics are present" -ForegroundColor Green
} else {
    Write-Host "   [WARN] No timestamp data" -ForegroundColor Yellow
}

# Test 8: Check ASP.NET Core metrics
Write-Host "`n8. Checking ASP.NET Core metrics..." -ForegroundColor Yellow
$aspnetMetrics = @(
    "http_requests_received_total",
    "http_request_duration_seconds",
    "process_cpu_seconds_total",
    "process_working_set_bytes"
)

$foundAspNet = 0
foreach ($metric in $aspnetMetrics) {
    $found = ($lines | Where-Object { $_ -match "^$metric" -or $_ -match "^# TYPE\s+$metric" }).Count -gt 0
    if ($found) {
        $foundAspNet++
        Write-Host "   [OK] Found: $metric" -ForegroundColor Green
    }
}

if ($foundAspNet -gt 0) {
    Write-Host "   [OK] ASP.NET Core metrics are present" -ForegroundColor Green
} else {
    Write-Host "   [WARN] No ASP.NET Core metrics found" -ForegroundColor Yellow
}

# Test 9: Test metrics update (wait and check again)
Write-Host "`n9. Testing metrics update (waiting 35 seconds for next check cycle)..." -ForegroundColor Yellow
$firstTimestamp = ($lines | Where-Object { $_ -match "^webdoctor_item_last_check_date" } | Select-Object -First 1)
if ($firstTimestamp -match '\s+(\d+)\s*$') {
    $firstTime = [long]$matches[1]
    Write-Host "   Initial timestamp: $firstTime" -ForegroundColor Gray
    
    Write-Host "   Waiting 35 seconds..." -ForegroundColor Gray
    Start-Sleep -Seconds 35
    
    Write-Host "   Fetching updated metrics..." -ForegroundColor Gray
    $response2 = Invoke-WebRequest -Uri $metricsUrl -UseBasicParsing
    $metrics2 = $response2.Content
    $lines2 = $metrics2 -split "`n"
    $secondTimestamp = ($lines2 | Where-Object { $_ -match "^webdoctor_item_last_check_date" } | Select-Object -First 1)
    
    if ($secondTimestamp -match '\s+(\d+)\s*$') {
        $secondTime = [long]$matches[1]
        Write-Host "   Updated timestamp: $secondTime" -ForegroundColor Gray
        
        if ($secondTime -gt $firstTime) {
            Write-Host "   [OK] Metrics are updating (timestamp increased)" -ForegroundColor Green
        } else {
            Write-Host "   [WARN] Timestamp didn't change (may need more time)" -ForegroundColor Yellow
        }
    }
}

Write-Host "`n=== Test Summary ===" -ForegroundColor Cyan
Write-Host "All basic tests completed!" -ForegroundColor Green
Write-Host ""
Write-Host "Metrics endpoint: $metricsUrl" -ForegroundColor Gray
Write-Host "You can view metrics in browser or use Prometheus to scrape them." -ForegroundColor Gray
