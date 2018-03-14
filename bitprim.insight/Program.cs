using System.Collections.Generic;
using System.IO;
using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using System;

namespace api
{
    public class Program
    {
        private const int DEFAULT_PORT = 1549;

        public static void Main(string[] args)
        {
            try
            {
                ConfigureLogging();
                Log.Information("Starting web host");
                var config = GetServerPortFromCommandLine(args);
                var serverPort = config.GetValue<int>("server.port");
                var host = new WebHostBuilder()
                    .UseKestrel(options => {
                        options.Listen(IPAddress.Loopback, serverPort);
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

        private static IConfigurationRoot GetServerPortFromCommandLine(string[] args)
        {
            var config = new ConfigurationBuilder()
                .AddCommandLine(args)
                .Build();
            var serverPort = config.GetValue<int>("server.port", DEFAULT_PORT);
            var configDictionary = new Dictionary<string, string>
            {
                {"server.port", serverPort.ToString()}
            };            
            return new ConfigurationBuilder()
                .AddCommandLine(args)
                .AddInMemoryCollection(configDictionary)
                .Build(); 
        }

        private static void ConfigureLogging()
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .CreateLogger();
        }
    }
}
