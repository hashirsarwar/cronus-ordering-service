using System.Net;
using System.Text;
using System.Text.Json;

namespace Cronus.Ordering.Tests.Infrastructure;

/// <summary>
/// Scripted responses standing in for the delivery service, covering each way the call can turn out.
/// </summary>
internal static class FakeDeliveryService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Accepts the delivery and reports it as created, which is the success path.</summary>
    public static StubHttpMessageHandler Accepting(Guid deliveryId, string status = "Pending") =>
        new(_ => Json(HttpStatusCode.Created, new { id = deliveryId, status }));

    /// <summary>Answers with a non-success status, as when the request is rejected.</summary>
    public static StubHttpMessageHandler Rejecting(HttpStatusCode statusCode = HttpStatusCode.InternalServerError) =>
        new(_ => new HttpResponseMessage(statusCode) { Content = new StringContent("upstream failure") });

    /// <summary>Fails at the transport level, as when nothing is listening.</summary>
    public static StubHttpMessageHandler Unreachable() =>
        new(_ => throw new HttpRequestException("No connection could be made because the target machine actively refused it."));

    /// <summary>Reports success but sends a body that cannot be deserialized.</summary>
    public static StubHttpMessageHandler RespondingWithMalformedBody() =>
        new(_ => new HttpResponseMessage(HttpStatusCode.Created)
        {
            Content = new StringContent("not json", Encoding.UTF8, "application/json"),
        });

    private static HttpResponseMessage Json(HttpStatusCode statusCode, object body) => new(statusCode)
    {
        Content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json"),
    };
}
