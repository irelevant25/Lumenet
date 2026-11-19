using System.Net;

namespace DmxBox.Server;

/// <summary>
/// The page another device gets before the desk: one field for the LAN password. It stands
/// on its own, without the files of the UI, and looks like the desk.
/// </summary>
internal static class LanPage
{
    /// <summary>The page.</summary>
    /// <param name="back">The address the device asked for; it goes there after the right password.</param>
    /// <param name="problem">Why the last try did not work; null on the first visit.</param>
    public static string Ask(string back, string? problem)
    {
        string problemLine = problem is null
            ? ""
            : $"""<p class="problem" role="alert">{WebUtility.HtmlEncode(problem)}</p>""";

        return $$"""
            <!doctype html>
            <html lang="en">
            <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <meta name="color-scheme" content="dark">
            <title>Lumenet</title>
            <style>
              html, body { height: 100%; margin: 0; }
              body {
                display: grid;
                place-items: center;
                background: #1c1f24;
                color: #d3d8df;
                font-family: Bahnschrift, 'DIN Alternate', 'Roboto Condensed', 'Arial Narrow', system-ui, sans-serif;
              }
              main {
                box-sizing: border-box;
                width: min(340px, calc(100vw - 32px));
                padding: 20px;
                border: 1px solid #363c46;
                border-radius: 6px;
                background: #23272e;
              }
              form { display: grid; gap: 10px; }
              h1 { margin: 0 0 6px; font-size: 24px; font-weight: 600; letter-spacing: 0.02em; }
              label {
                color: #8b93a1;
                font-size: 12px;
                font-weight: 600;
                letter-spacing: 0.06em;
                text-transform: uppercase;
              }
              input {
                box-sizing: border-box;
                width: 100%;
                height: 52px;
                padding: 0 12px;
                border: 1px solid #363c46;
                border-radius: 3px;
                background: #0e1013;
                color: #fff;
                font: inherit;
                font-size: 26px;
                letter-spacing: 0.25em;
                text-transform: uppercase;
              }
              input:focus { outline: 2px solid #8ab4ff; outline-offset: 1px; }
              button {
                height: 46px;
                margin-top: 4px;
                border: 1px solid #ffb020;
                border-radius: 3px;
                background: #ffb020;
                color: #0b0d10;
                font: inherit;
                font-size: 15px;
                font-weight: 600;
                letter-spacing: 0.06em;
                text-transform: uppercase;
                cursor: pointer;
              }
              button:focus-visible { outline: 2px solid #8ab4ff; outline-offset: 2px; }
              .problem { margin: 0; color: #ff7a70; font-size: 14px; }
              .hint { margin: 14px 0 0; color: #8b93a1; font-size: 13px; line-height: 1.35; }
            </style>
            </head>
            <body>
            <main>
            <form method="post" action="{{LanGate.LoginPath}}">
            <h1>Lumenet</h1>
            <label for="password">Password</label>
            <input id="password" name="password" type="text" autocomplete="off" autocapitalize="characters"
              spellcheck="false" maxlength="12" required autofocus>
            <input type="hidden" name="back" value="{{WebUtility.HtmlEncode(back)}}">
            {{problemLine}}
            <button type="submit">Open the desk</button>
            </form>
            <p class="hint">The password stands on the PC, in the top bar, while LAN is lit.</p>
            </main>
            </body>
            </html>
            """;
    }
}
