# HanimetaScraper

[中文](README.zh.md) | [English](README.md)

A comprehensive metadata scraping solution for adult animation content, designed for Jellyfin media server integration. This project provides a modular architecture with backend service and Jellyfin plugins for extracting rich metadata from multiple content providers.

## ⚠️ Important Notice

This project is designed for **personal and educational use only**. Users are responsible for:

- ✅ **Respecting target websites' terms of service**
- ✅ **Following robots.txt directives**
- ✅ **Using reasonable request rates to avoid server overload**
- ✅ **Complying with local laws and regulations**
- ❌ **Not using for commercial purposes or large-scale data collection**

The authors disclaim any liability for misuse of this software.

## 🚀 Features

### Multi-Provider Support
- **Hanime Provider**: JavaScript-enabled scraping with Playwright for dynamic content
- **DLsite Provider**: Efficient HTTP-based scraping for static content
- **Extensible Architecture**: Easy integration of new content providers

### Backend Service
- **RESTful API**: Clean HTTP endpoints with standardized JSON responses
- **Dual Network Clients**: HTTP and Playwright-based approaches for different site types
- **Anti-Bot Protection**: Built-in Cloudflare and anti-bot bypass mechanisms
- **Concurrent Processing**: Configurable request throttling and parallel processing
- **Authentication**: Optional token-based API security

### Jellyfin Integration
- **Native Plugins**: Seamless integration with Jellyfin 10.8+ media server
- **Metadata Extraction**: Title, description, ratings, cast, genres, images
- **Search Functionality**: Support for both ID-based and text-based searches
- **Image Provider**: Automatic image fetching and caching

### Production Features
- **Docker Support**: Containerized deployment with health checks
- **Configuration Management**: Flexible configuration via JSON and environment variables
- **Comprehensive Logging**: Structured logging with configurable levels
- **Error Handling**: Robust retry logic and graceful degradation
- **Performance Monitoring**: Built-in metrics and health endpoints

## 📦 Project Structure

```
HanimetaScraper/
├── ScraperBackendService/           # Core backend service
│   ├── Core/                       # Core functionality
│   │   ├── Abstractions/           # Interfaces and contracts
│   │   ├── Net/                    # Network clients (HTTP & Playwright)
│   │   ├── Pipeline/               # Orchestration and workflow
│   │   ├── Parsing/                # HTML and content parsing
│   │   └── Util/                   # Utility functions
│   ├── Providers/                  # Content provider implementations
│   │   ├── DLsite/                 # DLsite provider
│   │   └── Hanime/                 # Hanime provider
│   ├── Models/                     # Data models and DTOs
│   └── Configuration/              # Service configuration
├── Jellyfin.Plugin.HanimeScraper/   # Hanime Jellyfin plugin
├── Jellyfin.Plugin.DLsiteScraper/   # DLsite Jellyfin plugin
└── Test/                           # Testing projects
    ├── NewScraperTest/             # Comprehensive test suite
    └── ScraperConsoleTest/         # Legacy console tests
```

## 🚀 Quick Start

### Prerequisites
- .NET 8.0 SDK
- PowerShell (for Playwright browser installation)

### Backend Service

1. **Clone and Setup**
```bash
git clone https://github.com/your-repo/HanimetaScraper.git
cd HanimetaScraper/ScraperBackendService
dotnet restore
```

2. **Install Playwright Browsers**
```bash
pwsh bin/Debug/net8.0/playwright.ps1 install
```

3. **Run the Service**
```bash
dotnet run
```

The service will start on `http://localhost:8585` by default.

4. **Verify Installation**
```bash
curl http://localhost:8585/health
curl "http://localhost:8585/api/dlsite/search?title=example&max=3"
```

### Jellyfin Plugins

1. **Build Plugins**
```bash
# Build DLsite plugin
cd Jellyfin.Plugin.DLsiteScraper
dotnet build -c Release

# Build Hanime plugin
cd ../Jellyfin.Plugin.HanimeScraper
dotnet build -c Release
```

2. **Install in Jellyfin**
- Copy plugin files to Jellyfin plugins directory
- Restart Jellyfin server
- Configure backend URL in plugin settings

### Testing

```bash
cd Test/NewScraperTest
dotnet run
```

Choose from interactive test options:
1. Full test (both providers)
2. DLsite only test
3. Hanime only test
4. Backend API integration test
5. Concurrent load test

## ⚙️ Configuration

### Backend Service

Environment variables:
- `SCRAPER_PORT`: Service listening port (default: 8585)
- `SCRAPER_AUTH_TOKEN`: API authentication token
- `SCRAPER_LOG_LEVEL`: Logging verbosity

Configuration file (`appsettings.json`):
```json
{
  "ServiceConfig": {
    "Port": 8585,
    "Host": "0.0.0.0",
    "AuthToken": null,
    "EnableDetailedLogging": false,
    "MaxConcurrentRequests": 10,
    "RequestTimeoutSeconds": 60
  }
}
```

### Jellyfin Plugins

Both plugins share common configuration:
- **Backend URL**: Backend service URL (default: http://localhost:8585)
- **API Token**: Optional authentication token
- **Enable Logging**: Plugin-specific logging control

## 📋 API Reference

### Base Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/` | Service information |
| GET | `/health` | Health check |

### DLsite Provider

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/dlsite/search?title={query}&max={limit}` | Search content |
| GET | `/api/dlsite/{id}` | Get content details |

### Hanime Provider

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/hanime/search?title={query}&max={limit}` | Search content |
| GET | `/api/hanime/{id}` | Get content details |

### Response Format

```json
{
  "success": true,
  "data": {
    "id": "12345",
    "title": "Content Title",
    "description": "Content description...",
    "rating": 4.5,
    "year": 2024,
    "genres": ["romance", "comedy"],
    "studios": ["Studio Name"],
    "people": [
      {
        "name": "Person Name",
        "type": "Actor",
        "role": "Voice Actor"
      }
    ],
    "primary": "https://example.com/cover.jpg",
    "thumbnails": ["https://example.com/thumb1.jpg"]
  }
}
```

## 🐳 Docker Deployment

### Using Docker Compose

```yaml
version: '3.8'
services:
  scraper-backend:
    build: ./ScraperBackendService
    ports:
      - "8585:8585"
    environment:
      - SCRAPER_PORT=8585
      - SCRAPER_AUTH_TOKEN=your-secret-token
    volumes:
      - ./logs:/app/logs
    restart: unless-stopped
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8585/health"]
      interval: 30s
      timeout: 10s
      retries: 3
```

### Standalone Docker

```bash
# Build image
docker build -t hanime-scraper ./ScraperBackendService

# Run container
docker run -d \
  --name scraper-backend \
  -p 8585:8585 \
  -e SCRAPER_AUTH_TOKEN=your-token \
  hanime-scraper
```

## 🔧 Development

### Adding New Providers

1. Implement `IMediaProvider` interface
2. Register in dependency injection
3. Add API endpoints
4. Update documentation

### Code Quality

- StyleCop Analyzers for code style
- Nullable reference types enabled
- Comprehensive XML documentation
- Unit and integration tests

### Architecture Benefits

- Modern .NET 8 architecture
- Dependency injection throughout
- Interface-based design for testability
- Comprehensive error handling
- Production-ready logging and monitoring


## 📊 Performance

### Optimization Features
- HTTP client connection pooling
- Efficient browser context management
- Image URL normalization
- Configurable concurrent request limits
- Response caching capabilities

### Monitoring
- Built-in health checks
- Structured JSON logging
- Performance metrics tracking
- Comprehensive error categorization

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch
3. Follow coding standards
4. Add comprehensive tests
5. Update documentation
6. Submit a pull request

## 📝 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

- **Jellyfin Community**: Excellent media server platform
- **Playwright Team**: Robust browser automation framework
- **Microsoft**: .NET ecosystem and development tools

## 📞 Support

- **Issues**: Report bugs via GitHub Issues
- **Discussions**: Community help and ideas
- **Documentation**: Comprehensive guides in project directories

---

**Note**: This project is designed for educational and personal use. Please respect the terms of service of the websites being scraped and use responsibly.