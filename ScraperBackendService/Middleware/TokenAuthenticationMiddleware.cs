using ScraperBackendService.Configuration;
using Microsoft.Extensions.Options;

namespace ScraperBackendService.Middleware;

/// <summary>
/// Token authentication middleware for securing API endpoints.
/// Validates authentication tokens for API requests when authentication is enabled.
/// </summary>
/// <example>
/// Usage in Program.cs:
/// app.UseMiddleware&lt;TokenAuthenticationMiddleware&gt;();
/// 
/// Client usage with authentication:
/// var httpClient = new HttpClient();
/// httpClient.DefaultRequestHeaders.Add("X-API-Token", "your-secret-token");
/// var response = await httpClient.GetAsync("http://localhost:8585/api/hanime/search?title=Love");
/// 
/// Configuration in appsettings.json:
/// {
///   "ServiceConfig": {
///     "AuthToken": "your-secret-token",
///     "TokenHeaderName": "X-API-Token"
///   }
/// }
/// </example>
public sealed class TokenAuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ServiceConfiguration _config;
    private readonly ILogger<TokenAuthenticationMiddleware> _logger;

    /// <summary>
    /// Initializes a new instance of the TokenAuthenticationMiddleware.
    /// </summary>
    /// <param name="next">Next middleware in the pipeline</param>
    /// <param name="config">Service configuration options</param>
    /// <param name="logger">Logger for authentication events</param>
    public TokenAuthenticationMiddleware(
        RequestDelegate next, 
        IOptions<ServiceConfiguration> config,
        ILogger<TokenAuthenticationMiddleware> logger)
    {
        _next = next;
        _config = config.Value;
        _logger = logger;
    }

    /// <summary>
    /// Processes HTTP requests and validates authentication tokens for API endpoints.
    /// Skips authentication for health check endpoints and when no token is configured.
    /// </summary>
    /// <param name="context">HTTP context for the current request</param>
    /// <returns>Task representing the asynchronous middleware operation</returns>
    /// <example>
    /// Request flow examples:
    /// 
    /// // Public endpoints (no authentication required)
    /// GET / -> passes through
    /// GET /health -> passes through
    /// 
    /// // API endpoints (authentication required when enabled)
    /// GET /api/hanime/search -> validates token
    /// GET /api/dlsite/search -> validates token
    /// 
    /// // Authentication disabled (no token configured)
    /// All requests pass through regardless of endpoint
    /// 
    /// // Authentication enabled with valid token
    /// Headers: { "X-API-Token": "correct-token" }
    /// -> Request proceeds to next middleware
    /// 
    /// // Authentication enabled with missing token
    /// Headers: { }
    /// -> Returns 401 Unauthorized
    /// 
    /// // Authentication enabled with invalid token
    /// Headers: { "X-API-Token": "wrong-token" }
    /// -> Returns 401 Unauthorized
    /// </example>
    public async Task InvokeAsync(HttpContext context)
    {
        // If no token is configured, allow all requests through
        if (string.IsNullOrWhiteSpace(_config.AuthToken))
        {
            await _next(context);
            return;
        }

        // Health check and info endpoints don't require authentication
        if (context.Request.Path == "/" || context.Request.Path == "/health")
        {
            await _next(context);
            return;
        }

        // API endpoints require authentication when token is configured
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            var token = context.Request.Headers[_config.TokenHeaderName].FirstOrDefault();
            
            if (string.IsNullOrWhiteSpace(token))
            {
                _logger.LogWarning("API request without token from {RemoteIP}", context.Connection.RemoteIpAddress);
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync("Missing authentication token");
                return;
            }

            if (!string.Equals(token, _config.AuthToken, StringComparison.Ordinal))
            {
                _logger.LogWarning("API request with invalid token from {RemoteIP}", context.Connection.RemoteIpAddress);
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync("Invalid authentication token");
                return;
            }

            _logger.LogDebug("API request authenticated successfully from {RemoteIP}", context.Connection.RemoteIpAddress);
        }

        await _next(context);
    }
}