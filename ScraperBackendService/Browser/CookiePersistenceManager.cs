using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using ScraperBackendService.Core.Logging;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ScraperBackendService.Browser;

/// <summary>
/// Cookie 持久化数据模型
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
/// 域名 Cookie 存储
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
/// Cookie 持久化存储容器
/// </summary>
public class CookieStorage
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0";
    
    [JsonPropertyName("domains")]
    public Dictionary<string, DomainCookieStore> Domains { get; set; } = new();
}

/// <summary>
/// 管理 Cookies 的持久化存储和加载
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

        // 确保存储目录存在
        EnsureStorageDirectory();
        
        // 启动时加载已保存的 cookies
        _ = LoadFromFileAsync();
    }

    /// <summary>
    /// 保存 cookies 到持久化存储
    /// </summary>
    /// <param name="domain">域名 (如 hanime.tv)</param>
    /// <param name="cookies">要保存的 cookies</param>
    public async Task SaveCookiesAsync(string domain, IReadOnlyList<BrowserContextCookiesResult> cookies)
    {
        if (string.IsNullOrWhiteSpace(domain))
        {
            _logger.LogWarning("CookiePersistence", "域名为空,无法保存 cookies");
            return;
        }

        try
        {
            // 转换为持久化格式
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

            // 更新内存缓存
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

            // 保存到文件
            await SaveToFileAsync();

            _logger.LogSuccess("CookiePersistence", 
                $"已保存 {persistedCookies.Count} 个 cookies (域名: {domain}, 成功次数: {store.SuccessCount})");
        }
        catch (Exception ex)
        {
            _logger.LogFailure("CookiePersistence", "保存 cookies 失败", domain, ex);
        }
    }

    /// <summary>
    /// 从持久化存储加载指定域名的 cookies
    /// </summary>
    /// <param name="domain">域名</param>
    /// <returns>该域名的 cookies,如果不存在返回空列表</returns>
    public async Task<List<Microsoft.Playwright.Cookie>> LoadCookiesAsync(string domain)
    {
        if (string.IsNullOrWhiteSpace(domain))
        {
            return new List<Microsoft.Playwright.Cookie>();
        }

        try
        {
            // 先从内存缓存读取
            if (_memoryCache.TryGetValue(domain, out var store))
            {
                // 检查是否过期 (30天)
                var age = DateTime.UtcNow - store.LastUpdated;
                if (age.TotalDays > 30)
                {
                    _logger.LogDebug("CookiePersistence", 
                        $"域名 {domain} 的 cookies 已过期 ({age.TotalDays:F1}天), 将被清除");
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
                    $"{domain} | 年龄: {age.TotalHours:F1}小时",
                    cookies.Count);
                
                return cookies;
            }

            _logger.LogDebug("CookiePersistence", $"域名 {domain} 没有保存的 cookies");
            return new List<Microsoft.Playwright.Cookie>();
        }
        catch (Exception ex)
        {
            _logger.LogFailure("CookiePersistence", "加载 cookies 失败", domain, ex);
            return new List<Microsoft.Playwright.Cookie>();
        }
    }

    /// <summary>
    /// 获取所有域名的 cookies 用于应用到浏览器上下文
    /// </summary>
    /// <returns>所有域名的 cookies 合并列表</returns>
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
                $"来自 {_memoryCache.Count} 个域名",
                allCookies.Count);
        }

        return allCookies;
    }

    /// <summary>
    /// 清除指定域名的 cookies
    /// </summary>
    public async Task ClearCookiesAsync(string domain)
    {
        if (_memoryCache.TryRemove(domain, out _))
        {
            await SaveToFileAsync();
            _logger.LogSuccess("CookiePersistence", $"已清除域名 {domain} 的 cookies");
        }
    }

    /// <summary>
    /// 清除所有 cookies
    /// </summary>
    public async Task ClearAllCookiesAsync()
    {
        _memoryCache.Clear();
        await SaveToFileAsync();
        _logger.LogSuccess("CookiePersistence", "已清除所有 cookies");
    }

    /// <summary>
    /// 获取 Cookie 存储统计信息
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
    /// 从文件加载 cookies
    /// </summary>
    private async Task LoadFromFileAsync()
    {
        await _fileLock.WaitAsync();
        try
        {
            if (!File.Exists(_storageFilePath))
            {
                _logger.LogDebug("CookiePersistence", "Cookie 存储文件不存在,跳过加载");
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
                    $"从文件加载了 {storage.Domains.Count} 个域名的 cookies");
            }
        }
        catch (Exception ex)
        {
            _logger.LogFailure("CookiePersistence", "从文件加载 cookies 失败", _storageFilePath, ex);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <summary>
    /// 保存 cookies 到文件
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
                $"已保存 {storage.Domains.Count} 个域名的 cookies 到文件");
        }
        catch (Exception ex)
        {
            _logger.LogFailure("CookiePersistence", "保存 cookies 到文件失败", _storageFilePath, ex);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <summary>
    /// 确保存储目录存在
    /// </summary>
    private void EnsureStorageDirectory()
    {
        try
        {
            if (!Directory.Exists(_storageDirectory))
            {
                Directory.CreateDirectory(_storageDirectory);
                _logger.LogDebug("CookiePersistence", $"创建存储目录: {_storageDirectory}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogFailure("CookiePersistence", "创建存储目录失败", _storageDirectory, ex);
        }
    }

    /// <summary>
    /// 解析 SameSite 属性
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
