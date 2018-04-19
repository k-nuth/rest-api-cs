using System;
using System.Globalization;
using System.IO;
using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Events;

namespace bitprim.insight
{
    public class Program
    {
        private const int DEFAULT_PORT = 1549;

        public static void Main(string[] args)
        {
            try
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

                var host = new WebHostBuilder()
                    .UseKestrel(options => {
                        options.Listen(ip, serverPort);
                    })
                    .UseContentRoot(Directory.GetCurrentDirectory())
                    .UseSerilog()
                    .UseIISIntegration()
                    .UseStartup<Startup>()
                    .Build();

                host.Run();
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

    }
}
