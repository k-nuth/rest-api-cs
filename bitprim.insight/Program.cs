using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace api
{
    public class Program
    {
        private const int DEFAULT_PORT = 1549;

        public static void Main(string[] args)
        {
            var config = GetServerPortFromCommandLine(args);
            var serverPort = config.GetValue<int>("server.port");
            var host = new WebHostBuilder()
                .UseKestrel(options => {
                    options.Listen(IPAddress.Loopback, serverPort);
                })
                .UseContentRoot(Directory.GetCurrentDirectory())
                .UseIISIntegration()
                .UseStartup<Startup>()
                .Build();
            host.Run();
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
    }
}
