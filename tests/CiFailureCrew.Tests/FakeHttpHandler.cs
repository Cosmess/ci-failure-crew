using System.Net;

namespace CiFailureCrew.Tests;

internal sealed record RecordedRequest(HttpMethod Method, string Url, string? Accept, string? Body);

/// <summary>Records every request and answers with the configured function.</summary>
internal sealed class FakeHttpHandler(Func<RecordedRequest, HttpResponseMessage> respond) : HttpMessageHandler
{
    public List<RecordedRequest> Requests { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var recorded = new RecordedRequest(
            request.Method,
            request.RequestUri!.ToString(),
            request.Headers.Accept.ToString(),
            request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken));
        Requests.Add(recorded);
        return respond(recorded);
    }

    public static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") };
}
