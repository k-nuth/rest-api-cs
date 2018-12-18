using System;
using System.Collections.Generic;
using Bitprim;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using Swashbuckle.AspNetCore.Swagger;
using System.IO;
using System.Reflection;
using bitprim.insight.Middlewares;
using bitprim.insight.Websockets;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;

namespace bitprim.insight
{
    internal class Startup
    {
        private BlockChainObserver blockChainObserver_;
        private const string CORS_POLICY_NAME = "BI_CORS_POLICY";
        private Executor exec_;
        private WebSocketHandler webSocketHandler_;
        private WebSocketForwarderClient webSocketForwarderClient_;
        private readonly NodeConfig nodeConfig_;
        private readonly IConfiguration configuration_;

        public Startup(IConfiguration configuration)
        {
            configuration_ = configuration;
            
            ConfigureLogging();

            nodeConfig_ = configuration_.Get<NodeConfig>();
            LogSettings(nodeConfig_);
        }

        private void LogSettings<T>(T instance)
        {
            TypeInfo typeInfo = typeof(T).GetTypeInfo();
            foreach (PropertyInfo propertyInfo in typeInfo.DeclaredProperties)
            {
                Log.Debug(string.Format("{0}:{1}",propertyInfo.Name,propertyInfo.GetValue(instance))); 
            }
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            Log.Information("Current Dir: " + Environment.CurrentDirectory);

            // Add functionality to inject IOptions<T>
            services.AddOptions();
            // Add our Config object so it can be injected
            services.Configure<NodeConfig>(configuration_);
            // Add framework services.
            ConfigureFrameworkServices(services);

            ConfigureCors(services);

            

            // Register the Swagger generator, defining one or more Swagger documents  
            services.AddSwaggerGen(c =>  
            {  
                c.SwaggerDoc("v1", new Info { Title = "bitprim", Version = "v1" });
                c.IncludeXmlComments(string.Format(@"{0}/bitprim.insight.xml", System.AppDomain.CurrentDomain.BaseDirectory));
            });

            var serviceProvider = services.BuildServiceProvider();

            if (nodeConfig_.WebsocketsEnabled)
            {
                webSocketHandler_ = new WebSocketHandler(serviceProvider.GetService<ILogger<WebSocketHandler>>(), nodeConfig_);
                webSocketHandler_.Init();

                services.AddSingleton(webSocketHandler_);
            }
            
            var poolInfo = new PoolsInfo(nodeConfig_.PoolsFile);
            poolInfo.Load();
            services.AddSingleton<IPoolsInfo>(poolInfo);

            StartNode(services, serviceProvider);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, IServiceProvider serviceProvider,
                              IApplicationLifetime applicationLifetime)
        {
            app.UseRequestLoggerMiddleware();
            app.UseHttpStatusCodeExceptionMiddleware();
            app.UseResponseCompression();
            
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
                   if (nodeConfig_.CacheEnabled)
                    {
                        opt.CacheProfiles.Add(Constants.Cache.SHORT_CACHE_PROFILE_NAME,
                            new CacheProfile
                            {
                                Duration = nodeConfig_.ShortResponseCacheDurationInSeconds
                            });
                        opt.CacheProfiles.Add(Constants.Cache.LONG_CACHE_PROFILE_NAME,
                            new CacheProfile
                            {
                                Duration = nodeConfig_.LongResponseCacheDurationInSeconds
                            });
                    }
                    else
                    {
                        opt.CacheProfiles.Add(Constants.Cache.SHORT_CACHE_PROFILE_NAME,
                            new CacheProfile
                            {
                                Duration = null, NoStore = true
                            });
                        opt.CacheProfiles.Add(Constants.Cache.LONG_CACHE_PROFILE_NAME,
                            new CacheProfile
                            {
                                Duration = null,  NoStore = true
                            });


                    }
                    opt.RespectBrowserAcceptHeader = true;
                    opt.Conventions.Insert(0, new RouteConvention(new RouteAttribute(nodeConfig_.ApiPrefix)));
                })
            .AddApiExplorer()
            .AddFormatterMappings()
            .AddJsonFormatters()
            .AddCors();

            if (nodeConfig_.CacheEnabled)
            {
                services.AddMemoryCache(opt =>
                    {
                        opt.SizeLimit = nodeConfig_.MaxCacheSize;
                    }
                );
            }
            else
            {
                services.AddSingleton<IMemoryCache>(new DummyMemoryCache());
            }

            services.AddResponseCompression();
        }

        private void ConfigureLogging()
        {
            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration_)
                .CreateLogger();
        }

        private void ConfigureWebSockets(IApplicationBuilder app)
        {
            if (nodeConfig_.WebsocketsEnabled)
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
            else
            {
                if (!nodeConfig_.InitializeNode)
                {
                    app.UseWebSockets();
                    app.Use(async (context, next) =>
                    {
                        if (context.WebSockets.IsWebSocketRequest)
                        {
                            context.Response.StatusCode = 403;
                        }
                        else
                        {
                            await next();
                        }
                    });
                }
            }
        }

        private void ConfigureCors(IServiceCollection services)
        {
            services.AddCors(o => o.AddPolicy(CORS_POLICY_NAME, builder =>
            {
                builder.WithOrigins(nodeConfig_.AllowedOrigins);
            }));
        }

        private void StartNode(IServiceCollection services, ServiceProvider serviceProvider)
        {
            if (nodeConfig_.InitializeNode)
            {
                Log.Information("Initializing full node mode");
                StartFullNode(services, serviceProvider);
            }
            else
            {
                if (string.IsNullOrWhiteSpace(nodeConfig_.ForwardUrl))
                {
                    throw new ApplicationException("You must configure the ForwardUrl setting");
                }

                Log.Information("Initializing forwarder mode");
                Log.Information("Forward Url " + nodeConfig_.ForwardUrl);

                if (nodeConfig_.WebsocketsEnabled)
                {
                    webSocketForwarderClient_ = new WebSocketForwarderClient(
                        serviceProvider.GetService<IOptions<NodeConfig>>(),
                        serviceProvider.GetService<ILogger<WebSocketForwarderClient>>(), webSocketHandler_);
                    _ = webSocketForwarderClient_.Init();
                }
            }
        }

        private void StartFullNode(IServiceCollection services, ServiceProvider serviceProvider)
        {
            Log.Information("Node Config File: " + nodeConfig_.NodeConfigFile);

            if (!string.IsNullOrWhiteSpace(nodeConfig_.NodeConfigFile))
            {
                Log.Information("FullPath Node Config File: " + Path.GetFullPath(nodeConfig_.NodeConfigFile));
            }
                
            // Initialize and register chain service
            exec_ = new Executor(nodeConfig_.NodeConfigFile);

            if (!exec_.IsLoadConfigValid)
            {
                throw new ApplicationException("Error loading config file");
            }

            int result = exec_.InitAndRunAsync().GetAwaiter().GetResult();
            if (result != 0)
            {
                throw new ApplicationException("Executor::InitAndRunAsync failed; error code: " + result);
            }
            
            services.AddSingleton<Executor>(exec_);
            services.AddSingleton<IChain>(exec_.Chain);

            if (nodeConfig_.WebsocketsEnabled)
            {
                blockChainObserver_ = new BlockChainObserver(exec_, webSocketHandler_, nodeConfig_,  serviceProvider.GetService<ILogger<BlockChainObserver>>());
            }
        }

        private void OnShutdown()
        {
            Log.Information("Cancelling subscriptions...");
            if (webSocketHandler_ != null)
            {
                var task = webSocketHandler_.Shutdown();
                task.Wait();
            }
                        
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
            Log.Information("Waiting for node to stop...");
            System.Threading.Thread.Sleep(TimeSpan.FromMinutes(1)); //TODO Temporary workaround to node-cint shutdown issue
            Log.Information("Destroying node...");
            exec_.Dispose();
            Log.Information("Waiting for node to shut down...");
            System.Threading.Thread.Sleep(TimeSpan.FromSeconds(30)); //TODO Temporary workaround to node-cint shutdown issue
            Log.Information("Node shutdown OK!");
        }
    }

     class DummyMemoryCache : IMemoryCache
    {
        public void Dispose()
        {

        }

        public bool TryGetValue(object key, out object value)
        {
            value = null;
            return false;
        }

        public ICacheEntry CreateEntry(object key)
        {
            return new DummyCacheEntry();
        }

        public void Remove(object key)
        {

        }
    }

    class DummyCacheEntry : ICacheEntry
    {
        public void Dispose()
        {

        }

        public object Key { get; }
        public object Value { get; set; }
        public DateTimeOffset? AbsoluteExpiration { get; set; }
        public TimeSpan? AbsoluteExpirationRelativeToNow { get; set; }
        public TimeSpan? SlidingExpiration { get; set; }
        public IList<IChangeToken> ExpirationTokens { get; }
        public IList<PostEvictionCallbackRegistration> PostEvictionCallbacks { get; }
        public CacheItemPriority Priority { get; set; }
        public long? Size { get; set; }
    }
}
