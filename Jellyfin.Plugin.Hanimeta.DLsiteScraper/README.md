# Jellyfin DLsite Scraper Plugin

A Jellyfin plugin that provides metadata for DLsite content via backend scraper service.

## Features

- **Smart Search** - Search by title or DLsite ID (RJ/VJ format)
- **Rich Metadata** - Title, description, rating, release date, personnel
- **Image Support** - Cover, backdrop, thumbnails
- **Japanese Content** - Native Japanese character support
- **External IDs** - DLsite product ID tracking

## Requirements

- Jellyfin 10.10.7+
- .NET 8.0
- ScraperBackendService running

## Configuration

Configure via: **Admin Dashboard → Plugins → DLsite Scraper → Settings**

- **Backend URL** - Backend service URL (default: `http://127.0.0.1:8585`)
- **API Token** - Authentication token (optional)
- **Enable Logging** - Debug logging control

## Usage

The plugin automatically detects DLsite content during library scans by filename or manual search.

## License

MIT License
