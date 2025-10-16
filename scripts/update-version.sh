#!/bin/bash

# HanimetaScraper Version Update Script
# Usage: ./scripts/update-version.sh <new-version>
# Example: ./scripts/update-version.sh 1.1.0

set -e

NEW_VERSION=$1
if [ -z "$NEW_VERSION" ]; then
    echo "Usage: $0 <version>"
    echo "Example: $0 1.1.0"
    exit 1
fi

echo "Updating version to $NEW_VERSION..."

# Update project files
echo "Updating .csproj files..."
find . -name "*.csproj" -exec sed -i "s/<AssemblyVersion>.*<\/AssemblyVersion>/<AssemblyVersion>$NEW_VERSION.0<\/AssemblyVersion>/g" {} \;
find . -name "*.csproj" -exec sed -i "s/<FileVersion>.*<\/FileVersion>/<FileVersion>$NEW_VERSION.0<\/FileVersion>/g" {} \;

# Update manifest files
echo "Updating manifest.json files..."
TIMESTAMP=$(date -u +"%Y-%m-%dT%H:%M:%S.0000000Z")

# Update DLsite manifest
sed -i "s/\"version\": \"[^\"]*\"/\"version\": \"$NEW_VERSION.0\"/g" Jellyfin.Plugin.Hanimeta.DLsiteScraper/manifest.json
sed -i "s/\"timestamp\": \"[^\"]*\"/\"timestamp\": \"$TIMESTAMP\"/g" Jellyfin.Plugin.Hanimeta.DLsiteScraper/manifest.json
sed -i "s/download\/v[^\/]*\//download\/v$NEW_VERSION\//g" Jellyfin.Plugin.Hanimeta.DLsiteScraper/manifest.json

# Update Hanime manifest  
sed -i "s/\"version\": \"[^\"]*\"/\"version\": \"$NEW_VERSION.0\"/g" Jellyfin.Plugin.Hanimeta.HanimeScraper/manifest.json
sed -i "s/\"timestamp\": \"[^\"]*\"/\"timestamp\": \"$TIMESTAMP\"/g" Jellyfin.Plugin.Hanimeta.HanimeScraper/manifest.json
sed -i "s/download\/v[^\/]*\//download\/v$NEW_VERSION\//g" Jellyfin.Plugin.Hanimeta.HanimeScraper/manifest.json

# Update catalog.json
sed -i "s/\"version\": \"[^\"]*\"/\"version\": \"$NEW_VERSION.0\"/g" catalog.json
sed -i "s/\"timestamp\": \"[^\"]*\"/\"timestamp\": \"$TIMESTAMP\"/g" catalog.json
sed -i "s/download\/v[^\/]*\//download\/v$NEW_VERSION\//g" catalog.json

echo "Version updated to $NEW_VERSION"
echo "Timestamp set to $TIMESTAMP"
echo ""
echo "Next steps:"
echo "1. Update RELEASE_NOTES.md with changelog"
echo "2. Commit changes: git add . && git commit -m 'Bump version to v$NEW_VERSION'"
echo "3. Create tag: git tag v$NEW_VERSION"
echo "4. Push: git push origin main --tags"