using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using ScraperBackendService.Core.Logging;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ScraperBackendService.Browser;

/// <summary>
/// Cookie persistence data model
/// </summary>
public class PersistedCookie
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;
    
    [JsonPropertyName("domain")]
    public string Domain { get; set; } = string.Empty;
    
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;
    
    [JsonPropertyName("expires")]
    public double Expires { get; set; }
    
    [JsonPropertyName("httpOnly")]
    public bool HttpOnly { get; set; }
    
    [JsonPropertyName("secure")]
    public bool Secure { get; set; }
    
    [JsonPropertyName("sameSite")]
    public string SameSite { get; set; } = "None";
}

/// <summary>
/// Domain cookie store
/// </summary>
public class DomainCookieStore
{
    [JsonPropertyName("domain")]
    public string Domain { get; set; } = string.Empty;
    
    [JsonPropertyName("cookies")]
    public List<PersistedCookie> Cookies { get; set; } = new();
    
    [JsonPropertyName("lastUpdated")]
    public DateTime LastUpdated { get; set; }
    
    [JsonPropertyName("successCount")]
    public int SuccessCount { get; set; }
}

/// <summary>
/// Cookie persistence storage container
/// </summary>
public class CookieStorage
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0";
    
    [JsonPropertyName("domains")]
    public Dictionary<string, DomainCookieStore> Domains { get; set; } = new();
}

/// <summary>
/// Manages persistent cookie storage and loading
/// </summary>
public class CookiePersistenceManager
{
    private readonly ILogger _logger;
    private readonly string _storageDirectory;
    private readonly string _storageFilePath;
    private readonly SemaphoreSlim _fileLock = new(1, 1);
    private readonly ConcurrentDictionary<string, DomainCookieStore> _memoryCache = new();
    private readonly JsonSerializerOptions _jsonOptions;

    public CookiePersistenceManager(ILogger logger, string? storageDirectory = null)
    {
        _logger = logger;
        _storageDirectory = storageDirectory ?? Path.Combine(AppContext.BaseDirectory, "Data", "Cookies");
        _storageFilePath = Path.Combine(_storageDirectory, "cookies.json");
        
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        // Ensure storage directory exists
        EnsureStorageDirectory();
        
        // Load previously saved cookies at startup
        _ = LoadFromFileAsync();
    }

    /// <summary>
    /// Saves cookies to persistent storage
    /// </summary>
    /// <param name="domain">Domain name (e.g., hanime.tv)</param>
    /// <param name="cookies">Cookies to save</param>
    public async Task SaveCookiesAsync(string domain, IReadOnlyList<BrowserContextCookiesResult> cookies)
    {
        if (string.IsNullOrWhiteSpace(domain))
        {
            _logger.LogWarning("CookiePersistence", "Domain is empty, cannot save cookies");
            return;
        }

        try
        {
            // Convert to persistence format
            var persistedCookies = cookies
                .Select(c => new PersistedCookie
                {
                    Name = c.Name,
                    Value = c.Value,
                    Domain = c.Domain,
                    Path = c.Path,
                    Expires = c.Expires,
                    HttpOnly = c.HttpOnly,
                    Secure = c.Secure,
                    SameSite = c.SameSite.ToString()
                })
                .ToList();

            // Update in-memory cache
            var store = _memoryCache.AddOrUpdate(
                domain,
                _ => new DomainCookieStore
                {
                    Domain = domain,
                    Cookies = persistedCookies,
                    LastUpdated = DateTime.UtcNow,
                    SuccessCount = 1
                },
                (_, existing) =>
                {
                    existing.Cookies = persistedCookies;
                    existing.LastUpdated = DateTime.UtcNow;
                    existing.SuccessCount++;
                    return existing;
                });

            // Save to file
            await SaveToFileAsync();

            _logger.LogSuccess("CookiePersistence", 
                $"Saved {persistedCookies.Count} cookies (domain: {domain}, success count: {store.SuccessCount})");
        }
        catch (Exception ex)
        {
            _logger.LogFailure("CookiePersistence", "Failed to save cookies", domain, ex);
        }
    }

    /// <summary>
    /// Loads cookies for the specified domain from persistent storage
    /// </summary>
    /// <param name="domain">Domain name</param>
    /// <returns>Cookies for the domain; returns an empty list if none exist</returns>
    public async Task<List<Microsoft.Playwright.Cookie>> LoadCookiesAsync(string domain)
    {
        if (string.IsNullOrWhiteSpace(domain))
        {
            return new List<Microsoft.Playwright.Cookie>();
        }

        try
        {
            // Read from in-memory cache first
            if (_memoryCache.TryGetValue(domain, out var store))
            {
                // Check expiration (30 days)
                var age = DateTime.UtcNow - store.LastUpdated;
                if (age.TotalDays > 30)
                {
                    _logger.LogDebug("CookiePersistence", 
                        $"Cookies for domain {domain} have expired ({age.TotalDays:F1} days) and will be removed");
                    _memoryCache.TryRemove(domain, out _);
                    await SaveToFileAsync();
                    return new List<Microsoft.Playwright.Cookie>();
                }

                var cookies = store.Cookies
                    .Select(pc => new Microsoft.Playwright.Cookie
                    {
                        Name = pc.Name,
                        Value = pc.Value,
                        Domain = pc.Domain,
                        Path = pc.Path,
                        Expires = (float?)pc.Expires,
                        HttpOnly = pc.HttpOnly,
                        Secure = pc.Secure,
                        SameSite = ParseSameSite(pc.SameSite)
                    })
                    .ToList();

                _logger.LogSuccess("CookiePersistence",
                    $"{domain} | age: {age.TotalHours:F1} hours",
                    cookies.Count);
                
                return cookies;
            }

            _logger.LogDebug("CookiePersistence", $"No saved cookies for domain {domain}");
            return new List<Microsoft.Playwright.Cookie>();
        }
        catch (Exception ex)
        {
            _logger.LogFailure("CookiePersistence", "Failed to load cookies", domain, ex);
            return new List<Microsoft.Playwright.Cookie>();
        }
    }

    /// <summary>
    /// Loads cookies from all domains for browser-context application
    /// </summary>
    /// <returns>Merged cookie list from all domains</returns>
    public async Task<List<Microsoft.Playwright.Cookie>> LoadAllCookiesAsync()
    {
        var allCookies = new List<Microsoft.Playwright.Cookie>();

        foreach (var domain in _memoryCache.Keys)
        {
            var cookies = await LoadCookiesAsync(domain);
            allCookies.AddRange(cookies);
        }

        if (allCookies.Count > 0)
        {
            _logger.LogSuccess("CookiePersistence",
                $"Loaded from {_memoryCache.Count} domains",
                allCookies.Count);
        }

        return allCookies;
    }

    /// <summary>
    /// Clears cookies for the specified domain
    /// </summary>
    public async Task ClearCookiesAsync(string domain)
    {
        if (_memoryCache.TryRemove(domain, out _))
        {
            await SaveToFileAsync();
            _logger.LogSuccess("CookiePersistence", $"Cleared cookies for domain {domain}");
        }
    }

    /// <summary>
    /// Clears all cookies
    /// </summary>
    public async Task ClearAllCookiesAsync()
    {
        _memoryCache.Clear();
        await SaveToFileAsync();
        _logger.LogSuccess("CookiePersistence", "Cleared all cookies");
    }

    /// <summary>
    /// Gets cookie storage statistics
    /// </summary>
    public Dictionary<string, object> GetStatistics()
    {
        return new Dictionary<string, object>
        {
            { "TotalDomains", _memoryCache.Count },
            { "TotalCookies", _memoryCache.Values.Sum(s => s.Cookies.Count) },
            { "Domains", _memoryCache.Keys.OrderBy(k => k).ToList() },
            { "StorageFile", _storageFilePath },
            { "FileExists", File.Exists(_storageFilePath) }
        };
    }

    /// <summary>
    /// Loads cookies from file
    /// </summary>
    private async Task LoadFromFileAsync()
    {
        await _fileLock.WaitAsync();
        try
        {
            if (!File.Exists(_storageFilePath))
            {
                _logger.LogDebug("CookiePersistence", "Cookie storage file does not exist, skipping load");
                return;
            }

            var json = await File.ReadAllTextAsync(_storageFilePath);
            var storage = JsonSerializer.Deserialize<CookieStorage>(json, _jsonOptions);

            if (storage?.Domains != null)
            {
                _memoryCache.Clear();
                foreach (var kvp in storage.Domains)
                {
                    _memoryCache[kvp.Key] = kvp.Value;
                }

                _logger.LogSuccess("CookiePersistence", 
                    $"Loaded cookies for {storage.Domains.Count} domains from file");
            }
        }
        catch (Exception ex)
        {
            _logger.LogFailure("CookiePersistence", "Failed to load cookies from file", _storageFilePath, ex);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <summary>
    /// Saves cookies to file
    /// </summary>
    private async Task SaveToFileAsync()
    {
        await _fileLock.WaitAsync();
        try
        {
            var storage = new CookieStorage
            {
                Domains = new Dictionary<string, DomainCookieStore>(_memoryCache)
            };

            var json = JsonSerializer.Serialize(storage, _jsonOptions);
            await File.WriteAllTextAsync(_storageFilePath, json);

            _logger.LogDebug("CookiePersistence", 
                $"Saved cookies for {storage.Domains.Count} domains to file");
        }
        catch (Exception ex)
        {
            _logger.LogFailure("CookiePersistence", "Failed to save cookies to file", _storageFilePath, ex);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <summary>
    /// Ensures the storage directory exists
    /// </summary>
    private void EnsureStorageDirectory()
    {
        try
        {
            if (!Directory.Exists(_storageDirectory))
            {
                Directory.CreateDirectory(_storageDirectory);
                _logger.LogDebug("CookiePersistence", $"Created storage directory: {_storageDirectory}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogFailure("CookiePersistence", "Failed to create storage directory", _storageDirectory, ex);
        }
    }

    /// <summary>
    /// Parses the SameSite attribute
    /// </summary>
    private static SameSiteAttribute ParseSameSite(string sameSite)
    {
        return sameSite?.ToLowerInvariant() switch
        {
            "strict" => SameSiteAttribute.Strict,
            "lax" => SameSiteAttribute.Lax,
            "none" => SameSiteAttribute.None,
            _ => SameSiteAttribute.None
        };
    }
}
