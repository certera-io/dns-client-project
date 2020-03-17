using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DnsClientProject.Models
{
    public class DnsRecord
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("domain")]
        public string Domain { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("recordType")]
        public string RecordType { get; set; }

        [JsonPropertyName("ttl")]
        public string Ttl { get; set; }

        [JsonPropertyName("values")]
        public IList<string> Values { get; set; }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"ID: {Id}");
            sb.AppendLine($"Domain: {Domain}");
            sb.AppendLine($"Name: {Name}");
            sb.AppendLine($"RecordType: {RecordType}");
            sb.AppendLine($"TTL: {Ttl}");
            sb.Append("Values: ");

            if (Values?.Count > 0)
            {
                if (Values.Count == 1)
                {
                    sb.Append(Values[0]);
                }
                else
                {
                    sb.AppendLine();
                    foreach (var v in Values)
                    {
                        sb.AppendLine(v);
                    }
                }
            }

            return sb.ToString();
        }

        public string ToJson()
        {
            var opts = new JsonSerializerOptions
            {
                WriteIndented = true
            };
            return JsonSerializer.Serialize<DnsRecord>(this, opts);
        }
    }
}
