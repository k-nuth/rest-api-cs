using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading.Tasks;
using Serilog;
using Serilog.Context;

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
            var originalBodyStream = context.Response.Body;
            using (var responseBody = new MemoryStream())
            {
                context.Response.Body = responseBody;

                await _next(context);
                await LogHttpRequest(context);
                await responseBody.CopyToAsync(originalBodyStream);
            }
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

    private async Task LogHttpRequest(HttpContext context)
    {
        HttpResponse response = context.Response;
        response.Body.Seek(0, SeekOrigin.Begin);
        var responseText = await new StreamReader(response.Body).ReadToEndAsync(); 
        response.Body.Seek(0, SeekOrigin.Begin);
        using(LogContext.PushProperty("SourceIP", context.Connection.RemoteIpAddress))
        using(LogContext.PushProperty("UserName", context.User.Identity.Name))
        using(LogContext.PushProperty("HttpMethod", context.Request.Method))
        using(LogContext.PushProperty("HttpRequestUrl", context.Request.Path.Value))
        using(LogContext.PushProperty("HttpRequestProtocol", context.Request.Protocol))
        using(LogContext.PushProperty("HttpResponseStatusCode", context.Response.StatusCode))
        using(LogContext.PushProperty("HttpResponseLength", responseText.Length))
        {
            Log.Information("Received Http request");
        }
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