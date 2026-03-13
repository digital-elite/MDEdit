# PowerShell script to increment build number
$buildNumberFile = Join-Path $PSScriptRoot "BuildNumber.txt"

if (Test-Path $buildNumberFile) {
    $buildNumber = [int](Get-Content $buildNumberFile).Trim()
} else {
    $buildNumber = 0
}

$buildNumber++
$buildNumber | Out-File -FilePath $buildNumberFile -NoNewline -Encoding ASCII

Write-Host "Build number incremented to: $buildNumber" -ForegroundColor Green
