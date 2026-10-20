using System.Net;
using System.Text.RegularExpressions;

namespace PetWalker.Tests.Infrastructure;

// A browser-like client: keeps the sign-in cookie, follows redirects and sends the anti-forgery token
// from the page the form lives on, exactly like a real form post.
public partial class TestClient(HttpClient http)
{
    public HttpClient Http => http;

    public async Task<Page> GetAsync(string url)
    {
        var response = await http.GetAsync(url);
        return new Page(response, await response.Content.ReadAsStringAsync());
    }

    public async Task<Page> PostAsync(string url, IDictionary<string, string>? fields = null, string? formPage = null)
    {
        var token = await TokenAsync(formPage ?? url);
        var data = new Dictionary<string, string>(fields ?? new Dictionary<string, string>())
        {
            ["__RequestVerificationToken"] = token,
        };
        var response = await http.PostAsync(url, new FormUrlEncodedContent(data));
        return new Page(response, await response.Content.ReadAsStringAsync());
    }

    // Any page with a form (the sign-out form is on every page) carries a valid token.
    private async Task<string> TokenAsync(string url)
    {
        var html = await http.GetStringAsync(url);
        var match = TokenRegex().Match(html);
        if (!match.Success)
            throw new InvalidOperationException($"No anti-forgery token on {url}");
        return match.Groups[1].Value;
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex TokenRegex();
}

public record Page(HttpResponseMessage Response, string RawHtml)
{
    public HttpStatusCode Status => Response.StatusCode;

    // Final path after redirects, e.g. "/Account/Profile".
    public string Path => Response.RequestMessage!.RequestUri!.AbsolutePath;

    public string Query => Response.RequestMessage!.RequestUri!.Query;

    // Decoded HTML, so Turkish characters can be searched as plain text.
    public string Html { get; } = WebUtility.HtmlDecode(RawHtml);

    public bool Contains(string text) => Html.Contains(text, StringComparison.Ordinal);
}
