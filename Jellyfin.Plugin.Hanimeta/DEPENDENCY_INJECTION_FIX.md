# Dependency Injection Fix for Hanimeta Plugin

## Problem Description

The plugin was failing to load with the following errors:

```
[ERR] Emby.Server.Implementations.ApplicationHost: Error creating Jellyfin.Plugin.Hanimeta.Providers.Hanime.HanimeUnifiedImageProvider
System.InvalidOperationException: Unable to resolve service for type 'System.Func`1[Jellyfin.Plugin.Hanimeta.Providers.Hanime.HanimeUnifiedApiClient]' while attempting to activate 'Jellyfin.Plugin.Hanimeta.Providers.Hanime.HanimeUnifiedImageProvider'.
```

Similar errors occurred for:
- `HanimeUnifiedMetadataProvider`
- `DLsiteUnifiedImageProvider` 
- `DLsiteUnifiedMetadataProvider`

## Root Cause

The dependency injection container could not resolve `Func<T>` factory types because they were not properly registered in the service collection.

## Solution

### 1. Fixed Service Registration Logic

Modified `PluginServiceRegistrator.cs` to explicitly register API client factories:

```csharp
private static void RegisterApiClientFactory(IServiceCollection serviceCollection, IProviderPluginConfig config)
{
    switch (config.ProviderName.ToLowerInvariant())
    {
        case "hanime":
            serviceCollection.AddSingleton<Func<HanimeUnifiedApiClient>>(serviceProvider =>
            {
                var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
                var logger = loggerFactory.CreateLogger<HanimeUnifiedApiClient>();
                return () => new HanimeUnifiedApiClient(logger, Plugin.GetBackendUrl(), Plugin.GetApiToken());
            });
            break;
        // ... similar for DLsite
    }
}
```

### 2. Key Changes Made

1. **Explicit Factory Registration**: Instead of trying to register `object` types, we now register specific `Func<T>` types that match what the providers expect.

2. **Type-Safe Resolution**: Each provider type gets its own factory registration with correct typing.

3. **Proper Lifetime Management**: Factories are registered as singletons, creating new API client instances on each call.

### 3. Files Modified

- `PluginServiceRegistrator.cs` - Main fix for service registration
- `HanimeProviderPluginConfig.cs` - Simplified to use injected factories
- `DLsiteProviderPluginConfig.cs` - Simplified to use injected factories

## Testing

After applying these fixes:

1. ✅ Plugin compiles successfully
2. ✅ No dependency injection errors
3. ✅ All providers should load correctly

## Future Provider Development

When adding new providers:

1. Add the provider to `RegisterApiClientFactory` switch statement
2. Register the appropriate `Func<YourApiClient>` type
3. Ensure your provider config uses `serviceProvider.GetRequiredService<Func<YourApiClient>>()`

## Related Files

- `PluginServiceRegistrator.cs` - Service registration logic
- `Providers/*/ProviderPluginConfig.cs` - Provider configurations
- `Providers/*/Provider.cs` - Actual provider implementations
