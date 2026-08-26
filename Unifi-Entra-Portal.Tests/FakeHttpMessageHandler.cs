using System.Net;

namespace Unifi_Entra_Portal.Tests;

/// <summary>
/// One HTTP request captured by <see cref="FakeHttpMessageHandler"/>, for
/// asserting on what a service under test actually sent.
/// </summary>
public record CapturedRequest(HttpMethod Method, string? Host, string? Path, string? Body, string? CsrfTokenHeader, string? ApiKeyHeader);

/// <summary>
/// Stand-in <see cref="HttpMessageHandler"/> that records every request it
/// receives and replays a fixed, ordered queue of responses, so services
/// that call out over HTTP (like <c>UniFiClientService</c>) can be unit
/// tested without a real server.
/// </summary>
public class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<HttpResponseMessage> _responses;

    public List<CapturedRequest> Requests { get; } = [];

    public FakeHttpMessageHandler(params HttpResponseMessage[] responses)
    {
        _responses = new Queue<HttpResponseMessage>(responses);
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        var csrfToken = request.Headers.TryGetValues("X-Csrf-Token", out var csrfValues) ? csrfValues.FirstOrDefault() : null;
        var apiKey = request.Headers.TryGetValues("X-API-Key", out var apiKeyValues) ? apiKeyValues.FirstOrDefault() : null;
        Requests.Add(new CapturedRequest(
            request.Method,
            request.RequestUri?.Host,
            request.RequestUri?.PathAndQuery,
            body,
            csrfToken,
            apiKey));

        return _responses.Count > 0 ? _responses.Dequeue() : new HttpResponseMessage(HttpStatusCode.OK);
    }
}
