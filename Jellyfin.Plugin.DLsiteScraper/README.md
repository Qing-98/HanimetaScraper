# Jellyfin DLsite Scraper Plugin

[中文](README.zh.md) | [English](README.md)

A Jellyfin plugin that provides metadata for DLsite content by connecting to a backend scraper service. This plugin seamlessly integrates with Jellyfin's metadata system to provide comprehensive information for Japanese adult content from DLsite.

## 🚀 Features

- **Advanced Search**: Search DLsite content by title (supports Japanese)
- **Rich Metadata**: Extract detailed metadata including title, description, rating, release date
- **Personnel Information**: Voice actors, directors, and staff with role mapping
- **Image Support**: Primary image, backdrop, and thumbnails
- **External ID Management**: DLsite product ID tracking (RJ/VJ format)
- **Japanese Content Support**: Native support for Japanese titles and descriptions
- **Performance Optimized**: Efficient API communication with caching support

## 📋 Requirements

- **Jellyfin**: Version 10.8.0 or higher
- **.NET Runtime**: .NET 8.0
- **Backend Service**: ScraperBackendService running and accessible
- **Network Access**: Internet connection for content fetching

## ⚙️ Configuration

### Plugin Settings

Access plugin configuration via: **Admin Dashboard → Plugins → DLsite Scraper → Settings**

| Setting | Description | Default | Example |
|---------|-------------|---------|---------|
| **Backend URL** | URL of the scraper backend service | `http://127.0.0.1:8585` | `https://scraper.mydomain.com` |
| **API Token** | Authentication token (optional) | `null` | `your-secret-token-123` |
| **Enable Logging** | Plugin logging control | `true` | `false` (for production) |

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

The plugin automatically detects DLsite content during library scans:

1. **By Filename**: Detects DLsite IDs in filenames (e.g., `RJ01402281.mp4`)
2. **By Search**: Searches content by title when no ID is found
3. **By Manual ID**: Manually set DLsite ID in metadata editor

### Manual Metadata Refresh

1. Right-click on content in Jellyfin
2. Select **Identify**
3. Choose **DLsite** as metadata provider
4. Enter title or DLsite ID to search

### Supported ID Formats

- **RJ Series**: `RJ01402281`, `RJ123456`
- **VJ Series**: `VJ123456`, `VJ987654`
- **URL Format**: `https://www.dlsite.com/maniax/work/=/product_id/RJ01402281.html`
- **Mixed Content**: Plugin extracts ID from various formats

## 🔍 Search Examples

### Japanese Text Search
```
Search Term: "恋爱"
Results: Multiple works matching the Japanese title
```

### ID-based Search
```
Search Term: "RJ01402281"
Results: Specific work with ID RJ01402281
```

### URL-based Search
```
Search Term: "https://www.dlsite.com/maniax/work/=/product_id/RJ01402281.html"
Results: Specific work extracted from URL
```

## 🐛 Troubleshooting

### Common Issues

**Plugin Not Appearing**
- Verify Jellyfin version (10.8.0+)
- Check plugin installation directory
- Restart Jellyfin server
- Review Jellyfin logs for errors

**No Metadata Found**
- Verify backend service is running
- Check backend URL configuration
- Test backend connectivity: `curl http://your-backend:8585/health`
- Review plugin logs in Jellyfin

**Japanese Characters Issues**
- Ensure UTF-8 encoding is enabled
- Check database character set configuration
- Verify font support for Japanese characters

**Authentication Errors**
- Verify API token matches backend configuration
- Check token header name (default: `X-API-Token`)
- Ensure token is properly URL-encoded

**Search Not Working**
- Check network connectivity to backend
- Verify search query format (supports Japanese)
- Test backend search: `curl "http://backend:8585/api/dlsite/search?title=恋爱"`

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
cd HanimetaScraper/Jellyfin.Plugin.DLsiteScraper

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
GET /api/dlsite/search?title={query}&max={limit}

# Get content details
GET /api/dlsite/{id}

# Health check
GET /health
```

### Response Format

```json
{
  "success": true,
  "data": {
    "id": "RJ01402281",
    "title": "Content Title",
    "originalTitle": "Original Title",
    "description": "Content description...",
    "rating": 4.5,
    "year": 2024,
    "people": [
      {
        "name": "Voice Actor Name",
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
- **[Hanime Scraper Plugin](../Jellyfin.Plugin.HanimeScraper/)**: Companion Hanime plugin
- **[Jellyfin](https://jellyfin.org/)**: Open-source media server

---

**Note**: This plugin is designed for personal use. Please respect content provider terms of service and use responsibly.
