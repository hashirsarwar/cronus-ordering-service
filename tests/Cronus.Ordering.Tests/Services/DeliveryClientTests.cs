using System.Net;
using Cronus.Ordering.Services;
using Cronus.Ordering.Tests.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cronus.Ordering.Tests.Services;

[TestClass]
public sealed class DeliveryClientTests
{
    private static readonly Guid OrderId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [TestMethod]
    public async Task CreateDeliveryAsync_PostsTheOrderToTheDeliveriesEndpoint()
    {
        var deliveryId = Guid.NewGuid();
        var handler = FakeDeliveryService.Accepting(deliveryId);

        await TestDeliveryClient.Create(handler).CreateDeliveryAsync(Request(), CancellationToken.None);

        Assert.AreEqual("/deliveries", handler.RequestUris.Single().AbsolutePath);
        StringAssert.Contains(handler.RequestBodies.Single(), OrderId.ToString());
        StringAssert.Contains(handler.RequestBodies.Single(), "Ada Lovelace");
    }

    [TestMethod]
    public async Task CreateDeliveryAsync_ReturnsTheDeliveryReference()
    {
        var deliveryId = Guid.NewGuid();

        var delivery = await TestDeliveryClient
            .Create(FakeDeliveryService.Accepting(deliveryId, "Assigned"))
            .CreateDeliveryAsync(Request(), CancellationToken.None);

        Assert.AreEqual(deliveryId, delivery.Id);
        Assert.AreEqual("Assigned", delivery.Status);
    }

    [TestMethod]
    public async Task CreateDeliveryAsync_ReportsUnreachable_WhenTheTransportFails()
    {
        var exception = await Assert.ThrowsExactlyAsync<DeliveryRequestException>(() =>
            TestDeliveryClient.Create(FakeDeliveryService.Unreachable())
                .CreateDeliveryAsync(Request(), CancellationToken.None));

        Assert.AreEqual(DeliveryFailure.Unreachable, exception.Failure);
    }

    [TestMethod]
    public async Task CreateDeliveryAsync_ReportsRejected_WhenTheDeliveryServiceAnswersWithAnError()
    {
        var exception = await Assert.ThrowsExactlyAsync<DeliveryRequestException>(() =>
            TestDeliveryClient.Create(FakeDeliveryService.Rejecting(HttpStatusCode.ServiceUnavailable))
                .CreateDeliveryAsync(Request(), CancellationToken.None));

        Assert.AreEqual(DeliveryFailure.Rejected, exception.Failure);
    }

    [TestMethod]
    public async Task CreateDeliveryAsync_ReportsInvalidResponse_WhenTheBodyCannotBeRead()
    {
        var exception = await Assert.ThrowsExactlyAsync<DeliveryRequestException>(() =>
            TestDeliveryClient.Create(FakeDeliveryService.RespondingWithMalformedBody())
                .CreateDeliveryAsync(Request(), CancellationToken.None));

        Assert.AreEqual(DeliveryFailure.InvalidResponse, exception.Failure);
    }

    [TestMethod]
    public async Task CreateDeliveryAsync_ReportsTimeout_WhenTheCallExceedsTheDeadline()
    {
        // A cancelled caller token is a caller decision, so the client must not misreport it as a
        // timeout; this test covers the client's own deadline elapsing.
        var handler = new StubHttpMessageHandler(_ => throw new TaskCanceledException("timed out"));

        var exception = await Assert.ThrowsExactlyAsync<DeliveryRequestException>(() =>
            TestDeliveryClient.Create(handler).CreateDeliveryAsync(Request(), CancellationToken.None));

        Assert.AreEqual(DeliveryFailure.Timeout, exception.Failure);
    }

    [TestMethod]
    public void UserSafeReason_DoesNotLeakTechnicalDetail()
    {
        // The reason is persisted and shown to customers, so it must never carry the internal address
        // or the exception message.
        foreach (var failure in Enum.GetValues<DeliveryFailure>())
        {
            var exception = new DeliveryRequestException(failure, $"transport detail for {failure}");
            var reason = exception.UserSafeReason;

            Assert.DoesNotContain("http://", reason, $"the reason for {failure} must not expose a URL");
            Assert.DoesNotContain("delivery.test", reason, $"the reason for {failure} must not expose a host");
            Assert.DoesNotContain(failure.ToString(), reason, $"the reason for {failure} must be human readable");
        }
    }

    private static DeliveryRequest Request() =>
        new(OrderId, "Ada Lovelace", "1 Analytical Way", "London", "E1 6AN");
}
