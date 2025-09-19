# New Scraper Test Project

This is a comprehensive test application for the HanimetaScraper backend service with enhanced architecture and English documentation.

## 🚀 Features

### 1. Multiple Testing Modes
- **Full Test**: Comprehensive testing of both DLsite (HTTP) and Hanime (Playwright) providers
- **DLsite Only**: Focused testing of DLsite provider using HTTP client
- **Hanime Only**: Focused testing of Hanime provider using Playwright client  
- **Integration Test**: Simulates Jellyfin frontend accessing backend API endpoints
- **Concurrent Test**: Simulates multiple Jellyfin plugin instances accessing backend simultaneously

### 2. Enhanced Architecture
- **Unified Interface**: Uses `IMediaProvider` and `ScrapeOrchestrator` for consistent provider handling
- **Network Client Abstraction**: Supports both HTTP and Playwright-based network clients
- **Comprehensive Error Handling**: Detailed exception information and graceful error recovery
- **Flexible Routing**: Supports Auto, ById, and ByFilename scraping routes

### 3. Detailed Metadata Display
The test application provides comprehensive metadata information:

#### Result Information Display
- **Basic Info**: ID, title, original title, description
- **Media Details**: Rating, release date, year
- **Classification**: Studios, series, genres/tags
- **Personnel**: Actors, directors, writers, producers with roles
- **Visual Assets**: Primary image, backdrop, thumbnails with counts
- **Source Links**: Original URLs and reference links

#### Example Output
```
📋 Result #1:
   🏷️  ID: RJ123456
   📝 Title: Example Animation Title
   📄 Description: This is a sample description...
   ⭐ Rating: 4.5/5
   📅 Release: 2024-01-15
   🏢 Studios: Studio Example
   📚 Series: Example Series
   🏷️ Tags: romance, comedy, slice-of-life
   👥 People: 
      - Actor Name (Voice Actor)
      - Director Name (Director)
   🖼️  Images: Primary + 5 thumbnails
   🔗 Source: https://example.com/product/...
```

## 🛠️ Quick Start

### Automated Setup (Windows)
Run the `run.bat` file which will:
1. Build the project automatically
2. Install Playwright browsers if needed
3. Start the interactive test program

### Manual Setup

#### 1. Install Dependencies
```bash
cd Test/NewScraperTest
dotnet restore
```

#### 2. Install Playwright Browsers
```bash
pwsh bin/Debug/net8.0/playwright.ps1 install
```

#### 3. Run Tests
```bash
dotnet run
```

## 📋 Test Scenarios

### DLsite Provider Tests
- **Keyword Search**: Tests with Japanese text (`恋爱`)
- **Product ID Search**: Tests with specific product IDs (`RJ01402281`, `RJ01464954`)
- **Network Method**: Uses HTTP client for efficient web scraping

### Hanime Provider Tests
- **Text Search**: Tests with English keywords (`Love`)
- **Video ID Search**: Tests with specific video IDs (`86994`)
- **Network Method**: Uses Playwright for JavaScript-heavy pages

### Integration Tests
- **API Endpoint Testing**: Tests backend service REST API endpoints
- **Authentication**: Supports optional token-based authentication
- **Response Validation**: Validates API response format and content

### Concurrent Tests
- **Load Testing**: Simulates multiple simultaneous requests
- **Performance Monitoring**: Measures response times and success rates
- **Scalability Testing**: Tests system behavior under concurrent load

## 🔧 Configuration Options

### Logging Configuration
```csharp
// Adjust log level for different verbosity
builder.AddConsole().SetMinimumLevel(LogLevel.Information); // or Debug, Warning, Error
```

### Playwright Options
```csharp
// Enable browser visibility for debugging
await playwright.Chromium.LaunchAsync(new() { Headless = false });
```

### Timeout Settings
```csharp
// Adjust timeout for slow networks
using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
```

### Result Limits
```csharp
// Control number of results returned
await orchestrator.FetchAsync(input, route, maxResults: 5, ct);
```

## 🧪 Quick Testing Methods

For rapid development and debugging, use the `QuickTest` class:

```csharp
// Test only DLsite provider
await QuickTest.TestDLsiteOnly();

// Test only Hanime provider  
await QuickTest.TestHanimeOnly();
```

## 📚 Code Examples

### Basic Provider Testing
```csharp
// Create provider instance
var provider = new DlsiteProvider(httpClient, logger);
var orchestrator = new ScrapeOrchestrator(provider, httpClient, logger);

// Perform search
var results = await orchestrator.FetchAsync("search term", ScrapeRoute.Auto, 3, cancellationToken);

// Display results
foreach (var result in results)
{
    Console.WriteLine($"{result.ID}: {result.Title}");
}
```

### Integration Testing
```csharp
// Test backend API with authentication
await BackendApiIntegrationTest.TestHanimeApiAsync("http://localhost:8585", "your-token");

// Test concurrent requests
await BackendApiIntegrationTest.TestConcurrentApiAsync("http://localhost:8585", null, 10);
```

## 🐛 Troubleshooting

### Common Issues and Solutions

| Issue | Solution |
|-------|----------|
| **Network Errors** | Check firewall settings and network connectivity |
| **Playwright Errors** | Ensure browsers are installed: `pwsh playwright.ps1 install` |
| **Timeout Errors** | Increase timeout duration or check website accessibility |
| **Parsing Errors** | Website structure may have changed, check provider implementation |
| **Build Errors** | Ensure all dependencies are restored: `dotnet restore` |

### Debug Tips
1. **Enable Browser Visibility**: Set `Headless = false` to watch browser operations
2. **Increase Logging**: Use `LogLevel.Debug` for detailed operation logs
3. **Extended Timeouts**: Use longer timeouts for slow networks or debugging
4. **Result Inspection**: Use smaller `maxResults` values for focused testing

## 🏗️ Architecture Benefits

Compared to legacy test implementations, this project provides:

1. **Modern Architecture**: Built on dependency injection and interface-based design
2. **Comprehensive Documentation**: Full English documentation with usage examples
3. **Enhanced Error Handling**: Detailed error messages and graceful degradation
4. **Flexible Testing**: Support for multiple testing scenarios and configurations
5. **Production Simulation**: Realistic simulation of Jellyfin integration scenarios
6. **Performance Testing**: Built-in concurrent testing capabilities

## 🔗 Related Components

- **`ScrapeOrchestrator`**: Central coordination of scraping operations
- **`IMediaProvider`**: Standardized interface for different content providers
- **`INetworkClient`**: Abstraction for HTTP and Playwright network operations
- **`HanimeMetadata`**: Unified metadata model for all providers
- **`ScrapeRoute`**: Flexible routing for different search strategies