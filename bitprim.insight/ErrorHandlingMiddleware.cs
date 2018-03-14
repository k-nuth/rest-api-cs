using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using Serilog;

public class HttpStatusCodeExceptionMiddleware
{
    private readonly RequestDelegate _next;

    public HttpStatusCodeExceptionMiddleware(RequestDelegate next)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
    }

    public async Task Invoke(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (HttpStatusCodeException ex)
        {
            if (context.Response.HasStarted)
            {
                Log.Warning("The response has already started, the http status code middleware will not be executed.");
                throw;
            }
            await HandleException(context, ex, ex.StatusCode, ex.ContentType);
            return;
        }
        catch(Exception ex)
        {
            if (context.Response.HasStarted)
            {
                Log.Warning("The response has already started, the http status code middleware will not be executed.");
                throw;
            }
            await HandleException(context, ex, (int) System.Net.HttpStatusCode.InternalServerError, "text/plain");
            return;
        }
    }

    private async Task HandleException(HttpContext context, Exception ex, int statusCode, string contentType)
    {
        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = contentType;
        Log.Error(ex.ToString());
        await context.Response.WriteAsync(ex.Message);
    }
}

// Extension method used to add the middleware to the HTTP request pipeline.
public static class HttpStatusCodeExceptionMiddlewareExtensions
{
    public static IApplicationBuilder UseHttpStatusCodeExceptionMiddleware(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<HttpStatusCodeExceptionMiddleware>();
    }
}