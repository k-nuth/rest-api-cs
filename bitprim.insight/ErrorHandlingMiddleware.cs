using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

public class HttpStatusCodeExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<HttpStatusCodeExceptionMiddleware> _logger;

    public HttpStatusCodeExceptionMiddleware(RequestDelegate next, ILoggerFactory loggerFactory)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = loggerFactory?.CreateLogger<HttpStatusCodeExceptionMiddleware>() ?? throw new ArgumentNullException(nameof(loggerFactory));
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
                _logger.LogWarning("The response has already started, the http status code middleware will not be executed.");
                throw;
            }
            await HandleException(context, ex, ex.StatusCode, ex.ContentType);
            return;
        }
        catch(Exception ex)
        {
            if (context.Response.HasStarted)
            {
                _logger.LogWarning("The response has already started, the http status code middleware will not be executed.");
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
        Console.WriteLine(ex); //TODO Implement logging (RA-16)
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