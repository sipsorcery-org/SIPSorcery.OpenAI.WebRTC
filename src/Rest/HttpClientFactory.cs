//-----------------------------------------------------------------------------
// Filename: HttpClientFactory.cs
//
// Description: HTTP client factory for use in non-dependency injection scenarios.
//
// Author(s):
// Aaron Clauson (aaron@sipsorcery.com)
// 
// History:
// 26 May 2025  Aaron Clauson   Created, Dublin, Ireland.
//
// License: 
// BSD 3-Clause "New" or "Revised" License and the additional
// BDS BY-NC-SA restriction, see included LICENSE.md file.
//-----------------------------------------------------------------------------

using System.Collections.Concurrent;
using System.Net.Http;
using System.Net.Http.Headers;

namespace SIPSorcery.OpenAI.WebRTC;

public class HttpClientFactory : IHttpClientFactory
{
    private readonly string _openAiKey;
    private readonly ConcurrentDictionary<string, HttpClient> _clients
        = new ConcurrentDictionary<string, HttpClient>();

    public HttpClientFactory(string openAiKey)
    {
        _openAiKey = openAiKey;
    }

    public HttpClient CreateClient(string name)
    {
        return _clients.GetOrAdd(name, _ =>
        {
            var client = new HttpClient();

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _openAiKey);

            return client;
        });
    }
}
