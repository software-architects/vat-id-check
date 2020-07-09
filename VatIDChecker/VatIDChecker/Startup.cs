using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using System.Runtime.CompilerServices;

[assembly: FunctionsStartup(typeof(VatIDChecker.Startup))]
[assembly: InternalsVisibleTo("VatIDChecker.Tests")]

namespace VatIDChecker
{
    public class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            builder.Services.AddHttpClient();
        }
    }
}
