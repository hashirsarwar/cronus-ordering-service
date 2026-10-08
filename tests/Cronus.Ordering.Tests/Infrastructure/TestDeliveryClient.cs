using Cronus.Ordering.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cronus.Ordering.Tests.Infrastructure;

/// <summary>
/// Builds a real <see cref="DeliveryClient"/> over a scripted handler, so service-level tests
/// exercise the actual client without going through the application host.
/// </summary>
internal static class TestDeliveryClient
{
    public static DeliveryClient Create(HttpMessageHandler handler) =>
        new(
            new HttpClient(handler) { BaseAddress = new Uri(OrderingApiFactory.DeliveryBaseUrl) },
            NullLogger<DeliveryClient>.Instance);
}
