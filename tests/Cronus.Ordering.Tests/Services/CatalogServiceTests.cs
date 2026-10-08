using Cronus.Ordering.Errors;
using Cronus.Ordering.Services;
using Cronus.Ordering.Tests.Infrastructure;

namespace Cronus.Ordering.Tests.Services;

[TestClass]
public sealed class CatalogServiceTests
{
    [TestMethod]
    public async Task GetRestaurantsAsync_ReturnsOnlyActiveRestaurants_OrderedByName()
    {
        await using var db = await TestDatabase.CreateCleanContextAsync();
        await TestData.AddRestaurantAsync(db, "Zebra Grill");
        await TestData.AddRestaurantAsync(db, "Alpha Cafe");
        await TestData.AddRestaurantAsync(db, "Closed Kitchen", isActive: false);

        var results = await new CatalogService(db).GetRestaurantsAsync(CancellationToken.None);

        CollectionAssert.AreEqual(
            new[] { "Alpha Cafe", "Zebra Grill" },
            results.Select(restaurant => restaurant.Name).ToArray());
    }

    [TestMethod]
    public async Task GetMenuAsync_ReturnsOnlyThatRestaurantsItems_OrderedByName()
    {
        await using var db = await TestDatabase.CreateCleanContextAsync();
        var restaurant = await TestData.AddRestaurantAsync(
            db,
            "Test Kitchen",
            menuItems: [TestData.MenuItem("Tiramisu", 5.25m), TestData.MenuItem("Margherita", 9.50m)]);

        await TestData.AddRestaurantAsync(db, "Other Kitchen", menuItems: [TestData.MenuItem("Elsewhere")]);

        var menu = await new CatalogService(db).GetMenuAsync(restaurant.Id, CancellationToken.None);

        CollectionAssert.AreEqual(
            new[] { "Margherita", "Tiramisu" },
            menu.Select(menuItem => menuItem.Name).ToArray());
    }

    [TestMethod]
    public async Task GetMenuAsync_ThrowsNotFound_ForAnUnknownRestaurant()
    {
        await using var db = await TestDatabase.CreateCleanContextAsync();

        await Assert.ThrowsExactlyAsync<NotFoundException>(() =>
            new CatalogService(db).GetMenuAsync(Guid.NewGuid(), CancellationToken.None));
    }
}
