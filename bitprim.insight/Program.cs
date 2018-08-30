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
        private const int DEFAULT_MAX_REQUEST_URL_LENGTH = 600; //10 addresses
        private const int DEFAULT_MAX_POST_BODY_SIZE = 204800; //10 addresses = 700, /tx/send = 200k

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

            var serverPort = config.GetValue("server.port", DEFAULT_PORT);
            var maxPostBodySize = config.GetValue("max.post", DEFAULT_MAX_POST_BODY_SIZE);
            var maxRequestUrlLength = config.GetValue("max.url", DEFAULT_MAX_REQUEST_URL_LENGTH);

            return new WebHostBuilder()
                .UseKestrel(options => 
                {
                    options.Limits.MaxRequestBodySize = maxPostBodySize;
                    options.Limits.MaxRequestLineSize = maxRequestUrlLength;
                    options.Listen(ip, serverPort);
                })
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
