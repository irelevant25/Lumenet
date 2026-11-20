using System.Net;
using Microsoft.AspNetCore.Http;

namespace DmxBox.Server;

/// <summary>
/// Stands in front of everything the server serves, for requests from other devices in the
/// network: without the LAN password they get the page that asks for it, and nothing else.
/// Requests from this PC pass untouched. See <see cref="LanAccess"/>.
/// </summary>
internal static class LanGate
{
    /// <summary>Where the password page sends the password to.</summary>
    public const string LoginPath = "/lan/login";

    // The cookie lasts as long as a browser lets a cookie last (400 days in Chrome). The device
    // keeps it while LAN is off, and gets in again once it is on.
    private static readonly TimeSpan CookieLifetime = TimeSpan.FromDays(400);

    /// <summary>Handles one request.</summary>
    /// <param name="context">The request.</param>
    /// <param name="lan">Whether LAN is on, and the password.</param>
    /// <param name="next">What comes after this gate.</param>
    public static async Task InvokeAsync(HttpContext context, LanAccess lan, RequestDelegate next)
    {
        IPAddress? remote = context.Connection.RemoteIpAddress;
        if (lan.IsLocal(remote))
        {
            await next(context);
            return;
        }

        // LAN is off, or the device came in at an address of this PC other than the one chosen
        // for LAN: it gets nothing at all. Its connection is normally refused when it is made;
        // this is for one that was opened while LAN was still on, or before another address was chosen.
        if (!lan.MayConnect(remote, context.Connection.LocalIpAddress))
        {
            context.Abort();
            return;
        }

        HttpRequest request = context.Request;
        if (request.Path.Equals(LoginPath, StringComparison.OrdinalIgnoreCase))
        {
            await LogInAsync(context, lan, remote);
            return;
        }

        if (lan.AcceptsCookie(request.Cookies[LanAccess.CookieName]))
        {
            await next(context);
            return;
        }

        await AskAsync(context, back: request.Path + request.QueryString, problem: null, StatusCodes.Status401Unauthorized);
    }

    private static async Task LogInAsync(HttpContext context, LanAccess lan, IPAddress? remote)
    {
        HttpRequest request = context.Request;
        if (!HttpMethods.IsPost(request.Method) || !request.HasFormContentType)
        {
            await AskAsync(context, back: "/", problem: null, StatusCodes.Status401Unauthorized);
            return;
        }

        IFormCollection form = await request.ReadFormAsync(context.RequestAborted);
        string back = SafeBack(form["back"].ToString());
        switch (lan.LogIn(remote, form["password"].ToString()))
        {
            case LanAccess.Answer.Welcome:
                context.Response.Cookies.Append(LanAccess.CookieName, lan.CookieValue, new CookieOptions
                {
                    HttpOnly = true,                 // the pages' scripts never see it
                    SameSite = SameSiteMode.Strict,  // and no other site's page can send it along
                    Path = "/",
                    MaxAge = CookieLifetime,
                    IsEssential = true,
                });

                // "See other": the browser then asks for the page it wanted, with GET.
                context.Response.StatusCode = StatusCodes.Status303SeeOther;
                context.Response.Headers.Location = back;
                return;

            case LanAccess.Answer.TooMany:
                await AskAsync(
                    context,
                    back,
                    "Too many wrong passwords. Wait a minute, then try again.",
                    StatusCodes.Status429TooManyRequests);
                return;

            default:
                await AskAsync(context, back, "That is not the password.", StatusCodes.Status401Unauthorized);
                return;
        }
    }

    // The page that asks for the password, for a browser that asks for a page. Any other
    // request (the hub, a script, the API) only gets the status.
    private static async Task AskAsync(HttpContext context, string back, string? problem, int status)
    {
        HttpResponse response = context.Response;
        response.StatusCode = status;
        response.Headers.CacheControl = "no-store";

        HttpRequest request = context.Request;
        bool wantsPage = (HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method) || request.Path == LoginPath)
            && !request.Path.StartsWithSegments("/hub")
            && !request.Path.StartsWithSegments("/api");
        if (!wantsPage)
        {
            response.ContentType = "text/plain; charset=utf-8";
            await response.WriteAsync("The desk asks for the LAN password.");
            return;
        }

        response.ContentType = "text/html; charset=utf-8";
        await response.WriteAsync(LanPage.Ask(back, problem));
    }

    // Where to go after the password: a path on this server, nothing else. "//other.example"
    // would lead to another site, and so would "/\other.example" and "/<tab>/other.example":
    // browsers read a backslash as a slash and drop tabs and line breaks from an address. So a
    // backslash, a control character or anything else that is not a plain address sends the
    // device to the start page instead.
    private static string SafeBack(string back)
    {
        bool onThisServer = back.StartsWith('/')
            && !back.StartsWith("//", StringComparison.Ordinal)
            && !back.Contains('\\')
            && !back.Any(char.IsControl)
            && Uri.IsWellFormedUriString(back, UriKind.Relative);
        return onThisServer ? back : "/";
    }
}
