using DnsClientProject.Models;
using Microsoft.Azure.Management.Dns.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DnsClientProject.Providers
{
    public class Azure : IProvider
    {
        private const string EnvSubscriptionId = "AZURE_SUBSCRIPTION_ID";
        private const string EnvResourceGroup = "AZURE_RESOURCE_GROUP";
        private const string EnvTenantId = "AZURE_TENANT_ID";
        private const string EnvClientId = "AZURE_CLIENT_ID";
        private const string EnvClientSecret = "AZURE_CLIENT_SECRET";
        private const string EnvCloud = "AZURE_CLOUD";

        private string _envSubscriptionId;
        private string _envResourceGroup;
        private string _envTenantId;
        private string _envClientId;
        private string _envClientSecret;
        private string _envCloud;

        private AzureDnsClientWrapper _dnsClient;

        public async Task Initialize(Options opts)
        {
            _envSubscriptionId = ConfigManager.GetEnvVarRequired(EnvSubscriptionId);
            _envResourceGroup = ConfigManager.GetEnvVarRequired(EnvResourceGroup);
            _envTenantId = ConfigManager.GetEnvVarRequired(EnvTenantId);
            _envClientId = ConfigManager.GetEnvVarRequired(EnvClientId);
            _envClientSecret = ConfigManager.GetEnvVarRequired(EnvClientSecret);
            _envCloud = ConfigManager.GetEnvVarOrDefault(EnvCloud, "AzureCloud");

            _dnsClient = await InitializeDnsClient(_envTenantId, _envClientId, _envClientSecret, _envSubscriptionId, _envCloud);
        }

        internal virtual async Task<AzureDnsClientWrapper> InitializeDnsClient(string tenantId, string clientId, string clientSecret, string subscriptionId, string cloud)
        {
            var dnsClient = new AzureDnsClientWrapper();
            await dnsClient.Initialize(tenantId, clientId, clientSecret, subscriptionId, cloud);

            return dnsClient;
        }

        public Task<DnsRecord> Get(Options opts)
        {
            var recordType = ConvertRecordType(opts.RecordType);
            RecordSet record = null;
            try
            {
                record = _dnsClient.Get(_envResourceGroup, opts.Domain, opts.Name, recordType);
            }
            catch { }

            if (record == null)
            {
                throw new Exception($"Record {opts.Name} not found");
            }

            DnsRecord result = FromRecordSet(opts, recordType, record);

            return Task.FromResult(result);
        }

        private static DnsRecord FromRecordSet(Options opts, RecordType recordType, RecordSet record)
        {
            var result = new DnsRecord
            {
                Id = record.Id,
                Domain = opts.Domain,
                Name = opts.Name,
                Ttl = record.TTL?.ToString(),
                RecordType = opts.RecordType
            };

            switch (recordType)
            {
                case RecordType.A:
                    result.Values = record.ARecords?.Select(x => x.Ipv4Address).ToList();
                    break;
                case RecordType.AAAA:
                    result.Values = record.AaaaRecords?.Select(x => x.Ipv6Address).ToList();
                    break;
                case RecordType.CAA:
                    result.Values = record.CaaRecords?.Select(x => $"{x.Flags} {x.Tag} {x.Value}").ToList();
                    break;
                case RecordType.CNAME:
                    result.Values = new List<string> { record.CnameRecord?.Cname };
                    break;
                case RecordType.MX:
                    result.Values = record.MxRecords?.Select(x => $"{x.Preference} {x.Exchange}").ToList();
                    break;
                case RecordType.NS:
                    result.Values = record.NsRecords?.Select(x => x.Nsdname).ToList();
                    break;
                case RecordType.PTR:
                    result.Values = record.PtrRecords?.Select(x => x.Ptrdname).ToList();
                    break;
                case RecordType.SOA:
                    result.Values = new List<string> { $"{record.SoaRecord?.SerialNumber} {record.SoaRecord?.SerialNumber}" };
                    break;
                case RecordType.SRV:
                    result.Values = record.SrvRecords?.Select(x => $"{x.Priority} {x.Weight} {x.Port} {x.Target}").ToList();
                    break;
                case RecordType.TXT:
                    result.Values = record.TxtRecords?.SelectMany(x => x.Value).ToList();
                    break;
                default:
                    break;
            }

            return result;
        }

        public Task<SetResult> Set(Options opts)
        {
            var recordType = ConvertRecordType(opts.RecordType);
            RecordSet record = null;
            try
            {
                record = _dnsClient.Get(_envResourceGroup, opts.Domain, opts.Name, recordType);
            }
            catch { }

            if (record == null)
            {
                record = new RecordSet
                {
                    TTL = opts.Ttl ?? ConfigManager.DefaultTtlInSeconds
                };

                // Create
                switch (recordType)
                {
                    case RecordType.A:
                        record.ARecords = new List<ARecord> { new ARecord { Ipv4Address = opts.Value } };
                        break;
                    case RecordType.AAAA:
                        record.AaaaRecords = new List<AaaaRecord> { new AaaaRecord { Ipv6Address = opts.Value } };
                        break;
                    case RecordType.CAA:
                        throw new NotImplementedException("CAA records are not implemented");
                    case RecordType.CNAME:
                        record.CnameRecord = new CnameRecord { Cname = opts.Value };
                        break;
                    case RecordType.MX:
                        record.MxRecords = new List<MxRecord> { new MxRecord { Preference = opts.Priority, Exchange = opts.Value } };
                        break;
                    case RecordType.NS:
                        record.NsRecords = new List<NsRecord> { new NsRecord { Nsdname = opts.Value } };
                        break;
                    case RecordType.PTR:
                        record.PtrRecords = new List<PtrRecord> { new PtrRecord { Ptrdname = opts.Value } };
                        break;
                    case RecordType.SOA:
                        throw new NotImplementedException("SOA records are not implemented");
                    case RecordType.SRV:
                        record.SrvRecords = new List<SrvRecord>
                        {
                            new SrvRecord
                            {
                                Port = opts.Port,
                                Priority = opts.Priority,
                                Target = opts.Value,
                                Weight = opts.Weight
                            }
                        };

                        break;
                    case RecordType.TXT:
                        record.TxtRecords = new List<TxtRecord> { new TxtRecord { Value = new List<string> { opts.Value } } };
                        break;
                }

                _dnsClient.CreateOrUpdate(_envResourceGroup, opts.Domain, opts.Name, recordType, record);

                var result = FromRecordSet(opts, recordType, record);

                return Task.FromResult(new SetResult
                {
                    DnsRecord = result,
                    SetOperation = SetOperation.Created
                }); 
            }
            else
            {
                // Update
                bool dirty = false;

                // Update the TTL if it's null/not set or if a TTL was specified and it differs from what was specified.
                // In other words, don't blindly always overwrite the TTL with the global default to always
                // update records needlessly. e.g. existing record's only difference is TTL value of 5 minutes to global
                // value of 1 hour, keep it as 5 minutes and don't update unless the user specified a value via commandline.
                if (record.TTL == null)
                {
                    record.TTL = opts.Ttl ?? ConfigManager.DefaultTtlInSeconds;
                    dirty = true;
                }
                else if (record.TTL != opts.Ttl && opts.Ttl != null)
                {
                    record.TTL = opts.Ttl;
                    dirty = true;
                }

                switch (recordType)
                {
                    case RecordType.A:
                        if (!record.ARecords.Any(x => x.Ipv4Address == opts.Value))
                        {
                            record.ARecords.Add(new ARecord { Ipv4Address = opts.Value });
                            dirty = true;
                        }
                        break;
                    case RecordType.AAAA:
                        if (!record.AaaaRecords.Any(x => x.Ipv6Address == opts.Value))
                        {
                            record.AaaaRecords.Add(new AaaaRecord { Ipv6Address = opts.Value });
                            dirty = true;
                        }
                        break;
                    case RecordType.CAA:
                        throw new NotImplementedException("CAA records are not implemented");
                    case RecordType.CNAME:
                        if (!string.Equals(record.CnameRecord.Cname, opts.Value))
                        {
                            record.CnameRecord.Cname = opts.Value;
                            dirty = true;
                        }
                        break;
                    case RecordType.MX:
                        if (!record.MxRecords.Any(x => x.Preference == opts.Priority && x.Exchange == opts.Value))
                        {
                            record.MxRecords.Add(new MxRecord { Preference = opts.Priority, Exchange = opts.Value });
                            dirty = true;
                        }
                        break;
                    case RecordType.NS:
                        if (!record.NsRecords.Any(x => x.Nsdname == opts.Value))
                        {
                            record.NsRecords.Add(new NsRecord { Nsdname = opts.Value });
                            dirty = true;
                        }
                        break;
                    case RecordType.PTR:
                        if (!record.PtrRecords.Any(x => x.Ptrdname == opts.Value))
                        {
                            record.PtrRecords.Add(new PtrRecord { Ptrdname = opts.Value });
                            dirty = true;
                        }
                        break;
                    case RecordType.SOA:
                        throw new NotImplementedException("SOA records are not implemented");
                    case RecordType.SRV:
                        throw new NotImplementedException("SRV records are not implemented");
                    case RecordType.TXT:
                        if (!record.TxtRecords.SelectMany(x => x.Value).Any(x => x == opts.Value))
                        {
                            record.TxtRecords.First().Value.Add(opts.Value);
                            dirty = true;
                        }
                        break;
                }

                if (dirty)
                {
                    _dnsClient.Update(_envResourceGroup, opts.Domain, opts.Name, recordType, record);
                }

                var result = FromRecordSet(opts, recordType, record);

                return Task.FromResult(new SetResult
                {
                    DnsRecord = result,
                    SetOperation = dirty ? SetOperation.Updated : SetOperation.Noop
                });
            }
        }

        public Task<DeleteOperation> Delete(Options opts)
        {
            // A value was specified, so attempt to delete just the value within the record set
            bool delete = false;
            var recordType = ConvertRecordType(opts.RecordType);

            RecordSet record = null;
            try
            {
                record = _dnsClient.Get(_envResourceGroup, opts.Domain, opts.Name, recordType);
            }
            catch { }

            if (record == null)
            {
                return Task.FromResult(DeleteOperation.Noop);
            }

            if (opts.Value != null)
            {
                bool dirty = false;

                switch (recordType)
                {
                    case RecordType.A:
                        {
                            var obj = record.ARecords.FirstOrDefault(x => x.Ipv4Address == opts.Value);
                            if (obj != null)
                            {
                                record.ARecords.Remove(obj);
                                dirty = true;
                            }
                            break;
                        }
                    case RecordType.AAAA:
                        {
                            var obj = record.AaaaRecords.FirstOrDefault(x => x.Ipv6Address == opts.Value);
                            if (obj != null)
                            {
                                record.AaaaRecords.Remove(obj);
                                dirty = true;
                            }
                            break;
                        }
                    case RecordType.CAA:
                        throw new NotImplementedException("CAA records are not implemented");
                    case RecordType.CNAME:
                        delete = true;
                        break;
                    case RecordType.MX:
                        {
                            var obj = record.MxRecords.FirstOrDefault(x => 
                                (opts.Priority.HasValue ? x.Preference == opts.Priority : true)
                                && x.Exchange == opts.Value);
                            if (obj != null)
                            {
                                record.MxRecords.Remove(obj);
                                dirty = true;
                            }
                            break;
                        }
                    case RecordType.NS:
                        {
                            var obj = record.NsRecords.FirstOrDefault(x => x.Nsdname == opts.Value);
                            if (obj != null)
                            {
                                record.NsRecords.Remove(obj);
                                dirty = true;
                            }
                            break;
                        }
                    case RecordType.PTR:
                        {
                            var obj = record.PtrRecords.FirstOrDefault(x => x.Ptrdname == opts.Value);
                            if (obj != null)
                            {
                                record.PtrRecords.Remove(obj);
                                dirty = true;
                            }
                            break;
                        }
                    case RecordType.SOA:
                        throw new NotImplementedException("SOA records are not implemented");
                    case RecordType.SRV:
                        throw new NotImplementedException("SRV records are not implemented");
                    case RecordType.TXT:
                        {
                            var buckets = new List<Tuple<IList<string>, string>>();
                            foreach (var txtRec in record.TxtRecords)
                            {
                                foreach (var txtValue in txtRec.Value)
                                {
                                    if (string.Equals(txtValue, opts.Value))
                                    {
                                        buckets.Add(Tuple.Create(txtRec.Value, opts.Value));
                                    }
                                }
                            }

                            if (buckets.Count > 0)
                            {
                                foreach(var b in buckets)
                                {
                                    b.Item1.Remove(b.Item2);
                                }
                                dirty = true;
                            }

                            // Can't perform update that leaves an empty TXT record set. Simply delete it.
                            var emptyTxtRecords = new List<TxtRecord>();
                            foreach (var txtRec in record.TxtRecords)
                            {
                                if (txtRec.Value.Count == 0)
                                {
                                    emptyTxtRecords.Add(txtRec);
                                }
                            }
                            foreach (var emptyTxtRec in emptyTxtRecords)
                            {
                                record.TxtRecords.Remove(emptyTxtRec);
                            }

                            if (!record.TxtRecords.Any(x => x.Value.Count > 0))
                            {
                                _dnsClient.Delete(_envResourceGroup, opts.Domain, opts.Name, recordType);

                                return Task.FromResult(DeleteOperation.Deleted);
                            }

                            break;
                        }
                }

                if (dirty)
                {
                    _dnsClient.Update(_envResourceGroup, opts.Domain, opts.Name, recordType, record);

                    return Task.FromResult(DeleteOperation.Deleted);
                }
            }
            else
            {
                delete = true;
            }

            if (delete)
            {
                _dnsClient.Delete(_envResourceGroup, opts.Domain, opts.Name, recordType);

                return Task.FromResult(DeleteOperation.Deleted);
            }

            return Task.FromResult(DeleteOperation.Noop);
        }

        private RecordType ConvertRecordType(string recordType)
        {
            return Enum.Parse<RecordType>(recordType, true);
        }
    }
}