using Sitecore.Configuration;
using Sitecore.Diagnostics;
using Sitecore.Pipelines.PreprocessRequest;
using Sitecore.Text;
using SitecoreCDN.Configuration;

namespace SitecoreCDN.Pipelines
{
    public class CDNInterceptPipeline : PreprocessRequestProcessor
    {
        /// <summary>
        /// rewrite CDN urls from  /path/to/file!cf!a=1!b=2.ext to original form /path/to/file.ext?a=1&b=2
        /// </summary>
        /// <param name="args"></param>
        public override void Process(PreprocessRequestArgs args)
        {
            Assert.ArgumentNotNull(args, "args");

            // rehydrate original url
            string fullPath = Sitecore.Context.RawUrl;
            UrlString url = new UrlString(fullPath);

            // if this item is a minifiable css or js
            // rewrite for ~/minify handler
            if (CDNSettings.Enabled &&
                CDNSettings.MinifyEnabled &&
                url["min"] == "1" &&
                !url.Path.StartsWith(Settings.Media.DefaultMediaPrefix) &&
                (url.Path.EndsWith(".css") || url.Path.EndsWith(".js")))
            {
                args.HttpContext.Items["MinifyPath"] = fullPath;   // set this for the Minifier handler
                args.HttpContext.RewritePath("/~/minify" + url.Path, "", url.Query);  // rewrite with ~/minify to trigger custom handler
            }
          //  else
          //  {
          //      args.Context.RewritePath(url.Path, "", url.Query); // rewrite proper url
          //  }
        }
    }
}
