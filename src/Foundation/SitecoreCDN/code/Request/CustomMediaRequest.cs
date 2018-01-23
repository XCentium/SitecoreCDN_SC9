using Sitecore.Resources.Media;

namespace SitecoreCDN.Request
{
    public class CustomMediaRequest : MediaRequest
  {
    public string GetCustomMediaPath(string localPath)
    {
      return GetMediaPath(localPath);
    }
  }
}
