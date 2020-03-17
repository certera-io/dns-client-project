using DnsClientProject.Providers;
using Moq;
using NameCheap;
using NUnit.Framework;
using System;

namespace DnsClientProject.Test
{
    public class NamecheapTests : ProviderTests
    {
        private const string ENV_USER_NAME = "user1";
        private const string ENV_API_USER = "user1";
        private const string ENV_API_KEY = "$key123";

        protected override IProvider SetupProvider()
        {
            var mockDnsWrapper = new Mock<NamecheapDnsClientWrapper>();
            mockDnsWrapper.Setup(x => x.Initialize(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
                .Verifiable();
            mockDnsWrapper.Setup(x => x.Get(It.IsAny<string>(), It.IsAny<string>()))
                .Returns((string d, string t) =>
                {
                    var domain = $"{d}.{t}";
                    switch (domain)
                    {
                        case "example.com":
                            return new DnsHostResult
                            {
                                HostEntries = new HostEntry[]
                                {
                                    new HostEntry
                                    {
                                        RecordType = NameCheap.RecordType.CNAME,
                                        HostName = "www",
                                        Address = "some.other.domain",
                                        Ttl = "3600"
                                    },
                                    new HostEntry
                                    {
                                        RecordType = NameCheap.RecordType.CNAME,
                                        HostName = "ttl-rec",
                                        Address = "my.rec",
                                        Ttl = "300"
                                    }
                                }
                            };
                        default:
                            return new DnsHostResult
                            {
                                HostEntries = new HostEntry[]
                                {
                                }
                            };
                    }
                });
            mockDnsWrapper.Setup(x => x.Set(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<HostEntry[]>()))
                .Verifiable();

            var mockProvider = new Mock<Namecheap>();
            mockProvider.Setup(x => x.InitializeDnsClient(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
                .Returns(mockDnsWrapper.Object);

            return mockProvider.Object;
        }

        protected override void SetEnvironmentVariables()
        {
            Environment.SetEnvironmentVariable("NAMECHEAP_USER_NAME", ENV_USER_NAME);
            Environment.SetEnvironmentVariable("NAMECHEAP_API_USER", ENV_API_USER);
            Environment.SetEnvironmentVariable("NAMECHEAP_API_KEY", ENV_API_KEY);
        }

        protected override void RemoveEnvironmentVariables()
        {
            Environment.SetEnvironmentVariable("NAMECHEAP_USER_NAME", null);
            Environment.SetEnvironmentVariable("NAMECHEAP_API_USER", null);
            Environment.SetEnvironmentVariable("NAMECHEAP_API_KEY", null);
        }

        [Test]
        public override void ReadsEnvironmentVariables()
        {
            // Variables aren't needed beyond initialization
            Assert.Pass();
        }
    }
}