using System.Net.Http;
using MudBlazor;
using Novels.Data;

namespace Novels.Services;
public static class HttpClientHelper {

    /// <summary>クッキー付きでGetAsync</summary>
    public static async Task<HttpResponseMessage> GetWithCookiesAsync (this HttpClient client, string url, IEnumerable<KeyValuePair<string, string>> cookies) {
        var request = new HttpRequestMessage (HttpMethod.Get, url);
        request.Headers.Add ("Cookie", string.Join ("; ", cookies.Select (c => $"{c.Key}={c.Value}")));
        return await client.SendAsync (request);
    }

}
