# Jellyfin DLsite Scraper Plugin

A Jellyfin plugin that provides metadata for DLsite content by connecting to a backend scraper service.

## Features

- 🔍 **Search Support**: Search for DLsite content by title or ID
- 📊 **Metadata Extraction**: Get detailed metadata including title, description, ratings, and more
- 👥 **People Information**: Extract cast and crew information
- 🖼️ **Image Support**: Primary and backdrop images
- 🆔 **External ID Support**: DLsite ID tracking for proper identification

## Installation

1. Download the latest release
2. Extract to your Jellyfin plugins directory: `C:\ProgramData\Jellyfin\Server\plugins\Jellyfin.Plugin.DLsiteScraper\`
3. Restart Jellyfin
4. Configure the plugin in the admin dashboard

## Configuration

- **Backend URL**: The URL of your scraper backend service (default: `http://localhost:8585`)
- **API Token**: Optional authentication token for the backend service

## Requirements

- Jellyfin 10.8.0 or higher
- .NET 8.0
- Backend scraper service running

## Development

This plugin is built using:
- .NET 8.0
- Jellyfin Plugin SDK
- StyleCop Analyzers for code quality

### Building

```bash
dotnet build
```

### Code Analysis

The project includes StyleCop analyzers and follows Jellyfin coding standards.

## License

This project is licensed under the MIT License.
