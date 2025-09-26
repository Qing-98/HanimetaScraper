# Jellyfin Hanime Scraper Plugin

[中文](README.zh.md) | [English](README.md)

A Jellyfin plugin that provides metadata for Hanime content by connecting to a backend scraper service. This plugin seamlessly integrates with Jellyfin's metadata system to provide rich information for adult animation content.

## 🚀 Features

- **Advanced Search**: Search Hanime content by title with intelligent ID detection
- **Rich Metadata**: Extract detailed metadata including title, description, rating, release date
- **Personnel Information**: Cast and crew with role mapping
- **Image Support**: Primary image, backdrop, and thumbnails
- **External ID Management**: Hanime ID tracking for accurate identification
- **Multi-language Support**: Handles both English and Japanese content
- **Performance Optimized**: Efficient API communication with caching support

## 📋 Requirements

- **Jellyfin**: Version 10.10.7 or higher
- **.NET Runtime**: .NET 8.0
- **Backend Service**: ScraperBackendService running and accessible
- **Network Access**: Internet connection for content fetching

## ⚙️ Configuration

### Plugin Settings

Access plugin configuration via: **Admin Dashboard → Plugins → Hanime Scraper → Settings**

| Setting | Description | Default | Example |
|---------|-------------|---------|---------|
| **Backend URL** | URL of the scraper backend service | `http://127.0.0.1:8585` | `https://scraper.mydomain.com` |
| **API Token** | Authentication token (optional) | `null` | `your-secret-token-123` |
| **Enable Logging** | Plugin logging control | `false` | `true` (for debugging) |

### Backend URL Configuration

The backend URL should point to your running ScraperBackendService instance:

- **Local Development**: `http://127.0.0.1:8585`
- **Docker Compose**: `http://scraper-backend:8585`
- **Remote Server**: `https://your-scraper-domain.com`
- **Custom Port**: `http://192.168.1.100:9090`

### Authentication Setup

If your backend service uses authentication:

1. Configure API token in backend service
2. Set the same token in plugin configuration
3. Restart Jellyfin to apply changes

## 🔧 Usage

### Automatic Metadata Detection

The plugin automatically detects Hanime content during library scans:

1. **By Filename**: Detects Hanime IDs in filenames (e.g., `86994.mp4`)
2. **By Search**: Searches content by title when no ID is found
3. **By Manual ID**: Manually set Hanime ID in metadata editor

### Manual Metadata Refresh

1. Right-click on content in Jellyfin
2. Select **Identify**
3. Choose **Hanime** as metadata provider
4. Enter title or Hanime ID to search

### Supported ID Formats

- **Direct ID**: `86994`, `12345` (4+ digits required)
- **URL Format**: `https://hanime1.me/watch?v=86994`
- **Mixed Content**: Plugin extracts ID from various formats

**Note**: To avoid false positives during title searches, numeric IDs must be at least 4 digits long. Short numbers like "123" or "99" will be treated as part of the title rather than IDs.

## 🔍 Search Examples

### Text-based Search
```
Search Term: "Love Story"
Results: Multiple anime matching the title
```

### ID-based Search
```
Search Term: "86994"
Results: Specific anime with ID 86994
```

### URL-based Search
```
Search Term: "https://hanime1.me/watch?v=86994"
Results: Specific anime extracted from URL
```

### Mixed Content Search
```
Search Term: "Episode 123"
Results: Title search for "Episode 123" (not treated as ID 123)
```

## 🐛 Troubleshooting

### Common Issues

**Plugin Not Appearing**
- Verify Jellyfin version (10.10.7+)
- Check plugin installation directory
- Restart Jellyfin server
- Review Jellyfin logs for errors

**No Metadata Found**
- Verify backend service is running
- Check backend URL configuration
- Test backend connectivity: `curl http://your-backend:8585/health`
- Review plugin logs in Jellyfin

**Authentication Errors**
- Verify API token matches backend configuration
- Check token header name (default: `X-API-Token`)
- Ensure token is properly URL-encoded

**Search Not Working**
- Check network connectivity to backend
- Verify search query format
- Test backend search: `curl "http://backend:8585/api/hanime/search?title=test"`
- For numeric searches, ensure ID has 4+ digits for ID-based search

### Debug Mode

Enable debug logging for detailed troubleshooting:

1. Set **Enable Logging** to `true` in plugin settings
2. Set Jellyfin log level to `Debug`
3. Reproduce the issue
4. Check Jellyfin logs for detailed information

### Log Locations

- **Windows**: `C:\ProgramData\Jellyfin\Server\logs\`
- **Linux**: `/var/log/jellyfin/`
- **Docker**: Container logs or mounted log directory

## 🔧 Development

### Building from Source

```bash
# Clone repository
git clone https://github.com/your-repo/HanimetaScraper.git
cd HanimetaScraper/Jellyfin.Plugin.HanimeScraper

# Restore dependencies
dotnet restore

# Build debug version
dotnet build

# Build release version
dotnet build -c Release
```

### Code Quality

This plugin follows strict coding standards:

- **StyleCop Analyzers**: Enforced code style rules
- **Nullable Reference Types**: Null safety enabled
- **XML Documentation**: Comprehensive API documentation
- **Unit Testing**: Extensive test coverage

### Contributing

1. Fork the repository
2. Create a feature branch
3. Follow coding standards
4. Add comprehensive tests
5. Update documentation
6. Submit a pull request

## 📊 Performance

### Optimization Features

- **HTTP Client Reuse**: Efficient connection pooling
- **Response Caching**: Reduces redundant API calls
- **Asynchronous Operations**: Non-blocking metadata fetching
- **Error Resilience**: Graceful handling of backend failures

### Performance Tips

- Use local backend deployment for best performance
- Configure appropriate timeout values
- Enable caching in backend service
- Monitor network latency to backend

## 🔒 Security

### Best Practices

- Use HTTPS for remote backend connections
- Secure API tokens with strong values
- Regularly update plugin and dependencies
- Monitor access logs for suspicious activity

## 📚 API Integration

### Backend API Endpoints

The plugin communicates with these backend endpoints:

```http
# Search content
GET /api/hanime/search?title={query}&max={limit}

# Get content details
GET /api/hanime/{id}

# Health check
GET /health
```

### Response Format

```json
{
  "success": true,
  "data": {
    "id": "86994",
    "title": "Content Title",
    "description": "Content description...",
    "rating": 4.5,
    "year": 2024,
    "people": [
      {
        "name": "Person Name",
        "type": "Actor",
        "role": "Voice Actor"
      }
    ],
    "primary": "https://example.com/cover.jpg"
  }
}
```

## 📝 License

This project is licensed under the MIT License - see the [LICENSE](../LICENSE) file for details.

## 🤝 Support

- **Issues**: Report bugs via [GitHub Issues](https://github.com/your-repo/HanimetaScraper/issues)
- **Documentation**: Comprehensive guides in project repository
- **Community**: Join discussions for help and feature requests

## 🔗 Related Projects

- **[ScraperBackendService](../ScraperBackendService/)**: Backend scraping service
- **[DLsite Scraper Plugin](../Jellyfin.Plugin.DLsiteScraper/)**: Companion DLsite plugin
- **[Jellyfin](https://jellyfin.org/)**: Open-source media server

---

**Note**: This plugin is designed for personal use. Please respect content provider terms of service and use responsibly.
