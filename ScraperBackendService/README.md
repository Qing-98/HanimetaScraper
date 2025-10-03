# ScraperBackendService

Core backend service for HanimetaScraper, providing REST API for metadata scraping.

## Features

- **Multi-Provider Support** - Hanime, DLsite
- **RESTful API** - Standardized JSON responses
- **Anti-Bot Protection** - Playwright browser automation
- **Concurrency Control** - Configurable provider access limits
- **Caching Mechanism** - Memory cache to reduce duplicate requests
- **Authentication** - Optional API Token auth

## API Endpoints

### Base
- `GET /` - Service information
- `GET /health` - Health check
- `GET /cache/stats` - Cache statistics
- `DELETE /cache/clear` - Clear all cache
- `DELETE /cache/{provider}/{id}` - Remove specific cache entry

### Hanime
- `GET /api/hanime/search?title={query}&max={limit}` - Search
- `GET /api/hanime/{id}` - Get details

### DLsite  
- `GET /api/dlsite/search?title={query}&max={limit}` - Search
- `GET /api/dlsite/{id}` - Get details

## Configuration

### Main Configuration Items (appsettings.json)

```json
{
  "ServiceConfig": {
    "Port": 8585,
    "Host": "0.0.0.0",
    "AuthToken": "",
    "TokenHeaderName": "X-API-Token",
    "HanimeMaxConcurrentRequests": 3,
    "DlsiteMaxConcurrentRequests": 3,
    "RequestTimeoutSeconds": 60,
    "EnableAggressiveMemoryOptimization": true
  }
}
```

### Configuration Options Explained

| Setting | Description | Default | Recommended Range |
|---------|-------------|---------|-------------------|
| **Port** | HTTP listening port | 8585 | 1024-65535 |
| **Host** | Listening address | "0.0.0.0" | "127.0.0.1" (local)/"0.0.0.0" (all interfaces) |
| **AuthToken** | API authentication token | Empty string | Strong random string (required for production) |
| **TokenHeaderName** | Auth header name | "X-API-Token" | Custom header name |
| **HanimeMaxConcurrentRequests** | Hanime concurrency limit | 3 | 1-15 |
| **DlsiteMaxConcurrentRequests** | DLsite concurrency limit | 3 | 1-15 |
| **RequestTimeoutSeconds** | Request timeout in seconds | 60 | 30-300 |
| **EnableAggressiveMemoryOptimization** | Enable aggressive memory optimization | false | true/false |

### Concurrency Control Explanation

**Provider Concurrency Limits** - Unified control for website access:
- `HanimeMaxConcurrentRequests` - Limits simultaneous access to Hanime website
- `DlsiteMaxConcurrentRequests` - Limits simultaneous access to DLsite website
- Covers all operations: search, detail fetching, direct ID queries
- Returns 429 status when limit exceeded, frontend handles automatic retry

### Environment Variable Overrides

The following environment variables can override configuration file settings:

| Environment Variable | Corresponding Config | Example |
|---------------------|---------------------|---------|
| **SCRAPER_PORT** | Port | `8080` |
| **SCRAPER_AUTH_TOKEN** | AuthToken | `your-secret-token-here` |

### Cache Configuration

Cache system is automatically configured with main parameters:
- **Cache Capacity**: 100 entries
- **Success Result TTL**: 2 minutes
- **Failure Result TTL**: 2 minutes
- **Eviction Policy**: LRU (Least Recently Used)

### Performance Tuning Recommendations

**Low Load Environment (Personal Use):**
```json
{
  "HanimeMaxConcurrentRequests": 3,
  "DlsiteMaxConcurrentRequests": 3
}
```

**High Load Environment (Multi-User):**
```json
{
  "HanimeMaxConcurrentRequests": 10,
  "DlsiteMaxConcurrentRequests": 10
}
```

**Conservative Settings (Avoid Blocking):**
```json
{
  "HanimeMaxConcurrentRequests": 1,
  "DlsiteMaxConcurrentRequests": 1
}
```

## Tech Stack

- **.NET 8** - Modern C# runtime
- **ASP.NET Core** - Web API framework
- **Playwright** - Browser automation
- **HtmlAgilityPack** - HTML parsing