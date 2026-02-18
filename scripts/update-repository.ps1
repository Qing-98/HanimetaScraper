#!/usr/bin/env pwsh
# Update Repository Manifest Script
# Updates repository.json with new plugin version information

param(
    [Parameter(Mandatory=$true, HelpMessage="Plugin version (e.g., 1.3.0)")]
    [string]$Version,
    
    [Parameter(Mandatory=$true, HelpMessage="Changelog description")]
    [string]$Changelog,
    
    [Parameter(Mandatory=$true, HelpMessage="Path to plugin ZIP file")]
    [string]$ZipPath,
    
    [Parameter(Mandatory=$false, HelpMessage="Target Jellyfin ABI version")]
    [string]$TargetAbi = "10.10.1.0",
    
    [Parameter(Mandatory=$false, HelpMessage="GitHub repository owner")]
    [string]$Owner = "Qing-98",
    
    [Parameter(Mandatory=$false, HelpMessage="GitHub repository name")]
    [string]$RepoName = "HanimetaScraper"
)

# Color output functions
function Write-Info { param($msg) Write-Host "ℹ️  $msg" -ForegroundColor Cyan }
function Write-Success { param($msg) Write-Host "✅ $msg" -ForegroundColor Green }
function Write-Error { param($msg) Write-Host "❌ $msg" -ForegroundColor Red }
function Write-Warning { param($msg) Write-Host "⚠️  $msg" -ForegroundColor Yellow }

# Validate input
Write-Info "Validating inputs..."

if (-not (Test-Path $ZipPath)) {
    Write-Error "ZIP file not found: $ZipPath"
    exit 1
}

if (-not (Test-Path "repository.json")) {
    Write-Error "repository.json not found in current directory"
    exit 1
}

# Calculate MD5 checksum
Write-Info "Calculating MD5 checksum..."
try {
    $md5 = (Get-FileHash -Algorithm MD5 $ZipPath).Hash.ToLower()
    Write-Success "MD5 checksum: $md5"
} catch {
    Write-Error "Failed to calculate MD5: $_"
    exit 1
}

# Generate timestamp
$timestamp = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
Write-Info "Timestamp: $timestamp"

# Construct download URL
$zipFileName = Split-Path $ZipPath -Leaf
$downloadUrl = "https://github.com/$Owner/$RepoName/releases/download/v$Version/$zipFileName"
Write-Info "Download URL: $downloadUrl"

# Read current repository.json
Write-Info "Reading repository.json..."
try {
    $repoContent = Get-Content "repository.json" -Raw -Encoding UTF8
    $repo = $repoContent | ConvertFrom-Json
} catch {
    Write-Error "Failed to read repository.json: $_"
    exit 1
}

# Create new version entry
$newVersion = [PSCustomObject]@{
    version = "$Version.0"
    changelog = $Changelog
    targetAbi = $TargetAbi
    sourceUrl = $downloadUrl
    checksum = $md5
    timestamp = $timestamp
}

Write-Info "New version entry:"
Write-Host ($newVersion | ConvertTo-Json -Depth 10)

# Insert new version at the beginning of versions array
Write-Info "Updating repository manifest..."
$plugin = $repo[0]
$plugin.versions = @($newVersion) + $plugin.versions

# Backup original file
$backupPath = "repository.json.backup"
Copy-Item "repository.json" $backupPath
Write-Info "Backup created: $backupPath"

# Save updated repository.json
try {
    $repo | ConvertTo-Json -Depth 10 | Set-Content "repository.json" -Encoding UTF8
    Write-Success "Repository manifest updated successfully!"
} catch {
    Write-Error "Failed to save repository.json: $_"
    Write-Warning "Restoring backup..."
    Copy-Item $backupPath "repository.json" -Force
    exit 1
}

# Display summary
Write-Host ""
Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "              Repository Update Summary" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "Version:      " -NoNewline -ForegroundColor Yellow
Write-Host "$Version.0"
Write-Host "Changelog:    " -NoNewline -ForegroundColor Yellow
Write-Host "$Changelog"
Write-Host "Target ABI:   " -NoNewline -ForegroundColor Yellow
Write-Host "$TargetAbi"
Write-Host "Checksum:     " -NoNewline -ForegroundColor Yellow
Write-Host "$md5"
Write-Host "Download URL: " -NoNewline -ForegroundColor Yellow
Write-Host "$downloadUrl"
Write-Host "Timestamp:    " -NoNewline -ForegroundColor Yellow
Write-Host "$timestamp"
Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

# Next steps
Write-Host "Next Steps:" -ForegroundColor Green
Write-Host "1. Review the changes in repository.json"
Write-Host "2. Commit and push to GitHub:"
Write-Host "   git add repository.json" -ForegroundColor Gray
Write-Host "   git commit -m 'Release v$Version'" -ForegroundColor Gray
Write-Host "   git push origin main" -ForegroundColor Gray
Write-Host "3. Create GitHub release with tag v$Version" -ForegroundColor Gray
Write-Host "4. Upload $zipFileName to the release" -ForegroundColor Gray
Write-Host ""

exit 0
