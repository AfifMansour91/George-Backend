using System.Net;
using George.Services.Marketing;
using George.Services.Response;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace George.Api.Controllers;

/// <summary>
/// The two links inside every marketing SMS - no login, the recipient's short token is the key:
/// <c>/s/{token}</c> counts the click and redirects; <c>/x/{token}</c> is the unsubscribe page.
/// Routes are as short as possible on purpose: every character is paid for in every message.
/// </summary>
[ApiController]
[AllowAnonymous]
[ApiExplorerSettings(IgnoreApi = true)]
public class PublicMarketingController : ControllerBase
{
    private readonly MarketingService _marketingService;

    public PublicMarketingController(MarketingService marketingService)
    {
        _marketingService = marketingService;
    }

    [HttpGet("/s/{token}")]
    public async Task<IActionResult> ClickAsync([FromRoute] string token, CancellationToken cancelToken = default)
    {
        if (string.Equals(token, MarketingService.TestToken, StringComparison.OrdinalIgnoreCase))
            return Page("זו הודעת בדיקה", "בהודעה אמיתית הקישור הזה מוביל לקישור שהגדרת ונספר כקליק.", null);
        var url = await _marketingService.ResolveClickAsync(token, cancelToken);
        // Tokens never expire; a miss means a mistyped/partially copied link or a message that carried no link.
        return url == null ? Page("הקישור לא נמצא", "ייתכן שהקישור הועתק חלקית. אפשר לחזור להודעה ולנסות שוב.", null) : Redirect(url);
    }

    /// <summary>The confirm screen. A GET never unsubscribes - link previews and SMS scanners open links on their own.</summary>
    [HttpGet("/x/{token}")]
    public async Task<IActionResult> UnsubscribePageAsync([FromRoute] string token, CancellationToken cancelToken = default)
    {
        var info = await _marketingService.GetUnsubscribeInfoAsync(token, cancelToken);
        return UnsubscribeView(info, token, done: false);
    }

    [HttpPost("/x/{token}")]
    public async Task<IActionResult> UnsubscribeAsync([FromRoute] string token, CancellationToken cancelToken = default)
    {
        var info = await _marketingService.UnsubscribeAsync(token, cancelToken);
        return UnsubscribeView(info, token, done: true);
    }

    private ContentResult UnsubscribeView(MarketingUnsubscribeInfo info, string token, bool done)
    {
        if (!info.Found)
            return Page("הקישור לא נמצא", "לא הצלחנו לזהות את ההודעה. ייתכן שהקישור הועתק חלקית.", null);
        if (info.IsTestToken)
            return Page("זו הודעת בדיקה", "בהודעה אמיתית הקישור הזה מסיר את הלקוח מרשימת הדיוור של החנות בלחיצה אחת. בהודעת בדיקה הוא רק מציג את הדף הזה.", null);

        var store = string.IsNullOrWhiteSpace(info.StoreName) ? "החנות" : WebUtility.HtmlEncode(info.StoreName);
        var brand = $"<p class=\"store\">{store}</p>";
        var backLink = Uri.TryCreate(info.StoreUrl, UriKind.Absolute, out var storeUri) && (storeUri.Scheme == "https" || storeUri.Scheme == "http")
            ? $"<a class=\"back\" href=\"{WebUtility.HtmlEncode(storeUri.ToString())}\">חזרה לאתר החנות</a>"
            : string.Empty;
        if (done)
            return Page("הוסרת מרשימת הדיוור", $"לא יישלחו אליך עוד הודעות שיווקיות מ{store}. הודעות על ההזמנות שלך ימשיכו להגיע כרגיל.", backLink, brand);
        if (info.AlreadyOptedOut)
        {
            var when = info.OptedOutAt.HasValue ? $" ב-{IsraelDate(info.OptedOutAt.Value)}" : string.Empty;
            return Page("כבר הוסרת מרשימת הדיוור", $"הבקשה שלך נרשמה{when}. לא יישלחו אליך הודעות שיווקיות נוספות מ{store}; הודעות על ההזמנות שלך ימשיכו להגיע כרגיל.", backLink, brand);
        }

        var form = $"<form method=\"post\" action=\"/x/{WebUtility.HtmlEncode(token)}\"><input type=\"hidden\" name=\"confirm\" value=\"1\"><button type=\"submit\">הסר אותי מרשימת הדיוור</button></form><p class=\"note\">הודעות על ההזמנות שלך ימשיכו להגיע כרגיל.</p>{backLink}";
        return Page("הסרה מרשימת הדיוור", $"לא לקבל עוד הודעות שיווקיות מ{store}?", form, brand);
    }

    private static string IsraelDate(DateTime utc)
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById(OperatingSystem.IsWindows() ? "Israel Standard Time" : "Asia/Jerusalem");
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), tz).ToString("d.M.yyyy");
    }

    private ContentResult Page(string title, string text, string? extraHtml, string? brandHtml = null)
    {
        var html = $@"<!doctype html>
<html lang=""he"" dir=""rtl"">
<head>
<meta charset=""utf-8"">
<meta name=""viewport"" content=""width=device-width, initial-scale=1"">
<meta name=""robots"" content=""noindex"">
<title>{WebUtility.HtmlEncode(title)}</title>
<style>
  body {{ margin:0; min-height:100vh; display:flex; align-items:center; justify-content:center; background:#F3F4F6; font-family:'Segoe UI',system-ui,sans-serif; color:#111827; }}
  main {{ background:#fff; border:1px solid #E5E7EB; border-radius:14px; padding:32px 28px; max-width:380px; margin:16px; text-align:center; }}
  h1 {{ margin:0 0 12px; font-size:20px; }}
  p {{ margin:0; font-size:15px; line-height:1.6; color:#4B5563; }}
  .store {{ margin:0 0 16px; font-size:13px; font-weight:600; letter-spacing:.02em; color:#6B7280; text-transform:uppercase; }}
  .note {{ margin-top:14px; font-size:13px; color:#6B7280; }}
  .back {{ display:inline-block; margin-top:18px; font-size:14px; color:#374151; text-decoration:underline; }}
  button {{ margin-top:24px; width:100%; padding:13px; border:0; border-radius:10px; background:#111827; color:#fff; font-size:16px; font-weight:600; cursor:pointer; }}
</style>
</head>
<body><main>{brandHtml}<h1>{WebUtility.HtmlEncode(title)}</h1><p>{text}</p>{extraHtml}</main></body>
</html>";
        return new ContentResult { Content = html, ContentType = "text/html; charset=utf-8", StatusCode = (int)HttpStatusCode.OK };
    }
}
