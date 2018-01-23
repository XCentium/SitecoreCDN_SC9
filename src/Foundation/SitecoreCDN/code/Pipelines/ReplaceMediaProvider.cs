using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sitecore.Diagnostics;
using Sitecore.Pipelines.HttpRequest;
using Sitecore.Resources.Media;
using SitecoreCDN.Providers;
using Sitecore.Pipelines;
using SitecoreCDN.Configuration;

namespace SitecoreCDN.Pipelines
{
    /// <summary>
    /// Injects the CDN replacement media provider into the MediaManager
    /// </summary>
    //public class ReplaceMediaProvider
    //{
    //    public void Process(PipelineArgs args)
    //    {
    //        Assert.ArgumentNotNull(args, "args");
    //        if (CDNSettings.Enabled)
    //            MediaManager.Provider = new CDNMediaProvider();           
    //    }
    //}
}
