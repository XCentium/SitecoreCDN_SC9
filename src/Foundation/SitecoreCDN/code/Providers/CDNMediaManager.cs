﻿using Sitecore.Abstractions;
using Sitecore.Resources.Media;
using Sitecore.Text;
using SitecoreCDN.Switchers;

namespace SitecoreCDN.Providers
{
    /// <summary>
    /// Extends MediaProvider to allow for CDN url replacement at the Provider level
    /// </summary>
    public class CDNMediaManager : DefaultMediaManager
    {
        public CDNMediaManager(BaseFactory factory, MediaProvider provider) : base(factory, provider)
        {
        }

        /// <summary>
        /// If CDN is enabled for this request, replace the outgoing media url with the cdn version
        /// </summary>
        /// <param name="item"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        public override string GetMediaUrl(Sitecore.Data.Items.MediaItem item, MediaUrlOptions options)
        {
            string hostname = CDNManager.GetCDNHostName();
            string url = base.GetMediaUrl(item, options);

            bool shouldReplace = !string.IsNullOrEmpty(hostname) && // cdnHostname exists for site
                Sitecore.Context.PageMode.IsNormal;  // PageMode is normal

            bool dontReplace = !CDNManager.IsMediaPubliclyAccessible(item) ||  // media is publicly accessible
                CDNManager.IsMediaAnalyticsTracked(item); // media is analytics tracked

            CDNUrlState contextState = CDNUrlSwitcher.CurrentValue;

            if (contextState == CDNUrlState.Enabled)
            {
                shouldReplace = true;
            }
            else if (contextState == CDNUrlState.Disabled)
            {
                UrlString url2 = new UrlString(url);
                url2[CDNManager.StopToken] = "1";
                url = url2.ToString();
                shouldReplace = false;
            }

            
            if (shouldReplace && !dontReplace) // media not DMS tracked
            {
                return CDNManager.ReplaceMediaUrl(url, hostname);
            }
            else
            {
                return url;
            }
        }
    }
}
