# HanimetaScraper

A .NET 8 based Jellyfin metadata scraping solution for Hanime and DLsite content.

## Project Structure

### Backend Service
- **ScraperBackendService** - Core scraping backend service providing REST API

### Jellyfin Plugins
- **Jellyfin.Plugin.Hanimeta.HanimeScraper** - Hanime metadata provider plugin
- **Jellyfin.Plugin.Hanimeta.DLsiteScraper** - DLsite metadata provider plugin
- **Jellyfin.Plugin.Hanimeta.Common** - Shared plugin library

### Test Tools
- **NewScraperTest** - Backend service test suite

## Features

- 🔍 **Smart Search** - Search content by title or ID
- 📊 **Rich Metadata** - Title, description, rating, release date, personnel information
- 🖼️ **Image Support** - Cover, backdrop, thumbnails
- 🎌 **Multi-language** - Support for Chinese and Japanese content
- ⚡ **High Performance** - Concurrent processing, caching, retry mechanisms
- 🛡️ **Anti-Detection** - Handle Cloudflare and other anti-bot mechanisms

## Architecture

```
Jellyfin Plugins → HTTP API → ScraperBackendService → Website Scrapers
```

The backend service provides unified API, plugins fetch metadata via HTTP requests with caching and concurrency control.

## Configuration

### Backend Service Configuration

Main configuration items (appsettings.json):
```json
{
  "ServiceConfig": {
    "Port": 8585,
    "AuthToken": "",
    "MaxConcurrentRequests": 10,
    "RequestTimeoutSeconds": 60
  }
}
```

### Plugin Configuration Options

Each plugin supports the following configuration items:

| Setting | Description | Default | Example |
|---------|-------------|---------|---------|
| **Backend URL** | ScraperBackendService URL | `http://127.0.0.1:8585` | `https://scraper.mydomain.com` |
| **API Token** | Backend service auth token (optional) | Empty | `your-secret-token-123` |
| **Enable Logging** | Plugin debug logging control | `false` | `true` (for debugging) |
| **Tag Mapping Mode** | Tag destination selection | `Tags` | `Tags` or `Genres` |

**Tag Mapping Mode Explanation:**
- **Tags Mode**: Series + Content Tags → Jellyfin Tags field, Backend Genres → Jellyfin Genres field
- **Genres Mode**: Series + Content Tags → Jellyfin Genres field (merged with Backend Genres)

Configuration Path: **Admin Dashboard → Plugins → [Plugin Name] → Settings**

## License

MIT License