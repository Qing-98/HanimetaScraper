@echo off
REM HanimetaScraper Version Update Script for Windows
REM Usage: scripts\update-version.bat <new-version>
REM Example: scripts\update-version.bat 1.1.0

setlocal enabledelayedexpansion

set NEW_VERSION=%1
if "%NEW_VERSION%"=="" (
    echo Usage: %0 ^<version^>
    echo Example: %0 1.1.0
    exit /b 1
)

echo Updating version to %NEW_VERSION%...

REM Get current timestamp in ISO format
for /f "tokens=1-6 delims=/ :. " %%a in ("%date% %time%") do (
    set TIMESTAMP=%%c-%%a-%%bT%%d:%%e:%%f.0000000Z
)

echo Updating manifest.json files...

REM Update DLsite manifest
powershell -Command "(Get-Content 'Jellyfin.Plugin.Hanimeta.DLsiteScraper\manifest.json') -replace '\"version\": \"[^\"]*\"', '\"version\": \"%NEW_VERSION%.0\"' | Set-Content 'Jellyfin.Plugin.Hanimeta.DLsiteScraper\manifest.json'"
powershell -Command "(Get-Content 'Jellyfin.Plugin.Hanimeta.DLsiteScraper\manifest.json') -replace '\"timestamp\": \"[^\"]*\"', '\"timestamp\": \"%TIMESTAMP%\"' | Set-Content 'Jellyfin.Plugin.Hanimeta.DLsiteScraper\manifest.json'"
powershell -Command "(Get-Content 'Jellyfin.Plugin.Hanimeta.DLsiteScraper\manifest.json') -replace 'download/v[^/]*/', 'download/v%NEW_VERSION%/' | Set-Content 'Jellyfin.Plugin.Hanimeta.DLsiteScraper\manifest.json'"

REM Update Hanime manifest
powershell -Command "(Get-Content 'Jellyfin.Plugin.Hanimeta.HanimeScraper\manifest.json') -replace '\"version\": \"[^\"]*\"', '\"version\": \"%NEW_VERSION%.0\"' | Set-Content 'Jellyfin.Plugin.Hanimeta.HanimeScraper\manifest.json'"
powershell -Command "(Get-Content 'Jellyfin.Plugin.Hanimeta.HanimeScraper\manifest.json') -replace '\"timestamp\": \"[^\"]*\"', '\"timestamp\": \"%TIMESTAMP%\"' | Set-Content 'Jellyfin.Plugin.Hanimeta.HanimeScraper\manifest.json'"
powershell -Command "(Get-Content 'Jellyfin.Plugin.Hanimeta.HanimeScraper\manifest.json') -replace 'download/v[^/]*/', 'download/v%NEW_VERSION%/' | Set-Content 'Jellyfin.Plugin.Hanimeta.HanimeScraper\manifest.json'"

REM Update catalog.json
powershell -Command "(Get-Content 'catalog.json') -replace '\"version\": \"[^\"]*\"', '\"version\": \"%NEW_VERSION%.0\"' | Set-Content 'catalog.json'"
powershell -Command "(Get-Content 'catalog.json') -replace '\"timestamp\": \"[^\"]*\"', '\"timestamp\": \"%TIMESTAMP%\"' | Set-Content 'catalog.json'"
powershell -Command "(Get-Content 'catalog.json') -replace 'download/v[^/]*/', 'download/v%NEW_VERSION%/' | Set-Content 'catalog.json'"

echo Version updated to %NEW_VERSION%
echo Timestamp set to %TIMESTAMP%
echo.
echo Next steps:
echo 1. Update RELEASE_NOTES.md with changelog
echo 2. Commit changes: git add . ^&^& git commit -m "Bump version to v%NEW_VERSION%"
echo 3. Create tag: git tag v%NEW_VERSION%
echo 4. Push: git push origin main --tags