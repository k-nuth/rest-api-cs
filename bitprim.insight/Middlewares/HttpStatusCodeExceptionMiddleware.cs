using System;
using System.Net;
using System.Threading.Tasks;
using bitprim.insight.Exceptions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace bitprim.insight.Middlewares
{
    internal class HttpStatusCodeExceptionMiddleware
    {
        private readonly RequestDelegate next_;
        private readonly ILogger<HttpStatusCodeExceptionMiddleware> logger_;

        public HttpStatusCodeExceptionMiddleware(RequestDelegate next, ILogger<HttpStatusCodeExceptionMiddleware> logger)
        {
            next_ = next ?? throw new ArgumentNullException(nameof(next));
            logger_ = logger;
        }

        public async Task Invoke(HttpContext context)
        {
            try
            {
                await next_(context);
            }
            catch (HttpStatusCodeException ex)
            {
                if (context.Response.HasStarted)
                {
                    logger_.LogWarning("The response has already started, the http status code middleware will not be executed.");
                    throw;
                }

                context.Response.Clear();
                context.Response.StatusCode = (int)ex.StatusCode;
                context.Response.ContentType = ex.ContentType;

                await context.Response.WriteAsync(ex.Message);
            }
            catch (BitprimException ex)
            {
                if (context.Response.HasStarted)
                {
                    logger_.LogWarning("The response has already started, the http status code middleware will not be executed.");
                    throw;
                }

                context.Response.Clear();
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                context.Response.ContentType = ex.ContentType;

                await context.Response.WriteAsync(ex.ErrorCode.ToString());
            }
        }
    }

    // Extension method used to add the middleware to the HTTP request pipeline.
    internal static class HttpStatusCodeExceptionMiddlewareExtensions
    {
        public static IApplicationBuilder UseHttpStatusCodeExceptionMiddleware(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<HttpStatusCodeExceptionMiddleware>();
        }
    }
}