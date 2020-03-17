using Microsoft.Azure.Management.Dns;
using Microsoft.Azure.Management.Dns.Models;
using Microsoft.Rest.Azure.Authentication;
using System.Threading.Tasks;

namespace DnsClientProject.Providers
{
    public class AzureDnsClientWrapper
    {
        private DnsManagementClient _dnsClient;

        public virtual async Task Initialize(string tenantId, string clientId, string clientSecret, string subscriptionId)
        {
            var serviceCreds = await ApplicationTokenProvider.LoginSilentAsync(tenantId, clientId, clientSecret);
            _dnsClient = new DnsManagementClient(serviceCreds);
            _dnsClient.SubscriptionId = subscriptionId;
        }

        public virtual RecordSet Get(string resourceGroup, string domain, string name, RecordType recordType)
        {
            var record = _dnsClient.RecordSets.Get(resourceGroup, domain, name, recordType);
            return record;
        }
        public virtual void CreateOrUpdate(string resourceGroup, string domain, string name, RecordType recordType, RecordSet record)
        {
            _dnsClient.RecordSets.CreateOrUpdate(resourceGroup, domain, name, recordType, record);
        }

        public virtual void Update(string resourceGroup, string domain, string name, RecordType recordType, RecordSet record)
        {
            _dnsClient.RecordSets.Update(resourceGroup, domain, name, recordType, record, record.Etag);
        }

        public virtual void Delete(string resourceGroup, string domain, string name, RecordType recordType)
        {
            _dnsClient.RecordSets.Delete(resourceGroup, domain, name, recordType);
        }
    }
}
