using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading.Tasks;
using Serilog;
using Serilog.Context;

public static class LogPropertyNames
{
    public const string HTTP_METHOD = "HttpMethod";
    public const string HTTP_PROTOCOL_VERSION = "HttpProtocol";
    public const string HTTP_REQUEST_URL = "HttpRequestUrl";
    public const string HTTP_RESPONSE_LENGTH = "HttpResponseLength";
    public const string HTTP_RESPONSE_STATUS_CODE = "HttpResponseStatusCode";
    public const string SOURCE_IP = "SourceIP";
    public const string TIME_ZONE = "TimeZone";
    public const string USER_ID = "UserID";
    public const string USER_NAME = "UserName";
}

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
        const string CLF_EMPTY_DATA = "-";
        HttpResponse response = context.Response;
        response.Body.Seek(0, SeekOrigin.Begin);
        var responseText = await new StreamReader(response.Body).ReadToEndAsync(); 
        response.Body.Seek(0, SeekOrigin.Begin);
        string userName = context.User.Identity.Name != null? context.User.Identity.Name : CLF_EMPTY_DATA;
        using(LogContext.PushProperty(LogPropertyNames.SOURCE_IP, context.Connection.RemoteIpAddress))
        using(LogContext.PushProperty(LogPropertyNames.USER_ID, CLF_EMPTY_DATA))
        using(LogContext.PushProperty(LogPropertyNames.USER_NAME, userName))
        using(LogContext.PushProperty(LogPropertyNames.HTTP_METHOD, context.Request.Method))
        using(LogContext.PushProperty(LogPropertyNames.HTTP_REQUEST_URL, context.Request.Path.Value))
        using(LogContext.PushProperty(LogPropertyNames.HTTP_PROTOCOL_VERSION, context.Request.Protocol))
        using(LogContext.PushProperty(LogPropertyNames.HTTP_RESPONSE_STATUS_CODE, context.Response.StatusCode))
        using(LogContext.PushProperty(LogPropertyNames.HTTP_RESPONSE_LENGTH, responseText.Length))
        {
            Log.Information(""); //Properties cover all information, so empty message
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