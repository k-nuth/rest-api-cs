using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;

namespace bitprim.insight
{
    public class ForwarderMiddleware
    {
        private readonly RequestDelegate next_;
        private readonly ILogger<ForwarderMiddleware> logger_;
        private static readonly HttpClient client = new HttpClient();

        private const int MAX_RETRIES = 3;
        private const int SEED_DELAY = 100;
        private const int MAX_DELAY = 2;

        private readonly Policy retryPolicy_ = Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(RetryUtils.DecorrelatedJitter(MAX_RETRIES, TimeSpan.FromMilliseconds(SEED_DELAY), TimeSpan.FromSeconds(MAX_DELAY)));

        public ForwarderMiddleware(RequestDelegate next, ILogger<ForwarderMiddleware> logger, IOptions<NodeConfig> config)
        {
            next_ = next ?? throw new ArgumentNullException(nameof(next));
            logger_ = logger;
            client.BaseAddress = new Uri(config.Value.ForwardUrl);
            client.Timeout = TimeSpan.FromSeconds(config.Value.WebSocketTimeoutInSeconds);
        }

        public async Task Invoke(HttpContext context)
        {
            logger_.LogInformation("Invoking request " + context.Request.Path);
            
            var method = new HttpMethod(context.Request.Method);

            StringContent httpContent;
            using (var sr = new StreamReader(context.Request.Body))
            {
                var content = await sr.ReadToEndAsync();
                httpContent = new StringContent(content, Encoding.UTF8, "application/json");
            }

            var ret = await retryPolicy_.ExecuteAsync(() =>
            {
                var message = new HttpRequestMessage(method,(context.Request.Path.Value ?? "") + (context.Request.QueryString.Value ?? ""))
                {
                    Content = httpContent
                };

                return client.SendAsync(message);
            });

            context.Response.StatusCode = (int)ret.StatusCode;
            context.Response.ContentType = ret.Content.Headers.ContentType?.ToString();
            await context.Response.WriteAsync(await ret.Content.ReadAsStringAsync());
        }

    }

    public static class ForwarderMiddlewareExtensions
    {
        public static IApplicationBuilder UseForwarderMiddleware(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<ForwarderMiddleware>();
        }
    }
}