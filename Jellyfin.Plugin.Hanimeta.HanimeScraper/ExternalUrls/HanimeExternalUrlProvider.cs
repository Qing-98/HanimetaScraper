using Jellyfin.Plugin.Hanimeta.Common.ExternalUrls;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Hanimeta.HanimeScraper.ExternalUrls
{
    /// <summary>
    /// Provides external URLs for Hanime content.
    /// </summary>
    public class HanimeExternalUrlProvider : BaseExternalUrlProvider
    {
        private readonly ILogger<HanimeExternalUrlProvider> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="HanimeExternalUrlProvider"/> class.
        /// </summary>
        /// <param name="logger">The logger instance.</param>
        public HanimeExternalUrlProvider(ILogger<HanimeExternalUrlProvider> logger)
            : base(HanimeExternalUrlStore.Instance, "Hanime")
        {
            this.logger = logger;
        }

        /// <inheritdoc />
        public override string Name => "Hanime";

        /// <inheritdoc />
        public override string UrlFormatString => "https://hanime1.me/watch?v={0}";
    }
}
