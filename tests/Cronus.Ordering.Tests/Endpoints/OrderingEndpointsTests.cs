using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cronus.Ordering.Models;
using Cronus.Ordering.Tests.Infrastructure;

namespace Cronus.Ordering.Tests.Endpoints;

/// <summary>
/// Exercises the HTTP surface through the real application host, so routing, validation, status code
/// selection and serialization are all covered. Tests reset the database first because the cart is
/// inherently a single shared piece of state.
/// </summary>
[TestClass]
public sealed class OrderingEndpointsTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [TestMethod]
    public async Task GetRestaurants_ReturnsOnlyTheActiveCatalogue()
    {
        await TestDatabase.ResetAsync();
        await ArrangeRestaurantAsync("Zebra Grill");
        await ArrangeRestaurantAsync("Alpha Cafe");
        await ArrangeRestaurantAsync("Closed Kitchen", isActive: false);

        using var factory = NewFactory();
        using var client = factory.CreateClient();

        var restaurants = await client.GetFromJsonAsync<List<RestaurantBody>>("/restaurants", JsonOptions);

        Assert.IsNotNull(restaurants);
        CollectionAssert.AreEqual(
            new[] { "Alpha Cafe", "Zebra Grill" },
            restaurants.Select(restaurant => restaurant.Name).ToArray());
    }

    [TestMethod]
    public async Task GetMenu_ReturnsNotFound_ForAnUnknownRestaurant()
    {
        await TestDatabase.ResetAsync();

        using var factory = NewFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/restaurants/{Guid.NewGuid()}/menu");

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    [TestMethod]
    public async Task GetCart_ReturnsNoContent_WhenThereIsNoOpenCart()
    {
        await TestDatabase.ResetAsync();

        using var factory = NewFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/cart");

        Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);
    }

    [TestMethod]
    public async Task PostCartItems_ReturnsTheUpdatedCart()
    {
        await TestDatabase.ResetAsync();
        var restaurant = await ArrangeRestaurantAsync("Test Kitchen", menuItems: [TestData.MenuItem("Margherita", 9.50m)]);

        using var factory = NewFactory();
        using var client = factory.CreateClient();

        var cart = await AddToCartAsync(client, restaurant.MenuItems[0].Id, 2);

        Assert.AreEqual(19.00m, cart.Total);
        Assert.AreEqual("Test Kitchen", cart.RestaurantName);
        Assert.HasCount(1, cart.Items);
    }

    [TestMethod]
    public async Task PostCartItems_ReturnsAValidationProblem_ForAQuantityOutOfRange()
    {
        await TestDatabase.ResetAsync();
        var restaurant = await ArrangeRestaurantAsync("Test Kitchen", menuItems: [TestData.MenuItem("Margherita")]);

        using var factory = NewFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/cart/items",
            new { menuItemId = restaurant.MenuItems[0].Id, quantity = 0 },
            JsonOptions);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemBody>(JsonOptions);
        Assert.IsNotNull(problem?.Errors);
        Assert.IsTrue(problem.Errors.ContainsKey("quantity"));
    }

    [TestMethod]
    public async Task PostCartItems_ReturnsNotFound_ForAnUnknownMenuItem()
    {
        await TestDatabase.ResetAsync();

        using var factory = NewFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/cart/items",
            new { menuItemId = Guid.NewGuid(), quantity = 1 },
            JsonOptions);

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    [TestMethod]
    public async Task PostCartItems_ReturnsConflict_WhenTheCartAlreadyHoldsAnotherRestaurant()
    {
        await TestDatabase.ResetAsync();
        var pizzeria = await ArrangeRestaurantAsync("Pizzeria", menuItems: [TestData.MenuItem("Margherita")]);
        var sushi = await ArrangeRestaurantAsync("Sushi", menuItems: [TestData.MenuItem("Nigiri")]);

        using var factory = NewFactory();
        using var client = factory.CreateClient();
        await AddToCartAsync(client, pizzeria.MenuItems[0].Id, 1);

        var response = await client.PostAsJsonAsync(
            "/cart/items",
            new { menuItemId = sushi.MenuItems[0].Id, quantity = 1 },
            JsonOptions);

        Assert.AreEqual(HttpStatusCode.Conflict, response.StatusCode);
    }

    [TestMethod]
    public async Task DeleteCartItem_ReturnsNoContent_WhenTheCartBecomesEmpty()
    {
        await TestDatabase.ResetAsync();
        var restaurant = await ArrangeRestaurantAsync("Test Kitchen", menuItems: [TestData.MenuItem("Margherita")]);

        using var factory = NewFactory();
        using var client = factory.CreateClient();
        var cart = await AddToCartAsync(client, restaurant.MenuItems[0].Id, 1);

        var response = await client.DeleteAsync($"/cart/items/{cart.Items[0].Id}");

        Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);
        Assert.AreEqual(HttpStatusCode.NoContent, (await client.GetAsync("/cart")).StatusCode);
    }

    [TestMethod]
    public async Task PostOrders_ReturnsCreated_WithTheLinkedDelivery_WhenTheDeliverySucceeds()
    {
        await TestDatabase.ResetAsync();
        var deliveryId = Guid.NewGuid();
        var restaurant = await ArrangeRestaurantAsync("Test Kitchen", menuItems: [TestData.MenuItem("Margherita", 9.50m)]);

        using var factory = NewFactory(FakeDeliveryService.Accepting(deliveryId, "Assigned"));
        using var client = factory.CreateClient();
        await AddToCartAsync(client, restaurant.MenuItems[0].Id, 2);

        var response = await client.PostAsJsonAsync("/orders", NewOrderRequest(), JsonOptions);

        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);

        var order = await response.Content.ReadFromJsonAsync<OrderBody>(JsonOptions);
        Assert.IsNotNull(order);
        Assert.AreEqual("Confirmed", order.Status);
        Assert.AreEqual(19.00m, order.TotalAmount);
        Assert.IsNotNull(order.Delivery);
        Assert.AreEqual(deliveryId, order.Delivery.Id);
        Assert.AreEqual("Assigned", order.Delivery.Status);
        Assert.AreEqual($"/orders/{order.Id}", response.Headers.Location?.OriginalString);
    }

    [TestMethod]
    public async Task PostOrders_StillReturnsCreated_WhenTheDeliveryServiceIsUnavailable()
    {
        await TestDatabase.ResetAsync();
        var restaurant = await ArrangeRestaurantAsync("Test Kitchen", menuItems: [TestData.MenuItem("Margherita")]);

        using var factory = NewFactory(FakeDeliveryService.Unreachable());
        using var client = factory.CreateClient();
        await AddToCartAsync(client, restaurant.MenuItems[0].Id, 1);

        var response = await client.PostAsJsonAsync("/orders", NewOrderRequest(), JsonOptions);

        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);

        var order = await response.Content.ReadFromJsonAsync<OrderBody>(JsonOptions);
        Assert.IsNotNull(order);
        Assert.AreEqual("Placed", order.Status);
        Assert.IsNull(order.Delivery);
        Assert.IsNotNull(order.DeliveryFailureReason);
        Assert.DoesNotContain("http://", order.DeliveryFailureReason);
    }

    [TestMethod]
    public async Task PostOrders_ReturnsAValidationProblem_WhenTheCartIsEmpty()
    {
        await TestDatabase.ResetAsync();

        using var factory = NewFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/orders", NewOrderRequest(), JsonOptions);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemBody>(JsonOptions);
        Assert.IsNotNull(problem?.Errors);
        Assert.IsTrue(problem.Errors.ContainsKey("cart"));
    }

    [TestMethod]
    public async Task GetOrder_ReturnsNotFound_ForAnUnknownId()
    {
        await TestDatabase.ResetAsync();

        using var factory = NewFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/orders/{Guid.NewGuid()}");

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    [TestMethod]
    public async Task Liveness_ReportsHealthy_AndRunsNoChecks()
    {
        await TestDatabase.ResetAsync();

        using var factory = NewFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/live");
        var body = await response.Content.ReadFromJsonAsync<HealthBody>(JsonOptions);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.IsNotNull(body);
        Assert.AreEqual("Healthy", body.Status);

        // An empty check list is the point: liveness must not depend on anything downstream.
        Assert.HasCount(0, body.Checks);
    }

    [TestMethod]
    public async Task Readiness_ReportsHealthy_WhenTheDatabaseIsReachable()
    {
        await TestDatabase.ResetAsync();

        using var factory = NewFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/ready");
        var body = await response.Content.ReadFromJsonAsync<HealthBody>(JsonOptions);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.IsNotNull(body);
        Assert.AreEqual("Healthy", body.Status);
        Assert.AreEqual("Healthy", body.Checks["postgresql"].Status);
    }

    [TestMethod]
    public async Task Liveness_StaysHealthy_WhenTheDatabaseIsUnreachable()
    {
        // The reason the probes are split: a database outage must not make an otherwise healthy
        // process look dead, because the orchestrator would restart it in a loop to no effect.
        using var factory = OrderingApiFactory.WithUnreachableDatabase();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/live");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [TestMethod]
    public async Task Readiness_ReportsUnhealthy_WhenTheDatabaseIsUnreachable()
    {
        using var factory = OrderingApiFactory.WithUnreachableDatabase();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/ready");
        var body = await response.Content.ReadFromJsonAsync<HealthBody>(JsonOptions);

        Assert.AreEqual(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.IsNotNull(body);
        Assert.AreEqual("Unhealthy", body.Status);
        Assert.AreEqual("Unhealthy", body.Checks["postgresql"].Status);

        // The detail is returned to callers, so it must not describe where the database lives.
        Assert.DoesNotContain("127.0.0.1", body.Checks["postgresql"].Description ?? string.Empty);
        Assert.DoesNotContain("Port=1", body.Checks["postgresql"].Description ?? string.Empty);
    }

    private static OrderingApiFactory NewFactory(HttpMessageHandler? deliveryHandler = null) =>
        new(deliveryHandler ?? FakeDeliveryService.Accepting(Guid.NewGuid()));

    private static async Task<Restaurant> ArrangeRestaurantAsync(
        string name,
        bool isActive = true,
        params MenuItem[] menuItems)
    {
        await using var db = TestDatabase.CreateContext();
        return await TestData.AddRestaurantAsync(db, name, isActive, menuItems);
    }

    private static async Task<CartBody> AddToCartAsync(HttpClient client, Guid menuItemId, int quantity)
    {
        var response = await client.PostAsJsonAsync("/cart/items", new { menuItemId, quantity }, JsonOptions);
        response.EnsureSuccessStatusCode();

        var cart = await response.Content.ReadFromJsonAsync<CartBody>(JsonOptions);
        Assert.IsNotNull(cart);
        return cart;
    }

    private static object NewOrderRequest() => new
    {
        customerName = "Ada Lovelace",
        addressLine = "1 Analytical Way",
        city = "London",
        postalCode = "E1 6AN",
    };

    private sealed record RestaurantBody(Guid Id, string Name, string? Description, string AddressLine, string City);

    private sealed record CartBody(
        Guid Id,
        Guid? RestaurantId,
        string? RestaurantName,
        string Status,
        List<CartLineBody> Items,
        decimal Total);

    private sealed record CartLineBody(
        Guid Id,
        Guid MenuItemId,
        string Name,
        decimal UnitPrice,
        int Quantity,
        decimal LineTotal);

    private sealed record OrderBody(
        Guid Id,
        string Status,
        decimal TotalAmount,
        List<OrderLineBody> Items,
        DeliveryBody? Delivery,
        string? DeliveryFailureReason);

    private sealed record OrderLineBody(Guid MenuItemId, string Name, decimal UnitPrice, int Quantity, decimal LineTotal);

    private sealed record DeliveryBody(Guid Id, string? Status);

    private sealed record ValidationProblemBody(
        string? Title,
        int? Status,
        Dictionary<string, string[]>? Errors);

    private sealed record HealthBody(string Status, Dictionary<string, HealthCheckBody> Checks);

    private sealed record HealthCheckBody(string Status, string? Description);
}
