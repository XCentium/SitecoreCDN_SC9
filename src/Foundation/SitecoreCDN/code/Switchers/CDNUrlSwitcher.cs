using Sitecore.Common;

namespace SitecoreCDN.Switchers
{
    public class CDNUrlSwitcher : Switcher<CDNUrlState>
    {
        public CDNUrlSwitcher(CDNUrlState state) : base(state) { }
    }
}
