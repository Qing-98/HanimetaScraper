namespace ScraperBackendService.Configuration;

/// <summary>
/// Backend service configuration settings.
/// Defines server hosting options, authentication, and operational parameters.
/// </summary>
/// <example>
/// Usage in appsettings.json:
/// {
///   "ServiceConfig": {
///     "Port": 8585,
///     "Host": "0.0.0.0",
///     "AuthToken": "your-secure-token",
///     "HanimeMaxConcurrentRequests": 5,
///     "DlsiteMaxConcurrentRequests": 5,
///     "RequestTimeoutSeconds": 120
///   }
/// }
///
/// Usage in code:
/// var config = Configuration.GetSection(ServiceConfiguration.SectionName).Get&lt;ServiceConfiguration&gt;();
/// Console.WriteLine($"Server will listen on {config.Host}:{config.Port}");
/// </example>
public sealed class ServiceConfiguration
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "ServiceConfig";

    /// <summary>
    /// Service listening port. Default is 8585.
    /// </summary>
    /// <example>
    /// // Listen on custom port
    /// Port = 9090;
    ///
    /// // Use default port
    /// Port = 8585;
    /// </example>
    public int Port { get; set; } = 8585;

    /// <summary>
    /// Service listening address. Default is 0.0.0.0 (all interfaces).
    /// </summary>
    /// <example>
    /// // Listen on all interfaces (default)
    /// Host = "0.0.0.0";
    ///
    /// // Listen on localhost only
    /// Host = "127.0.0.1";
    ///
    /// // Listen on specific interface
    /// Host = "192.168.1.100";
    /// </example>
    public string Host { get; set; } = "0.0.0.0";

    /// <summary>
    /// Optional authentication token. If empty, authentication is disabled.
    /// When set, clients must include this token in request headers.
    /// </summary>
    /// <example>
    /// // Disable authentication
    /// AuthToken = null;
    ///
    /// // Enable token-based authentication
    /// AuthToken = "my-secret-api-token-123";
    ///
    /// // Client usage:
    /// httpClient.DefaultRequestHeaders.Add("X-API-Token", "my-secret-api-token-123");
    /// </example>
    public string? AuthToken { get; set; }

    /// <summary>
    /// Token verification header name. Default is "X-API-Token".
    /// Specifies which HTTP header contains the authentication token.
    /// </summary>
    /// <example>
    /// // Use default header
    /// TokenHeaderName = "X-API-Token";
    ///
    /// // Use custom header
    /// TokenHeaderName = "Authorization";
    ///
    /// // Client usage:
    /// httpClient.DefaultRequestHeaders.Add(config.TokenHeaderName, token);
    /// </example>
    public string TokenHeaderName { get; set; } = "X-API-Token";

    /// <summary>
    /// Request timeout duration in seconds. Default is 60 seconds.
    /// Applied to both HTTP requests and scraping operations.
    /// </summary>
    /// <example>
    /// // Quick timeout for fast responses
    /// RequestTimeoutSeconds = 30;
    ///
    /// // Extended timeout for complex scraping
    /// RequestTimeoutSeconds = 180;
    ///
    /// // Default timeout
    /// RequestTimeoutSeconds = 60;
    /// </example>
    public int RequestTimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// Provider-specific limit for Hanime concurrent requests.
    /// Controls the maximum number of simultaneous requests to Hanime website.
    /// </summary>
    /// <example>
    /// // Conservative setting for shared hosting
    /// HanimeMaxConcurrentRequests = 3;
    ///
    /// // Aggressive setting for dedicated server
    /// HanimeMaxConcurrentRequests = 10;
    ///
    /// // Default setting
    /// HanimeMaxConcurrentRequests = 5;
    /// </example>
    public int HanimeMaxConcurrentRequests { get; set; } = 5;

    /// <summary>
    /// Provider-specific limit for DLsite concurrent requests.
    /// Controls the maximum number of simultaneous requests to DLsite website.
    /// </summary>
    /// <example>
    /// // Conservative setting for shared hosting
    /// DlsiteMaxConcurrentRequests = 3;
    ///
    /// // Aggressive setting for dedicated server
    /// DlsiteMaxConcurrentRequests = 10;
    ///
    /// // Default setting
    /// DlsiteMaxConcurrentRequests = 5;
    /// </example>
    public int DlsiteMaxConcurrentRequests { get; set; } = 5;

    /// <summary>
    /// Whether to enable aggressive memory optimization including frequent garbage collection.
    /// When enabled, triggers more frequent GC to prevent memory buildup at the cost of some performance.
    /// </summary>
    /// <example>
    /// // Enable for memory-constrained environments
    /// EnableAggressiveMemoryOptimization = true;
    ///
    /// // Disable for high-performance scenarios (default)
    /// EnableAggressiveMemoryOptimization = false;
    /// </example>
    public bool EnableAggressiveMemoryOptimization { get; set; } = false;
}
