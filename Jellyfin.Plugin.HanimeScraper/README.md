# Jellyfin Hanime Scraper Plugin

A Jellyfin plugin that provides metadata for Hanime content by connecting to a backend scraper service.

## Features

- 🔍 **Search Support**: Search for Hanime content by title
- 📊 **Metadata Extraction**: Get detailed metadata including title, description, ratings, and more
- 👥 **People Information**: Extract cast and crew information
- 🖼️ **Image Support**: Primary and backdrop images
- 🆔 **External ID Support**: Hanime ID tracking for proper identification

## Installation

1. Download the latest release
2. Extract to your Jellyfin plugins directory: `C:\ProgramData\Jellyfin\Server\plugins\Jellyfin.Plugin.HanimeScraper\`
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
