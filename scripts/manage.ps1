# HanimetaScraper Windows PowerShell Management Script
# Provides advanced management capabilities for Windows users

param(
    [Parameter(Position=0)]
    [ValidateSet("build", "start", "stop", "status", "install", "uninstall", "logs", "test", "release", "setup-playwright", "help")]
    [string]$Action = "help",
    
    [string]$Version = "1.0.0",
    [switch]$Force,
    [switch]$UseDebug,
    [switch]$Quiet
)

# Configuration
$BackendPort = 8585
$JellyfinPort = 8096
$BackendServiceName = "HanimetaScraperBackend"
$JellyfinPluginsPath = "$env:ProgramData\Jellyfin\Server\plugins"
$ReleaseDir = ".\release"

# Ensure we're in the root directory
$ScriptDir = $PSScriptRoot
$RootDir = Split-Path $ScriptDir -Parent
Set-Location $RootDir

# Colors for console output
$Colors = @{
    Success = "Green"
    Warning = "Yellow" 
    Error = "Red"
    Info = "Cyan"
    Header = "Magenta"
}

function Write-ColorOutput {
    param(
        [string]$Message,
        [string]$Color = "White",
        [switch]$NoNewline
    )
    
    if (-not $Quiet) {
        if ($NoNewline) {
            Write-Host $Message -ForegroundColor $Color -NoNewline
        } else {
            Write-Host $Message -ForegroundColor $Color
        }
    }
}

function Write-Header {
    param([string]$Title)
    Write-ColorOutput "" 
    Write-ColorOutput "============================================" -Color $Colors.Header
    Write-ColorOutput "    $Title" -Color $Colors.Header
    Write-ColorOutput "============================================" -Color $Colors.Header
    Write-ColorOutput ""
}

function Test-AdminRights {
    $currentUser = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($currentUser)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Test-ServiceRunning {
    param([string]$ServiceName)
    try {
        $service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
        return ($service -and $service.Status -eq 'Running')
    } catch {
        return $false
    }
}

function Test-ProcessRunning {
    param([string]$ProcessName)
    return (Get-Process -Name $ProcessName -ErrorAction SilentlyContinue) -ne $null
}

function Test-PortListening {
    param([int]$Port)
    try {
        $connection = Test-NetConnection -Port $Port -ComputerName localhost -InformationLevel Quiet
        return $connection
    } catch {
        return $false
    }
}

function Install-DotNetIfMissing {
    try {
        $dotnetVersion = & dotnet --version 2>$null
        if ($LASTEXITCODE -eq 0) {
            $majorVersion = [int]($dotnetVersion.Split('.')[0])
            if ($majorVersion -ge 8) {
                Write-ColorOutput "✓ .NET $dotnetVersion found" -Color $Colors.Success
                return $true
            }
        }
    } catch {
        # dotnet not found
    }
    
    Write-ColorOutput "⚠ .NET 8 not found. Installing..." -Color $Colors.Warning
    
    # Download and install .NET 8
    $installerUrl = "https://download.visualstudio.microsoft.com/download/pr/93961dfb-d1e0-49c8-9230-abcba1ebab5a/811ed1eb63d7652325727720edda26a8/dotnet-sdk-8.0.403-win-x64.exe"
    $installerPath = "$env:TEMP\dotnet-sdk-installer.exe"
    
    try {
        Write-ColorOutput "Downloading .NET 8 SDK..." -Color $Colors.Info
        Invoke-WebRequest -Uri $installerUrl -OutFile $installerPath -UseBasicParsing
        
        Write-ColorOutput "Installing .NET 8 SDK..." -Color $Colors.Info
        Start-Process -FilePath $installerPath -ArgumentList "/quiet" -Wait
        
        Remove-Item $installerPath -Force
        Write-ColorOutput "✓ .NET 8 SDK installed successfully" -Color $Colors.Success
        return $true
    } catch {
        Write-ColorOutput "✗ Failed to install .NET 8 SDK: $($_.Exception.Message)" -Color $Colors.Error
        return $false
    }
}

function Install-PlaywrightBrowsers {
    Write-Header "Setting Up Playwright Browsers"
    
    Write-ColorOutput "Checking Playwright installation..." -Color $Colors.Info
    
    try {
        # First, try to find the latest Playwright version
        Write-ColorOutput "Installing/updating Playwright CLI..." -Color $Colors.Info
        
        # Try to install with latest version first
        $installResult = & dotnet tool install --global Microsoft.Playwright.CLI 2>&1
        if ($LASTEXITCODE -ne 0) {
            # If install fails, try update
            $updateResult = & dotnet tool update --global Microsoft.Playwright.CLI 2>&1
            if ($LASTEXITCODE -ne 0) {
                Write-ColorOutput "⚠ Could not install/update Playwright CLI: $updateResult" -Color $Colors.Warning
                # Try specific version as fallback
                & dotnet tool install --global Microsoft.Playwright.CLI --version 1.40.0 2>$null
                if ($LASTEXITCODE -ne 0) {
                    Write-ColorOutput "✗ Failed to install Playwright CLI" -Color $Colors.Error
                    return $false
                }
            }
        }
        
        # Refresh PATH to include .NET tools
        $env:PATH = "$env:PATH;$env:USERPROFILE\.dotnet\tools"
        
        # Check if playwright command is available
        $playwrightTest = & playwright --version 2>$null
        if ($LASTEXITCODE -ne 0) {
            Write-ColorOutput "✗ Playwright CLI not accessible after installation" -Color $Colors.Error
            return $false
        }
        
        # Install Chromium browser from the ScraperBackendService directory
        Write-ColorOutput "Installing Playwright browsers (this may take a while)..." -Color $Colors.Info
        Push-Location "ScraperBackendService"
        $browserInstall = & playwright install chromium 2>&1
        Pop-Location
        
        if ($LASTEXITCODE -eq 0) {
            Write-ColorOutput "✓ Playwright browsers installed successfully" -Color $Colors.Success
            
            # Verify installation
            $playwrightVersion = & playwright --version 2>$null
            Write-ColorOutput "Playwright version: $playwrightVersion" -Color $Colors.Info
            return $true
        } else {
            Write-ColorOutput "✗ Playwright browser installation failed: $browserInstall" -Color $Colors.Error
            return $false
        }
    } catch {
        Write-ColorOutput "✗ Playwright setup failed: $($_.Exception.Message)" -Color $Colors.Error
        return $false
    }
}

function Test-PlaywrightSetup {
    try {
        # Check if playwright CLI is available
        $playwrightVersion = & playwright --version 2>$null
        if ($LASTEXITCODE -eq 0) {
            Write-ColorOutput "✓ Playwright CLI found: $playwrightVersion" -Color $Colors.Success
            return $true
        } else {
            Write-ColorOutput "✗ Playwright CLI not found" -Color $Colors.Error
            return $false
        }
    } catch {
        Write-ColorOutput "✗ Playwright not available" -Color $Colors.Error
        return $false
    }
}

function Build-Solution {
    Write-Header "Building HanimetaScraper Solution"
    
    if (-not (Install-DotNetIfMissing)) {
        return $false
    }
    
    # Check and setup Playwright if needed
    if (-not (Test-PlaywrightSetup)) {
        Write-ColorOutput "Playwright not found. Setting up..." -Color $Colors.Warning
        if (-not (Install-PlaywrightBrowsers)) {
            Write-ColorOutput "⚠ Playwright setup failed. Backend service may not work properly with anti-bot features." -Color $Colors.Warning
        }
    }
    
    Write-ColorOutput "Restoring NuGet packages..." -Color $Colors.Info
    & dotnet restore
    if ($LASTEXITCODE -ne 0) {
        Write-ColorOutput "✗ Package restore failed" -Color $Colors.Error
        return $false
    }
    
    $configuration = if ($UseDebug) { "Debug" } else { "Release" }
    Write-ColorOutput "Building solution in $configuration mode..." -Color $Colors.Info
    
    & dotnet build --configuration $configuration --no-restore
    if ($LASTEXITCODE -ne 0) {
        Write-ColorOutput "✗ Build failed" -Color $Colors.Error
        return $false
    }
    
    Write-ColorOutput "✓ Solution built successfully" -Color $Colors.Success
    return $true
}

function Start-BackendService {
    Write-Header "Starting Backend Service"
    
    # Check if already running
    if (Test-PortListening -Port $BackendPort) {
        Write-ColorOutput "✓ Backend service is already running on port $BackendPort" -Color $Colors.Success
        return $true
    }
    
    # Verify Playwright setup before starting
    if (-not (Test-PlaywrightSetup)) {
        Write-ColorOutput "⚠ Playwright not found. Service may not work properly with anti-bot features." -Color $Colors.Warning
        Write-ColorOutput "  Run: .\scripts\manage.ps1 setup-playwright" -Color $Colors.Info
    }
    
    $backendPath = "ScraperBackendService\bin\Release\net8.0\ScraperBackendService.dll"
    if ($UseDebug) {
        $backendPath = "ScraperBackendService\bin\Debug\net8.0\ScraperBackendService.dll"
    }
    
    if (-not (Test-Path $backendPath)) {
        Write-ColorOutput "✗ Backend service not found. Run 'build' first." -Color $Colors.Error
        return $false
    }
    
    Write-ColorOutput "Starting backend service..." -Color $Colors.Info
    
    $job = Start-Job -ScriptBlock {
        param($BackendPath, $RootDir)
        Set-Location $RootDir
        & dotnet $BackendPath
    } -ArgumentList $backendPath, $RootDir
    
    # Wait for startup
    $timeout = 30
    $elapsed = 0
    while ($elapsed -lt $timeout) {
        if (Test-PortListening -Port $BackendPort) {
            Write-ColorOutput "✓ Backend service started successfully on port $BackendPort" -Color $Colors.Success
            Write-ColorOutput "  Access at: http://localhost:$BackendPort" -Color $Colors.Info
            return $true
        }
        Start-Sleep -Seconds 1
        $elapsed++
        Write-ColorOutput "." -Color $Colors.Info -NoNewline
    }
    
    Write-ColorOutput ""
    Write-ColorOutput "✗ Backend service failed to start within $timeout seconds" -Color $Colors.Error
    Stop-Job $job -Force
    Remove-Job $job -Force
    return $false
}

function Stop-BackendService {
    Write-Header "Stopping Backend Service"
    
    $processes = Get-Process | Where-Object { $_.ProcessName -like "*ScraperBackendService*" -or $_.MainWindowTitle -like "*ScraperBackendService*" }
    
    if ($processes) {
        Write-ColorOutput "Stopping backend service processes..." -Color $Colors.Info
        $processes | ForEach-Object {
            try {
                $_.Kill()
                Write-ColorOutput "✓ Stopped process $($_.Id)" -Color $Colors.Success
            } catch {
                Write-ColorOutput "✗ Failed to stop process $($_.Id): $($_.Exception.Message)" -Color $Colors.Warning
            }
        }
    } else {
        Write-ColorOutput "No backend service processes found" -Color $Colors.Info
    }
    
    # Also stop any background jobs
    Get-Job | Where-Object { $_.Name -like "*Backend*" -or $_.Command -like "*ScraperBackendService*" } | Stop-Job -Force
    Get-Job | Where-Object { $_.State -eq "Stopped" } | Remove-Job -Force
}

function Get-ServiceStatus {
    Write-Header "Service Status"
    
    # Backend service status
    Write-ColorOutput "Backend Service:" -Color $Colors.Header
    if (Test-PortListening -Port $BackendPort) {
        Write-ColorOutput "  ✓ Running on port $BackendPort" -Color $Colors.Success
        
        # Try to get version info
        try {
            $response = Invoke-RestMethod -Uri "http://localhost:$BackendPort/" -TimeoutSec 5
            Write-ColorOutput "  Version: $($response.version)" -Color $Colors.Info
            Write-ColorOutput "  Uptime: $($response.uptime)" -Color $Colors.Info
        } catch {
            Write-ColorOutput "  ⚠ Running but not responding to API calls" -Color $Colors.Warning
        }
    } else {
        Write-ColorOutput "  ✗ Not running" -Color $Colors.Error
    }
    
    # Playwright status
    Write-ColorOutput ""
    Write-ColorOutput "Playwright Status:" -Color $Colors.Header
    if (Test-PlaywrightSetup) {
        $playwrightVersion = & playwright --version 2>$null
        Write-ColorOutput "  ✓ Installed: $playwrightVersion" -Color $Colors.Success
    } else {
        Write-ColorOutput "  ✗ Not installed" -Color $Colors.Error
        Write-ColorOutput "  Run: .\scripts\manage.ps1 setup-playwright" -Color $Colors.Info
    }
    
    # Jellyfin status
    Write-ColorOutput ""
    Write-ColorOutput "Jellyfin Service:" -Color $Colors.Header
    if (Test-PortListening -Port $JellyfinPort) {
        Write-ColorOutput "  ✓ Running on port $JellyfinPort" -Color $Colors.Success
        Write-ColorOutput "  Access at: http://localhost:$JellyfinPort" -Color $Colors.Info
    } else {
        Write-ColorOutput "  ✗ Not running" -Color $Colors.Error
    }
    
    # Plugin installation status
    Write-ColorOutput ""
    Write-ColorOutput "Plugin Installation:" -Color $Colors.Header
    
    if (Test-Path $JellyfinPluginsPath) {
        $dlsitePlugin = Test-Path "$JellyfinPluginsPath\Jellyfin.Plugin.Hanimeta.DLsiteScraper\Jellyfin.Plugin.Hanimeta.DLsiteScraper.dll"
        $hanimePlugin = Test-Path "$JellyfinPluginsPath\Jellyfin.Plugin.Hanimeta.HanimeScraper\Jellyfin.Plugin.Hanimeta.HanimeScraper.dll"
        
        Write-ColorOutput "  DLsite Plugin: $(if ($dlsitePlugin) { "✓ Installed" } else { "✗ Not installed" })" -Color $(if ($dlsitePlugin) { $Colors.Success } else { $Colors.Error })
        Write-ColorOutput "  Hanime Plugin: $(if ($hanimePlugin) { "✓ Installed" } else { "✗ Not installed" })" -Color $(if ($hanimePlugin) { $Colors.Success } else { $Colors.Error })
    } else {
        Write-ColorOutput "  ✗ Jellyfin plugins directory not found" -Color $Colors.Error
    }
}

function Install-Plugins {
    Write-Header "Installing Jellyfin Plugins"
    
    if (-not (Test-Path $JellyfinPluginsPath)) {
        Write-ColorOutput "✗ Jellyfin plugins directory not found: $JellyfinPluginsPath" -Color $Colors.Error
        Write-ColorOutput "  Make sure Jellyfin is installed and has been run at least once." -Color $Colors.Info
        return $false
    }
    
    # Check if Jellyfin is running
    if (Test-PortListening -Port $JellyfinPort) {
        if (-not $Force) {
            Write-ColorOutput "⚠ Jellyfin is running. Stop it before installing plugins or use -Force" -Color $Colors.Warning
            Write-ColorOutput "  You can stop Jellyfin service with: Stop-Service JellyfinServer" -Color $Colors.Info
            return $false
        } else {
            Write-ColorOutput "⚠ Jellyfin is running, but -Force specified. Installing anyway..." -Color $Colors.Warning
        }
    }
    
    # Build plugins first if needed
    $dlsiteDll = "Jellyfin.Plugin.Hanimeta.DLsiteScraper\bin\Release\net8.0\Jellyfin.Plugin.Hanimeta.DLsiteScraper.dll"
    $hanimeDll = "Jellyfin.Plugin.Hanimeta.HanimeScraper\bin\Release\net8.0\Jellyfin.Plugin.Hanimeta.HanimeScraper.dll"
    $commonDll = "Jellyfin.Plugin.Hanimeta.Common\bin\Release\net8.0\Jellyfin.Plugin.Hanimeta.Common.dll"
    
    if (-not (Test-Path $dlsiteDll) -or -not (Test-Path $hanimeDll) -or -not (Test-Path $commonDll)) {
        Write-ColorOutput "Plugin files not found. Building first..." -Color $Colors.Info
        if (-not (Build-Solution)) {
            return $false
        }
    }
    
    # Install DLsite plugin
    Write-ColorOutput "Installing DLsite plugin..." -Color $Colors.Info
    $dlsitePluginDir = "$JellyfinPluginsPath\Jellyfin.Plugin.Hanimeta.DLsiteScraper"
    New-Item -Path $dlsitePluginDir -ItemType Directory -Force | Out-Null
    
    Copy-Item $dlsiteDll $dlsitePluginDir -Force
    Copy-Item $commonDll $dlsitePluginDir -Force
    Copy-Item "Jellyfin.Plugin.Hanimeta.DLsiteScraper\manifest.json" $dlsitePluginDir -Force
    
    # Install Hanime plugin
    Write-ColorOutput "Installing Hanime plugin..." -Color $Colors.Info
    $hanimePluginDir = "$JellyfinPluginsPath\Jellyfin.Plugin.Hanimeta.HanimeScraper"
    New-Item -Path $hanimePluginDir -ItemType Directory -Force | Out-Null
    
    Copy-Item $hanimeDll $hanimePluginDir -Force
    Copy-Item $commonDll $hanimePluginDir -Force
    Copy-Item "Jellyfin.Plugin.Hanimeta.HanimeScraper\manifest.json" $hanimePluginDir -Force
    
    Write-ColorOutput "✓ Plugins installed successfully" -Color $Colors.Success
    Write-ColorOutput "  Restart Jellyfin to load the new plugins" -Color $Colors.Info
    
    return $true
}

function Uninstall-Plugins {
    Write-Header "Uninstalling Jellyfin Plugins"
    
    if (Test-PortListening -Port $JellyfinPort) {
        if (-not $Force) {
            Write-ColorOutput "⚠ Jellyfin is running. Stop it before uninstalling plugins or use -Force" -Color $Colors.Warning
            return $false
        }
    }
    
    $dlsitePluginDir = "$JellyfinPluginsPath\Jellyfin.Plugin.Hanimeta.DLsiteScraper"
    $hanimePluginDir = "$JellyfinPluginsPath\Jellyfin.Plugin.Hanimeta.HanimeScraper"
    
    if (Test-Path $dlsitePluginDir) {
        Remove-Item $dlsitePluginDir -Recurse -Force
        Write-ColorOutput "✓ DLsite plugin uninstalled" -Color $Colors.Success
    }
    
    if (Test-Path $hanimePluginDir) {
        Remove-Item $hanimePluginDir -Recurse -Force
        Write-ColorOutput "✓ Hanime plugin uninstalled" -Color $Colors.Success
    }
    
    Write-ColorOutput "✓ Plugins uninstalled. Restart Jellyfin to complete removal." -Color $Colors.Success
}

function Show-Logs {
    Write-Header "Service Logs"
    
    # Backend service logs (if running in console)
    Write-ColorOutput "Recent backend service activity:" -Color $Colors.Header
    
    if (Test-PortListening -Port $BackendPort) {
        try {
            $logResponse = Invoke-RestMethod -Uri "http://localhost:$BackendPort/logs/recent" -TimeoutSec 5
            $logResponse | ForEach-Object { Write-ColorOutput $_ -Color $Colors.Info }
        } catch {
            Write-ColorOutput "Could not retrieve logs from backend service" -Color $Colors.Warning
        }
    } else {
        Write-ColorOutput "Backend service is not running" -Color $Colors.Error
    }
    
    # Windows Event Logs
    Write-ColorOutput ""
    Write-ColorOutput "Windows Event Logs (Application):" -Color $Colors.Header
    try {
        $events = Get-EventLog -LogName Application -Source "*Jellyfin*" -Newest 5 -ErrorAction SilentlyContinue
        if ($events) {
            $events | ForEach-Object {
                Write-ColorOutput "$($_.TimeGenerated): $($_.Message)" -Color $Colors.Info
            }
        } else {
            Write-ColorOutput "No recent Jellyfin events found" -Color $Colors.Info
        }
    } catch {
        Write-ColorOutput "Could not access Windows Event Log" -Color $Colors.Warning
    }
}

function Test-Installation {
    Write-Header "Testing Installation"
    
    $success = $true
    
    # Test Playwright setup
    Write-ColorOutput "Testing Playwright setup..." -Color $Colors.Info
    if (Test-PlaywrightSetup) {
        Write-ColorOutput "✓ Playwright is properly installed" -Color $Colors.Success
    } else {
        Write-ColorOutput "✗ Playwright setup incomplete" -Color $Colors.Error
        Write-ColorOutput "  Run: .\scripts\manage.ps1 setup-playwright" -Color $Colors.Info
        $success = $false
    }
    
    # Test backend service
    Write-ColorOutput ""
    Write-ColorOutput "Testing backend service..." -Color $Colors.Info
    if (-not (Test-PortListening -Port $BackendPort)) {
        Write-ColorOutput "✗ Backend service not running" -Color $Colors.Error
        $success = $false
    } else {
        try {
            $healthResponse = Invoke-RestMethod -Uri "http://localhost:$BackendPort/health" -TimeoutSec 5
            Write-ColorOutput "✓ Backend service health check passed" -Color $Colors.Success
            
            # Test API endpoints
            $searchResponse = Invoke-RestMethod -Uri "http://localhost:$BackendPort/api/hanime/search?title=test&max=1" -TimeoutSec 10
            Write-ColorOutput "✓ API search endpoint working" -Color $Colors.Success
        } catch {
            Write-ColorOutput "✗ Backend service API test failed: $($_.Exception.Message)" -Color $Colors.Error
            $success = $false
        }
    }
    
    # Test plugin installation
    Write-ColorOutput ""
    Write-ColorOutput "Testing plugin installation..." -Color $Colors.Info
    $dlsiteInstalled = Test-Path "$JellyfinPluginsPath\Jellyfin.Plugin.Hanimeta.DLsiteScraper\Jellyfin.Plugin.Hanimeta.DLsiteScraper.dll"
    $hanimeInstalled = Test-Path "$JellyfinPluginsPath\Jellyfin.Plugin.Hanimeta.HanimeScraper\Jellyfin.Plugin.Hanimeta.HanimeScraper.dll"
    
    if ($dlsiteInstalled -and $hanimeInstalled) {
        Write-ColorOutput "✓ Both plugins are installed" -Color $Colors.Success
    } else {
        Write-ColorOutput "✗ Some plugins are missing" -Color $Colors.Error
        $success = $false
    }
    
    return $success
}

function Create-Release {
    param([string]$Version)
    
    Write-Header "Creating Release v$Version"
    
    # Build everything first
    if (-not (Build-Solution)) {
        return $false
    }
    
    # Ensure Playwright is set up
    if (-not (Test-PlaywrightSetup)) {
        Write-ColorOutput "⚠ Playwright not found. Installing..." -Color $Colors.Warning
        if (-not (Install-PlaywrightBrowsers)) {
            Write-ColorOutput "✗ Failed to set up Playwright. Release may not work properly." -Color $Colors.Error
        }
    }
    
    # Create release directory
    if (Test-Path $ReleaseDir) {
        Remove-Item $ReleaseDir -Recurse -Force
    }
    New-Item -Path $ReleaseDir -ItemType Directory -Force | Out-Null
    
    # Package backend service
    Write-ColorOutput "Packaging backend service..." -Color $Colors.Info
    $backendReleaseDir = "$ReleaseDir\backend"
    New-Item -Path $backendReleaseDir -ItemType Directory -Force | Out-Null
    
    Push-Location "ScraperBackendService"
    & dotnet publish -c Release -o "..\$backendReleaseDir" --no-build
    Pop-Location
    
    # Package plugins
    Write-ColorOutput "Packaging plugins..." -Color $Colors.Info
    $pluginsReleaseDir = "$ReleaseDir\plugins"
    New-Item -Path $pluginsReleaseDir -ItemType Directory -Force | Out-Null
    
    # DLsite plugin
    $dlsitePluginReleaseDir = "$pluginsReleaseDir\DLsiteScraper"
    New-Item -Path $dlsitePluginReleaseDir -ItemType Directory -Force | Out-Null
    Copy-Item "Jellyfin.Plugin.Hanimeta.DLsiteScraper\bin\Release\net8.0\*.dll" $dlsitePluginReleaseDir
    Copy-Item "Jellyfin.Plugin.Hanimeta.DLsiteScraper\manifest.json" $dlsitePluginReleaseDir
    
    # Hanime plugin
    $hanimePluginReleaseDir = "$pluginsReleaseDir\HanimeScraper"
    New-Item -Path $hanimePluginReleaseDir -ItemType Directory -Force | Out-Null
    Copy-Item "Jellyfin.Plugin.Hanimeta.HanimeScraper\bin\Release\net8.0\*.dll" $hanimePluginReleaseDir
    Copy-Item "Jellyfin.Plugin.Hanimeta.HanimeScraper\manifest.json" $hanimePluginReleaseDir
    
    # Create Playwright setup scripts for backend
    Write-ColorOutput "Creating Playwright setup scripts..." -Color $Colors.Info
    
    @"
#!/bin/bash
echo "Installing Playwright browsers for HanimetaScraper..."
dotnet tool install --global Microsoft.Playwright.CLI || dotnet tool update --global Microsoft.Playwright.CLI
export PATH="`$PATH:`$HOME/.dotnet/tools"
playwright install chromium --with-deps
echo "Playwright setup complete."
"@ | Out-File -FilePath "$backendReleaseDir\install-playwright.sh" -Encoding UTF8
    
    @"
@echo off
echo Installing Playwright browsers for HanimetaScraper...
dotnet tool install --global Microsoft.Playwright.CLI || dotnet tool update --global Microsoft.Playwright.CLI
playwright install chromium --with-deps
echo Playwright setup complete.
pause
"@ | Out-File -FilePath "$backendReleaseDir\install-playwright.bat" -Encoding UTF8
    
    # Create ZIP files
    Write-ColorOutput "Creating ZIP files..." -Color $Colors.Info
    
    # Backend service ZIP
    Compress-Archive -Path "$backendReleaseDir\*" -DestinationPath "$ReleaseDir\ScraperBackendService-$Version.zip" -Force
    
    # Plugin ZIPs
    Compress-Archive -Path "$dlsitePluginReleaseDir\*" -DestinationPath "$ReleaseDir\Jellyfin.Plugin.Hanimeta.DLsiteScraper.zip" -Force
    Compress-Archive -Path "$hanimePluginReleaseDir\*" -DestinationPath "$ReleaseDir\Jellyfin.Plugin.Hanimeta.HanimeScraper.zip" -Force
    
    # Create startup scripts
    Write-ColorOutput "Creating startup scripts..." -Color $Colors.Info
    
    @"
@echo off
echo Starting HanimetaScraper Backend Service...
echo.
echo Note: This service requires Playwright browsers.
echo If this is the first run, please run install-playwright.bat first.
echo.
cd backend
dotnet ScraperBackendService.dll
pause
"@ | Out-File -FilePath "$ReleaseDir\start-backend.bat" -Encoding ASCII
    
    @"
#!/bin/bash
echo "Starting HanimetaScraper Backend Service..."
echo ""
echo "Note: This service requires Playwright browsers."
echo "If this is the first run, please run install-playwright.sh first."
echo ""
cd backend
dotnet ScraperBackendService.dll
"@ | Out-File -FilePath "$ReleaseDir\start-backend.sh" -Encoding UTF8
    
    # Generate checksums
    Write-ColorOutput "Generating checksums..." -Color $Colors.Info
    $checksums = @()
    Get-ChildItem "$ReleaseDir\*.zip" | ForEach-Object {
        $hash = Get-FileHash $_.FullName -Algorithm SHA256
        $checksums += "$($hash.Hash.ToLower())  $($_.Name)"
    }
    $checksums | Out-File -FilePath "$ReleaseDir\checksums.txt" -Encoding UTF8
    
    Write-ColorOutput "✓ Release v$Version created successfully in $ReleaseDir" -Color $Colors.Success
    
    # List files
    Write-ColorOutput ""
    Write-ColorOutput "Release contents:" -Color $Colors.Header
    Get-ChildItem $ReleaseDir | ForEach-Object {
        Write-ColorOutput "  $($_.Name)" -Color $Colors.Info
    }
    
    return $true
}

function Show-Help {
    Write-Header "HanimetaScraper Windows Management Script"
    
    Write-ColorOutput "Usage: .\scripts\manage.ps1 [Action] [Options]" -Color $Colors.Info
    Write-ColorOutput ""
    Write-ColorOutput "Actions:" -Color $Colors.Header
    Write-ColorOutput "  build           Build the entire solution" -Color $Colors.Info
    Write-ColorOutput "  start           Start the backend service" -Color $Colors.Info
    Write-ColorOutput "  stop            Stop the backend service" -Color $Colors.Info
    Write-ColorOutput "  status          Show service status" -Color $Colors.Info
    Write-ColorOutput "  install         Install Jellyfin plugins" -Color $Colors.Info
    Write-ColorOutput "  uninstall       Uninstall Jellyfin plugins" -Color $Colors.Info
    Write-ColorOutput "  logs            Show recent logs" -Color $Colors.Info
    Write-ColorOutput "  test            Test the installation" -Color $Colors.Info
    Write-ColorOutput "  release         Create release package" -Color $Colors.Info
    Write-ColorOutput "  setup-playwright Setup Playwright browsers" -Color $Colors.Info
    Write-ColorOutput "  help            Show this help" -Color $Colors.Info
    Write-ColorOutput ""
    Write-ColorOutput "Options:" -Color $Colors.Header
    Write-ColorOutput "  -Version   Specify version for release (default: 1.0.0)" -Color $Colors.Info
    Write-ColorOutput "  -Force     Force action even if conflicts exist" -Color $Colors.Info
    Write-ColorOutput "  -UseDebug  Use Debug build configuration" -Color $Colors.Info
    Write-ColorOutput "  -Quiet     Suppress non-essential output" -Color $Colors.Info
    Write-ColorOutput ""
    Write-ColorOutput "Examples:" -Color $Colors.Header
    Write-ColorOutput "  .\scripts\manage.ps1 build" -Color $Colors.Info
    Write-ColorOutput "  .\scripts\manage.ps1 setup-playwright" -Color $Colors.Info
    Write-ColorOutput "  .\scripts\manage.ps1 start" -Color $Colors.Info
    Write-ColorOutput "  .\scripts\manage.ps1 install -Force" -Color $Colors.Info
    Write-ColorOutput "  .\scripts\manage.ps1 release -Version '1.1.0'" -Color $Colors.Info
    Write-ColorOutput ""
    Write-ColorOutput "Note: Backend service uses Playwright for anti-bot features." -Color $Colors.Warning
    Write-ColorOutput "Run 'setup-playwright' before first use." -Color $Colors.Warning
    Write-ColorOutput ""
    Write-ColorOutput "Current working directory: $(Get-Location)" -Color $Colors.Info
}

# Main execution
try {
    switch ($Action.ToLower()) {
        "build" { 
            $result = Build-Solution
            exit $(if ($result) { 0 } else { 1 })
        }
        "start" { 
            $result = Start-BackendService
            exit $(if ($result) { 0 } else { 1 })
        }
        "stop" { 
            Stop-BackendService
            exit 0
        }
        "status" { 
            Get-ServiceStatus
            exit 0
        }
        "install" { 
            $result = Install-Plugins
            exit $(if ($result) { 0 } else { 1 })
        }
        "uninstall" { 
            Uninstall-Plugins
            exit 0
        }
        "logs" { 
            Show-Logs
            exit 0
        }
        "test" { 
            $result = Test-Installation
            exit $(if ($result) { 0 } else { 1 })
        }
        "release" { 
            $result = Create-Release -Version $Version
            exit $(if ($result) { 0 } else { 1 })
        }
        "setup-playwright" {
            $result = Install-PlaywrightBrowsers
            exit $(if ($result) { 0 } else { 1 })
        }
        default { 
            Show-Help
            exit 0
        }
    }
} catch {
    Write-ColorOutput "✗ Unexpected error: $($_.Exception.Message)" -Color $Colors.Error
    Write-ColorOutput "Stack trace: $($_.ScriptStackTrace)" -Color $Colors.Error
    exit 1
}