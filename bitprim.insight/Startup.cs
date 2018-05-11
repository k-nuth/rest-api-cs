using System;
using Bitprim;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using Swashbuckle.AspNetCore.Swagger;
using System.Globalization;
using System.IO;
using Microsoft.AspNetCore.Mvc;

namespace bitprim.insight
{
    public class Startup
    {
        private BlockChainObserver blockChainObserver_;
        private const string CORS_POLICY_NAME = "BI_CORS_POLICY";
        private Executor exec_;
        private WebSocketHandler webSocketHandler_;
        private WebSocketForwarderClient webSocketForwarderClient_;
        private readonly NodeConfig nodeConfig_;     

        public Startup(IHostingEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
                .AddEnvironmentVariables();
            Configuration = builder.Build();
            ConfigureLogging();
            nodeConfig_ = Configuration.Get<NodeConfig>();
        }

        public IConfigurationRoot Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            
            Log.Information("Current Dir: " + Environment.CurrentDirectory); 

            // Add functionality to inject IOptions<T>
            services.AddOptions();
            // Add our Config object so it can be injected
            services.Configure<NodeConfig>(Configuration);
            // Add framework services.
            ConfigureFrameworkServices(services);
           
            ConfigureCors(services);
            // Register the Swagger generator, defining one or more Swagger documents  
            services.AddSwaggerGen(c =>  
            {  
                c.SwaggerDoc("v1", new Info { Title = "bitprim", Version = "v1" });  
            });

            var serviceProvider = services.BuildServiceProvider();

            webSocketHandler_ = new WebSocketHandler(serviceProvider.GetService<ILogger<WebSocketHandler>>());
            webSocketHandler_.Init();

            services.AddSingleton<WebSocketHandler>(webSocketHandler_);

            var poolInfo = new PoolsInfo(nodeConfig_.PoolsFile);
            poolInfo.Load();
            services.AddSingleton<PoolsInfo>(poolInfo);

            StartNode(services, serviceProvider);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, IServiceProvider serviceProvider,
                              IApplicationLifetime applicationLifetime)
        {
            //Enable web sockets for sending block and tx notifications
            ConfigureWebSockets(app);
            // Enable middleware to serve generated Swagger as a JSON endpoint.
            app.UseSwagger();
            // Enable middleware to serve swagger-ui (HTML, JS, CSS, etc.), specifying the Swagger JSON endpoint.  
            app.UseSwaggerUI(c =>  
            {  
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "bitprim V1");
            });
            // Register shutdown handler
            applicationLifetime.ApplicationStopping.Register(OnShutdown);
            app.UseCors(CORS_POLICY_NAME);
            app.UseStaticFiles(); //TODO For testing web sockets
            app.UseHttpStatusCodeExceptionMiddleware();

            if (!nodeConfig_.InitializeNode)
            {
                app.UseForwarderMiddleware();
            }
            
            app.UseMvc();
        }

        private void ConfigureFrameworkServices(IServiceCollection services)
        {
            services.AddMvcCore(opt =>
                {
                    opt.CacheProfiles.Add(Constants.Cache.SHORT_CACHE_PROFILE_NAME,
                        new CacheProfile()
                        {
                            Duration = nodeConfig_.ShortResponseCacheDurationInSeconds
                        });
                    opt.CacheProfiles.Add(Constants.Cache.LONG_CACHE_PROFILE_NAME,
                        new CacheProfile()
                        {
                            Duration = nodeConfig_.LongResponseCacheDurationInSeconds
                        });
                    opt.RespectBrowserAcceptHeader = true;
                    opt.Conventions.Insert(0, new RouteConvention(new  RouteAttribute(nodeConfig_.ApiPrefix) ));
                })
            .AddApiExplorer()
            .AddFormatterMappings()
            .AddJsonFormatters()
            .AddCors();
            services.AddMemoryCache( opt =>
                {
                    opt.SizeLimit = nodeConfig_.MaxCacheSize;
                }
            );
        }

        private void ConfigureLogging()
        {
            var timeZone = DateTimeOffset.Now.ToString("%K").Replace(CultureInfo.CurrentCulture.DateTimeFormat.TimeSeparator, "");
            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(Configuration)
                .Enrich.WithProperty(LogPropertyNames.TIME_ZONE, timeZone)
                .CreateLogger();
        }

        private void ConfigureWebSockets(IApplicationBuilder app)
        {
            app.UseWebSockets();
            app.Use(async (context, next) =>
            {
                if (context.WebSockets.IsWebSocketRequest)
                {
                    using (var webSocket = await context.WebSockets.AcceptWebSocketAsync())
                    {
                        await webSocketHandler_.Subscribe(context, webSocket);
                    }
                }
                else
                {
                    await next();
                }
            });
        }

        private void ConfigureCors(IServiceCollection services)
        {
            services.AddCors(o => o.AddPolicy(CORS_POLICY_NAME, builder =>
            {
                builder.WithOrigins(Configuration.GetValue<string>("AllowedOrigins"));
            }));
        }

        private void StartNode(IServiceCollection services, ServiceProvider serviceProvider)
        {
            if (nodeConfig_.InitializeNode)
            {
                Log.Information("Initializing full node mode");
                StartFullNode(services);
            }
            else
            {
                if (string.IsNullOrWhiteSpace(nodeConfig_.ForwardUrl))
                {
                    throw new ApplicationException("You must configure the ForwardUrl setting");
                }

                Log.Information("Initializing forwarder mode");
                Log.Information("Forward Url " + nodeConfig_.ForwardUrl);
                
                webSocketForwarderClient_ = new WebSocketForwarderClient(
                    serviceProvider.GetService<IOptions<NodeConfig>>(),
                    serviceProvider.GetService<ILogger<WebSocketForwarderClient>>(), webSocketHandler_);
               _ = webSocketForwarderClient_.Init();
            }
        }

        private void StartFullNode(IServiceCollection services)
        {
            Log.Information("Node Config File: " + nodeConfig_.NodeConfigFile);
            if (!string.IsNullOrWhiteSpace(nodeConfig_.NodeConfigFile))
                Log.Information("FullPath Node Config File: " + Path.GetFullPath(nodeConfig_.NodeConfigFile) );

            // Initialize and register chain service
            exec_ = new Executor(nodeConfig_.NodeConfigFile);
             
            int result = exec_.InitAndRunAsync().GetAwaiter().GetResult();
            if (result != 0)
            {
                throw new ApplicationException("Executor::InitAndRunAsync failed; error code: " + result);
            }
                
            blockChainObserver_ = new BlockChainObserver(exec_, webSocketHandler_);
            services.AddSingleton<Executor>(exec_);
            services.AddSingleton<Chain>(exec_.Chain);
        }

        private void OnShutdown()
        {
            Log.Information("Cancelling subscriptions...");
            var task = webSocketHandler_.Shutdown();
            task.Wait();

            if (webSocketForwarderClient_ != null)
            {
                Log.Information("Cancelling websocket forwarder...");
                webSocketForwarderClient_.Close().GetAwaiter().GetResult();
                webSocketForwarderClient_.Dispose();
                Log.Information("Websocket forwarder shutdown ok");
            }

            if (exec_ == null) 
                return;
            
            Log.Information("Stopping node...");
            exec_.Stop();
            Log.Information("Destroying node...");
            exec_.Dispose();
            Log.Information("Node shutdown OK!");
        }
    }
}
