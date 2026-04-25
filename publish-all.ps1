#Requires -Version 5.1
$ErrorActionPreference = "Stop"
$root = $PSScriptRoot

function Publish-Project([string]$csproj, [string]$label) {
    Write-Host ""
    Write-Host "[$label] Publishing..." -ForegroundColor Cyan
    dotnet publish "$root\$csproj" /p:PublishProfile=Release-win-x64
    if ($LASTEXITCODE -ne 0) { throw "[$label] dotnet publish failed (exit code $LASTEXITCODE)" }
    Write-Host "[$label] Done." -ForegroundColor Green
}

Publish-Project "HeartBeatProject.Tx\HeartBeatProject.Tx.csproj" "TX"
Publish-Project "HeartBeatProject.Rx\HeartBeatProject.Rx.csproj" "RX"

Write-Host ""
Write-Host "All packages ready:" -ForegroundColor Green
Write-Host "  TX -> $root\publish\TX"
Write-Host "  RX -> $root\publish\RX"
