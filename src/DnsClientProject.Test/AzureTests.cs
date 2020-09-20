using DnsClientProject.Models;
using DnsClientProject.Providers;
using Microsoft.Azure.Management.Dns.Models;
using Moq;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DnsClientProject.Test
{
    public class AzureTests : ProviderTests
    {
        private const string ENV_SUB_ID = "abc-123";
        private const string ENV_TENANT_ID = "12345";
        private const string ENV_RESOURCE_GROUP = "rg1";
        private const string ENV_CLIENT_ID = "67890";
        private const string ENV_CLIENT_SECRET = "$ecret";

        private readonly Dictionary<string, RecordSet> _recordSets = new Dictionary<string, RecordSet>
            {
                { "www", new RecordSet
                            {
                                TTL = 3600,
                                CnameRecord = new CnameRecord { Cname = "some.other.domain" }
}
                },
                { "ttl-rec", new RecordSet
                            {
                                TTL = 300,
                                CnameRecord = new CnameRecord { Cname = "my.rec" }
                            }
                },
                { "delete1", new RecordSet
                            {
                                TTL = 300,
                                TxtRecords = new List<TxtRecord> { new TxtRecord { Value = new List<string> { "a", "b" } } }
                            }
                },
                { "delete2", new RecordSet
                            {
                                TTL = 300,
                                TxtRecords = new List<TxtRecord> { new TxtRecord { Value = new List<string> { "a", "b" } } }
                            }
                },
                { "delete3", new RecordSet
                            {
                                TTL = 300,
                                TxtRecords = new List<TxtRecord> { new TxtRecord { Value = new List<string> { "a" } }, new TxtRecord { Value = new List<string> { "b" } } }
                            }
                },
                { "delete4", new RecordSet
                            {
                                TTL = 300,
                                TxtRecords = new List<TxtRecord> { new TxtRecord { Value = new List<string> { "a" } }, new TxtRecord { Value = new List<string> { "b" } } }
                            }
                }
            };

        protected override IProvider SetupProvider()
        {
            var mockDnsWrapper = new Mock<AzureDnsClientWrapper>();
            mockDnsWrapper.Setup(x => x.Initialize(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Verifiable();
            mockDnsWrapper.Setup(x => x.Get(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RecordType>()))
                .Returns((string rg, string d, string n, RecordType rt) =>
                {
                    _recordSets.TryGetValue(n, out var rs);
                    return rs;
                });
            mockDnsWrapper.Setup(x => x.CreateOrUpdate(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RecordType>(), It.IsAny<RecordSet>()))
                .Verifiable();
            mockDnsWrapper.Setup(x => x.Delete(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RecordType>()))
                .Verifiable();

            var mockProvider = new Mock<Azure>();
            mockProvider.Setup(x => x.InitializeDnsClient(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.FromResult(mockDnsWrapper.Object));

            return mockProvider.Object;
        }

        protected override void SetEnvironmentVariables()
        {
            Environment.SetEnvironmentVariable("AZURE_SUBSCRIPTION_ID", ENV_SUB_ID);
            Environment.SetEnvironmentVariable("AZURE_RESOURCE_GROUP", ENV_RESOURCE_GROUP);
            Environment.SetEnvironmentVariable("AZURE_TENANT_ID", ENV_TENANT_ID);
            Environment.SetEnvironmentVariable("AZURE_CLIENT_ID", ENV_CLIENT_ID);
            Environment.SetEnvironmentVariable("AZURE_CLIENT_SECRET", ENV_CLIENT_SECRET);
        }

        protected override void RemoveEnvironmentVariables()
        {
            Environment.SetEnvironmentVariable("AZURE_SUBSCRIPTION_ID", null);
            Environment.SetEnvironmentVariable("AZURE_RESOURCE_GROUP", null);
            Environment.SetEnvironmentVariable("AZURE_TENANT_ID", null);
            Environment.SetEnvironmentVariable("AZURE_CLIENT_ID", null);
            Environment.SetEnvironmentVariable("AZURE_CLIENT_SECRET", null);
        }

        [Test]
        public override void ReadsEnvironmentVariables()
        {
            Assert.AreEqual(_provider.GetFieldValue<string>("_envSubscriptionId"), ENV_SUB_ID);
            Assert.AreEqual(_provider.GetFieldValue<string>("_envTenantId"), ENV_TENANT_ID);
            Assert.AreEqual(_provider.GetFieldValue<string>("_envResourceGroup"), ENV_RESOURCE_GROUP);
            Assert.AreEqual(_provider.GetFieldValue<string>("_envClientId"), ENV_CLIENT_ID);
            Assert.AreEqual(_provider.GetFieldValue<string>("_envClientSecret"), ENV_CLIENT_SECRET);

            Assert.Pass();
        }

        [Test]
        public async Task DeletesTxtRecord()
        {
            var opts = new Options
            {
                Name = "delete1",
                Value = "a",
                RecordType = "TXT"
            };
            var result = await _provider.Delete(opts);
            Assert.AreEqual(DeleteOperation.Deleted, result);

            var rs = _recordSets["delete1"];
            Assert.IsTrue(rs.TxtRecords[0].Value.Count == 1);
            Assert.IsTrue(rs.TxtRecords[0].Value[0] == "b");
        }

        [Test]
        public async Task DeletesAllTxtRecords()
        {
            var opts = new Options
            {
                Name = "delete2",
                Value = "a",
                RecordType = "TXT"
            };
            var result = await _provider.Delete(opts);
            Assert.AreEqual(DeleteOperation.Deleted, result);

            var rs = _recordSets["delete2"];

            Assert.IsTrue(rs.TxtRecords[0].Value.Count == 1);

            opts = new Options
            {
                Name = "delete2",
                Value = "b",
                RecordType = "TXT"
            };
            result = await _provider.Delete(opts);
            Assert.AreEqual(DeleteOperation.Deleted, result);

            Assert.IsTrue(rs.TxtRecords.Count == 0);
        }

        [Test]
        public async Task DeletesPartialTxtRecord()
        {
            var opts = new Options
            {
                Name = "delete3",
                Value = "a",
                RecordType = "TXT"
            };
            var result = await _provider.Delete(opts);
            Assert.AreEqual(DeleteOperation.Deleted, result);

            var rs = _recordSets["delete3"];

            opts = new Options
            {
                Name = "delete3",
                Value = "b",
                RecordType = "TXT"
            };
            result = await _provider.Delete(opts);
            Assert.AreEqual(DeleteOperation.Deleted, result);

            Assert.IsTrue(rs.TxtRecords.Count == 0);
        }

        [Test]
        public async Task DeletesLeavesOtherTxtRecord()
        {
            var opts = new Options
            {
                Name = "delete4",
                Value = "a",
                RecordType = "TXT"
            };
            var result = await _provider.Delete(opts);
            Assert.AreEqual(DeleteOperation.Deleted, result);

            var rs = _recordSets["delete4"];

            Assert.IsTrue(rs.TxtRecords.Count == 1);
            Assert.IsTrue(rs.TxtRecords[0].Value.Count == 1);
            Assert.IsTrue(rs.TxtRecords[0].Value[0] == "b");
        }

        [Test]
        public async Task DeletesFullTxtRecord()
        {
            var opts = new Options
            {
                Name = "delete1",
                RecordType = "TXT"
            };
            var result = await _provider.Delete(opts);
            Assert.AreEqual(DeleteOperation.Deleted, result);
        }

        [Test]
        public async Task DoesNotDeleteRecord()
        {
            var opts = new Options
            {
                Name = "unknown",
                Value = "a",
                RecordType = "TXT"
            };
            var result = await _provider.Delete(opts);
            Assert.AreEqual(DeleteOperation.Noop, result);
        }
    }
}