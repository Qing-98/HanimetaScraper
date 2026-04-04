param(
    [Parameter(Mandatory=$true, HelpMessage="Action triggered ReleaseTag (e.g. plugin-v1.3.0)")]
    [string]$ReleaseTag,
    
    [Parameter(Mandatory=$false, HelpMessage="GitHub repository owner")]
    [string]$Owner = "Qing-98",
    
    [Parameter(Mandatory=$false, HelpMessage="GitHub repository name")]
    [string]$RepoName = "HanimetaScraper"
)

$ErrorActionPreference = "Stop"

function Write-Info { param($msg) Write-Host "ℹ️  $msg" -ForegroundColor Cyan }
function Write-Success { param($msg) Write-Host "✅ $msg" -ForegroundColor Green }
function Write-Error { param($msg) Write-Host "❌ $msg" -ForegroundColor Red }

# 1. 验证和提取版本号
if ($ReleaseTag -notmatch "^plugin-v(.*)") {
    Write-Error "Invalid tag format. Expecing 'plugin-v*.*.*' but got '$ReleaseTag'"
    exit 1
}
$Version = $matches[1]

# 2. 构造下载 URL 和临时文件路径
$zipFileName = "jellyfin-plugin-hanimeta-$Version.zip"
$downloadUrl = "https://github.com/$Owner/$RepoName/releases/download/$ReleaseTag/$zipFileName"
$tempZipPath = Join-Path $env:TEMP $zipFileName

Write-Info "Plugin Version: $Version"
Write-Info "Downloading from: $downloadUrl"

# 3. 下载 Release 包
try {
    Invoke-WebRequest -Uri $downloadUrl -OutFile $tempZipPath -UseBasicParsing
    Write-Success "Downloaded successfully to $tempZipPath"
} catch {
    Write-Error "Failed to download release from $downloadUrl"
    Write-Error $_
    exit 1
}

# 4. 计算 MD5
try {
    $md5 = (Get-FileHash -Algorithm MD5 -Path $tempZipPath).Hash.ToLower()
    Write-Success "MD5 Checksum: $md5"
} catch {
    Write-Error "Failed to calculate MD5: $_"
    exit 1
}

# 5. 更新 manifest.json
$manifestPath = Join-Path $PSScriptRoot "..\Jellyfin.Plugin.Hanimeta\manifest.json"
if (-not (Test-Path $manifestPath)) {
    Write-Error "manifest.json not found at $manifestPath"
    exit 1
}

Write-Info "Updating manifest.json..."
$timestamp = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
$manifestContent = Get-Content $manifestPath -Raw | ConvertFrom-Json

# Jellyfin 通常倾向于 4 位版本号 (e.g. 1.2.0.0)。如果是 3 位，自动补齐
$formattedVersion = $Version
if (($Version -split '\.').Count -eq 3) {
    $formattedVersion = "$Version.0"
}

$manifestContent.version = $formattedVersion
$manifestContent.checksum = $md5
$manifestContent.timestamp = $timestamp

# 保存回文件
$manifestContent | ConvertTo-Json -Depth 10 | Set-Content $manifestPath -Encoding UTF8

Write-Success "Manifest Updated:"
Write-Host "  - Version:   $formattedVersion"
Write-Host "  - Checksum:  $md5"
Write-Host "  - Timestamp: $timestamp"

# 清理临时文件
Remove-Item $tempZipPath -Force

Write-Info "Next Steps:"
Write-Host "1. Git Add :" -ForegroundColor Gray "git add Jellyfin.Plugin.Hanimeta/manifest.json"
Write-Host "2. Git Cm  :" -ForegroundColor Gray "git commit -m `"chore: update manifest for release $ReleaseTag`""
Write-Host "3. Git Push:" -ForegroundColor Gray "git push origin main"

exit 0
