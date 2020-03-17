using NameCheap;
using System.Net;

namespace DnsClientProject.Providers
{
    public class NamecheapDnsClientWrapper
    {
        private const string GetIpUrl = "https://api.ipify.org";
        private NameCheapApi _ncApi;

        public virtual void Initialize(string envUsername, string envApiUser, string envApiKey, bool sandbox)
        {
            _ncApi = new NameCheapApi(envUsername, envApiUser, envApiKey, GetClientIP(), isSandbox: sandbox);
        }

        private string GetClientIP()
        {
            using (var client = new WebClient())
            {
                return client.DownloadString(GetIpUrl);
            }
        }

        public virtual DnsHostResult Get(string domain, string tld)
        {
            return _ncApi.Dns.GetHosts(domain, tld);
        }
        public virtual void Set(string domain, string tld, HostEntry[] hostEntries)
        {
            _ncApi.Dns.SetHosts(domain, tld, hostEntries);
        }
    }
}
