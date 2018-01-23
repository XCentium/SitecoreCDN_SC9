using CsQuery;
using Sitecore;
using Sitecore.Configuration;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.IO;
using Sitecore.Security.Domains;
using Sitecore.SecurityModel;
using Sitecore.Sites;
using Sitecore.Text;
using Sitecore.Web;
using SitecoreCDN.Caching;
using SitecoreCDN.Configuration;
using SitecoreCDN.Request;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace SitecoreCDN.Providers
{
    /// <summary>
    /// Contains all CDN related provider methods.
    /// </summary>
    public class CDNProvider
    {
        private UrlCache _cache; // cache url/security/tracking results here
        private ExcludeIncludeCache _excludeUrlCache; // cache url excludes here
        private ExcludeIncludeCache _includeUrlCache; // cache url includes here
        private ExcludeIncludeCache _excludeRequestCache; // cache url request excludes here


        /// <summary>
        /// The token used to stop url replacement
        /// </summary>
        public virtual string StopToken
        {
            get { return "ncdn"; }
        }

        /// <summary>
        /// special value to indicate no caching
        /// </summary>
        public virtual string NoCacheToken
        {
            get { return "#nocache#"; }
        }

        public CDNProvider()
        {
            long cacheSize = StringUtil.ParseSizeString(Settings.GetSetting("SitecoreCDN.FileVersionCacheSize", "5MB"));
            _cache = new UrlCache("CDNUrl", cacheSize);
            _excludeUrlCache = new ExcludeIncludeCache("CDNExcludes", cacheSize);
            _includeUrlCache = new ExcludeIncludeCache("CDNIncludes", cacheSize);
            _excludeRequestCache = new ExcludeIncludeCache("CDNRequestExcludes", cacheSize);
        }

        /// <summary>
        /// replace appropriate media urls in a full HtmlDocument
        /// </summary>
        /// <param name="dom"></param>
        public virtual void ReplaceMediaUrls(CQ dom)
        {
            try
            {
                string cdnHostname = GetCDNHostName();
                string href;
                // for any <link href=".." /> do replacement
                CQ hrefs = dom["[href],[link]"];
                //doc.DocumentNode.SelectNodes("//link");
                if (hrefs != null)
                {
                    foreach (var hrefDom in hrefs)
                    {
                        Log.Debug(String.Format("The current href = {0}", hrefDom.Attributes["href"]), this);

                        href = hrefDom.Attributes["href"];

                        if (!string.IsNullOrEmpty(href) && !UrlIsExluded(href) && !UrlHasFileExtension(href))  // don't replace VisitorIdentification.aspx
                        {
                            hrefDom.Attributes["href"] = ReplaceMediaUrl(href, cdnHostname);
                        }
                        Log.Debug(String.Format("The new href = {0}", hrefDom.Attributes["href"]), this);
                    }
                }
                // for any <img src=".." /> do replacement
                CQ images = dom["img,script"];

                //doc.DocumentNode.SelectNodes("//img"); is old html agility pack syntax
                if (images != null)
                {
                    string src;
                    foreach (var image in images)
                    {
                        Log.Debug(String.Format("The current image src = {0}", image.Attributes["src"]), this);

                        src = image.Attributes["src"];
                        if (!string.IsNullOrEmpty(src) && !UrlIsExluded(src))  // don't replace VisitorIdentification.aspx
                        {
                            image.Attributes["src"] = ReplaceMediaUrl(src, cdnHostname);
                        }
                        Log.Debug(String.Format("The new image src = {0}", image.Attributes["src"]), this);
                    }
                }
                //TODO:  Look at enabling fastLoadJS

            }
            catch (Exception ex)
            {
                Log.Error("ReplaceMediaUrls", ex, this);
            }

        }


        /// <summary>
        /// The problem is that the dom hrefs is matching things that should just be local.
        /// For example it shouldn't replace this:
        /// /my-acc/ncdr-physician-dashboard
        /// Thats a link to the dashboard.
        /// However, it should replace /StyleSheets/base.css
        /// The logic I am taking is that if you have a file extension then I am replacing you.  Otherwise you are staying local
        /// </summary>
        /// <param name="href"></param>
        /// <returns></returns>
        private bool UrlHasFileExtension(string href)
        {
            Regex shouldStayLocal = new Regex(@".+\.[A-Za-z0-9]{2,4}$");
            return !shouldStayLocal.Match(href).Success;
        }


        /// <summary>
        /// Rewrites media urls to point to CDN hostname and dehydrates querystring into filename
        /// </summary>
        /// <param>/path/to/file.ext?a=1&b=2</param>
        /// <param name="inputUrl"></param>
        /// <param name="cdnHostname"></param>
        /// <returns>http://cdnHostname/path/to/file!cf!a=1!b=2.ext</returns>
        public virtual string ReplaceMediaUrl(string inputUrl, string cdnHostname)
        {
            //string versionKey = inputUrl + "_v";
            //string updatedKey = inputUrl + "_d";
            string cachedKey = string.Concat("https", inputUrl);

            try
            {

                string cachedUrl = _cache.GetUrl(cachedKey);

                if (!string.IsNullOrEmpty(cachedUrl))
                {
                    return cachedUrl;
                }

                // ignore fully qualified urls or data:
                if (WebUtil.IsExternalUrl(inputUrl) || inputUrl.ToUpper().StartsWith("DATA:") || inputUrl.ToUpper().StartsWith("HTTP") || inputUrl.StartsWith("//") || inputUrl.ToUpper().StartsWith("MAIL") || inputUrl.ToUpper().StartsWith("FTP") || inputUrl.ToUpper().StartsWith("NEWS"))
                    return inputUrl;

                UrlString url = new UrlString(WebUtil.NormalizeUrl(inputUrl));
                UrlString originalUrl = new UrlString(WebUtil.NormalizeUrl(inputUrl));

                //  if the stoptoken ex. ?nfc=1  is non-empty, don't replace this url
                if (!string.IsNullOrEmpty(url[StopToken]))
                {
                    url.Remove(StopToken);
                }
                else
                {

                    if (!string.IsNullOrEmpty(cdnHostname))
                        url.HostName = cdnHostname;  // insert CDN hostname

                    if (CDNSettings.MatchProtocol)
                        url.Protocol = "https"; //Forcing CDN calls to be https

                    url.Path = StringUtil.EnsurePrefix('/', url.Path);  //ensure first "/" before ~/media


                    if (CDNSettings.FilenameVersioningEnabled)
                    {
                        // if this is a media library request
                        if (inputUrl.Contains(Settings.Media.MediaLinkPrefix))
                        {
                            string version = url["vs"] ?? string.Empty;
                            string updated = string.Empty;


                            // get sitecore path of media item
                            string mediaItemPath = GetMediaItemPath(url.Path);
                            if (!string.IsNullOrEmpty(mediaItemPath) && Context.Database != null)
                            {
                                Item mediaItem = null;
                                if (!string.IsNullOrEmpty(version))
                                {
                                    mediaItem = Context.Database.GetItem(mediaItemPath, Context.Language, Sitecore.Data.Version.Parse(version));
                                }
                                else
                                {
                                    mediaItem = Context.Database.SelectSingleItem(mediaItemPath);
                                }

                                if (mediaItem == null)
                                {
                                    // no change to url
                                    url = originalUrl;
                                }
                                else
                                {
                                    // do not replace url if media item isn't public or requires Analytics processing
                                    // keep local url for this case
                                    if (!this.IsMediaPubliclyAccessible(mediaItem) || IsMediaAnalyticsTracked(mediaItem))
                                    {
                                        // no change to url
                                        url = originalUrl;
                                    }
                                    else
                                    {
                                        version = mediaItem.Version.Number.ToString();
                                        updated = DateUtil.ToIsoDate(mediaItem.Statistics.Updated);
                                    }
                                }
                            }

                            if (!string.IsNullOrEmpty(version))
                            {
                                // append version number qs
                                url.Add("vs", version);
                            }
                            if (!string.IsNullOrEmpty(updated))
                            {
                                // append  timestamp qs
                                url.Add("d", updated);
                            }
                        }
                        else // else this is a static file url
                        {
                            string updated = string.Empty;

                            if (string.IsNullOrEmpty(updated))
                            {
                                if (FileUtil.FileExists(url.Path))
                                {
                                    DateTime lastWrite = FileUtil.GetFileWriteTime(url.Path);
                                    updated = DateUtil.ToIsoDate(lastWrite);
                                }
                            }
                            if (!string.IsNullOrEmpty(updated))
                            {
                                // append timestamp qs
                                url.Add("d", updated);
                            }

                            if (CDNSettings.MinifyEnabled && (url.Path.EndsWith(".css") || url.Path.EndsWith(".js")))
                                url.Add("min", "1");
                        }
                    }
                }

                string outputUrl = url.ToString().TrimEnd('?');//prevent trailing ? with blank querystring

                _cache.SetUrl(cachedKey, outputUrl);

                return outputUrl;
            }
            catch (Exception ex)
            {
                Log.Error(string.Format("ReplaceMediaUrl {0} {1}", cdnHostname, inputUrl), ex, this);
                return inputUrl;
            }
        }

        /// <summary>
        /// Tells you if the url is excluded by ExcludeUrlPatterns in .config
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public virtual bool UrlIsExluded(string url)
        {
            bool? exc = _excludeUrlCache.GetResult(url);
            if (exc.HasValue)
                return exc.Value;
            bool output = CDNSettings.ExcludeUrlPatterns.Any(re => re.IsMatch(url));
            _excludeUrlCache.SetResult(url, output);
            return output;
        }

        /// <summary>
        /// Tells you if an incoming request's url should have it's contents Url replaced.
        /// ProcessRequestPatterns in .config
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public virtual bool ShouldProcessRequest(string url)
        {
            bool? inc = _includeUrlCache.GetResult(url);
            if (inc.HasValue)
                return inc.Value;
            bool output = CDNSettings.ProcessRequestPatterns.Any(re => re.IsMatch(url));
            _includeUrlCache.SetResult(url, output);
            return output;
        }

        /// <summary>
        /// Tells you if an incoming request's url should NOT hav its contents Url replaced.
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public virtual bool ShouldExcludeRequest(string url)
        {
            bool? exc = _excludeRequestCache.GetResult(url);
            if (exc.HasValue)
                return exc.Value;
            bool output = CDNSettings.ExcludeRequestPatterns.Any(re => re.IsMatch(url));
            _excludeRequestCache.SetResult(url, output);
            return output;
        }


        /// <summary>
        /// Extracts the sitecore media item path from a Url 
        /// </summary>
        /// <param name="localPath">~/media/path/to/file.ashx?w=1</param>
        /// <returns>/sitecore/media library/path/to/file</returns>
        public virtual string GetMediaItemPath(string localPath)
        {
            var mr = new CustomMediaRequest();
            mr.Initialize(System.Web.HttpContext.Current.Request);
            return mr.GetCustomMediaPath(localPath);
        }


        /// <summary>
        /// Attempts to retrieve the CDN hostname for the current site
        /// </summary>
        /// <returns></returns>
        public virtual string GetCDNHostName()
        {
            return GetCDNHostName(Sitecore.Context.Site);
        }

        /// <summary>
        /// Attempts to retrive the CDN hostname for this site
        /// </summary>
        /// <param name="siteContext"></param>
        /// <returns></returns>
        public virtual string GetCDNHostName(SiteContext siteContext)
        {
            if (siteContext == null)
                return string.Empty;
            // try to find <site name='[sitename]'  cdnHostName='[cdnhostname]' />
            return StringUtil.GetString(siteContext.Properties.Get("cdnHostName"));
        }

        /// <summary>
        /// Is this media item publicly accessible by the anonymous user?
        /// </summary>
        /// <param name="media"></param>
        /// <returns></returns>
        public virtual bool IsMediaPubliclyAccessible(MediaItem media)
        {
            string cacheKey = media.ID.ToString() + "_public";
            string cached = _cache.GetUrl(cacheKey);
            bool output = true;

            // cached result
            if (!string.IsNullOrEmpty(cached))
            {
                output = MainUtil.GetBool(cached, true);
            }
            else
            {
                Domain domain = Sitecore.Context.Domain ?? DomainManager.GetDomain("extranet");
                var anon = domain.GetAnonymousUser();
                if (anon != null)
                    output = media.InnerItem.Security.CanRead(anon);

                _cache.SetUrl(cacheKey, output.ToString());
            }
            return output;
        }

        /// <summary>
        /// Is this media item Tracked by DMS?
        /// </summary>
        /// <param name="media"></param>
        /// <returns></returns>
        public virtual bool IsMediaAnalyticsTracked(MediaItem media)
        {
            try
            {

                if (!Sitecore.Xdb.Configuration.XdbSettings.Enabled)
                    return false;

                string cacheKey = media.ID.ToString() + "_tracked";
                string cached = _cache.GetUrl(cacheKey);
                bool output = false;

                // cached result
                if (!string.IsNullOrEmpty(cached))
                {
                    output = MainUtil.GetBool(cached, true);
                }
                else
                {
                    string aData = media.InnerItem["__Tracking"];

                    if (string.IsNullOrEmpty(aData))
                    {
                        output = false;
                    }
                    else
                    {
                        XElement el = XElement.Parse(aData);
                        var ignore = el.Attribute("ignore");

                        if (ignore != null && ignore.Value == "1")
                        {
                            output = false;
                        }
                        else
                        {
                            // if the tracking element has any events, campaigns or profiles.
                            output = el.Elements("event").Any() || el.Elements("campaign").Any() || el.Elements("profile").Any();
                        }
                    }
                    _cache.SetUrl(cacheKey, output.ToString());
                }
                return output;
            }
            catch (Exception ex)
            {
                Log.Error("IsMediaAnalyticsTracked", ex, this);
                return false;
            }
        }
    }
}
