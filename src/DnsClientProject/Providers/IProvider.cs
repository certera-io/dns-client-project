using DnsClientProject.Models;
using System.Threading.Tasks;

namespace DnsClientProject.Providers
{
    public interface IProvider
    {
        Task Initialize(Options opts);
        Task<DnsRecord> Get(Options opts);
        Task<SetResult> Set(Options opts);
        Task<DeleteOperation> Delete(Options opts);
    }
}
