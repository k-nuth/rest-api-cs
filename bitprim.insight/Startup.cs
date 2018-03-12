using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Swashbuckle.AspNetCore.Swagger;
using Bitprim;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Cors.Internal;

namespace api
{
    public class Startup
    {
        private BlockChainObserver blockChainObserver_;
        private const string CORS_POLICY_NAME = "BI_CORS_POLICY";
        private Executor exec_;
        private WebSocketHandler webSocketHandler_;

        public Startup(IHostingEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
                .AddEnvironmentVariables();
            Configuration = builder.Build();
        }

        public IConfigurationRoot Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            // Add functionality to inject IOptions<T>
            services.AddOptions();
            // Add our Config object so it can be injected
            services.Configure<NodeConfig>(Configuration);
            // Add framework services.
            services.AddMvc();
            ConfigureCors(services);
            // Register the Swagger generator, defining one or more Swagger documents  
            services.AddSwaggerGen(c =>  
            {  
                c.SwaggerDoc("v1", new Info { Title = "bitprim", Version = "v1" });  
            });
            StartBitprimNode(services);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, IServiceProvider serviceProvider,
                              ILoggerFactory loggerFactory, IApplicationLifetime applicationLifetime)
        {
            loggerFactory.AddConsole(Configuration.GetSection("Logging"));
            loggerFactory.AddDebug();
            //Enable web sockets for sending block and tx notifications
            ConfigureWebSockets(app, loggerFactory);
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
            app.UseMvc();
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseHttpStatusCodeExceptionMiddleware();
            }
            else
            {
                app.UseHttpStatusCodeExceptionMiddleware();
                app.UseExceptionHandler();
            }
        }

        private void ConfigureWebSockets(IApplicationBuilder app, ILoggerFactory loggerFactory)
        {
            webSocketHandler_.Logger = loggerFactory.CreateLogger("SubscribeToBlocks");
            app.UseWebSockets();
            app.Use(async (context, next) =>
            {
                if (context.WebSockets.IsWebSocketRequest)
                {
                    var webSocket = await context.WebSockets.AcceptWebSocketAsync();
                    await webSocketHandler_.Subscribe(context, webSocket);
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

        private void StartBitprimNode(IServiceCollection services)
        {
            // Initialize and register chain service
            NodeConfig config = Configuration.Get<NodeConfig>();
            exec_ = new Executor(config.NodeConfigFile);
            if(config.StartDatabaseFromScratch)
            {
                bool ok = exec_.InitChain();
                if(!ok)
                {
                    throw new ApplicationException("Executor::InitChain failed; check log");
                }
            }
            int result = exec_.RunWait();
            if (result != 0)
            {
                throw new ApplicationException("Executor::RunWait failed; error code: " + result);
            }
            webSocketHandler_ = new WebSocketHandler();
            blockChainObserver_ = new BlockChainObserver(exec_, webSocketHandler_);
            services.AddSingleton<Chain>(exec_.Chain);
        }

        private void OnShutdown()
        {
            Console.WriteLine("Cancelling subscriptions...");
            webSocketHandler_.CancelAllSubscriptions();
            Console.WriteLine("Stopping node...");
            exec_.Stop();
            Console.WriteLine("Destroying node...");
            exec_.Dispose();
            Console.WriteLine("Node shutdown OK!");
        }
    }
}
