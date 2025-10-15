# NewScraperTest

[English](README.md) | [中文](README.zh.md)

A comprehensive test suite for the HanimetaScraper backend service and content providers. This interactive test application validates the functionality of DLsite and Hanime scraping providers through real-world scenarios.

## 🚀 Features

### Interactive Test Menu
- **Full Test Suite**: Comprehensive validation for both providers
- **Provider-Specific Tests**: Separate tests for DLsite and Hanime
- **Backend API Integration**: Direct API endpoint testing
- **Concurrent Load Test**: Performance and stability validation
- **Custom Test Scenarios**: Flexible tests with user-defined input

### Comprehensive Validation
- **Search Functionality**: Validates search accuracy and response format
- **Detail Retrieval**: Tests completeness of metadata extraction
- **Error Handling**: Validates graceful failure scenarios
- **Performance Metrics**: Measures response time and throughput
- **Data Quality**: Validates metadata format and content quality

### Real-World Testing
- **Live Content**: Tests against actual provider content
- **Edge Cases**: Handles special characters, long titles, and edge scenarios
- **Network Resilience**: Tests behavior under various network conditions
- **Concurrent Scenarios**: Validates behavior under concurrent load

## 📋 Prerequisites

- .NET 8 SDK
- ScraperBackendService running (for backend API tests)
- Internet connection for live content tests

## 🚀 Quick Start

### Run the Test Suite

1. **Navigate to the test directory**
```bash
cd Test/NewScraperTest
```

2. **Run the interactive test**
```bash
dotnet run
```

3. **Select test options from the menu**
```
1. Full test (both providers)
2. DLsite only test
3. Hanime only test
4. Backend API integration test
5. Concurrent load test
```

### Backend Service Test

For backend API integration tests, ensure ScraperBackendService is running:

```bash
# In another terminal
cd ScraperBackendService
dotnet run
```

Then run option 4 from the test menu to validate API endpoints.

## 🔧 Test Categories

### 1. Full Test Suite (Option 1)
Comprehensive tests for both providers with predefined cases:

**DLsite Test Cases:**
- Search: "恋爱" (Japanese romance content)
- Detail: "RJ01402281" (specific product ID)
- Validation: Metadata completeness, image URLs, personnel mapping

**Hanime Test Cases:**
- Search: "love" (English keyword)
- Detail: "86994" (specific content ID)
- Validation: Title extraction, rating accuracy, genre mapping

### 2. DLsite Only Test (Option 2)
Focuses on DLsite provider functionality:

```
Test Scenarios:
✓ Search Japanese content by keyword
✓ Retrieve detailed metadata by product ID
✓ Validate Japanese character handling
✓ Test RJ/VJ ID format parsing
✓ Validate personnel role mapping
✓ Check image URL validity
```

### 3. Hanime Only Test (Option 3)
Focuses on Hanime provider functionality:

```
Test Scenarios:
✓ Search content by English keyword
✓ Retrieve detailed metadata by content ID
✓ Validate numeric ID parsing
✓ Test URL format extraction
✓ Validate rating and year extraction
✓ Check image deduplication
```

### 4. Backend API Integration Test (Option 4)
Directly tests backend service API endpoints:

```
Tested API Endpoints:
✓ GET / (service info)
✓ GET /health (health check)
✓ GET /api/dlsite/search (DLsite search)
✓ GET /api/dlsite/{id} (DLsite detail)
✓ GET /api/hanime/search (Hanime search)
✓ GET /api/hanime/{id} (Hanime detail)
```

### 5. Concurrent Load Test (Option 5)
Performance and stability testing under concurrent load:

```
Load Test Scenarios:
✓ Multiple simultaneous searches
✓ Concurrent detail retrieval
✓ Mixed provider requests
✓ Error rate measurement
✓ Response time analysis
```

## 📊 Test Output Format

### Search Results Display
```
=== Search Results ===
Query: "love"
Results found: 12

[1] Title: Love Story
    ID: 12345
    URL: https://example.com/content/12345

[2] Title: Love Romance
    ID: 67890
    URL: https://example.com/content/67890
```

### Detail Results Display
```
=== Detail Results ===
ID: 86994
Title: Content Title
Description: Content description...
Rating: 4.5/5.0
Release Year: 2024
Genres: Romance, Drama
Studios: Studio Name
Personnel:
  - Voice Actor: Person Name
  - Director: Director Name
Images:
  - Primary: https://example.com/cover.jpg
  - Thumbnails: 5 images found
```

### Performance Metrics
```
=== Performance Metrics ===
Total requests: 50
Success: 48 (96%)
Failure: 2 (4%)
Average response time: 1.2s
Fastest: 0.8s
Slowest: 3.1s
```

## 🔍 Test Cases

### Predefined Test Scenarios

**DLsite Test Data:**
```csharp
// Search test cases
{ Query: "恋爱", ExpectedMin: 5, Type: "Romance" },
{ Query: "ボイス", ExpectedMin: 10, Type: "Voice" },
{ Query: "同人", ExpectedMin: 20, Type: "Doujin" }

// Detail test cases
{ ID: "RJ01402281", ExpectedTitle: true, ExpectedImages: true },
{ ID: "VJ123456", ExpectedPersonnel: true, ExpectedGenres: true }
```

**Hanime Test Data:**
```csharp
// Search test cases
{ Query: "love", ExpectedMin: 8, Type: "Romance" },
{ Query: "school", ExpectedMin: 15, Type: "School" },
{ Query: "fantasy", ExpectedMin: 10, Type: "Fantasy" }

// Detail test cases
{ ID: "86994", ExpectedRating: true, ExpectedYear: true },
{ ID: "12345", ExpectedGenres: true, ExpectedStudios: true }
```

## 🛠️ Customization

### Adding Custom Test Cases

Edit test configuration in `QuickTest.cs`:

```csharp
// Add new search tests
var customSearchTests = new[]
{
    new { Query = "your-search-term", Provider = "dlsite", MinResults = 5 },
    new { Query = "your-hanime-search", Provider = "hanime", MinResults = 3 }
};

// Add new detail tests
var customDetailTests = new[]
{
    new { ID = "RJ123456", Provider = "dlsite", ValidateImages = true },
    new { ID = "54321", Provider = "hanime", ValidateRating = true }
};
```

### Custom Validation Logic

Implement custom validation functions:

```csharp
public static bool ValidateCustomMetadata(HanimeMetadata metadata)
{
    // Custom validation logic
    return metadata.Title?.Length > 5 &&
           metadata.Rating > 0 &&
           metadata.Genres?.Any() == true;
}
```

## 🐛 Troubleshooting

### Common Test Failures

**Network Connection Issues**
```
Error: Unable to connect to provider
Solution: Check internet connection and provider site accessibility
```

**Backend Service Not Running**
```
Error: Connection refused to localhost:8585
Solution: Start ScraperBackendService before running API tests
```

**Outdated Test Data**
```
Error: Expected content not found
Solution: Update test IDs with currently valid content
```

### Debug Mode

Enable detailed output for debugging:

```csharp
// In Program.cs, set debug flag
const bool DEBUG_MODE = true;

if (DEBUG_MODE)
{
    Console.WriteLine($"Request URL: {url}");
    Console.WriteLine($"Response Headers: {headers}");
    Console.WriteLine($"Response Body: {body}");
}
```

## 📝 Test Reports

### Generate Test Reports

The test suite can generate detailed reports:

```bash
# Run and generate report
dotnet run -- --generate-report

# Specify output format
dotnet run -- --report-format json
dotnet run -- --report-format xml
```

### Report Contents

Test reports include:
- Test execution summary
- Individual test results
- Performance metrics
- Error details and stack traces
- Environment information
- Timestamps and durations

## 🔧 Development

### Adding New Test Types

1. **Create test method in QuickTest.cs:**
```csharp
public static async Task RunCustomTest()
{
    Console.WriteLine("Running custom test...");
    // Test implementation
}
```

2. **Add menu option in Program.cs:**
```csharp
Console.WriteLine("6. Custom Test Scenario");
// Handle menu selection
```

3. **Implement validation logic:**
```csharp
private static bool ValidateCustomResult(object result)
{
    // Custom validation
    return result != null;
}
```

### Test Data Management

Test data is managed via config files and constants:

```csharp
// Test config
public static class TestConfig
{
    public const string DEFAULT_BACKEND_URL = "http://localhost:8585";
    public const int DEFAULT_TIMEOUT = 30000;
    public const int MAX_CONCURRENT_REQUESTS = 10;
}
```

## 📊 Performance Benchmarking

### Benchmark Categories

1. **Response Time Benchmark**
   - Search operation latency
   - Detail retrieval speed
   - API endpoint response time

2. **Throughput Benchmark**
   - Requests per second
   - Concurrent request handling
   - Provider comparison metrics

3. **Reliability Benchmark**
   - Success rate over time
   - Error recovery tests
   - Network resilience validation

## 🤝 Contributing

### Adding Test Cases

1. Identify new test scenarios
2. Implement test methods
3. Add validation logic
4. Update documentation
5. Submit a pull request

### Testing Guidelines

- Use real test data
- Include positive and negative test cases
- Validate all metadata fields
- Test error handling scenarios
- Measure performance impact

## 📄 License

This project is licensed under the MIT License - see the LICENSE file for details.

## 🔗 Related Projects

- **[ScraperBackendService](../../ScraperBackendService/)**: Backend service under test
- **[Jellyfin Plugins](../../Jellyfin.Plugin.*)**: Plugin integration
- **[Main Project](../../)**: Complete HanimetaScraper solution

---

**Note**: This test suite is designed for development and validation purposes. Please ensure compliance with target site terms of service when running tests.