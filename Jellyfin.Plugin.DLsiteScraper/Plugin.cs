using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Serialization;
using System;

namespace Jellyfin.Plugin.DLsiteScraper;

public class Plugin : BasePlugin<PluginConfiguration>
{
    public Plugin(IApplicationPaths paths, IXmlSerializer serializer)
        : base(paths, serializer)
    {
        Instance = this;
    }

    public static Plugin Instance { get; private set; }

    public override string Name => "DLsite Scraper";

    public override Guid Id => Guid.Parse("11111111-aaaa-bbbb-cccc-111111111111");
}
