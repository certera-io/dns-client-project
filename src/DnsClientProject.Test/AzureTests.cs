using DnsClientProject.Providers;
using Microsoft.Azure.Management.Dns.Models;
using Moq;
using NUnit.Framework;
using System;
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

        protected override IProvider SetupProvider()
        {
            var mockDnsWrapper = new Mock<AzureDnsClientWrapper>();
            mockDnsWrapper.Setup(x => x.Initialize(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Verifiable();
            mockDnsWrapper.Setup(x => x.Get(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RecordType>()))
                .Returns((string rg, string d, string n, RecordType rt) =>
                {
                    switch (n)
                    {
                        case "www":
                            return new RecordSet
                            {
                                TTL = 3600,
                                CnameRecord = new CnameRecord { Cname = "some.other.domain" }
                            };
                        case "ttl-rec":
                            return new RecordSet
                            {
                                TTL = 300,
                                CnameRecord = new CnameRecord { Cname = "my.rec" }
                            };
                        default:
                            return null;
                    }
                });
            mockDnsWrapper.Setup(x => x.CreateOrUpdate(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RecordType>(), It.IsAny<RecordSet>()))
                .Verifiable();
            mockDnsWrapper.Setup(x => x.Delete(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RecordType>()))
                .Verifiable();

            var mockProvider = new Mock<Azure>();
            mockProvider.Setup(x => x.InitializeDnsClient(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
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
    }
}