using System;
using System.IO;
using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace bitprim.insight
{
    internal class Program
    {
        private const int DEFAULT_PORT = 1549;

        public static void Main(string[] args)
        {
            try
            {
                CreateWebHostBuilder(args).Build().Run();  
            }
            catch(Exception ex)
            {
                Log.Fatal(ex, "Host terminated unexpectedly");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        public static IWebHostBuilder CreateWebHostBuilder(string[] args)
        {
            var config = new ConfigurationBuilder()
                .AddCommandLine(args)
                .Build();

            var address = config.GetValue("server.address", IPAddress.Loopback.ToString());

            if (!IPAddress.TryParse(address,out var ip))
            {
                throw new ArgumentException("Error parsing server.address parameter",nameof(address));
            }

            var serverPort = config.GetValue("server.port",DEFAULT_PORT);

            return new WebHostBuilder()
                .UseKestrel(options => { options.Listen(ip, serverPort); })
                .UseContentRoot(Directory.GetCurrentDirectory())
                .UseSerilog()
                .UseIISIntegration()
                .ConfigureAppConfiguration((hostingContext, configBuilder) =>
                    {
                        configBuilder.SetBasePath(Directory.GetCurrentDirectory());
                        configBuilder.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                            .AddJsonFile($"appsettings.{hostingContext.HostingEnvironment.EnvironmentName}.json", optional: true, reloadOnChange: true);
                        configBuilder.AddEnvironmentVariables();
                        configBuilder.AddCommandLine(args);
                    })
                .UseStartup<Startup>();
        }
    }
}
