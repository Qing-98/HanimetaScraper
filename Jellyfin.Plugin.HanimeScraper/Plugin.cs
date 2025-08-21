using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Serialization;
using System;

namespace Jellyfin.Plugin.HanimeScraper;

public class Plugin : BasePlugin<PluginConfiguration>
{
    public Plugin(IApplicationPaths paths, IXmlSerializer serializer)
        : base(paths, serializer)
    {
        Instance = this;
    }

    public static Plugin Instance { get; private set; }

    public override string Name => "Hanime Scraper";

    public override Guid Id => Guid.Parse("22222222-aaaa-bbbb-cccc-222222222222");
}
