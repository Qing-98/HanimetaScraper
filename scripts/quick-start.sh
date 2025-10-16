#!/bin/bash

# HanimetaScraper Quick Start Script
# This script helps users quickly set up and start the HanimetaScraper solution

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Configuration
BACKEND_PORT=8585
JELLYFIN_PORT=8096
BACKEND_DIR="./ScraperBackendService"
RELEASE_DIR="./release"
PLUGINS_DIR="./plugins"

# Functions
print_header() {
    echo -e "${BLUE}============================================${NC}"
    echo -e "${BLUE}    HanimetaScraper Quick Start Setup     ${NC}"
    echo -e "${BLUE}============================================${NC}"
    echo
}

print_step() {
    echo -e "${GREEN}[STEP] $1${NC}"
}

print_warning() {
    echo -e "${YELLOW}[WARNING] $1${NC}"
}

print_error() {
    echo -e "${RED}[ERROR] $1${NC}"
}

check_dependencies() {
    print_step "Checking dependencies..."
    
    # Check for .NET 8
    if ! command -v dotnet &> /dev/null; then
        print_error ".NET SDK not found. Please install .NET 8 SDK first."
        echo "Download from: https://dotnet.microsoft.com/download/dotnet/8.0"
        exit 1
    fi
    
    # Check .NET version
    DOTNET_VERSION=$(dotnet --version | cut -d. -f1)
    if [ "$DOTNET_VERSION" -lt 8 ]; then
        print_error ".NET 8 or higher required. Found version: $(dotnet --version)"
        exit 1
    fi
    
    echo "✓ .NET $(dotnet --version) found"
    
    # Check for curl (for health checks)
    if ! command -v curl &> /dev/null; then
        print_warning "curl not found. Health checks will be skipped."
    fi
    
    # Check for unzip (for plugin installation)
    if ! command -v unzip &> /dev/null; then
        print_warning "unzip not found. Manual plugin extraction may be required."
    fi
    
    echo
}

setup_playwright() {
    print_step "Setting up Playwright browsers..."
    
    echo "Installing Playwright CLI..."
    if ! dotnet tool install --global Microsoft.Playwright.CLI 2>/dev/null; then
        # Try to update if already installed
        dotnet tool update --global Microsoft.Playwright.CLI 2>/dev/null || {
            print_error "Failed to install Playwright CLI"
            return 1
        }
    fi
    
    # Add .NET tools to PATH for current session
    export PATH="$PATH:$HOME/.dotnet/tools"
    
    # Check if playwright command is available
    if ! command -v playwright &> /dev/null; then
        print_error "Playwright CLI not accessible after installation"
        return 1
    fi
    
    echo "Installing Playwright browsers (this may take a while)..."
    cd "$BACKEND_DIR"
    if playwright install chromium --with-deps; then
        echo "✓ Playwright browsers installed successfully"
        playwright --version
        cd - > /dev/null
        return 0
    else
        print_error "Playwright browser installation failed"
        cd - > /dev/null
        return 1
    fi
}

build_backend() {
    print_step "Building backend service..."
    
    cd "$BACKEND_DIR"
    
    echo "Restoring packages..."
    dotnet restore
    
    echo "Building in Release mode..."
    dotnet build -c Release
    
    echo "✓ Backend service built successfully"
    cd - > /dev/null
    echo
}

build_plugins() {
    print_step "Building Jellyfin plugins..."
    
    echo "Building Common library..."
    dotnet build Jellyfin.Plugin.Hanimeta.Common -c Release
    
    echo "Building DLsite plugin..."
    dotnet build Jellyfin.Plugin.Hanimeta.DLsiteScraper -c Release
    
    echo "Building Hanime plugin..."
    dotnet build Jellyfin.Plugin.Hanimeta.HanimeScraper -c Release
    
    echo "✓ All plugins built successfully"
    echo
}

package_release() {
    print_step "Packaging release files..."
    
    # Create release directory
    mkdir -p "$RELEASE_DIR/backend"
    mkdir -p "$RELEASE_DIR/plugins"
    
    # Package backend service
    echo "Publishing backend service..."
    cd "$BACKEND_DIR"
    dotnet publish -c Release -o "../$RELEASE_DIR/backend" --no-build
    cd - > /dev/null
    
    # Package plugins
    echo "Packaging DLsite plugin..."
    mkdir -p "$RELEASE_DIR/plugins/DLsiteScraper"
    cp "Jellyfin.Plugin.Hanimeta.DLsiteScraper/bin/Release/net8.0/Jellyfin.Plugin.Hanimeta.DLsiteScraper.dll" "$RELEASE_DIR/plugins/DLsiteScraper/"
    cp "Jellyfin.Plugin.Hanimeta.Common/bin/Release/net8.0/Jellyfin.Plugin.Hanimeta.Common.dll" "$RELEASE_DIR/plugins/DLsiteScraper/"
    cp "Jellyfin.Plugin.Hanimeta.DLsiteScraper/manifest.json" "$RELEASE_DIR/plugins/DLsiteScraper/"
    
    echo "Packaging Hanime plugin..."
    mkdir -p "$RELEASE_DIR/plugins/HanimeScraper"
    cp "Jellyfin.Plugin.Hanimeta.HanimeScraper/bin/Release/net8.0/Jellyfin.Plugin.Hanimeta.HanimeScraper.dll" "$RELEASE_DIR/plugins/HanimeScraper/"
    cp "Jellyfin.Plugin.Hanimeta.Common/bin/Release/net8.0/Jellyfin.Plugin.Hanimeta.Common.dll" "$RELEASE_DIR/plugins/HanimeScraper/"
    cp "Jellyfin.Plugin.Hanimeta.HanimeScraper/manifest.json" "$RELEASE_DIR/plugins/HanimeScraper/"
    
    echo "✓ Release files packaged in $RELEASE_DIR"
    echo
}

create_startup_scripts() {
    print_step "Creating startup scripts..."
    
    # Backend startup script
    cat > "$RELEASE_DIR/start-backend.sh" << 'EOF'
#!/bin/bash
echo "Starting HanimetaScraper Backend Service..."
echo ""
echo "Note: This service requires Playwright browsers."
echo "If this is the first run and you encounter browser errors,"
echo "run the Playwright setup command from the documentation."
echo ""
cd backend
dotnet ScraperBackendService.dll
EOF
    chmod +x "$RELEASE_DIR/start-backend.sh"
    
    # Windows batch file
    cat > "$RELEASE_DIR/start-backend.bat" << 'EOF'
@echo off
echo Starting HanimetaScraper Backend Service...
echo.
echo Note: This service requires Playwright browsers.
echo If this is the first run and you encounter browser errors,
echo run install-playwright.bat first.
echo.
cd backend
dotnet ScraperBackendService.dll
pause
EOF
    
    # Playwright installation scripts
    cat > "$RELEASE_DIR/install-playwright.sh" << 'EOF'
#!/bin/bash
echo "Installing Playwright browsers for HanimetaScraper..."
dotnet tool install --global Microsoft.Playwright.CLI || dotnet tool update --global Microsoft.Playwright.CLI
export PATH="$PATH:$HOME/.dotnet/tools"
cd backend
playwright install chromium --with-deps
echo "Playwright setup complete."
EOF
    chmod +x "$RELEASE_DIR/install-playwright.sh"
    
    cat > "$RELEASE_DIR/install-playwright.bat" << 'EOF'
@echo off
echo Installing Playwright browsers for HanimetaScraper...
dotnet tool install --global Microsoft.Playwright.CLI || dotnet tool update --global Microsoft.Playwright.CLI
cd backend
playwright install chromium --with-deps
echo Playwright setup complete.
pause
EOF
    
    # Docker compose for easy deployment
    cat > "$RELEASE_DIR/docker-compose.yml" << 'EOF'
version: '3.8'
services:
  scraper-backend:
    build: 
      context: ..
      dockerfile: ScraperBackendService/Dockerfile
    ports:
      - "8585:8585"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
    restart: unless-stopped
EOF
    
    echo "✓ Startup scripts created"
    echo
}

start_backend() {
    print_step "Starting backend service..."
    
    cd "$RELEASE_DIR/backend"
    
    echo "Backend service starting on port $BACKEND_PORT..."
    echo "Press Ctrl+C to stop the service"
    echo
    
    dotnet ScraperBackendService.dll &
    BACKEND_PID=$!
    
    # Wait for service to start
    sleep 3
    
    # Health check
    if command -v curl &> /dev/null; then
        echo "Performing health check..."
        if curl -f "http://localhost:$BACKEND_PORT/health" > /dev/null 2>&1; then
            echo "✓ Backend service is healthy and responding"
        else
            print_warning "Backend service may not be responding correctly"
        fi
    fi
    
    echo
    echo "Backend service is running (PID: $BACKEND_PID)"
    echo "Access at: http://localhost:$BACKEND_PORT"
    echo
    
    cd - > /dev/null
}

install_plugins() {
    print_step "Plugin installation guide..."
    
    echo "To install the Jellyfin plugins:"
    echo
    echo "Option 1: Manual Installation"
    echo "  1. Stop Jellyfin server"
    echo "  2. Copy plugin directories to Jellyfin plugins folder:"
    echo "     - Linux: /var/lib/jellyfin/plugins/"
    echo "     - Windows: C:\\ProgramData\\Jellyfin\\Server\\plugins\\"
    echo "     - Docker: /config/plugins/"
    echo "  3. Restart Jellyfin server"
    echo
    echo "Option 2: Plugin Repository"
    echo "  1. Add repository URL in Jellyfin admin:"
    echo "     https://raw.githubusercontent.com/Qing-98/HanimetaScraper/main/catalog.json"
    echo "  2. Install plugins from catalog"
    echo "  3. Restart Jellyfin"
    echo
    echo "Plugin files are in: $RELEASE_DIR/plugins/"
    echo
}

show_configuration() {
    print_step "Configuration guide..."
    
    echo "Backend Service Configuration:"
    echo "  Edit: $RELEASE_DIR/backend/appsettings.json"
    echo "  Default settings are conservative and safe for most users"
    echo
    echo "Jellyfin Plugin Configuration:"
    echo "  1. Open Jellyfin Admin Dashboard"
    echo "  2. Go to Dashboard → Plugins"
    echo "  3. Configure each scraper plugin:"
    echo "     - Backend URL: http://127.0.0.1:$BACKEND_PORT"
    echo "     - API Token: (leave empty if not set in backend)"
    echo "     - Enable Logging: false (true for debugging)"
    echo
}

show_usage_examples() {
    print_step "Usage examples..."
    
    echo "Test backend service:"
    echo "  curl http://localhost:$BACKEND_PORT/"
    echo "  curl http://localhost:$BACKEND_PORT/health"
    echo "  curl \"http://localhost:$BACKEND_PORT/api/hanime/search?title=love&max=5\""
    echo
    echo "Check service logs:"
    echo "  tail -f logs/*.log  # if logging to file"
    echo
    echo "Stop backend service:"
    echo "  pkill -f ScraperBackendService"
    echo "  # or press Ctrl+C in the terminal"
    echo
}

main() {
    print_header
    
    case "${1:-all}" in
        "deps"|"dependencies")
            check_dependencies
            ;;
        "setup-playwright")
            check_dependencies
            setup_playwright
            ;;
        "build")
            check_dependencies
            setup_playwright || print_warning "Playwright setup failed, continuing anyway"
            build_backend
            build_plugins
            ;;
        "package")
            check_dependencies
            setup_playwright || print_warning "Playwright setup failed, continuing anyway"
            build_backend
            build_plugins
            package_release
            create_startup_scripts
            ;;
        "start")
            if [ ! -d "$RELEASE_DIR/backend" ]; then
                print_error "Release not found. Run '$0 package' first."
                exit 1
            fi
            start_backend
            ;;
        "install")
            install_plugins
            ;;
        "config")
            show_configuration
            ;;
        "examples")
            show_usage_examples
            ;;
        "all"|"")
            check_dependencies
            setup_playwright || print_warning "Playwright setup failed, but continuing with build"
            build_backend
            build_plugins
            package_release
            create_startup_scripts
            
            echo -e "${GREEN}========================================${NC}"
            echo -e "${GREEN}    Setup Complete! Next Steps:       ${NC}"
            echo -e "${GREEN}========================================${NC}"
            echo
            echo "1. Start backend service:"
            echo "   ./scripts/quick-start.sh start"
            echo
            echo "2. Install Jellyfin plugins:"
            echo "   ./scripts/quick-start.sh install"
            echo
            echo "3. Configure plugins:"
            echo "   ./scripts/quick-start.sh config"
            echo
            echo "4. See usage examples:"
            echo "   ./scripts/quick-start.sh examples"
            echo
            if ! command -v playwright &> /dev/null; then
                print_warning "Playwright setup incomplete. For full anti-bot features, run:"
                echo "   ./scripts/quick-start.sh setup-playwright"
                echo
            fi
            print_warning "Note: Configure Jellyfin plugins after starting the backend service!"
            ;;
        *)
            echo "Usage: $0 [command]"
            echo
            echo "Commands:"
            echo "  all              - Full setup (default)"
            echo "  deps             - Check dependencies only"
            echo "  setup-playwright - Install Playwright browsers"
            echo "  build            - Build projects only"
            echo "  package          - Build and package for release"
            echo "  start            - Start backend service"
            echo "  install          - Show plugin installation guide"
            echo "  config           - Show configuration guide"
            echo "  examples         - Show usage examples"
            echo
            echo "Examples:"
            echo "  $0               # Full setup"
            echo "  $0 build         # Build only"
            echo "  $0 start         # Start backend"
            echo "  $0 setup-playwright # Install Playwright browsers"
            ;;
    esac
}

# Trap to cleanup on exit
trap 'echo -e "\n${YELLOW}Shutting down...${NC}"; [ ! -z "$BACKEND_PID" ] && kill $BACKEND_PID 2>/dev/null; exit 0' INT TERM

main "$@"