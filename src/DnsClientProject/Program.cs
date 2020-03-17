using DnsClientProject.Models;
using DnsClientProject.Providers;
using Ookii.CommandLine;
using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

[assembly: InternalsVisibleTo("DnsClientProject.Test")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]

namespace DnsClientProject
{
    static class Program
    {
        static async Task<int> Main(string[] args)
        {
            if (args == null || args.Length == 0 ||
                args.Any(x =>
                    x == "-?" ||
                    x == "?" ||
                    x == "-h" ||
                    x == "-help" ||
                    x == "--h" ||
                    x == "--help"))
            {
                ShowHelp();

                return 0;
            }

            Options options = null;

            // Parse the commandline arguments
            var parser = new CommandLineParser(typeof(Options), new[] { "--", "-" });
            try
            {
                options = (Options)parser.Parse(args);
            }
            catch (CommandLineArgumentException ex)
            {
                var sb = new StringBuilder();
                sb.AppendLine("Error parsing command line arguments");
                sb.AppendLine($"Detailed error message: {ex.Message}");

                Console.Error.WriteLine(sb.ToString());

                return 1;
            }

            // Validate that only one action was specified
            var actionCount =
                (options.Get    ? 1 : 0) +
                (options.Set    ? 1 : 0) +
                (options.Delete ? 1 : 0);

            if (actionCount == 0)
            {
                Console.Error.WriteLine("Error: must specify an action (--get or --set or --delete)");

                return 1;
            }

            if (actionCount > 1)
            {
                Console.Error.WriteLine("Error: must specify only one action (--get or --set or --delete)");

                return 1;
            }

            // Locate the provider
            var assembly = Assembly.GetEntryAssembly();

            var providerTypes = Assembly.GetExecutingAssembly().DefinedTypes
                        .Where(x => x.ImplementedInterfaces.Contains(typeof(IProvider)))
                        .ToDictionary(x => x.Name, x => x.AsType(), StringComparer.OrdinalIgnoreCase);

            if (!providerTypes.TryGetValue(options.Provider, out var providerType))
            {
                var sb = new StringBuilder();
                sb.AppendLine($"Error: Provider {options.Provider} not implemented. Available options are:");

                providerTypes.Keys.OrderBy(x => x).ToList().ForEach(x => sb.AppendLine($"  {x}"));

                Console.Error.WriteLine(sb.ToString());

                return 1;
            }

            // Instantiate the requested provider
            var provider = (IProvider)Activator.CreateInstance(providerType);

            // Initialize the provider. This gives a chance to load environment variables, etc.
            try
            {
                await provider.Initialize(options);
            }
            catch (Exception ex)
            {
                var sb = new StringBuilder();
                sb.AppendLine($"Error initializing provider {providerType.Name}");
                sb.AppendLine($"Detailed error message: {ex.Message}");

                Console.Error.WriteLine(sb.ToString());

                return 1;
            }

            // Execute the provider
            var exitCode = 0;
            try
            {
                if (options.Get)
                {
                    var result = await provider.Get(options);
                    if (string.Equals(options.Format, "json", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine(result.ToJson());
                    }
                    else
                    {
                        Console.WriteLine(result.ToString());
                    }
                }
                else if (options.Set)
                {
                    var result = await provider.Set(options);
                    Console.WriteLine(result.SetOperation);
                }
                else
                {
                    var result = await provider.Delete(options);
                    Console.WriteLine(result);
                }
            }
            catch (Exception ex)
            {
                var sb = new StringBuilder();
                sb.AppendLine($"Error executing {(options.Get ? "get" : (options.Set ? "set" : "delete"))} " +
                    $"action for provider {providerType.Name}");
                sb.AppendLine($"Detailed error message: {ex.Message}");

                Console.Error.WriteLine(sb.ToString());

                exitCode = 1;
            }

            if (Debugger.IsAttached)
            {
                Debugger.Break();
            }

            return exitCode;
        }

        static void ShowHelp()
        {
            Console.WriteLine(@$"
dnsc, a tool to manage DNS records at supported external DNS hosting providers. 

Usage:
    dnsc action options

Action:
    One action is required.

    --get                           Gets a DNS record.

    --set                           Creates or updates a DNS record.

    --delete                        Deletes a DNS record.


Options:
    
    -p, --provider <PROVIDER>       Required. The DNS hosting provider.
    
    -d, --domain <DOMAIN>           The domain name for the record.
                                    Some providers use this as context (aka ""zone"").

    -r, --recordtype <RECORDTYPE>   The DNS record type.
                                    Example: a or cname or mx, etc.

    -n, --name <NAME>               The record name.
                                    Example: mail or mail. or mail.domain.com depending on the provider

    -v, --value <VALUE>             The value for the record.
                                    Required for ""set"" action.
                                    Optional for ""get"" and ""delete"" actions.

    -t, --ttl <TTL>                 Time to live for record.
                                    Default is 1 hour if no value is specified.

    -f, --format <FORMAT>           Output format. Options are: text (default) or json.

    -s, --sandbox                   Sets the sandbox flag that the provider may use if supported.
                                    (i.e. uses the provider's staging/sandbox APIs/environment)
    
    
    MX records:
    --priority <PRIORITY>           Priority of record.


Examples:

    dnsc --get -provider azure -domain example.com -recordtype CNAME -name www

    dnsc --set -p azure -d example.com -r TXT -v sometxtvalue123
    dnsc --set -p azure -d example.com -r CNAME -n www -v some.domain.com

    dnsc --delete -p azure -d example.com -r CNAME -n www

Exit codes:

    0   Completed successfully
    1   Error occurred

");
        }
    }
}
