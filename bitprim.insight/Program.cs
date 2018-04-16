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
            ConfigureLogging();
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

        private static void ConfigureLogging()
        {
            var timeZone = DateTimeOffset.Now.ToString("%K").Replace(CultureInfo.CurrentCulture.DateTimeFormat.TimeSeparator, "");
            const string outputTemplate = "{" + LogPropertyNames.SOURCE_IP + "} " +
                    "{" + LogPropertyNames.USER_ID +  "} {" + LogPropertyNames.USER_NAME  + "} " +
                    "[{Timestamp:dd/MMM/yyyy HH:mm:ss} {" + LogPropertyNames.TIME_ZONE + "}] {Level:u3} " +
                    "\"{" + LogPropertyNames.HTTP_METHOD + "} " +
                    "{" + LogPropertyNames.HTTP_REQUEST_URL + "} " +
                    "{" + LogPropertyNames.HTTP_PROTOCOL_VERSION + "}\" " +
                    "{" + LogPropertyNames.HTTP_RESPONSE_STATUS_CODE + "} " +
                    "{" + LogPropertyNames.HTTP_RESPONSE_LENGTH + "} " +
                    "{Message:lj}{NewLine}{Exception}";
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                //.MinimumLevel.Error()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                .Enrich.FromLogContext()
                .Enrich.WithProperty(LogPropertyNames.TIME_ZONE, timeZone)
                .WriteTo.Console(outputTemplate: outputTemplate)
                .WriteTo.File("log-.txt", rollingInterval: RollingInterval.Day, outputTemplate: outputTemplate)
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .CreateLogger();
        }
    }
}
