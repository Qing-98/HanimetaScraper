using System;
using Jellyfin.Plugin.Hanimeta.Common.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.Hanimeta.Common.Registration
{
    /// <summary>
    /// Base class for plugin service registration.
    /// </summary>
    public abstract class BasePluginServiceRegistrator
    {
        /// <summary>
        /// Registers common services for the plugins.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="isLoggingEnabled">Function to check if logging is enabled.</param>
        protected void RegisterServices(IServiceCollection services, Func<bool> isLoggingEnabled)
        {
            // Set up logging extensions
            LoggingExtensions.IsLoggingEnabled = isLoggingEnabled;

            // Register plugin-specific services
            this.RegisterPluginServices(services);
        }

        /// <summary>
        /// Registers plugin-specific services.
        /// </summary>
        /// <param name="services">The service collection.</param>
        protected abstract void RegisterPluginServices(IServiceCollection services);
    }
}
