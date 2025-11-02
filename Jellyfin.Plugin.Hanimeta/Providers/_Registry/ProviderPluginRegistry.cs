using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Plugin.Hanimeta.Providers.DLsite;
using Jellyfin.Plugin.Hanimeta.Providers.Hanime;

namespace Jellyfin.Plugin.Hanimeta.Providers._Registry;

/// <summary>
/// Central registry for all plugin provider configurations.
/// Automatically discovers and manages all available provider configurations.
/// Mirrors the backend provider registry pattern for consistency.
/// </summary>
public static class ProviderPluginRegistry
{
    /// <summary>
    /// Gets all registered provider configurations.
    /// </summary>
    /// <remarks>
    /// To add a new provider:
    /// 1. Create a new ProviderPluginConfig class implementing IProviderPluginConfig
    /// 2. Add it to this list
    /// 3. The registration will be handled automatically
    /// </remarks>
    public static IReadOnlyList<IProviderPluginConfig> AllConfigurations { get; } = new List<IProviderPluginConfig>
    {
        new HanimeProviderPluginConfig(),
        new DLsiteProviderPluginConfig()
    };

    /// <summary>
    /// Gets all registered provider names.
    /// </summary>
    public static IReadOnlyList<string> AllProviderNames { get; } = 
        AllConfigurations.Select(config => config.ProviderName).ToList();

    /// <summary>
    /// Gets a provider configuration by name.
    /// </summary>
    /// <param name="providerName">The provider name to look up</param>
    /// <returns>Provider configuration or null if not found</returns>
    public static IProviderPluginConfig? GetByName(string providerName)
    {
        return AllConfigurations.FirstOrDefault(config => 
            string.Equals(config.ProviderName, providerName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Gets a provider configuration by provider ID.
    /// </summary>
    /// <param name="providerId">The provider ID to look up</param>
    /// <returns>Provider configuration or null if not found</returns>
    public static IProviderPluginConfig? GetById(string providerId)
    {
        return AllConfigurations.FirstOrDefault(config => 
            string.Equals(config.ProviderId, providerId, StringComparison.OrdinalIgnoreCase));
    }
}
