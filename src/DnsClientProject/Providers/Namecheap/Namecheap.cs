using DnsClientProject.Models;
using Nager.PublicSuffix;
using NameCheap;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DnsClientProject.Providers
{
    public class Namecheap : IProvider
    {
        private const string EnvUsername = "NAMECHEAP_USER_NAME";
        private const string EnvApiUser = "NAMECHEAP_API_USER";
        private const string EnvApiKey = "NAMECHEAP_API_KEY";

        private NamecheapDnsClientWrapper _dnsClient;
        private DomainParser _domainParser;
        private int _ttlToUse;

        public Task Initialize(Options opts)
        {
            var envApiUser = ConfigManager.GetEnvVarRequired(EnvApiUser);
            var envUsername = ConfigManager.GetEnvVarOrDefault(EnvUsername) ?? envApiUser;
            var envApiKey = ConfigManager.GetEnvVarRequired(EnvApiKey);

            _dnsClient = InitializeDnsClient(envUsername, envApiUser, envApiKey, opts.Sandbox);

            var cacheProvider = new FileCacheProvider(cacheTimeToLive: new TimeSpan(10, 0, 0));
            var webTldRuleProvider = new WebTldRuleProvider(cacheProvider: cacheProvider);

            _domainParser = new DomainParser(webTldRuleProvider);

            _ttlToUse = opts.Ttl ?? ConfigManager.DefaultTtlInSeconds;

            return Task.CompletedTask;
        }

        internal virtual NamecheapDnsClientWrapper InitializeDnsClient(string envUsername, string envApiUser, string envApiKey, bool sandbox)
        {
            var dnsClient = new NamecheapDnsClientWrapper();
            dnsClient.Initialize(envUsername, envApiUser, envApiKey, sandbox);

            return dnsClient;
        }

        public Task<DnsRecord> Get(Options opts)
        {
            var domainName = _domainParser.Get(opts.Domain);
            var hosts = _dnsClient.Get(domainName.Domain, domainName.TLD);

            var recordType = ConvertRecordType(opts.RecordType);
            var hostEntries = hosts?.HostEntries?.Where(x => x.HostName == opts.Name && x.RecordType == recordType).ToList();

            if (hostEntries == null || hostEntries.Count == 0)
            {
                throw new Exception($"{opts.RecordType} record {opts.Name} not found");
            }
            var record = FromHostEntries(opts, hostEntries.ToArray());

            return Task.FromResult(record);
        }

        private DnsRecord FromHostEntries(Options opts, params HostEntry[] hostEntries)
        {
            if (hostEntries.Length == 1)
            {
                return new DnsRecord
                {
                    Id = hostEntries[0].Id.ToString(),
                    Domain = opts.Domain,
                    Name = opts.Name,
                    Ttl = hostEntries[0].Ttl,
                    RecordType = opts.RecordType,
                    Values = new List<string> { hostEntries[0].Address }
                };
            }

            var record = new DnsRecord
            {
                Domain = opts.Domain,
                Name = opts.Name,
                RecordType = opts.RecordType,
                Values = hostEntries.Select(x => x.Address).ToList()
            };
            return record;
        }

        public Task<SetResult> Set(Options opts)
        {
            var domainName = _domainParser.Get(opts.Domain);
            var hosts = _dnsClient.Get(domainName.Domain, domainName.TLD);

            var recordType = ConvertRecordType(opts.RecordType);

            var record = hosts?.HostEntries?.FirstOrDefault(x =>
                x.HostName == opts.Name &&
                x.RecordType == recordType &&
                (opts.Priority.HasValue ? x.MxPref == opts.Priority.ToString() : true));

            if (record == null)
            {
                // Create new
                var hostEntry = new HostEntry
                {
                    Address = opts.Value,
                    HostName = opts.Name,
                    RecordType = recordType,
                    Ttl = _ttlToUse.ToString(),
                    MxPref = opts.Priority?.ToString()
                };

                // All host records that are not included into the API call will be deleted, so add them in addition to new host records.
                var allNewEntries = new List<HostEntry>();
                if (hosts?.HostEntries != null)
                {
                    allNewEntries.AddRange(hosts.HostEntries);
                }
                allNewEntries.Add(hostEntry);

                _dnsClient.Set(domainName.Domain, domainName.TLD, allNewEntries.ToArray());

                var result = FromHostEntries(opts, hostEntry);

                return Task.FromResult(new SetResult
                {
                    DnsRecord = result,
                    SetOperation = SetOperation.Created
                });
            }
            else
            {
                // Update?
                bool dirty = false;

                if (record.Ttl == null)
                {
                    record.Ttl = opts.Ttl?.ToString() ?? ConfigManager.DefaultTtlInSeconds.ToString();
                    dirty = true;
                }
                else if (opts.Ttl != null && !string.Equals(record.Ttl, _ttlToUse.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    record.Ttl = opts.Ttl?.ToString();
                    dirty = true;
                }

                if (opts.Priority.HasValue && !string.Equals(record.MxPref, opts.Priority?.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    record.MxPref = opts.Priority?.ToString();
                    dirty = true;
                }

                if (!string.IsNullOrEmpty(opts.Value) && !string.Equals(record.Address, opts.Value, StringComparison.OrdinalIgnoreCase))
                {
                    record.Address = opts.Value;
                    dirty = true;
                }

                if (dirty)
                {
                    _dnsClient.Set(domainName.Domain, domainName.TLD, hosts.HostEntries);
                }

                var result = FromHostEntries(opts, record);

                return Task.FromResult(new SetResult
                {
                    DnsRecord = result,
                    SetOperation = dirty ? SetOperation.Updated : SetOperation.Noop
                });
            }
        }

        public Task<DeleteOperation> Delete(Options opts)
        {
            var domainName = _domainParser.Get(opts.Domain);
            var hosts = _dnsClient.Get(domainName.Domain, domainName.TLD);

            var recordType = ConvertRecordType(opts.RecordType);

            var record = hosts?.HostEntries?.FirstOrDefault(x =>
                x.HostName == opts.Name &&
                x.RecordType == recordType &&
                (opts.Value != null ? string.Equals(x.Address, opts.Value, StringComparison.OrdinalIgnoreCase) : true) &&
                (opts.Priority.HasValue ? x.MxPref == opts.Priority.ToString() : true));

            if (record == null)
            {
                return Task.FromResult(DeleteOperation.Noop);
            }
            else
            {
                var updatedEntries = hosts.HostEntries.Where(x => x.Id != record.Id).ToArray();

                _dnsClient.Set(domainName.Domain, domainName.TLD, updatedEntries);

                return Task.FromResult(DeleteOperation.Deleted);
            }
        }

        private RecordType ConvertRecordType(string recordType)
        {
            return Enum.Parse<RecordType>(recordType, true);
        }
    }
}
