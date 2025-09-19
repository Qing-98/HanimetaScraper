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
///     "EnableDetailedLogging": true,
///     "MaxConcurrentRequests": 20,
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
    /// Whether to enable detailed logging for debugging and monitoring.
    /// When enabled, logs include detailed scraping operations and network requests.
    /// </summary>
    /// <example>
    /// // Enable for development/debugging
    /// EnableDetailedLogging = true;
    /// 
    /// // Disable for production (default)
    /// EnableDetailedLogging = false;
    /// </example>
    public bool EnableDetailedLogging { get; set; } = false;

    /// <summary>
    /// Maximum number of concurrent requests the service can handle.
    /// Default is 10. Helps prevent server overload.
    /// </summary>
    /// <example>
    /// // Low-resource server
    /// MaxConcurrentRequests = 5;
    /// 
    /// // High-performance server
    /// MaxConcurrentRequests = 50;
    /// 
    /// // Default setting
    /// MaxConcurrentRequests = 10;
    /// </example>
    public int MaxConcurrentRequests { get; set; } = 10;

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
}