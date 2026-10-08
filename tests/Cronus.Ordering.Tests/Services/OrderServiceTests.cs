using Cronus.Ordering.Contracts;
using Cronus.Ordering.Data;
using Cronus.Ordering.Errors;
using Cronus.Ordering.Models;
using Cronus.Ordering.Services;
using Cronus.Ordering.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cronus.Ordering.Tests.Services;

[TestClass]
public sealed class OrderServiceTests
{
    [TestMethod]
    public async Task PlaceOrderAsync_PersistsTheOrderWithItsItems()
    {
        await using var db = await TestDatabase.CreateCleanContextAsync();
        var restaurant = await TestData.AddRestaurantAsync(
            db,
            "Nonna's Pizzeria",
            menuItems: [TestData.MenuItem("Margherita", 9.50m), TestData.MenuItem("Tiramisu", 5.25m)]);

        var cartService = new CartService(db, NullLogger<CartService>.Instance);
        await cartService.AddItemAsync(restaurant.MenuItems[0].Id, 2, CancellationToken.None);
        await cartService.AddItemAsync(restaurant.MenuItems[1].Id, 1, CancellationToken.None);

        var order = await CreateService(db, FakeDeliveryService.Accepting(Guid.NewGuid()))
            .PlaceOrderAsync(NewRequest(), CancellationToken.None);

        await using var verification = TestDatabase.CreateContext();
        var stored = await verification.Orders
            .Include(candidate => candidate.Items)
            .SingleAsync();

        Assert.AreEqual(order.Id, stored.Id);
        Assert.AreEqual("Nonna's Pizzeria", stored.RestaurantName);
        Assert.AreEqual(24.25m, stored.TotalAmount);
        Assert.HasCount(2, stored.Items);
    }

    [TestMethod]
    public async Task PlaceOrderAsync_SnapshotsItemNamesAndPrices_SoALaterMenuEditDoesNotRewriteTheOrder()
    {
        await using var db = await TestDatabase.CreateCleanContextAsync();
        var restaurant = await TestData.AddRestaurantAsync(db, menuItems: [TestData.MenuItem("Margherita", 9.50m)]);

        await new CartService(db, NullLogger<CartService>.Instance).AddItemAsync(restaurant.MenuItems[0].Id, 1, CancellationToken.None);

        var order = await CreateService(db, FakeDeliveryService.Accepting(Guid.NewGuid()))
            .PlaceOrderAsync(NewRequest(), CancellationToken.None);

        var menuItem = await db.MenuItems.SingleAsync();
        menuItem.Name = "Renamed";
        menuItem.Price = 99.00m;
        await db.SaveChangesAsync();

        var storedItem = order.Items.Single();
        Assert.AreEqual("Margherita", storedItem.Name);
        Assert.AreEqual(9.50m, storedItem.UnitPrice);
    }

    [TestMethod]
    public async Task PlaceOrderAsync_ConvertsTheCart_SoItIsNoLongerOpen()
    {
        await using var db = await TestDatabase.CreateCleanContextAsync();
        var restaurant = await TestData.AddRestaurantAsync(db, menuItems: [TestData.MenuItem("Margherita")]);

        var cartService = new CartService(db, NullLogger<CartService>.Instance);
        await cartService.AddItemAsync(restaurant.MenuItems[0].Id, 1, CancellationToken.None);

        await CreateService(db, FakeDeliveryService.Accepting(Guid.NewGuid()))
            .PlaceOrderAsync(NewRequest(), CancellationToken.None);

        Assert.IsNull(
            await cartService.GetOpenCartAsync(CancellationToken.None),
            "the converted cart must not remain the open cart");
    }

    [TestMethod]
    public async Task PlaceOrderAsync_TrimsValues_AndTreatsBlankOptionalsAsAbsent()
    {
        await using var db = await TestDatabase.CreateCleanContextAsync();
        var restaurant = await TestData.AddRestaurantAsync(db, menuItems: [TestData.MenuItem("Margherita")]);
        await new CartService(db, NullLogger<CartService>.Instance).AddItemAsync(restaurant.MenuItems[0].Id, 1, CancellationToken.None);

        var order = await CreateService(db, FakeDeliveryService.Accepting(Guid.NewGuid()))
            .PlaceOrderAsync(
                new CreateOrderRequest("  Ada Lovelace  ", " 1 Analytical Way ", "  ", ""),
                CancellationToken.None);

        Assert.AreEqual("Ada Lovelace", order.CustomerName);
        Assert.AreEqual("1 Analytical Way", order.AddressLine);
        Assert.IsNull(order.City);
        Assert.IsNull(order.PostalCode);
    }

    [TestMethod]
    public async Task PlaceOrderAsync_ThrowsValidation_WhenTheCartIsEmpty()
    {
        await using var db = await TestDatabase.CreateCleanContextAsync();

        var exception = await Assert.ThrowsExactlyAsync<RequestValidationException>(() =>
            CreateService(db, FakeDeliveryService.Accepting(Guid.NewGuid()))
                .PlaceOrderAsync(NewRequest(), CancellationToken.None));

        Assert.IsTrue(exception.Errors.ContainsKey("cart"));
    }

    [TestMethod]
    public async Task PlaceOrderAsync_ConfirmsTheOrder_WhenTheDeliverySucceeds()
    {
        await using var db = await TestDatabase.CreateCleanContextAsync();
        var deliveryId = Guid.NewGuid();
        var restaurant = await TestData.AddRestaurantAsync(db, menuItems: [TestData.MenuItem("Margherita")]);
        await new CartService(db, NullLogger<CartService>.Instance).AddItemAsync(restaurant.MenuItems[0].Id, 1, CancellationToken.None);

        var order = await CreateService(db, FakeDeliveryService.Accepting(deliveryId, "Assigned"))
            .PlaceOrderAsync(NewRequest(), CancellationToken.None);

        Assert.AreEqual(OrderStatus.Confirmed, order.Status);
        Assert.AreEqual(deliveryId, order.DeliveryId);
        Assert.AreEqual("Assigned", order.DeliveryStatus);
        Assert.IsNull(order.DeliveryFailureReason);
    }

    [TestMethod]
    public async Task PlaceOrderAsync_SendsTheDeliveryDetailsToTheDeliveryService()
    {
        await using var db = await TestDatabase.CreateCleanContextAsync();
        var handler = FakeDeliveryService.Accepting(Guid.NewGuid());
        var restaurant = await TestData.AddRestaurantAsync(db, menuItems: [TestData.MenuItem("Margherita")]);
        await new CartService(db, NullLogger<CartService>.Instance).AddItemAsync(restaurant.MenuItems[0].Id, 1, CancellationToken.None);

        var order = await CreateService(db, handler).PlaceOrderAsync(NewRequest(), CancellationToken.None);

        var body = handler.RequestBodies.Single();
        StringAssert.Contains(body, order.Id.ToString());
        StringAssert.Contains(body, "Ada Lovelace");
        StringAssert.Contains(body, "1 Analytical Way");
    }

    [TestMethod]
    public async Task PlaceOrderAsync_KeepsTheOrder_WhenTheDeliveryServiceIsUnreachable()
    {
        await using var db = await TestDatabase.CreateCleanContextAsync();
        var restaurant = await TestData.AddRestaurantAsync(db, menuItems: [TestData.MenuItem("Margherita")]);
        await new CartService(db, NullLogger<CartService>.Instance).AddItemAsync(restaurant.MenuItems[0].Id, 1, CancellationToken.None);

        var order = await CreateService(db, FakeDeliveryService.Unreachable())
            .PlaceOrderAsync(NewRequest(), CancellationToken.None);

        Assert.AreEqual(OrderStatus.Placed, order.Status);
        Assert.IsNull(order.DeliveryId);

        // The order must survive an unavailable delivery service rather than being lost with the request.
        await using var verification = TestDatabase.CreateContext();
        Assert.AreEqual(1, await verification.Orders.CountAsync());
    }

    [TestMethod]
    public async Task PlaceOrderAsync_RecordsTheUserSafeReason_NotTheTransportDetail()
    {
        await using var db = await TestDatabase.CreateCleanContextAsync();
        var restaurant = await TestData.AddRestaurantAsync(db, menuItems: [TestData.MenuItem("Margherita")]);
        await new CartService(db, NullLogger<CartService>.Instance).AddItemAsync(restaurant.MenuItems[0].Id, 1, CancellationToken.None);

        var order = await CreateService(db, FakeDeliveryService.Unreachable())
            .PlaceOrderAsync(NewRequest(), CancellationToken.None);

        var expected = new DeliveryRequestException(DeliveryFailure.Unreachable, "transport detail").UserSafeReason;

        Assert.AreEqual(expected, order.DeliveryFailureReason);
        Assert.DoesNotContain("http://", order.DeliveryFailureReason!);
        Assert.DoesNotContain("delivery.test", order.DeliveryFailureReason!);
    }

    [TestMethod]
    public async Task PlaceOrderAsync_KeepsTheOrder_WhenTheDeliveryServiceRejectsTheRequest()
    {
        await using var db = await TestDatabase.CreateCleanContextAsync();
        var restaurant = await TestData.AddRestaurantAsync(db, menuItems: [TestData.MenuItem("Margherita")]);
        await new CartService(db, NullLogger<CartService>.Instance).AddItemAsync(restaurant.MenuItems[0].Id, 1, CancellationToken.None);

        var order = await CreateService(db, FakeDeliveryService.Rejecting())
            .PlaceOrderAsync(NewRequest(), CancellationToken.None);

        Assert.AreEqual(OrderStatus.Placed, order.Status);
        Assert.IsNotNull(order.DeliveryFailureReason);

        await using var verification = TestDatabase.CreateContext();
        Assert.AreEqual(1, await verification.Orders.CountAsync());
    }

    [TestMethod]
    public async Task GetOrderAsync_ReturnsTheOrderWithItsItems()
    {
        await using var db = await TestDatabase.CreateCleanContextAsync();
        var restaurant = await TestData.AddRestaurantAsync(db, menuItems: [TestData.MenuItem("Margherita")]);
        await new CartService(db, NullLogger<CartService>.Instance).AddItemAsync(restaurant.MenuItems[0].Id, 1, CancellationToken.None);

        var orderService = CreateService(db, FakeDeliveryService.Accepting(Guid.NewGuid()));
        var placed = await orderService.PlaceOrderAsync(NewRequest(), CancellationToken.None);

        var found = await orderService.GetOrderAsync(placed.Id, CancellationToken.None);

        Assert.AreEqual(placed.Id, found.Id);
        Assert.HasCount(1, found.Items);
    }

    [TestMethod]
    public async Task GetOrderAsync_ThrowsNotFound_ForAnUnknownId()
    {
        await using var db = await TestDatabase.CreateCleanContextAsync();

        await Assert.ThrowsExactlyAsync<NotFoundException>(() =>
            CreateService(db, FakeDeliveryService.Accepting(Guid.NewGuid()))
                .GetOrderAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [TestMethod]
    public async Task PlaceOrderAsync_LogsThePlacedOrderAsStructuredProperties()
    {
        await using var db = await TestDatabase.CreateCleanContextAsync();
        var restaurant = await TestData.AddRestaurantAsync(
            db,
            menuItems: [TestData.MenuItem("Margherita", 9.50m)]);

        await new CartService(db, NullLogger<CartService>.Instance)
            .AddItemAsync(restaurant.MenuItems[0].Id, 2, CancellationToken.None);

        var logger = new RecordingLogger<OrderService>();
        var orderService = new OrderService(
            db,
            new CartService(db, NullLogger<CartService>.Instance),
            TestDeliveryClient.Create(FakeDeliveryService.Accepting(Guid.NewGuid())),
            logger);

        var order = await orderService.PlaceOrderAsync(NewRequest(), CancellationToken.None);

        // Both state transitions carry the order id, so the two lines are told apart by the property
        // only one of them has: the placement reports the restaurant and the total, the confirmation
        // reports the delivery.
        var placed = logger.Entries.Single(entry => entry.Properties.ContainsKey("RestaurantId"));
        var confirmed = logger.Entries.Single(entry => entry.Properties.ContainsKey("DeliveryId"));

        Assert.AreEqual(order.Id, (Guid)placed.Properties["OrderId"]!);
        Assert.AreEqual(restaurant.Id, (Guid)placed.Properties["RestaurantId"]!);
        Assert.AreEqual(order.Items.Count, Convert.ToInt32(placed.Properties["ItemCount"]));
        Assert.AreEqual(order.TotalAmount, Convert.ToDecimal(placed.Properties["TotalAmount"]));

        Assert.AreEqual(order.Id, (Guid)confirmed.Properties["OrderId"]!);
        Assert.AreEqual(order.DeliveryId, (Guid)confirmed.Properties["DeliveryId"]!);
        Assert.AreEqual(order.DeliveryStatus, confirmed.Properties["Status"]);
    }

    private static OrderService CreateService(OrderingDbContext db, HttpMessageHandler handler) =>
        new(db, new CartService(db, NullLogger<CartService>.Instance), TestDeliveryClient.Create(handler), NullLogger<OrderService>.Instance);

    private static CreateOrderRequest NewRequest() =>
        new("Ada Lovelace", "1 Analytical Way", "London", "E1 6AN");
}
