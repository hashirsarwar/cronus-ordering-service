namespace Cronus.Ordering.Tests.Infrastructure;

/// <summary>
/// A scripted <see cref="HttpMessageHandler"/> that stands in for the delivery service. It records
/// every outbound request, so a test can assert on what the ordering service actually sent rather
/// than only on the resulting state.
/// </summary>
internal sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
    : HttpMessageHandler
{
    public List<Uri> RequestUris { get; } = [];

    public List<string> RequestBodies { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        RequestUris.Add(request.RequestUri!);
        RequestBodies.Add(request.Content is null
            ? string.Empty
            : await request.Content.ReadAsStringAsync(cancellationToken));

        return respond(request);
    }
}
