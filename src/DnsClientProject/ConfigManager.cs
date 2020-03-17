using System;

namespace DnsClientProject
{
    public static class ConfigManager
    {
        public const int DefaultTtlInSeconds = 3600;

        public static string GetEnvVarRequired(string envVariable)
        {
            return Environment.GetEnvironmentVariable(envVariable) ??
                throw new ArgumentException("Missing required environment variable", envVariable);
        }

        public static string GetEnvVarOrDefault(string envVariable, string @default = null)
        {
            return Environment.GetEnvironmentVariable(envVariable) ?? @default;
        }
    }
}
