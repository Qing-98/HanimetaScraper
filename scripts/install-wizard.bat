@echo off
REM HanimetaScraper Windows Installation Wizard
REM Interactive setup for Windows users

setlocal enabledelayedexpansion

title HanimetaScraper Installation Wizard
color 0B

echo.
echo ========================================================
echo           HanimetaScraper Installation Wizard
echo ========================================================
echo.
echo This wizard will guide you through the installation of
echo HanimetaScraper backend service and Jellyfin plugins.
echo.
echo Note: This includes setting up Playwright browsers for
echo anti-bot features.
echo.
pause

:check_admin
echo [1/7] Checking administrator privileges...
net session >nul 2>&1
if %errorLevel% == 0 (
    echo ✓ Running as administrator
) else (
    echo.
    echo ⚠ This installer requires administrator privileges for:
    echo   - Installing .NET 8 SDK if missing
    echo   - Installing Playwright browsers
    echo   - Installing plugins to Jellyfin directory
    echo   - Creating Windows services (optional)
    echo.
    echo Please right-click this file and select "Run as administrator"
    pause
    exit /b 1
)

:check_dotnet
echo.
echo [2/7] Checking .NET 8 SDK...
dotnet --version >nul 2>&1
if %errorLevel% == 0 (
    for /f "tokens=1 delims=." %%i in ('dotnet --version 2^>nul') do set DOTNET_MAJOR=%%i
    if !DOTNET_MAJOR! geq 8 (
        echo ✓ .NET 8 SDK found
        goto setup_playwright
    )
)

echo.
echo .NET 8 SDK not found or version is too old.
echo.
set /p install_dotnet="Do you want to download and install .NET 8 SDK? (Y/N): "
if /i "%install_dotnet%"=="Y" (
    echo.
    echo Downloading .NET 8 SDK...
    powershell -Command "Invoke-WebRequest -Uri 'https://download.visualstudio.microsoft.com/download/pr/93961dfb-d1e0-49c8-9230-abcba1ebab5a/811ed1eb63d7652325727720edda26a8/dotnet-sdk-8.0.403-win-x64.exe' -OutFile '%TEMP%\dotnet-sdk-installer.exe'"
    
    echo Installing .NET 8 SDK...
    "%TEMP%\dotnet-sdk-installer.exe" /quiet
    
    echo Cleaning up...
    del "%TEMP%\dotnet-sdk-installer.exe" 2>nul
    
    echo.
    echo Please restart this installer after .NET 8 SDK installation is complete.
    pause
    exit /b 0
) else (
    echo.
    echo Installation cannot continue without .NET 8 SDK.
    echo Please download and install it manually from:
    echo https://dotnet.microsoft.com/download/dotnet/8.0
    pause
    exit /b 1
)

:setup_playwright
echo.
echo [3/7] Setting up Playwright browsers...
echo.
echo Playwright is required for anti-bot and anti-detection features.
echo This may take several minutes to download and install browsers.
echo.
set /p install_playwright="Install Playwright browsers? (Recommended: Y/N): "
if /i "%install_playwright%"=="Y" (
    echo.
    echo Installing Playwright CLI...
    dotnet tool install --global Microsoft.Playwright.CLI 2>nul
    if %errorLevel% neq 0 (
        dotnet tool update --global Microsoft.Playwright.CLI 2>nul
        if %errorLevel% neq 0 (
            echo ✗ Failed to install Playwright CLI
            echo The backend service will still work but may have limited anti-bot capabilities.
            pause
            goto build_solution
        )
    )
    
    echo Installing Playwright browsers (this may take 5-10 minutes)...
    REM Change to the backend service directory for browser installation
    pushd ScraperBackendService
    playwright install chromium --with-deps
    popd
    
    if %errorLevel% == 0 (
        echo ✓ Playwright browsers installed successfully
        playwright --version
    ) else (
        echo ✗ Playwright installation failed
        echo The backend service will still work but may have limited anti-bot capabilities.
        pause
    )
) else (
    echo Skipping Playwright installation.
    echo Note: Backend service may have limited functionality without Playwright.
)

:build_solution
echo.
echo [4/7] Building HanimetaScraper solution...
echo.
echo Restoring NuGet packages...
dotnet restore
if %errorLevel% neq 0 (
    echo ✗ Package restore failed
    pause
    exit /b 1
)

echo.
echo Building solution in Release mode...
dotnet build --configuration Release --no-restore
if %errorLevel% neq 0 (
    echo ✗ Build failed
    pause
    exit /b 1
)

echo ✓ Solution built successfully

:check_jellyfin
echo.
echo [5/7] Checking Jellyfin installation...
if exist "C:\ProgramData\Jellyfin\Server\plugins" (
    echo ✓ Jellyfin installation found
    set JELLYFIN_PLUGINS_DIR=C:\ProgramData\Jellyfin\Server\plugins
) else (
    echo.
    echo Jellyfin installation not found in the default location.
    echo Please make sure Jellyfin is installed and has been run at least once.
    echo.
    echo Default Jellyfin plugins directory: C:\ProgramData\Jellyfin\Server\plugins
    echo.
    set /p custom_path="Enter custom Jellyfin plugins directory (or press Enter to skip): "
    if not "!custom_path!"=="" (
        if exist "!custom_path!" (
            set JELLYFIN_PLUGINS_DIR=!custom_path!
            echo ✓ Custom Jellyfin directory set
        ) else (
            echo ✗ Directory not found: !custom_path!
            echo Skipping plugin installation
            set JELLYFIN_PLUGINS_DIR=
        )
    ) else (
        echo Skipping plugin installation
        set JELLYFIN_PLUGINS_DIR=
    )
)

:install_plugins
if not "%JELLYFIN_PLUGINS_DIR%"=="" (
    echo.
    echo [6/7] Installing Jellyfin plugins...
    
    REM Check if Jellyfin is running
    netstat -an | find "LISTENING" | find ":8096" >nul
    if %errorLevel% == 0 (
        echo.
        echo ⚠ Jellyfin appears to be running on port 8096
        set /p stop_jellyfin="Stop Jellyfin service before installing plugins? (Y/N): "
        if /i "!stop_jellyfin!"=="Y" (
            echo Stopping Jellyfin service...
            net stop JellyfinServer 2>nul
            sc stop JellyfinServer 2>nul
            timeout /t 3 >nul
        )
    )
    
    echo Installing DLsite plugin...
    if not exist "%JELLYFIN_PLUGINS_DIR%\Jellyfin.Plugin.Hanimeta.DLsiteScraper" (
        mkdir "%JELLYFIN_PLUGINS_DIR%\Jellyfin.Plugin.Hanimeta.DLsiteScraper"
    )
    copy "Jellyfin.Plugin.Hanimeta.DLsiteScraper\bin\Release\net8.0\Jellyfin.Plugin.Hanimeta.DLsiteScraper.dll" "%JELLYFIN_PLUGINS_DIR%\Jellyfin.Plugin.Hanimeta.DLsiteScraper\" >nul
    copy "Jellyfin.Plugin.Hanimeta.Common\bin\Release\net8.0\Jellyfin.Plugin.Hanimeta.Common.dll" "%JELLYFIN_PLUGINS_DIR%\Jellyfin.Plugin.Hanimeta.DLsiteScraper\" >nul
    copy "Jellyfin.Plugin.Hanimeta.DLsiteScraper\manifest.json" "%JELLYFIN_PLUGINS_DIR%\Jellyfin.Plugin.Hanimeta.DLsiteScraper\" >nul
    
    echo Installing Hanime plugin...
    if not exist "%JELLYFIN_PLUGINS_DIR%\Jellyfin.Plugin.Hanimeta.HanimeScraper" (
        mkdir "%JELLYFIN_PLUGINS_DIR%\Jellyfin.Plugin.Hanimeta.HanimeScraper"
    )
    copy "Jellyfin.Plugin.Hanimeta.HanimeScraper\bin\Release\net8.0\Jellyfin.Plugin.Hanimeta.HanimeScraper.dll" "%JELLYFIN_PLUGINS_DIR%\Jellyfin.Plugin.Hanimeta.HanimeScraper\" >nul
    copy "Jellyfin.Plugin.Hanimeta.Common\bin\Release\net8.0\Jellyfin.Plugin.Hanimeta.Common.dll" "%JELLYFIN_PLUGINS_DIR%\Jellyfin.Plugin.Hanimeta.HanimeScraper\" >nul
    copy "Jellyfin.Plugin.Hanimeta.HanimeScraper\manifest.json" "%JELLYFIN_PLUGINS_DIR%\Jellyfin.Plugin.Hanimeta.HanimeScraper\" >nul
    
    echo ✓ Plugins installed successfully
    
    set /p start_jellyfin="Restart Jellyfin service now? (Y/N): "
    if /i "!start_jellyfin!"=="Y" (
        echo Starting Jellyfin service...
        net start JellyfinServer 2>nul
        sc start JellyfinServer 2>nul
    )
) else (
    echo.
    echo [6/7] Skipping plugin installation (Jellyfin not found)
)

:create_shortcuts
echo.
echo [7/7] Creating shortcuts and startup files...

REM Create desktop shortcut for backend service
if not exist "%USERPROFILE%\Desktop\Start HanimetaScraper.bat" (
    echo @echo off > "%USERPROFILE%\Desktop\Start HanimetaScraper.bat"
    echo title HanimetaScraper Backend Service >> "%USERPROFILE%\Desktop\Start HanimetaScraper.bat"
    echo echo Starting HanimetaScraper Backend Service... >> "%USERPROFILE%\Desktop\Start HanimetaScraper.bat"
    echo echo. >> "%USERPROFILE%\Desktop\Start HanimetaScraper.bat"
    echo echo Note: This service uses Playwright for anti-bot features. >> "%USERPROFILE%\Desktop\Start HanimetaScraper.bat"
    echo echo If you experience issues, run scripts\manage.ps1 setup-playwright >> "%USERPROFILE%\Desktop\Start HanimetaScraper.bat"
    echo echo. >> "%USERPROFILE%\Desktop\Start HanimetaScraper.bat"
    echo cd /d "%CD%" >> "%USERPROFILE%\Desktop\Start HanimetaScraper.bat"
    echo cd ScraperBackendService\bin\Release\net8.0 >> "%USERPROFILE%\Desktop\Start HanimetaScraper.bat"
    echo dotnet ScraperBackendService.dll >> "%USERPROFILE%\Desktop\Start HanimetaScraper.bat"
    echo pause >> "%USERPROFILE%\Desktop\Start HanimetaScraper.bat"
    echo ✓ Desktop shortcut created
)

REM Create Start Menu shortcut
set START_MENU_DIR=%APPDATA%\Microsoft\Windows\Start Menu\Programs\HanimetaScraper
if not exist "%START_MENU_DIR%" mkdir "%START_MENU_DIR%"

if not exist "%START_MENU_DIR%\Start Backend Service.bat" (
    copy "%USERPROFILE%\Desktop\Start HanimetaScraper.bat" "%START_MENU_DIR%\Start Backend Service.bat" >nul
)

REM Create management shortcut
if not exist "%START_MENU_DIR%\Manage HanimetaScraper.bat" (
    echo @echo off > "%START_MENU_DIR%\Manage HanimetaScraper.bat"
    echo cd /d "%CD%" >> "%START_MENU_DIR%\Manage HanimetaScraper.bat"
    echo powershell -ExecutionPolicy Bypass -File scripts\manage.ps1 >> "%START_MENU_DIR%\Manage HanimetaScraper.bat"
    echo pause >> "%START_MENU_DIR%\Manage HanimetaScraper.bat"
)

REM Create Playwright setup shortcut
if not exist "%START_MENU_DIR%\Setup Playwright.bat" (
    echo @echo off > "%START_MENU_DIR%\Setup Playwright.bat"
    echo cd /d "%CD%" >> "%START_MENU_DIR%\Setup Playwright.bat"
    echo powershell -ExecutionPolicy Bypass -File scripts\manage.ps1 setup-playwright >> "%START_MENU_DIR%\Setup Playwright.bat"
    echo pause >> "%START_MENU_DIR%\Setup Playwright.bat"
)

:completion
echo.
echo ========================================================
echo                Installation Complete!
echo ========================================================
echo.
echo What was installed:
echo ✓ HanimetaScraper backend service (built from source)
if /i "%install_playwright%"=="Y" (
    playwright --version >nul 2>&1
    if %errorLevel% == 0 (
        echo ✓ Playwright browsers for anti-bot features
    ) else (
        echo - Playwright browsers (installation failed)
    )
) else (
    echo - Playwright browsers (skipped)
)
if not "%JELLYFIN_PLUGINS_DIR%"=="" (
    echo ✓ Jellyfin DLsite and Hanime scraper plugins
) else (
    echo - Jellyfin plugins (skipped - Jellyfin not found)
)
echo ✓ Desktop and Start Menu shortcuts
echo.
echo Next steps:
echo.
echo 1. Start the backend service:
echo    - Use desktop shortcut: "Start HanimetaScraper"
echo    - Or run: scripts\manage.ps1 start
echo    - Or manually: cd ScraperBackendService\bin\Release\net8.0 ^&^& dotnet ScraperBackendService.dll
echo.
if not "%JELLYFIN_PLUGINS_DIR%"=="" (
    echo 2. Configure Jellyfin plugins:
    echo    - Open Jellyfin admin dashboard
    echo    - Go to Dashboard ^> Plugins
    echo    - Configure DLsite and Hanime scrapers:
    echo      Backend URL: http://127.0.0.1:8585
    echo      API Token: (leave empty unless you set one)
    echo.
) else (
    echo 2. Install Jellyfin plugins manually:
    echo    - Install Jellyfin if not already installed
    echo    - Copy plugin files from release directory to Jellyfin plugins folder
    echo    - Restart Jellyfin and configure plugins
    echo.
)
echo 3. Test the installation:
echo    - Run: scripts\manage.ps1 test
echo    - Or visit: http://localhost:8585/health
echo.
playwright --version >nul 2>&1
if errorlevel 1 (
    echo 4. Optional: Set up Playwright for enhanced features:
    echo    - Run: scripts\manage.ps1 setup-playwright
    echo    - This enables advanced anti-bot capabilities
    echo.
)
echo For help and advanced management:
echo    - Run: scripts\manage.ps1 help
echo    - Check documentation: README.md
echo.
echo ========================================================
echo.
set /p start_now="Start the backend service now? (Y/N): "
if /i "%start_now%"=="Y" (
    echo.
    echo Starting backend service...
    start "HanimetaScraper Backend" cmd /k "cd /d "%CD%\ScraperBackendService\bin\Release\net8.0" && dotnet ScraperBackendService.dll"
    timeout /t 3 >nul
    echo.
    echo Backend service started in a new window.
    echo Visit http://localhost:8585 to verify it's running.
)

echo.
echo Thank you for using HanimetaScraper!
echo.
echo Important notes:
echo - The backend service uses Playwright for anti-bot features
echo - If you encounter browser-related errors, run the Playwright setup
echo - Check the management script for advanced options: scripts\manage.ps1 help
pause