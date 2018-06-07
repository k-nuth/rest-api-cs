using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Serilog.Context;

namespace bitprim.insight.Middlewares
{
    public class RequestLoggerMiddleware
    {
        private static class LogPropertyNames
        {
            public const string HTTP_METHOD = "HttpMethod";
            public const string HTTP_PROTOCOL_VERSION = "HttpProtocol";
            public const string HTTP_REQUEST_URL = "HttpRequestUrl";
            public const string HTTP_RESPONSE_LENGTH = "HttpResponseLength";
            public const string HTTP_RESPONSE_STATUS_CODE = "HttpResponseStatusCode";
            public const string SOURCE_IP = "SourceIP";
            public const string TIME_ZONE = "TimeZone";
            public const string ELAPSED_MS = "ElapsedMs";
        }

        
        private readonly RequestDelegate next_;
        private readonly ILogger<HttpStatusCodeExceptionMiddleware> logger_;

        private readonly string timeZone_ = DateTimeOffset.Now.ToString("%K").Replace(CultureInfo.CurrentCulture.DateTimeFormat.TimeSeparator, "");

        public RequestLoggerMiddleware(RequestDelegate next, ILogger<HttpStatusCodeExceptionMiddleware> logger)
        {
            next_ = next ?? throw new ArgumentNullException(nameof(next));
            logger_ = logger;
        }

        public async Task Invoke(HttpContext httpContext)
        {
            if (httpContext == null) 
                throw new ArgumentNullException(nameof(httpContext));

            var start = Stopwatch.GetTimestamp();
            try
            {
                var originalBodyStream = httpContext.Response.Body;
                using (var responseBody = new MemoryStream())
                {
                    httpContext.Response.Body = responseBody;
                    await next_(httpContext);
                    var elapsedMs = GetElapsedMilliseconds(start, Stopwatch.GetTimestamp());
                    LogHttpRequest(httpContext, elapsedMs);
                    if (!httpContext.WebSockets.IsWebSocketRequest)
                    {
                        await responseBody.CopyToAsync(originalBodyStream);
                    }
                }
            }
            // Never caught, because `LogException()` returns false.
            catch (Exception ex) when (LogException(httpContext, GetElapsedMilliseconds(start, Stopwatch.GetTimestamp()), ex)) { }
        }

        static double GetElapsedMilliseconds(long start, long stop)
        {
            return Math.Round((stop - start) * 1000 / (double)Stopwatch.Frequency,2);
        }

        private bool LogException(HttpContext httpContext, double elapsedMs, Exception ex)
        {
            LogHttpRequest(httpContext, elapsedMs,ex);
            return false;
        }

        private void LogHttpRequest(HttpContext context, double elapsedMs)
        {
             LogHttpRequest(context, elapsedMs, null);
        }
    
        private void LogHttpRequest(HttpContext context, double elapsedMs, Exception ex)
        {
            HttpResponse response = context.Response;
            using(LogContext.PushProperty(LogPropertyNames.SOURCE_IP, context.Connection.RemoteIpAddress))
            using(LogContext.PushProperty(LogPropertyNames.HTTP_METHOD, context.Request.Method))
            using(LogContext.PushProperty(LogPropertyNames.HTTP_REQUEST_URL, context.Request.Path.Value))
            using(LogContext.PushProperty(LogPropertyNames.HTTP_PROTOCOL_VERSION, context.Request.Protocol))
            using(LogContext.PushProperty(LogPropertyNames.HTTP_RESPONSE_STATUS_CODE, context.Response.StatusCode))
            using(LogContext.PushProperty(LogPropertyNames.HTTP_RESPONSE_LENGTH, response.ContentLength ?? context.Response.Body.Length))
            using(LogContext.PushProperty(LogPropertyNames.TIME_ZONE, timeZone_))
            using(LogContext.PushProperty(LogPropertyNames.ELAPSED_MS, elapsedMs))
            {
                if (ex != null)
                {
                    logger_.LogError(ex,""); //Properties cover all information, so empty message
                }
                else
                {
                    logger_.LogInformation(""); //Properties cover all information, so empty message
                }
            }
            context.Response.Body.Position = 0;
        }
    }

    // Extension method used to add the middleware to the HTTP request pipeline.
    public static class RequestLoggerMiddlewareExtensions
    {
        public static IApplicationBuilder UseRequestLoggerMiddleware(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<RequestLoggerMiddleware>();
        }
    }
}