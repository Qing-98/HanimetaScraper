#!/bin/bash
# Update Repository Manifest Script (Linux/macOS)
# Updates repository.json with new plugin version information

set -e

# Color output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

info() { echo -e "${CYAN}ℹ️  $1${NC}"; }
success() { echo -e "${GREEN}✅ $1${NC}"; }
error() { echo -e "${RED}❌ $1${NC}"; exit 1; }
warning() { echo -e "${YELLOW}⚠️  $1${NC}"; }

# Parse arguments
show_usage() {
    echo "Usage: $0 -v VERSION -c CHANGELOG -z ZIP_PATH [-a TARGET_ABI] [-o OWNER] [-r REPO]"
    echo ""
    echo "Options:"
    echo "  -v VERSION      Plugin version (e.g., 1.3.0)"
    echo "  -c CHANGELOG    Changelog description"
    echo "  -z ZIP_PATH     Path to plugin ZIP file"
    echo "  -a TARGET_ABI   Target Jellyfin ABI version (default: 10.10.1.0)"
    echo "  -o OWNER        GitHub repository owner (default: Qing-98)"
    echo "  -r REPO         GitHub repository name (default: HanimetaScraper)"
    echo "  -h              Show this help message"
    exit 1
}

# Default values
TARGET_ABI="10.10.1.0"
OWNER="Qing-98"
REPO="HanimetaScraper"

# Parse command line arguments
while getopts "v:c:z:a:o:r:h" opt; do
    case $opt in
        v) VERSION="$OPTARG" ;;
        c) CHANGELOG="$OPTARG" ;;
        z) ZIP_PATH="$OPTARG" ;;
        a) TARGET_ABI="$OPTARG" ;;
        o) OWNER="$OPTARG" ;;
        r) REPO="$OPTARG" ;;
        h) show_usage ;;
        *) show_usage ;;
    esac
done

# Validate required arguments
if [ -z "$VERSION" ] || [ -z "$CHANGELOG" ] || [ -z "$ZIP_PATH" ]; then
    error "Missing required arguments"
fi

# Validate files exist
info "Validating inputs..."
[ ! -f "$ZIP_PATH" ] && error "ZIP file not found: $ZIP_PATH"
[ ! -f "repository.json" ] && error "repository.json not found in current directory"

# Calculate MD5 checksum
info "Calculating MD5 checksum..."
if command -v md5sum &> /dev/null; then
    MD5=$(md5sum "$ZIP_PATH" | awk '{print $1}')
elif command -v md5 &> /dev/null; then
    MD5=$(md5 -q "$ZIP_PATH")
else
    error "Neither md5sum nor md5 command found"
fi
success "MD5 checksum: $MD5"

# Generate timestamp
TIMESTAMP=$(date -u +"%Y-%m-%dT%H:%M:%S.%3NZ")
info "Timestamp: $TIMESTAMP"

# Construct download URL
ZIP_FILENAME=$(basename "$ZIP_PATH")
DOWNLOAD_URL="https://github.com/$OWNER/$REPO/releases/download/v$VERSION/$ZIP_FILENAME"
info "Download URL: $DOWNLOAD_URL"

# Read and parse repository.json
info "Reading repository.json..."
if ! command -v jq &> /dev/null; then
    error "jq is required but not installed. Install it with: apt-get install jq / brew install jq"
fi

# Backup original file
cp repository.json repository.json.backup
info "Backup created: repository.json.backup"

# Create new version entry
NEW_VERSION=$(cat <<EOF
{
  "version": "$VERSION.0",
  "changelog": "$CHANGELOG",
  "targetAbi": "$TARGET_ABI",
  "sourceUrl": "$DOWNLOAD_URL",
  "checksum": "$MD5",
  "timestamp": "$TIMESTAMP"
}
EOF
)

info "New version entry:"
echo "$NEW_VERSION" | jq .

# Update repository.json
info "Updating repository manifest..."
jq --argjson newVersion "$NEW_VERSION" \
   '.[0].versions = [$newVersion] + .[0].versions' \
   repository.json > repository.json.tmp

mv repository.json.tmp repository.json
success "Repository manifest updated successfully!"

# Display summary
echo ""
echo -e "${CYAN}═══════════════════════════════════════════════════════${NC}"
echo -e "${CYAN}              Repository Update Summary${NC}"
echo -e "${CYAN}═══════════════════════════════════════════════════════${NC}"
echo -e "${YELLOW}Version:${NC}      $VERSION.0"
echo -e "${YELLOW}Changelog:${NC}    $CHANGELOG"
echo -e "${YELLOW}Target ABI:${NC}   $TARGET_ABI"
echo -e "${YELLOW}Checksum:${NC}     $MD5"
echo -e "${YELLOW}Download URL:${NC} $DOWNLOAD_URL"
echo -e "${YELLOW}Timestamp:${NC}    $TIMESTAMP"
echo -e "${CYAN}═══════════════════════════════════════════════════════${NC}"
echo ""

# Next steps
echo -e "${GREEN}Next Steps:${NC}"
echo "1. Review the changes in repository.json"
echo "2. Commit and push to GitHub:"
echo -e "   ${CYAN}git add repository.json${NC}"
echo -e "   ${CYAN}git commit -m 'Release v$VERSION'${NC}"
echo -e "   ${CYAN}git push origin main${NC}"
echo "3. Create GitHub release with tag v$VERSION"
echo "4. Upload $ZIP_FILENAME to the release"
echo ""

exit 0
