# Scraper Backend Service

[中文](README.zh.md) | [English](README.md)

A comprehensive web scraping backend service designed for metadata extraction of adult animation and similar content, supporting multiple providers. Built with .NET 8, designed for integration with Jellyfin media server.

## 🚀 Features

### Multi-Provider Support
- **Hanime Provider**: Playwright-based dynamic JavaScript content scraping
- **DLsite Provider**: Efficient HTTP-based static content scraping
- **Extensible Architecture**: Easily add new content providers

### Advanced Scraping Capabilities
- **Dual Network Clients**: HTTP and Playwright for different content types
- **Anti-Bot Protection**: Built-in handling for Cloudflare and other anti-bot mechanisms
- **Context Management**: Smart browser context reuse and rotation
- **Concurrent Processing**: Configurable concurrent request handling
- **Retry Logic**: Robust error handling and retry mechanisms

### Comprehensive Metadata Extraction
- **Basic Info**: Title, description, ID, rating, release date
- **Media Assets**: Primary image, backdrop, thumbnails with deduplication
- **Personnel**: Cast and crew with role mapping (Japanese → English)
- **Classification**: Genres, studios, series

### Production-Ready Features
- **RESTful API**: Clean HTTP API with standardized response format
- **Authentication**: Optional token-based authentication
- **Configuration**: Flexible via appsettings.json and environment variables
- **Logging**: Configurable comprehensive logging
- **Health Checks**: Built-in health monitoring endpoints
- **Timeout Management**: Configurable request timeouts
- **Rate Limiting**: Concurrent request throttling

## 📦 Installation

### Prerequisites
- .NET 8 SDK
- PowerShell (for Playwright browser installation)

### Quick Start

1. **Clone the repository**
```bash
git clone https://github.com/your-repo/HanimetaScraper.git
cd HanimetaScraper/ScraperBackendService
```

2. **Install dependencies**
```bash
dotnet restore
```

3. **Install Playwright browsers**
```bash
pwsh bin/Debug/net8.0/playwright.ps1 install
```

4. **Run the service**
```bash
dotnet run
```

The service will start at `http://localhost:8585` by default.

## ⚙️ Configuration

### appsettings.json
```json
{
  "ServiceConfig": {
    "Port": 8585,
    "Host": "0.0.0.0",
    "AuthToken": null,
    "TokenHeaderName": "X-API-Token",
    "EnableDetailedLogging": false,
    "MaxConcurrentRequests": 10,
    "RequestTimeoutSeconds": 60
  }
}
```

### Environment Variables
- `SCRAPER_PORT`: Override listening port
- `SCRAPER_AUTH_TOKEN`: Set authentication token

### Configuration Options

| Setting | Description | Default | Example |
|---------|-------------|---------|---------|
| `Port` | HTTP listening port | 8585 | 9090 |
| `Host` | Listening address | "0.0.0.0" | "127.0.0.1" |
| `AuthToken` | API authentication token | null | "secret-token-123" |
| `TokenHeaderName` | Auth header name | "X-API-Token" | "Authorization" |
| `EnableDetailedLogging` | Debug logging | false | true |
| `MaxConcurrentRequests` | Concurrent limit | 10 | 20 |
| `RequestTimeoutSeconds` | Request timeout | 60 | 120 |

## 🌐 API Reference

### Base URL
```
http://localhost:8585
```

### Authentication
When `AuthToken` is configured, include it in request headers:
```
X-API-Token: your-secret-token
```

### Endpoints

#### Service Information
```http
GET /
```
Returns service metadata and health status.

**Response:**
```json
{
  "success": true,
  "data": {
    "service": "ScraperBackendService",
    "version": "2.0.0",
    "providers": ["Hanime", "DLsite"],
    "authEnabled": false,
    "timestamp": "2024-01-15T10:30:00.000Z"
  }
}
```

#### Health Check
```http
GET /health
```
Returns service health status.

#### Hanime Content Search
```http
GET /api/hanime/search?title={title}&max={max}
```

**Parameters:**
- `title` (required): Search keyword or phrase
- `max` (optional): Maximum results (default: 12, max: 50)

**Example:**
```bash
curl "http://localhost:8585/api/hanime/search?title=Love&max=5"
```

#### Hanime Content Details
```http
GET /api/hanime/{id}
```

**Parameters:**
- `id`: Hanime content ID (numeric)

**Example:**
```bash
curl "http://localhost:8585/api/hanime/12345"
```

#### DLsite Content Search
```http
GET /api/dlsite/search?title={title}&max={max}
```

**Parameters:**
- `title` (required): Search keyword (supports Japanese)
- `max` (optional): Maximum results (default: 12, max: 50)

**Example:**
```bash
curl "http://localhost:8585/api/dlsite/search?title=恋爱&max=5"
```

#### DLsite Content Details
```http
GET /api/dlsite/{id}
```

**Parameters:**
- `id`: DLsite product ID (e.g., "RJ123456")

**Example:**
```bash
curl "http://localhost:8585/api/dlsite/RJ123456"
```

### Response Format

All API responses follow this standard format:

**Success Response:**
```json
{
  "success": true,
  "data": { ... },
  "message": "Optional success message",
  "timestamp": "2024-01-15T10:30:00.000Z"
}
```

**Error Response:**
```json
{
  "success": false,
  "error": "Error description",
  "message": "Optional error details",
  "timestamp": "2024-01-15T10:30:00.000Z"
}
```

### Metadata Schema

```json
{
  "id": "12345",
  "title": "Content Title",
  "originalTitle": "Original Language Title",
  "description": "Content description...",
  "rating": 4.5,
  "releaseDate": "2024-01-15T00:00:00Z",
  "year": 2024,
  "studios": ["Studio Name"],
  "genres": ["Romance", "Comedy"],
  "series": ["Series Name"],
  "people": [
    {
      "name": "Person Name",
      "type": "Actor",
      "role": "Voice Actor"
    }
  ],
  "primary": "https://example.com/cover.jpg",
  "backdrop": "https://example.com/backdrop.jpg",
  "thumbnails": [
    "https://example.com/thumb1.jpg",
    "https://example.com/thumb2.jpg"
  ],
  "sourceUrls": ["https://source-site.com/content/12345"]
}
```

## 🔧 Development

### Project Structure
```
ScraperBackendService/
├── Core/                    # Core functionality
│   ├── Abstractions/       # Interfaces and contracts
│   ├── Net/                # Network clients
│   ├── Parsing/            # HTML/content parsing
│   ├── Pipeline/           # Orchestration logic
│   ├── Routing/            # URL and ID handling
│   ├── Normalize/          # Data normalization
│   └── Util/               # Utility functions
├── Providers/              # Content provider implementations
│   ├── DLsite/            # DLsite provider
│   └── Hanime/            # Hanime provider
├── Models/                 # Data models
├── Configuration/          # Configuration classes
├── Middleware/             # HTTP middleware
├── Extensions/             # Service extensions
└── Program.cs             # Application entry point
```

### Adding New Providers

1. **Implement IMediaProvider interface:**
```csharp
public class MyProvider : IMediaProvider
{
    public string Name => "MyProvider";
    public bool TryParseId(string input, out string id) { ... }
    public string BuildDetailUrlById(string id) { ... }
    public async Task<IReadOnlyList<SearchHit>> SearchAsync(...) { ... }
    public async Task<HanimeMetadata?> FetchDetailAsync(...) { ... }
}
```

2. **Register in ServiceCollectionExtensions:**
```csharp
services.AddScoped<MyProvider>(sp => 
{
    var networkClient = sp.GetRequiredService<HttpNetworkClient>();
    var logger = sp.GetRequiredService<ILogger<MyProvider>>();
    return new MyProvider(networkClient, logger);
});
```

3. **Add API endpoints in Program.cs:**
```csharp
app.MapGet("/api/myprovider/search", async (...) => { ... });
app.MapGet("/api/myprovider/{id}", async (...) => { ... });
```

### Testing

Use the test project for development and validation:

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

## 🔗 Jellyfin Integration

This service is designed to work with Jellyfin media server through custom metadata plugins. Plugins communicate with this backend service via REST API.

### Plugin Configuration
1. Install the companion Jellyfin plugins
2. Configure the backend service URL in plugin settings
3. Set authentication token if enabled
4. Enable the providers you want to use

## 📝 Logging

The service provides comprehensive logging:

- **Information**: Basic operation flow
- **Warning**: Recoverable errors and unusual conditions
- **Error**: Unrecoverable errors
- **Debug**: Detailed operation information (when DetailedLogging is enabled)

Log configuration in `appsettings.json`:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "ScraperBackendService": "Debug"
    }
  }
}
```

## 🔐 Security

### Authentication
- Token-based authentication for API endpoints
- Configurable token header name
- Public endpoints: `/`, `/health`
- Protected endpoints: `/api/*`

## 🚨 Troubleshooting

### Common Issues

**Playwright Browser Installation**
```bash
# Manually install browsers
pwsh bin/Debug/net8.0/playwright.ps1 install chromium

# Install system dependencies (Linux)
pwsh bin/Debug/net8.0/playwright.ps1 install-deps
```

**Permission Issues (Linux)**
```bash
# Add user to appropriate groups
sudo usermod -a -G audio,video $USER

# Install additional dependencies
sudo apt-get install libnss3 libatk-bridge2.0-0 libdrm2 libxkbcommon0 libxss1 libasound2
```

**Memory Issues**
- Reduce `MaxConcurrentRequests` in configuration
- Increase system memory or swap space
- Use `--disable-dev-shm-usage` browser flag (already configured)

**Network Issues**
- Check firewall settings
- Verify target sites are accessible
- Configure proxy settings if behind corporate firewall

### Debug Mode

Enable detailed logging:
```json
{
  "ServiceConfig": {
    "EnableDetailedLogging": true
  },
  "Logging": {
    "LogLevel": {
      "Default": "Debug"
    }
  }
}
```

## 📄 License

This project is licensed under the MIT License - see the LICENSE file for details.

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests for new functionality
5. Ensure all tests pass
6. Submit a pull request

## 📞 Support

For issues and questions:
1. Check the troubleshooting section
2. Search existing GitHub issues
3. Create a new issue with detailed information
4. Include logs and configuration (remove sensitive data)