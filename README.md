## DNS Client Project

This project aims to create open, flexible and reusable DNS clients for the various public DNS hosting providers out there, 
such as Cloudflare, Azure, AWS Route53 and others. ~There are a few projects out there that have implementations of clients 
for their purposes, but nothing that is really reusable for any purpose to serve the community at large.~ Now that I've found Lexicon (https://github.com/AnalogJ/lexicon), I recommend using that instead of DNSC.

The following projects were used as inspiration and reference:
* https://github.com/go-acme/lego
* https://github.com/kubernetes-sigs/external-dns
* https://github.com/gomodules/dns

Available DNS providers:
* Azure
* Namecheap

## Usage
    
Display help

    dnsc --?

Examples

    dnsc --get -provider azure -domain example.com -recordtype CNAME -name www

    dnsc --set -p azure -d example.com -r TXT -n "@" -v sometxtvalue123
    dnsc --set -p azure -d example.com -r CNAME -n www -v some.domain.com

    # delete entire record
    dnsc --delete -p azure -d example.com -r CNAME -n www
    
    # delete single value from record
    dnsc --delete -p azure -d example.com -r TXT -n www -v "single value"
