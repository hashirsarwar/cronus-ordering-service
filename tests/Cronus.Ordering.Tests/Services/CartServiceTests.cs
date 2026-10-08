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
public sealed class CartServiceTests
{
    [TestMethod]
    public async Task GetOpenCartAsync_ReturnsNull_WhenThereIsNoCart()
    {
        await using var db = await TestDatabase.CreateCleanContextAsync();

        Assert.IsNull(await new CartService(db, NullLogger<CartService>.Instance).GetOpenCartAsync(CancellationToken.None));
    }

    [TestMethod]
    public async Task AddItemAsync_CreatesTheCart_AndLocksItToTheRestaurant()
    {
        await using var db = await TestDatabase.CreateCleanContextAsync();
        var restaurant = await TestData.AddRestaurantAsync(
            db,
            menuItems: [TestData.MenuItem("Margherita", 9.50m)]);

        var cart = await new CartService(db, NullLogger<CartService>.Instance).AddItemAsync(restaurant.MenuItems[0].Id, 2, CancellationToken.None);

        Assert.AreEqual(restaurant.Id, cart.RestaurantId);
        Assert.AreEqual(19.00m, cart.Total);
        await AssertStoredQuantityAsync(2);
    }

    [TestMethod]
    public async Task AddItemAsync_MergesQuantities_ForTheSameMenuItem()
    {
        await using var db = await TestDatabase.CreateCleanContextAsync();
        var restaurant = await TestData.AddRestaurantAsync(db, menuItems: [TestData.MenuItem("Margherita")]);
        var menuItemId = restaurant.MenuItems[0].Id;
        var cartService = new CartService(db, NullLogger<CartService>.Instance);

        await cartService.AddItemAsync(menuItemId, 2, CancellationToken.None);
        await cartService.AddItemAsync(menuItemId, 3, CancellationToken.None);

        await using var verification = TestDatabase.CreateContext();
        Assert.AreEqual(1, await verification.CartItems.CountAsync(), "the line should be merged, not duplicated");
        Assert.AreEqual(5, (await verification.CartItems.SingleAsync()).Quantity);
    }

    [TestMethod]
    public async Task AddItemAsync_SnapshotsTheMenuPrice_SoALaterMenuEditDoesNotChangeTheCart()
    {
        await using var db = await TestDatabase.CreateCleanContextAsync();
        var restaurant = await TestData.AddRestaurantAsync(db, menuItems: [TestData.MenuItem("Margherita", 9.50m)]);
        var menuItemId = restaurant.MenuItems[0].Id;

        await new CartService(db, NullLogger<CartService>.Instance).AddItemAsync(menuItemId, 2, CancellationToken.None);

        var menuItem = await db.MenuItems.SingleAsync(item => item.Id == menuItemId);
        menuItem.Price = 12.00m;
        await db.SaveChangesAsync();

        await using var verification = TestDatabase.CreateContext();
        var cartItem = await verification.CartItems.SingleAsync();
        Assert.AreEqual(9.50m, cartItem.UnitPrice);
        Assert.AreEqual(19.00m, cartItem.LineTotal);
    }

    [TestMethod]
    public async Task AddItemAsync_ThrowsNotFound_ForAnUnknownMenuItem()
    {
        await using var db = await TestDatabase.CreateCleanContextAsync();

        await Assert.ThrowsExactlyAsync<NotFoundException>(() =>
            new CartService(db, NullLogger<CartService>.Instance).AddItemAsync(Guid.NewGuid(), 1, CancellationToken.None));
    }

    [TestMethod]
    public async Task AddItemAsync_ThrowsConflict_WhenTheItemIsUnavailable()
    {
        await using var db = await TestDatabase.CreateCleanContextAsync();
        var restaurant = await TestData.AddRestaurantAsync(
            db,
            menuItems: [TestData.MenuItem("Sold Out", isAvailable: false)]);

        await Assert.ThrowsExactlyAsync<ConflictException>(() =>
            new CartService(db, NullLogger<CartService>.Instance).AddItemAsync(restaurant.MenuItems[0].Id, 1, CancellationToken.None));
    }

    [TestMethod]
    public async Task AddItemAsync_ThrowsConflict_WhenTheCartAlreadyHoldsAnotherRestaurant()
    {
        await using var db = await TestDatabase.CreateCleanContextAsync();
        var pizzeria = await TestData.AddRestaurantAsync(db, "Pizzeria", menuItems: [TestData.MenuItem("Margherita")]);
        var sushi = await TestData.AddRestaurantAsync(db, "Sushi", menuItems: [TestData.MenuItem("Nigiri")]);
        var cartService = new CartService(db, NullLogger<CartService>.Instance);

        await cartService.AddItemAsync(pizzeria.MenuItems[0].Id, 1, CancellationToken.None);

        await Assert.ThrowsExactlyAsync<ConflictException>(() =>
            cartService.AddItemAsync(sushi.MenuItems[0].Id, 1, CancellationToken.None));
    }

    [TestMethod]
    public async Task AddItemAsync_ThrowsValidation_WhenTheQuantityExceedsTheCap()
    {
        await using var db = await TestDatabase.CreateCleanContextAsync();
        var restaurant = await TestData.AddRestaurantAsync(db, menuItems: [TestData.MenuItem("Margherita")]);

        var exception = await Assert.ThrowsExactlyAsync<RequestValidationException>(() =>
            new CartService(db, NullLogger<CartService>.Instance).AddItemAsync(
                restaurant.MenuItems[0].Id,
                CartLimits.MaxQuantityPerItem + 1,
                CancellationToken.None));

        Assert.IsTrue(exception.Errors.ContainsKey("quantity"));
    }

    [TestMethod]
    public async Task AddItemAsync_ThrowsValidation_WhenTheMergedQuantityExceedsTheCap()
    {
        await using var db = await TestDatabase.CreateCleanContextAsync();
        var restaurant = await TestData.AddRestaurantAsync(db, menuItems: [TestData.MenuItem("Margherita")]);
        var menuItemId = restaurant.MenuItems[0].Id;
        var cartService = new CartService(db, NullLogger<CartService>.Instance);

        await cartService.AddItemAsync(menuItemId, CartLimits.MaxQuantityPerItem, CancellationToken.None);

        var exception = await Assert.ThrowsExactlyAsync<RequestValidationException>(() =>
            cartService.AddItemAsync(menuItemId, 1, CancellationToken.None));

        Assert.IsTrue(exception.Errors.ContainsKey("quantity"));
    }

    [TestMethod]
    public async Task RemoveItemAsync_RemovesOnlyThatLine()
    {
        await using var db = await TestDatabase.CreateCleanContextAsync();
        var restaurant = await TestData.AddRestaurantAsync(
            db,
            menuItems: [TestData.MenuItem("Margherita"), TestData.MenuItem("Tiramisu", 5.25m)]);
        var cartService = new CartService(db, NullLogger<CartService>.Instance);

        await cartService.AddItemAsync(restaurant.MenuItems[0].Id, 1, CancellationToken.None);
        var cart = await cartService.AddItemAsync(restaurant.MenuItems[1].Id, 1, CancellationToken.None);
        var lineToRemove = cart.Items.Single(item => item.MenuItemId == restaurant.MenuItems[0].Id).Id;

        var remaining = await cartService.RemoveItemAsync(lineToRemove, CancellationToken.None);

        Assert.IsNotNull(remaining);
        Assert.HasCount(1, remaining.Items);
        Assert.AreEqual(restaurant.MenuItems[1].Id, remaining.Items[0].MenuItemId);
    }

    [TestMethod]
    public async Task RemoveItemAsync_DiscardsTheCart_WhenTheLastLineIsRemoved()
    {
        await using var db = await TestDatabase.CreateCleanContextAsync();
        var restaurant = await TestData.AddRestaurantAsync(db, menuItems: [TestData.MenuItem("Margherita")]);
        var cartService = new CartService(db, NullLogger<CartService>.Instance);

        var cart = await cartService.AddItemAsync(restaurant.MenuItems[0].Id, 1, CancellationToken.None);
        var result = await cartService.RemoveItemAsync(cart.Items[0].Id, CancellationToken.None);

        Assert.IsNull(result, "an emptied cart should be discarded rather than left as a locked shell");

        await using var verification = TestDatabase.CreateContext();
        Assert.AreEqual(0, await verification.Carts.CountAsync());
        Assert.AreEqual(0, await verification.CartItems.CountAsync());
    }

    [TestMethod]
    public async Task RemoveItemAsync_AllowsSwitchingRestaurant_OnceTheCartIsEmptied()
    {
        await using var db = await TestDatabase.CreateCleanContextAsync();
        var pizzeria = await TestData.AddRestaurantAsync(db, "Pizzeria", menuItems: [TestData.MenuItem("Margherita")]);
        var sushi = await TestData.AddRestaurantAsync(db, "Sushi", menuItems: [TestData.MenuItem("Nigiri")]);
        var cartService = new CartService(db, NullLogger<CartService>.Instance);

        var cart = await cartService.AddItemAsync(pizzeria.MenuItems[0].Id, 1, CancellationToken.None);
        await cartService.RemoveItemAsync(cart.Items[0].Id, CancellationToken.None);

        var newCart = await cartService.AddItemAsync(sushi.MenuItems[0].Id, 1, CancellationToken.None);

        Assert.AreEqual(sushi.Id, newCart.RestaurantId);
    }

    [TestMethod]
    public async Task RemoveItemAsync_ThrowsNotFound_ForAnUnknownLine()
    {
        await using var db = await TestDatabase.CreateCleanContextAsync();
        var restaurant = await TestData.AddRestaurantAsync(db, menuItems: [TestData.MenuItem("Margherita")]);
        var cartService = new CartService(db, NullLogger<CartService>.Instance);

        await cartService.AddItemAsync(restaurant.MenuItems[0].Id, 1, CancellationToken.None);

        await Assert.ThrowsExactlyAsync<NotFoundException>(() =>
            cartService.RemoveItemAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [TestMethod]
    public async Task RemoveItemAsync_ThrowsNotFound_WhenThereIsNoCart()
    {
        await using var db = await TestDatabase.CreateCleanContextAsync();

        await Assert.ThrowsExactlyAsync<NotFoundException>(() =>
            new CartService(db, NullLogger<CartService>.Instance).RemoveItemAsync(Guid.NewGuid(), CancellationToken.None));
    }

    private static async Task AssertStoredQuantityAsync(int expectedQuantity)
    {
        await using var verification = TestDatabase.CreateContext();
        var stored = await verification.CartItems.SingleAsync();
        Assert.AreEqual(expectedQuantity, stored.Quantity);
    }
}
