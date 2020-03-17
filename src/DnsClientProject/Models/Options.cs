using Ookii.CommandLine;

namespace DnsClientProject.Models
{
    public class Options
    {
        [CommandLineArgument("get")]
        public bool Get { get; set; }

        [CommandLineArgument("set")]
        public bool Set { get; set; }

        [CommandLineArgument("delete")]
        public bool Delete { get; set; }

        [CommandLineArgument("provider", IsRequired = true), Alias("p")]
        public string Provider { get; set; }

        [CommandLineArgument("domain"), Alias("d")]
        public string Domain { get; set; }

        [CommandLineArgument("recordtype"), Alias("r")]
        public string RecordType { get; set; }

        [CommandLineArgument("name"), Alias("n")]
        public string Name { get; set; }

        [CommandLineArgument("value"), Alias("v")]
        public string Value { get; set; }

        [CommandLineArgument("ttl"), Alias("t")]
        public int? Ttl { get; set; }

        [CommandLineArgument("priority")]
        public int? Priority { get; set; }

        [CommandLineArgument("weight")]
        public int? Weight { get; set; }

        [CommandLineArgument("port")]
        public int? Port { get; set; }

        [CommandLineArgument("format"), Alias("f")]
        public string Format { get; set; }

        [CommandLineArgument("sandbox"), Alias("s")]
        public bool Sandbox { get; set; }
    }
}
