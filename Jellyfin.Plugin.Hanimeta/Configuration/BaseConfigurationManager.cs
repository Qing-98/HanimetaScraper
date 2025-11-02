using System;
using Jellyfin.Plugin.Hanimeta.Configuration;

namespace Jellyfin.Plugin.Hanimeta.Configuration
{
    /// <summary>
    /// Base configuration manager for Hanimeta plugins.
    /// </summary>
    /// <typeparam name="TPlugin">The plugin type.</typeparam>
    /// <typeparam name="TPluginConfiguration">The plugin configuration type.</typeparam>
    public abstract class BaseConfigurationManager<TPlugin, TPluginConfiguration>
        where TPluginConfiguration : HanimetaPluginConfiguration, new()
        where TPlugin : class
    {
        private readonly Func<TPlugin?> getPluginInstance;
        private readonly Func<TPlugin, TPluginConfiguration?> getPluginConfiguration;

        /// <summary>
        /// Initializes a new instance of the <see cref="BaseConfigurationManager{TPlugin, TPluginConfiguration}"/> class.
        /// </summary>
        /// <param name="getPluginInstance">Function to get the plugin instance.</param>
        /// <param name="getPluginConfiguration">Function to get the plugin configuration from instance.</param>
        protected BaseConfigurationManager(
            Func<TPlugin?> getPluginInstance,
            Func<TPlugin, TPluginConfiguration?> getPluginConfiguration)
        {
            this.getPluginInstance = getPluginInstance;
            this.getPluginConfiguration = getPluginConfiguration;
        }

        /// <summary>
        /// Gets the current plugin configuration with fallback defaults.
        /// </summary>
        /// <returns>The plugin configuration.</returns>
        public TPluginConfiguration GetConfiguration()
        {
            var instance = this.getPluginInstance();
            return instance != null ? this.getPluginConfiguration(instance) ?? new TPluginConfiguration() : new TPluginConfiguration();
        }

        /// <summary>
        /// Gets the backend URL with proper formatting.
        /// </summary>
        /// <returns>The formatted backend URL.</returns>
        public string GetBackendUrl()
        {
            var config = this.GetConfiguration();
            return string.IsNullOrWhiteSpace(config.BackendUrl)
                ? "http://127.0.0.1:8585"
                : config.BackendUrl.TrimEnd('/');
        }

        /// <summary>
        /// Gets the API token if configured.
        /// </summary>
        /// <returns>The API token or null if not configured.</returns>
        public string? GetApiToken()
        {
            var config = this.GetConfiguration();
            return string.IsNullOrWhiteSpace(config.ApiToken) ? null : config.ApiToken.Trim();
        }

        /// <summary>
        /// Gets whether logging is enabled.
        /// </summary>
        /// <returns>True if logging is enabled.</returns>
        public bool IsLoggingEnabled()
        {
            return this.GetConfiguration().EnableLogging;
        }

        /// <summary>
        /// Validates the current configuration.
        /// </summary>
        /// <returns>True if the configuration is valid.</returns>
        public bool IsConfigurationValid()
        {
            var config = this.GetConfiguration();

            // Check if backend URL is properly formatted
            var backendUrl = this.GetBackendUrl();
            if (!Uri.TryCreate(backendUrl, UriKind.Absolute, out var uri) ||
                (uri.Scheme != "http" && uri.Scheme != "https"))
            {
                return false;
            }

            return true;
        }
    }
}
