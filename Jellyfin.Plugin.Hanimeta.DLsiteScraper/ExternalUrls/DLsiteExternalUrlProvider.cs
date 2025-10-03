using Jellyfin.Plugin.Hanimeta.Common.ExternalUrls;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Hanimeta.DLsiteScraper.ExternalUrls
{
    /// <summary>
    /// Provides external URLs for DLsite content.
    /// </summary>
    public class DLsiteExternalUrlProvider : BaseExternalUrlProvider
    {
        private readonly ILogger<DLsiteExternalUrlProvider> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="DLsiteExternalUrlProvider"/> class.
        /// </summary>
        /// <param name="logger">The logger instance.</param>
        public DLsiteExternalUrlProvider(ILogger<DLsiteExternalUrlProvider> logger)
            : base(DLsiteExternalUrlStore.Instance, "DLsite")
        {
            this.logger = logger;
        }

        /// <inheritdoc />
        public override string Name => "DLsite";

        /// <inheritdoc />
        public override string UrlFormatString => "https://www.dlsite.com/maniax/work/=/product_id/{0}.html";
    }
}
