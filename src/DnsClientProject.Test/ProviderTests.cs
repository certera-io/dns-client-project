using DnsClientProject.Models;
using DnsClientProject.Providers;
using NUnit.Framework;
using System;
using System.Threading.Tasks;

namespace DnsClientProject.Test
{
    public abstract class ProviderTests
    {
        protected IProvider _provider;

        [SetUp]
        public async Task Setup()
        {
            SetEnvironmentVariables();

            _provider = SetupProvider();

            var opts = new Options();
            await _provider.Initialize(opts);
        }

        protected abstract IProvider SetupProvider();

        protected abstract void SetEnvironmentVariables();

        protected abstract void RemoveEnvironmentVariables();

        [Test]
        public void ThrowsExceptionMissingEnvVar()
        {
            RemoveEnvironmentVariables();

            var opts = new Options();

            var providerType = _provider.GetType();
            var newInstance = (IProvider)Activator.CreateInstance(providerType);
            Assert.ThrowsAsync<ArgumentException>(async () => await newInstance.Initialize(opts));

            Assert.Pass();

            SetEnvironmentVariables();
        }

        [Test]
        public abstract void ReadsEnvironmentVariables();

        [Test]
        public void ThrowsExceptionInvalidRecordType()
        {
            var opts = new Options
            {
                RecordType = "invalid",
                Domain = "example.com"
            };
            Assert.ThrowsAsync<ArgumentException>(async () => await _provider.Get(opts));

            Assert.Pass();
        }

        [Test]
        public async Task GetRecord()
        {
            var opts = new Options
            {
                RecordType = "cname", // Lowercase to parse insensitive
                Name = "www",
                Domain = "example.com"
            };

            var result = await _provider.Get(opts);

            Assert.IsTrue(result.Values.Count == 1);
            Assert.IsTrue(result.Values[0] == "some.other.domain");

            Assert.Pass();
        }

        [Test]
        public async Task CreatesNewRecord()
        {
            var opts = new Options
            {
                RecordType = "cname",
                Name = "subdomain",
                Value = "sub.site.com",
                Domain = "example.com"
            };

            var result = await _provider.Set(opts);

            Assert.AreEqual(SetOperation.Created, result.SetOperation);
            Assert.IsTrue(result.DnsRecord.Name == "subdomain");
            Assert.IsTrue(result.DnsRecord.Values.Count == 1);
            Assert.IsTrue(result.DnsRecord.Values[0] == "sub.site.com");

            Assert.Pass();
        }

        [Test]
        public async Task UpdatesExistingRecord()
        {
            var opts = new Options
            {
                RecordType = "cname",
                Name = "www",
                Value = "sub.site.com",
                Domain = "example.com"
            };

            var result = await _provider.Set(opts);

            Assert.AreEqual(SetOperation.Updated, result.SetOperation);
            Assert.IsTrue(result.DnsRecord.Name == "www");
            Assert.IsTrue(result.DnsRecord.Values.Count == 1);
            Assert.IsTrue(result.DnsRecord.Values[0] == "sub.site.com");

            Assert.Pass();
        }

        [Test]
        public async Task UpdatesTtl()
        {
            var opts = new Options
            {
                RecordType = "cname",
                Name = "ttl-rec",
                Value = "sub.site.com",
                Domain = "example.com",
                Ttl = 600
            };

            var result = await _provider.Set(opts);

            Assert.AreEqual(SetOperation.Updated, result.SetOperation);
            Assert.IsTrue(result.DnsRecord.Name == "ttl-rec");
            Assert.IsTrue(result.DnsRecord.Values.Count == 1);
            Assert.IsTrue(result.DnsRecord.Values[0] == "sub.site.com");
            Assert.IsTrue(result.DnsRecord.Ttl == "600");

            Assert.Pass();
        }

        [Test]
        public async Task DoesNotUpdateTtl()
        {
            var opts = new Options
            {
                RecordType = "cname",
                Name = "ttl-rec",
                Value = "my.rec",
                Domain = "example.com"
            };

            var result = await _provider.Set(opts);

            Assert.AreEqual(SetOperation.Noop, result.SetOperation);
            Assert.IsTrue(result.DnsRecord.Name == "ttl-rec");
            Assert.IsTrue(result.DnsRecord.Values.Count == 1);
            Assert.IsTrue(result.DnsRecord.Values[0] == "my.rec");
            Assert.IsTrue(result.DnsRecord.Ttl == "300"); // Was not set to 3600 (global default)

            Assert.Pass();
        }

        [Test]
        public async Task NoOpOnUpdatingUnchangedRecord()
        {
            var opts = new Options
            {
                RecordType = "cname",
                Name = "www",
                Value = "some.other.domain",
                Domain = "example.com"
            };

            var result = await _provider.Set(opts);

            Assert.AreEqual(SetOperation.Noop, result.SetOperation);
            Assert.IsTrue(result.DnsRecord.Name == "www");
            Assert.IsTrue(result.DnsRecord.Values.Count == 1);
            Assert.IsTrue(result.DnsRecord.Values[0] == "some.other.domain");

            Assert.Pass();
        }

        [Test]
        public async Task NoOpOnDeletingMissingRecord()
        {
            var opts = new Options
            {
                RecordType = "cname",
                Name = "unknown",
                Domain = "unknown.com"
            };

            var result = await _provider.Delete(opts);

            Assert.AreEqual(DeleteOperation.Noop, result);

            Assert.Pass();
        }

        [Test]
        public async Task DeletsOnExistingRecord()
        {
            var opts = new Options
            {
                RecordType = "cname",
                Name = "www",
                Domain = "example.com"
            };

            var result = await _provider.Delete(opts);

            Assert.AreEqual(DeleteOperation.Deleted, result);

            Assert.Pass();
        }
    }
}