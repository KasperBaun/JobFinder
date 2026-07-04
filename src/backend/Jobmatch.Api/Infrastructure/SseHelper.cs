using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace Jobmatch.Api.Infrastructure;

public static class SseHelper
{
    public static readonly JsonSerializerOptions JsonOptions = Jobmatch.Json.JobmatchJsonOptions.Default;

    public static void SetHeaders(HttpContext ctx)
    {
        ctx.Response.ContentType = "text/event-stream; charset=utf-8";
        ctx.Response.Headers.CacheControl = "no-cache";
        ctx.Response.Headers.Connection = "keep-alive";
        ctx.Response.Headers["X-Accel-Buffering"] = "no";
    }

    public static async Task SendAsync<T>(HttpContext ctx, T payload)
    {
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        await ctx.Response.WriteAsync($"data: {json}\n\n");
        await ctx.Response.Body.FlushAsync();
    }
}
