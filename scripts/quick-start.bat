@echo off
REM HanimetaScraper Quick Start Script for Windows
REM This script helps users quickly set up and start the HanimetaScraper solution

setlocal enabledelayedexpansion

REM Configuration
set BACKEND_PORT=8585
set JELLYFIN_PORT=8096
set BACKEND_DIR=.\ScraperBackendService
set RELEASE_DIR=.\release
set PLUGINS_DIR=.\plugins

REM Functions using labels
goto main

:print_header
echo ============================================
echo     HanimetaScraper Quick Start Setup     
echo ============================================
echo.
goto :eof

:print_step
echo [STEP] %1
goto :eof

:print_warning
echo [WARNING] %1
goto :eof

:print_error
echo [ERROR] %1
goto :eof

:check_dependencies
call :print_step "Checking dependencies..."

REM Check for .NET 8
dotnet --version >nul 2>&1
if errorlevel 1 (
    call :print_error ".NET SDK not found. Please install .NET 8 SDK first."
    echo Download from: https://dotnet.microsoft.com/download/dotnet/8.0
    exit /b 1
)

for /f "tokens=1 delims=." %%i in ('dotnet --version') do set DOTNET_MAJOR=%%i
if %DOTNET_MAJOR% lss 8 (
    call :print_error ".NET 8 or higher required. Found version: %DOTNET_MAJOR%"
    exit /b 1
)

echo ✓ .NET %DOTNET_MAJOR% found
echo.
goto :eof

:setup_playwright
call :print_step "Setting up Playwright browsers..."

echo Installing Playwright CLI...
dotnet tool install --global Microsoft.Playwright.CLI 2>nul
if errorlevel 1 (
    REM Try to update if already installed
    dotnet tool update --global Microsoft.Playwright.CLI 2>nul
    if errorlevel 1 (
        call :print_error "Failed to install Playwright CLI"
        exit /b 1
    )
)

REM Check if playwright command is available
playwright --version >nul 2>&1
if errorlevel 1 (
    call :print_error "Playwright CLI not accessible after installation"
    exit /b 1
)

echo Installing Playwright browsers (this may take a while)...
cd "%BACKEND_DIR%"
playwright install chromium --with-deps
if errorlevel 1 (
    call :print_error "Playwright browser installation failed"
    cd ..
    exit /b 1
)

echo ✓ Playwright browsers installed successfully
playwright --version
cd ..
echo.
goto :eof

:build_backend
call :print_step "Building backend service..."

cd "%BACKEND_DIR%"

echo Restoring packages...
dotnet restore

echo Building in Release mode...
dotnet build -c Release

if errorlevel 1 (
    call :print_error "Backend build failed"
    exit /b 1
)

echo ✓ Backend service built successfully
cd ..
echo.
goto :eof

:build_plugins
call :print_step "Building Jellyfin plugins..."

echo Building Common library...
dotnet build Jellyfin.Plugin.Hanimeta.Common -c Release

echo Building DLsite plugin...
dotnet build Jellyfin.Plugin.Hanimeta.DLsiteScraper -c Release

echo Building Hanime plugin...
dotnet build Jellyfin.Plugin.Hanimeta.HanimeScraper -c Release

echo ✓ All plugins built successfully
echo.
goto :eof

:package_release
call :print_step "Packaging release files..."

REM Create release directory
if not exist "%RELEASE_DIR%\backend" mkdir "%RELEASE_DIR%\backend"
if not exist "%RELEASE_DIR%\plugins" mkdir "%RELEASE_DIR%\plugins"

REM Package backend service
echo Publishing backend service...
cd "%BACKEND_DIR%"
dotnet publish -c Release -o "..\%RELEASE_DIR%\backend" --no-build
cd ..

REM Package plugins
echo Packaging DLsite plugin...
if not exist "%RELEASE_DIR%\plugins\DLsiteScraper" mkdir "%RELEASE_DIR%\plugins\DLsiteScraper"
copy "Jellyfin.Plugin.Hanimeta.DLsiteScraper\bin\Release\net8.0\Jellyfin.Plugin.Hanimeta.DLsiteScraper.dll" "%RELEASE_DIR%\plugins\DLsiteScraper\" >nul
copy "Jellyfin.Plugin.Hanimeta.Common\bin\Release\net8.0\Jellyfin.Plugin.Hanimeta.Common.dll" "%RELEASE_DIR%\plugins\DLsiteScraper\" >nul
copy "Jellyfin.Plugin.Hanimeta.DLsiteScraper\manifest.json" "%RELEASE_DIR%\plugins\DLsiteScraper\" >nul

echo Packaging Hanime plugin...
if not exist "%RELEASE_DIR%\plugins\HanimeScraper" mkdir "%RELEASE_DIR%\plugins\HanimeScraper"
copy "Jellyfin.Plugin.Hanimeta.HanimeScraper\bin\Release\net8.0\Jellyfin.Plugin.Hanimeta.HanimeScraper.dll" "%RELEASE_DIR%\plugins\HanimeScraper\" >nul
copy "Jellyfin.Plugin.Hanimeta.Common\bin\Release\net8.0\Jellyfin.Plugin.Hanimeta.Common.dll" "%RELEASE_DIR%\plugins\HanimeScraper\" >nul
copy "Jellyfin.Plugin.Hanimeta.HanimeScraper\manifest.json" "%RELEASE_DIR%\plugins\HanimeScraper\" >nul

echo ✓ Release files packaged in %RELEASE_DIR%
echo.
goto :eof

:create_startup_scripts
call :print_step "Creating startup scripts..."

REM Backend startup batch file
echo @echo off > "%RELEASE_DIR%\start-backend.bat"
echo echo Starting HanimetaScraper Backend Service... >> "%RELEASE_DIR%\start-backend.bat"
echo echo. >> "%RELEASE_DIR%\start-backend.bat"
echo echo Note: This service requires Playwright browsers. >> "%RELEASE_DIR%\start-backend.bat"
echo echo If this is the first run and you encounter browser errors, >> "%RELEASE_DIR%\start-backend.bat"
echo echo run install-playwright.bat first. >> "%RELEASE_DIR%\start-backend.bat"
echo echo. >> "%RELEASE_DIR%\start-backend.bat"
echo cd backend >> "%RELEASE_DIR%\start-backend.bat"
echo dotnet ScraperBackendService.dll >> "%RELEASE_DIR%\start-backend.bat"
echo pause >> "%RELEASE_DIR%\start-backend.bat"

REM Linux shell script
echo #!/bin/bash > "%RELEASE_DIR%\start-backend.sh"
echo echo "Starting HanimetaScraper Backend Service..." >> "%RELEASE_DIR%\start-backend.sh"
echo echo "" >> "%RELEASE_DIR%\start-backend.sh"
echo echo "Note: This service requires Playwright browsers." >> "%RELEASE_DIR%\start-backend.sh"
echo echo "If this is the first run and you encounter browser errors," >> "%RELEASE_DIR%\start-backend.sh"
echo echo "run install-playwright.sh first." >> "%RELEASE_DIR%\start-backend.sh"
echo echo "" >> "%RELEASE_DIR%\start-backend.sh"
echo cd backend >> "%RELEASE_DIR%\start-backend.sh"
echo dotnet ScraperBackendService.dll >> "%RELEASE_DIR%\start-backend.sh"

REM Playwright installation scripts
echo #!/bin/bash > "%RELEASE_DIR%\install-playwright.sh"
echo echo "Installing Playwright browsers for HanimetaScraper..." >> "%RELEASE_DIR%\install-playwright.sh"
echo dotnet tool install --global Microsoft.Playwright.CLI ^|^| dotnet tool update --global Microsoft.Playwright.CLI >> "%RELEASE_DIR%\install-playwright.sh"
echo export PATH="$PATH:$HOME/.dotnet/tools" >> "%RELEASE_DIR%\install-playwright.sh"
echo cd backend >> "%RELEASE_DIR%\install-playwright.sh"
echo playwright install chromium --with-deps >> "%RELEASE_DIR%\install-playwright.sh"
echo echo "Playwright setup complete." >> "%RELEASE_DIR%\install-playwright.sh"

echo @echo off > "%RELEASE_DIR%\install-playwright.bat"
echo echo Installing Playwright browsers for HanimetaScraper... >> "%RELEASE_DIR%\install-playwright.bat"
echo dotnet tool install --global Microsoft.Playwright.CLI ^|^| dotnet tool update --global Microsoft.Playwright.CLI >> "%RELEASE_DIR%\install-playwright.bat"
echo cd backend >> "%RELEASE_DIR%\install-playwright.bat"
echo playwright install chromium --with-deps >> "%RELEASE_DIR%\install-playwright.bat"
echo echo Playwright setup complete. >> "%RELEASE_DIR%\install-playwright.bat"
echo pause >> "%RELEASE_DIR%\install-playwright.bat"

echo ✓ Startup scripts created
echo.
goto :eof

:start_backend
call :print_step "Starting backend service..."

if not exist "%RELEASE_DIR%\backend" (
    call :print_error "Release not found. Run 'quick-start.bat package' first."
    exit /b 1
)

cd "%RELEASE_DIR%\backend"

echo Backend service starting on port %BACKEND_PORT%...
echo Press Ctrl+C to stop the service
echo.

start "HanimetaScraper Backend" dotnet ScraperBackendService.dll

timeout /t 3 /nobreak >nul

REM Simple health check
curl -f "http://localhost:%BACKEND_PORT%/health" >nul 2>&1
if not errorlevel 1 (
    echo ✓ Backend service is healthy and responding
) else (
    call :print_warning "Backend service may not be responding correctly"
)

echo.
echo Backend service is running
echo Access at: http://localhost:%BACKEND_PORT%
echo.

cd ..\..
goto :eof

:install_plugins
call :print_step "Plugin installation guide..."

echo To install the Jellyfin plugins:
echo.
echo Option 1: Manual Installation
echo   1. Stop Jellyfin server
echo   2. Copy plugin directories to Jellyfin plugins folder:
echo      - Windows: C:\ProgramData\Jellyfin\Server\plugins\
echo      - Docker: /config/plugins/
echo   3. Restart Jellyfin server
echo.
echo Option 2: Plugin Repository
echo   1. Add repository URL in Jellyfin admin:
echo      https://raw.githubusercontent.com/Qing-98/HanimetaScraper/main/catalog.json
echo   2. Install plugins from catalog
echo   3. Restart Jellyfin
echo.
echo Plugin files are in: %RELEASE_DIR%\plugins\
echo.
goto :eof

:show_configuration
call :print_step "Configuration guide..."

echo Backend Service Configuration:
echo   Edit: %RELEASE_DIR%\backend\appsettings.json
echo   Default settings are conservative and safe for most users
echo.
echo Jellyfin Plugin Configuration:
echo   1. Open Jellyfin Admin Dashboard
echo   2. Go to Dashboard → Plugins
echo   3. Configure each scraper plugin:
echo      - Backend URL: http://127.0.0.1:%BACKEND_PORT%
echo      - API Token: (leave empty if not set in backend)
echo      - Enable Logging: false (true for debugging)
echo.
goto :eof

:show_usage_examples
call :print_step "Usage examples..."

echo Test backend service:
echo   curl http://localhost:%BACKEND_PORT%/
echo   curl http://localhost:%BACKEND_PORT%/health
echo   curl "http://localhost:%BACKEND_PORT%/api/hanime/search?title=love&max=5"
echo.
echo Stop backend service:
echo   Close the backend service window
echo   Or use Task Manager to end the process
echo.
goto :eof

:main
call :print_header

set COMMAND=%1
if "%COMMAND%"=="" set COMMAND=all

if "%COMMAND%"=="deps" goto deps_only
if "%COMMAND%"=="dependencies" goto deps_only
if "%COMMAND%"=="setup-playwright" goto playwright_only
if "%COMMAND%"=="build" goto build_only
if "%COMMAND%"=="package" goto package_only
if "%COMMAND%"=="start" goto start_only
if "%COMMAND%"=="install" goto install_only
if "%COMMAND%"=="config" goto config_only
if "%COMMAND%"=="examples" goto examples_only
if "%COMMAND%"=="all" goto all_setup
if "%COMMAND%"=="help" goto show_help
goto show_help

:deps_only
call :check_dependencies
goto end

:playwright_only
call :check_dependencies
call :setup_playwright
goto end

:build_only
call :check_dependencies
call :setup_playwright || call :print_warning "Playwright setup failed, continuing anyway"
call :build_backend
call :build_plugins
goto end

:package_only
call :check_dependencies
call :setup_playwright || call :print_warning "Playwright setup failed, continuing anyway"
call :build_backend
call :build_plugins
call :package_release
call :create_startup_scripts
goto end

:start_only
call :start_backend
goto end

:install_only
call :install_plugins
goto end

:config_only
call :show_configuration
goto end

:examples_only
call :show_usage_examples
goto end

:all_setup
call :check_dependencies
call :setup_playwright || call :print_warning "Playwright setup failed, but continuing with build"
call :build_backend
call :build_plugins
call :package_release
call :create_startup_scripts

echo ========================================
echo     Setup Complete! Next Steps:       
echo ========================================
echo.
echo 1. Start backend service:
echo    scripts\quick-start.bat start
echo.
echo 2. Install Jellyfin plugins:
echo    scripts\quick-start.bat install
echo.
echo 3. Configure plugins:
echo    scripts\quick-start.bat config
echo.
echo 4. See usage examples:
echo    scripts\quick-start.bat examples
echo.
playwright --version >nul 2>&1
if errorlevel 1 (
    call :print_warning "Playwright setup incomplete. For full anti-bot features, run:"
    echo    scripts\quick-start.bat setup-playwright
    echo.
)
call :print_warning "Note: Configure Jellyfin plugins after starting the backend service!"
goto end

:show_help
echo Usage: %0 [command]
echo.
echo Commands:
echo   all              - Full setup (default)
echo   deps             - Check dependencies only
echo   setup-playwright - Install Playwright browsers
echo   build            - Build projects only
echo   package          - Build and package for release
echo   start            - Start backend service
echo   install          - Show plugin installation guide
echo   config           - Show configuration guide
echo   examples         - Show usage examples
echo   help             - Show this help
echo.
echo Examples:
echo   %0               # Full setup
echo   %0 build         # Build only
echo   %0 start         # Start backend
echo   %0 setup-playwright # Install Playwright browsers

:end
pause