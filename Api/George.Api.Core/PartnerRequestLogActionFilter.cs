using System.Diagnostics;
using System.Security.Claims;
using George.DB;
using George.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Newtonsoft.Json;

namespace George.Api.Core;

/// <summary>
/// Logs every Partner API call (request body, outcome, duration) to IntegrationLog under entity type
/// <c>partner</c>, direction <c>inbound</c>, so support and the integrating vendor can debug a conversation
/// together from the Integration Logs screen. Bodies are truncated; the API key itself is never logged.
/// Applied with <c>[ServiceFilter(typeof(PartnerRequestLogActionFilter))]</c> on PartnerController.
/// </summary>
public sealed class PartnerRequestLogActionFilter : IAsyncActionFilter
{
    public const string EntityType = "partner";
    private const int MaxBodyChars = 8000;

    private readonly IIntegrationLogQueue _queue;

    public PartnerRequestLogActionFilter(IIntegrationLogQueue queue)
    {
        _queue = queue;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var sw = Stopwatch.StartNew();
        var http = context.HttpContext;
        var siteId = int.TryParse(http.User.FindFirstValue(PartnerApiKeyAuthenticationHandler.ClaimSiteId), out var sid) ? sid : 0;
        var requestJson = SerializeArguments(context.ActionArguments);
        var operation = $"{http.Request.Method} {http.Request.Path}{http.Request.QueryString}";

        var executed = await next();
        sw.Stop();

        var (success, statusCode, description, responseBody) = Describe(executed);
        if (executed.Exception != null && !executed.ExceptionHandled)
        {
            success = false;
            description = executed.Exception.GetType().Name + ": " + executed.Exception.Message;
        }

        _queue.TryEnqueue(new IntegrationLog
        {
            SiteId = siteId,
            EntityType = EntityType,
            EntityId = ExtractOrderId(executed),
            Direction = IntegrationLogDirection.Inbound.ToWire(),
            Operation = Truncate(operation, 80)!,
            Level = success ? IntegrationLogLevel.Info.ToWire() : IntegrationLogLevel.Warning.ToWire(),
            Url = Truncate(operation, 1000),
            HttpStatus = statusCode,
            Success = success,
            RequestJson = requestJson,
            ResponseBody = responseBody,
            DurationMs = (int)sw.ElapsedMilliseconds,
            Error = success ? null : Truncate(description, 1000),
            CreatedAtUtc = DateTime.UtcNow,
        });
    }

    private static (bool Success, int? Status, string? Description, string? Body) Describe(ActionExecutedContext executed)
    {
        if (executed.Result is ObjectResult obj)
        {
            var status = obj.StatusCode ?? 200;
            if (obj.Value is George.Common.IApiResponse<object> typed)
                return (typed.IsSuccessful, status, typed.Description, Truncate(SafeSerialize(obj.Value), MaxBodyChars));
            // Generic IApiResponse<T>: read the common members reflectively (they all implement IsSuccessful/Description).
            var t = obj.Value?.GetType();
            var isSuccessful = t?.GetProperty("IsSuccessful")?.GetValue(obj.Value) as bool? ?? status < 400;
            var description = t?.GetProperty("Description")?.GetValue(obj.Value) as string;
            return (isSuccessful, status, description, Truncate(SafeSerialize(obj.Value), MaxBodyChars));
        }
        if (executed.Result is StatusCodeResult sc)
            return (sc.StatusCode < 400, sc.StatusCode, null, null);
        return (executed.Exception == null, null, null, null);
    }

    private static int? ExtractOrderId(ActionExecutedContext executed)
    {
        if (executed.RouteData.Values.TryGetValue("orderId", out var v) && int.TryParse(v?.ToString(), out var id))
            return id;
        return null;
    }

    private static string? SerializeArguments(IDictionary<string, object?> args)
    {
        if (args.Count == 0) return null;
        var simple = new Dictionary<string, object?>();
        foreach (var kv in args)
        {
            if (kv.Value is CancellationToken) continue;
            simple[kv.Key] = kv.Value;
        }
        return Truncate(SafeSerialize(simple), MaxBodyChars);
    }

    private static string SafeSerialize(object? value)
    {
        try { return JsonConvert.SerializeObject(value); }
        catch { return value?.ToString() ?? ""; }
    }

    private static string? Truncate(string? s, int max) =>
        s == null ? null : (s.Length <= max ? s : s.Substring(0, max));
}
